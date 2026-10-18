using System.Globalization;
using System.Text.RegularExpressions;

namespace ServerDesk.Infrastructure;

public sealed record DatabaseBackupEntry(string Path,DateTimeOffset CreatedUtc,long Bytes,int SchemaVersion,bool BeforeMigration)
{
    public bool BeforeReset { get; init; }
}
public sealed record DatabaseBackupListing(IReadOnlyList<DatabaseBackupEntry> Entries,int TotalCount,bool DirectoryExists);

// Metadata only: listing a filename never certifies the contents as restorable.
public static partial class DatabaseBackupCatalog
{
    public const int VisibleLimit=20;
    public static Task<DatabaseBackupListing> ReadAsync(string directory,CancellationToken ct=default)=>Task.Run(()=>Read(directory,ct),ct);
    private static DatabaseBackupListing Read(string directory,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(File.Exists(directory))throw new IOException("The backup directory is a file.");
        var recent=new PriorityQueue<DatabaseBackupEntry,(long,string)>(); var total=0;
        try
        {
            foreach(var path in Directory.EnumerateFiles(directory,"serverdesk-*.db",SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested(); var name=System.IO.Path.GetFileName(path); var match=BackupName().Match(name);
                if(!match.Success || !DateTimeOffset.TryParseExact(match.Groups["time"].Value,"yyyyMMdd-HHmmssfff",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var created) || !int.TryParse(match.Groups["schema"].Value,out var schema))continue;
                try
                {
                    var file=new FileInfo(path); file.Refresh();
                    if(!file.Exists || (file.Attributes & (FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)continue;
                    var entry=new DatabaseBackupEntry(file.FullName,created,file.Length,schema,match.Groups["reason"].Value=="before-migration") { BeforeReset=match.Groups["reason"].Value=="before-reset" }; total++;
                    recent.Enqueue(entry,(created.UtcTicks,name)); if(recent.Count>VisibleLimit)recent.Dequeue();
                }
                catch(FileNotFoundException) { /* A completed backup may be removed during enumeration. */ }
            }
        }
        catch(DirectoryNotFoundException) { return new([],0,false); }
        ct.ThrowIfCancellationRequested();
        return new(recent.UnorderedItems.Select(i=>i.Element).OrderByDescending(e=>e.CreatedUtc).ThenBy(e=>e.Path,StringComparer.Ordinal).ToArray(),total,true);
    }
    [GeneratedRegex(@"^serverdesk-(?<time>\d{8}-\d{9})-v(?<schema>\d+)-(?<reason>manual|before-migration|before-reset)-[a-f0-9]{32}\.db$",RegexOptions.CultureInvariant)]
    private static partial Regex BackupName();
}
