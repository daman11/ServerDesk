using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ServerDesk.Infrastructure;

public sealed record ApplicationLogEntry(string Name,long Bytes,DateTimeOffset ModifiedUtc);
public sealed record ApplicationLogListing(IReadOnlyList<ApplicationLogEntry> Entries,bool DirectoryExists);
public sealed record ApplicationLogPreview(string Text,long SourceBytes,bool Truncated);

// Read-only access to this application's daily logs; exporting never writes to
// their directory. The preview is bounded, while export streams a full snapshot.
public sealed partial class ApplicationLogFiles
{
    public const int PreviewBytes=128*1024,VisibleLimit=50;
    public string DirectoryPath { get; }
    private static StringComparison PathComparison=>OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
    public ApplicationLogFiles(string directory)=>DirectoryPath=Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    public Task<ApplicationLogListing> ListAsync(CancellationToken ct=default)=>Task.Run(()=>
    {
        var recent=new PriorityQueue<ApplicationLogEntry,(long,string)>();
        try
        {
            foreach(var path in Directory.EnumerateFiles(DirectoryPath,"serverdesk-*.log",SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested(); var name=Path.GetFileName(path); if(!ValidName(name))continue;
                try
                {
                    var file=new FileInfo(path); file.Refresh(); if(!file.Exists || (file.Attributes & (FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)continue;
                    var entry=new ApplicationLogEntry(name,file.Length,file.LastWriteTimeUtc); recent.Enqueue(entry,(entry.ModifiedUtc.UtcTicks,name)); if(recent.Count>VisibleLimit)recent.Dequeue();
                }
                catch(FileNotFoundException) { }
            }
        }
        catch(DirectoryNotFoundException) { return new ApplicationLogListing([],false); }
        ct.ThrowIfCancellationRequested();
        return new ApplicationLogListing(recent.UnorderedItems.Select(i=>i.Element).OrderByDescending(e=>e.ModifiedUtc).ThenByDescending(e=>e.Name,StringComparer.Ordinal).ToArray(),true);
    },ct);
    private static bool ValidName(string name)=>LogName().IsMatch(name) && DateOnly.TryParseExact(name.AsSpan(11,8),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _);
    private FileStream Open(string name)
    {
        if(!ValidName(name))throw new ArgumentException("Invalid application log filename.",nameof(name));
        var path=Path.Combine(DirectoryPath,name); var attributes=File.GetAttributes(path);
        if((attributes & (FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)throw new IOException("The application log must be a regular file.");
        return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,64*1024,true);
    }
    public Task<ApplicationLogPreview> ReadTailAsync(string name,CancellationToken ct=default)=>Task.Run(async()=>
    {
        ct.ThrowIfCancellationRequested(); await using var stream=Open(name); var length=stream.Length; var start=Math.Max(0,length-PreviewBytes); stream.Position=start;
        var bytes=new byte[(int)(length-start)]; await stream.ReadExactlyAsync(bytes,ct).ConfigureAwait(false);
        var skip=0;
        if(start>0)
        {
            var newline=Array.IndexOf(bytes,(byte)'\n');
            if(newline>=0 && newline<bytes.Length-1)skip=newline+1;
            else while(skip<bytes.Length && (bytes[skip]&0xC0)==0x80)skip++;
        }
        return new ApplicationLogPreview(Encoding.UTF8.GetString(bytes,skip,bytes.Length-skip),length,start>0);
    },ct);
    public Task ExportAsync(string name,string destination,CancellationToken ct=default)=>Task.Run(async()=>
    {
        ct.ThrowIfCancellationRequested(); var target=Path.GetFullPath(destination);
        var physicalLogs=PhysicalDirectory(DirectoryPath);
        var physicalTarget=Path.Combine(PhysicalDirectory(Path.GetDirectoryName(target)!),Path.GetFileName(target));
        if(Inside(target,DirectoryPath) || Inside(physicalTarget,physicalLogs))throw new ArgumentException("Choose a destination outside the application log directory.",nameof(destination));
        await using var source=Open(name); var length=source.Length;
        await AtomicFile.ReplaceAsync(target,async(output,token)=>
        {
            var buffer=new byte[64*1024]; var remaining=length;
            while(remaining>0)
            {
                var count=await source.ReadAsync(buffer.AsMemory(0,(int)Math.Min(buffer.Length,remaining)),token).ConfigureAwait(false);
                if(count==0)throw new IOException("The log changed during export. Refresh and retry.");
                await output.WriteAsync(buffer.AsMemory(0,count),token).ConfigureAwait(false); remaining-=count;
            }
        },ct).ConfigureAwait(false);
    },ct);
    private static bool Inside(string path,string directory)=>path.Equals(directory,PathComparison) || path.StartsWith(Path.TrimEndingDirectorySeparator(directory)+Path.DirectorySeparatorChar,PathComparison);
    private static string PhysicalDirectory(string path)
    {
        var full=Path.GetFullPath(path); var followed=0;
        // Resolve aliases in every ancestor, including ancestors of a link's
        // target. A lexical prefix alone misses junction/symlink destinations.
        while(true)
        {
            var root=Path.GetPathRoot(full)!; var current=root; var parts=full[root.Length..].Split(new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries); var restart=false;
            for(var i=0;i<parts.Length;i++)
            {
                var directory=new DirectoryInfo(Path.Combine(current,parts[i]));
                if(directory.LinkTarget==null) { current=directory.FullName; continue; }
                if(++followed>40)throw new IOException("Too many directory links in the log export path.");
                var target=directory.ResolveLinkTarget(true)??throw new IOException("The log export directory link is unavailable.");
                full=Path.Combine(new[]{target.FullName}.Concat(parts.Skip(i+1)).ToArray()); restart=true; break;
            }
            if(!restart)return Path.TrimEndingDirectorySeparator(current);
        }
    }
    [GeneratedRegex(@"\Aserverdesk-[0-9]{8}(?:_[0-9]{3,})?\.log\z",RegexOptions.CultureInvariant)]
    private static partial Regex LogName();
}
