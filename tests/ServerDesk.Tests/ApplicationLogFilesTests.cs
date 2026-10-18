using System.Text;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ApplicationLogFilesTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-logs-"+Guid.NewGuid().ToString("N"));
    private string DirectoryPath=>Path.Combine(root,"Żółte dane","logs");
    private string Log(string name="serverdesk-20261018.log") { Directory.CreateDirectory(DirectoryPath); return Path.Combine(DirectoryPath,name); }
    [Fact] public async Task Missing_log_directory_is_empty_and_is_not_created()
    {
        var listing=await new ApplicationLogFiles(DirectoryPath).ListAsync(); Assert.False(listing.DirectoryExists); Assert.Empty(listing.Entries); Assert.False(Directory.Exists(DirectoryPath));
    }
    [Fact] public async Task Listing_is_bounded_newest_first_and_ignores_unrelated_or_invalid_names()
    {
        var names=new List<string>();
        for(var i=0;i<60;i++) { var path=Log("serverdesk-20261018_"+i.ToString("D3")+".log"); await File.WriteAllTextAsync(path,"fixture"); File.SetLastWriteTimeUtc(path,new DateTime(2026,10,18,0,0,0,DateTimeKind.Utc).AddMinutes(i)); names.Add(Path.GetFileName(path)); }
        foreach(var name in new[]{"serverdesk-20260230.log","other.log","serverdesk.db","serverdesk-20261018.log.part"})await File.WriteAllTextAsync(Log(name),"keep");
        Directory.CreateDirectory(Path.Combine(DirectoryPath,"nested")); await File.WriteAllTextAsync(Path.Combine(DirectoryPath,"nested","serverdesk-20261018.log"),"keep");
        var listing=await new ApplicationLogFiles(DirectoryPath).ListAsync(); Assert.True(listing.DirectoryExists); Assert.Equal(ApplicationLogFiles.VisibleLimit,listing.Entries.Count); Assert.Equal(names.Skip(10).Reverse(),listing.Entries.Select(e=>e.Name));
    }
    [Fact] public async Task Tail_is_bounded_and_preserves_unicode_and_stack_trace_lines()
    {
        var text=string.Concat(Enumerable.Repeat("żółć 🙂 fixture line\n",20000))+"[ERR] Fixture failure\n   at Fixture.DeleteAsync()\n"; var path=Log(); await File.WriteAllTextAsync(path,text,new UTF8Encoding(false));
        var preview=await new ApplicationLogFiles(DirectoryPath).ReadTailAsync(Path.GetFileName(path));
        Assert.True(preview.Truncated); Assert.Equal(new FileInfo(path).Length,preview.SourceBytes); Assert.True(Encoding.UTF8.GetByteCount(preview.Text)<=ApplicationLogFiles.PreviewBytes); Assert.DoesNotContain('\uFFFD',preview.Text); Assert.EndsWith("   at Fixture.DeleteAsync()\n",preview.Text); Assert.Equal(text,await File.ReadAllTextAsync(path));
    }
    [Fact] public async Task An_empty_file_and_a_large_single_unicode_line_are_readable()
    {
        var path=Log(); await File.WriteAllTextAsync(path,""); var store=new ApplicationLogFiles(DirectoryPath); Assert.Equal(new("",0,false),await store.ReadTailAsync(Path.GetFileName(path)));
        await File.WriteAllTextAsync(path,new string('界',ApplicationLogFiles.PreviewBytes)); var tail=await store.ReadTailAsync(Path.GetFileName(path)); Assert.True(tail.Truncated); Assert.NotEmpty(tail.Text); Assert.DoesNotContain('\uFFFD',tail.Text);
    }
    [Theory][InlineData("../serverdesk.db")][InlineData("sub/serverdesk-20261018.log")][InlineData("serverdesk-20261018.log/../serverdesk.db")]
    public async Task Only_a_daily_log_filename_can_be_selected(string name)
    {
        var store=new ApplicationLogFiles(DirectoryPath); await Assert.ThrowsAsync<ArgumentException>(()=>store.ReadTailAsync(name));
        Directory.CreateDirectory(root); var target=Path.Combine(root,"export.log"); await File.WriteAllTextAsync(target,"keep"); await Assert.ThrowsAsync<ArgumentException>(()=>store.ExportAsync(name,target)); Assert.Equal("keep",await File.ReadAllTextAsync(target));
    }
    [Fact] public async Task Full_export_works_while_the_logger_keeps_the_source_open_and_preserves_source_bytes()
    {
        var path=Log(); var bytes=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("[ERR] żółć\n  at Fixture()\n",20000))); var destination=Path.Combine(root,"export.log");
        await using(var logger=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read))
        {
            await logger.WriteAsync(bytes); await logger.FlushAsync();
            var store=new ApplicationLogFiles(DirectoryPath); Assert.True((await store.ReadTailAsync(Path.GetFileName(path))).Truncated); await store.ExportAsync(Path.GetFileName(path),destination);
        }
        Assert.Equal(bytes,await File.ReadAllBytesAsync(destination)); Assert.Equal(bytes,await File.ReadAllBytesAsync(path));
    }
    [Fact] public async Task Source_directory_cannot_be_used_as_an_export_destination()
    {
        var path=Log(); await File.WriteAllTextAsync(path,"original"); var store=new ApplicationLogFiles(DirectoryPath);
        await Assert.ThrowsAsync<ArgumentException>(()=>store.ExportAsync(Path.GetFileName(path),path));
        await Assert.ThrowsAsync<ArgumentException>(()=>store.ExportAsync(Path.GetFileName(path),Path.Combine(DirectoryPath,"copy.log")));
        Assert.Equal("original",await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(DirectoryPath));
    }
    [Fact] public async Task Missing_source_and_cancelled_export_preserve_an_existing_destination()
    {
        var path=Log(); await File.WriteAllTextAsync(path,"source"); var destination=Path.Combine(root,"export.log"); await File.WriteAllTextAsync(destination,"keep"); var store=new ApplicationLogFiles(DirectoryPath);
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.ExportAsync(Path.GetFileName(path),destination,cancelled.Token));
        File.Delete(path); await Assert.ThrowsAnyAsync<IOException>(()=>store.ExportAsync(Path.GetFileName(path),destination)); Assert.Equal("keep",await File.ReadAllTextAsync(destination)); Assert.Empty(Directory.GetFiles(root,"*.part"));
    }
    [Fact] public async Task Locked_destination_preserves_the_previous_export_and_cleans_staging()
    {
        if(!OperatingSystem.IsWindows())return;
        var path=Log(); await File.WriteAllTextAsync(path,"source"); var destination=Path.Combine(root,"export.log"); await File.WriteAllTextAsync(destination,"keep");
        using(var locked=new FileStream(destination,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            var error=await Record.ExceptionAsync(()=>new ApplicationLogFiles(DirectoryPath).ExportAsync(Path.GetFileName(path),destination)); Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("keep",await File.ReadAllTextAsync(destination)); Assert.Empty(Directory.GetFiles(root,"*.part"));
    }
    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
}
