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
        if(target.Equals(DirectoryPath,PathComparison) || target.StartsWith(DirectoryPath+Path.DirectorySeparatorChar,PathComparison))throw new ArgumentException("Choose a destination outside the application log directory.",nameof(destination));
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
    [GeneratedRegex(@"^serverdesk-\d{8}(?:_\d{3,})?\.log$",RegexOptions.CultureInvariant)]
    private static partial Regex LogName();
}
