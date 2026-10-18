using System.Text;
using System.Diagnostics;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ApplicationLogEdgeCaseTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-log-edges-"+Guid.NewGuid().ToString("N"));
    private string Logs=>Path.Combine(root,"źródło","logs");
    private const string Name="serverdesk-20261018.log";
    private async Task<string> Source(string text="original") { Directory.CreateDirectory(Logs); var path=Path.Combine(Logs,Name); await File.WriteAllTextAsync(path,text,new UTF8Encoding(false)); return path; }

    [Theory]
    [InlineData("serverdesk-20261018.log\n")]
    [InlineData("serverdesk-20261018_001.log\n")]
    [InlineData("serverdesk-20261018.log\r\n")]
    public async Task Filename_must_match_the_whole_input_including_its_final_character(string name)
    {
        await Source(); var store=new ApplicationLogFiles(Logs);
        await Assert.ThrowsAsync<ArgumentException>(()=>store.ReadTailAsync(name));
        await Assert.ThrowsAsync<ArgumentException>(()=>store.ExportAsync(name,Path.Combine(root,"copy.log")));
        Assert.False(File.Exists(Path.Combine(root,"copy.log")));
    }

    [Theory][InlineData(".")][InlineData("../logs")]
    public async Task Normalized_directory_alias_cannot_overwrite_the_source(string alias)
    {
        var source=await Source(); var target=Path.Combine(Logs,alias,Name);
        await Assert.ThrowsAsync<ArgumentException>(()=>new ApplicationLogFiles(Logs).ExportAsync(Name,target));
        Assert.Equal("original",await File.ReadAllTextAsync(source)); Assert.Empty(Directory.GetFiles(root,"*.part",SearchOption.AllDirectories));
    }

    private static async Task Alias(string alias,string target)
    {
        if(!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(alias,target); return; }
        // Only generated fixture paths are passed to mklink. Directory junctions
        // work without enabling Developer Mode or requesting administrator rights.
        var start=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),$"/d /c mklink /J \"{alias}\" \"{target}\"") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        using var process=Process.Start(start)!; using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10)); await process.WaitForExitAsync(timeout.Token); Assert.Equal(0,process.ExitCode);
    }

    [Fact] public async Task Directory_alias_cannot_replace_a_live_log()
    {
        var source=await Source(); var alias=Path.Combine(root,"alias"); await Alias(alias,Logs);
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(()=>new ApplicationLogFiles(Logs).ExportAsync(Name,Path.Combine(alias,Name)));
            Assert.Equal("original",await File.ReadAllTextAsync(source)); Assert.Single(Directory.GetFiles(Logs));
        }
        finally { Directory.Delete(alias); }
    }

    [Fact] public async Task Source_directory_alias_still_protects_its_real_directory()
    {
        var source=await Source(); var alias=Path.Combine(root,"alias"); await Alias(alias,Path.GetDirectoryName(Logs)!);
        try
        {
            var store=new ApplicationLogFiles(Path.Combine(alias,"logs"));
            await Assert.ThrowsAsync<ArgumentException>(()=>store.ExportAsync(Name,Path.Combine(Logs,"copy.log")));
            Assert.Equal("original",await File.ReadAllTextAsync(source)); Assert.Single(Directory.GetFiles(Logs));
        }
        finally { Directory.Delete(alias); }
    }

    [Fact] public async Task An_alias_in_the_target_of_another_alias_is_also_resolved()
    {
        await Source(); var first=Path.Combine(root,"first"); var second=Path.Combine(root,"second"); await Alias(first,Path.GetDirectoryName(Logs)!); await Alias(second,Path.Combine(first,"logs"));
        try { await Assert.ThrowsAsync<ArgumentException>(()=>new ApplicationLogFiles(Logs).ExportAsync(Name,Path.Combine(second,"copy.log"))); Assert.Single(Directory.GetFiles(Logs)); }
        finally { Directory.Delete(second); Directory.Delete(first); }
    }

    [Fact] public async Task An_export_directory_alias_outside_logs_remains_usable()
    {
        var source=await Source(); var destination=Path.Combine(root,"copies"); Directory.CreateDirectory(destination); var alias=Path.Combine(root,"alias"); await Alias(alias,destination);
        try { await new ApplicationLogFiles(Logs).ExportAsync(Name,Path.Combine(alias,"copy.log")); Assert.Equal("original",await File.ReadAllTextAsync(Path.Combine(destination,"copy.log"))); Assert.Equal("original",await File.ReadAllTextAsync(source)); }
        finally { Directory.Delete(alias); }
    }

    [Fact] public async Task Rotation_replaces_the_preview_and_missing_selection_preserves_an_existing_export()
    {
        var source=await Source("old log\n"); var store=new ApplicationLogFiles(Logs); Assert.Equal("old log\n",(await store.ReadTailAsync(Name)).Text);
        File.Move(source,source+".retired"); var destination=Path.Combine(root,"export.log"); await File.WriteAllTextAsync(destination,"keep");
        await Assert.ThrowsAnyAsync<IOException>(()=>store.ExportAsync(Name,destination)); Assert.Equal("keep",await File.ReadAllTextAsync(destination));
        await Source("new log\n"); Assert.Equal("new log\n",(await store.ReadTailAsync(Name)).Text); await store.ExportAsync(Name,destination); Assert.Equal("new log\n",await File.ReadAllTextAsync(destination));
        Assert.Single((await store.ListAsync()).Entries); Assert.Equal("old log\n",await File.ReadAllTextAsync(source+".retired"));
    }

    [Fact] public async Task Preview_of_a_large_sparse_log_is_bounded_and_export_to_a_missing_parent_is_non_destructive()
    {
        var path=await Source(); var suffix=Encoding.UTF8.GetBytes("\n[ERR] żółć 🙂\n   at Fixture.Edge()\n"); const long length=32L*1024*1024;
        await using(var file=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.Read)) { file.SetLength(length); file.Position=length-suffix.Length; await file.WriteAsync(suffix); }
        var store=new ApplicationLogFiles(Logs); var tail=await store.ReadTailAsync(Name); Assert.True(tail.Truncated); Assert.Equal(length,tail.SourceBytes); Assert.True(Encoding.UTF8.GetByteCount(tail.Text)<=ApplicationLogFiles.PreviewBytes); Assert.EndsWith("   at Fixture.Edge()\n",tail.Text);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(()=>store.ExportAsync(Name,Path.Combine(root,"missing","copy.log")));
        Assert.False(Directory.Exists(Path.Combine(root,"missing"))); Assert.Equal(length,new FileInfo(path).Length); Assert.Empty(Directory.GetFiles(root,"*.part",SearchOption.AllDirectories));
    }

    [Fact] public async Task Parallel_preview_and_exports_preserve_every_byte_and_leave_no_staging_files()
    {
        var text=string.Concat(Enumerable.Repeat("żółć 🙂\n   at Fixture.Edge()\n",10000)); var source=await Source(text); var store=new ApplicationLogFiles(Logs);
        await Task.WhenAll(Enumerable.Range(0,12).Select(async i=> { var preview=await store.ReadTailAsync(Name); Assert.EndsWith("   at Fixture.Edge()\n",preview.Text); var target=Path.Combine(root,$"copy-{i}.log"); await store.ExportAsync(Name,target); Assert.Equal(text,await File.ReadAllTextAsync(target)); }));
        Assert.Equal(text,await File.ReadAllTextAsync(source)); Assert.Empty(Directory.GetFiles(root,"*.part",SearchOption.AllDirectories)); Assert.Single((await store.ListAsync()).Entries);
    }

    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
}
