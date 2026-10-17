using System.Text;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class MvpHardeningTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-hardening-"+Guid.NewGuid().ToString("N"));
    private async Task<ProfileRepository> Repository()
    { var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); return repo; }
    private sealed class Vault:ICredentialStore
    {
        internal readonly Dictionary<Guid,string> Values=[];
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Values.GetValueOrDefault(id));
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Values[id]=secret; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Values.Remove(id); return Task.CompletedTask; }
    }
    [Theory] [InlineData("Version")] [InlineData("Host")] [InlineData("escaped")]
    public async Task Duplicate_configuration_properties_are_rejected_without_writes(string field)
    {
        var repo=await Repository(); await repo.AddFolderAsync("Keep");
        var json=field=="Version"?"{\"Version\":1,\"Version\":2,\"Servers\":[],\"Folders\":[\"New\"]}":"{\"Version\":2,\"Servers\":[{\"Name\":\"Fixture\",\"Username\":\"fixture\",\"Host\":\"one.test\",\""+(field=="escaped"?"\\u0048ost":"Host")+"\":\"two.test\"}],\"Folders\":[\"New\"]}";
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)); await Assert.ThrowsAsync<InvalidDataException>(()=>repo.ImportAsync(stream));
        Assert.Empty(await repo.ListAsync()); Assert.Equal(new[]{"Keep"},await repo.FoldersAsync());
    }
    [Fact] public async Task Import_starts_source_read_on_a_worker_and_keeps_the_caller_available()
    {
        var repo=await Repository(); using var entered=new ManualResetEventSlim(); using var release=new ManualResetEventSlim();
        using var source=new BlockingSource("{\"Version\":2,\"Servers\":[],\"Folders\":[\"Imported\"]}"u8.ToArray(),entered,release);
        var caller=Environment.CurrentManagedThreadId; var importing=repo.ImportAsync(source);
        try { Assert.True(entered.Wait(TimeSpan.FromSeconds(5))); Assert.NotEqual(caller,source.ReaderThread); Assert.False(importing.IsCompleted); }
        finally { release.Set(); await importing.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.Equal(new[]{"Imported"},await repo.FoldersAsync());
    }
    private sealed class BlockingSource(byte[] bytes,ManualResetEventSlim entered,ManualResetEventSlim release):MemoryStream(bytes)
    {
        internal int ReaderThread;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default)
        { ReaderThread=Environment.CurrentManagedThreadId; entered.Set(); if(!release.Wait(TimeSpan.FromSeconds(5),ct))throw new TimeoutException(); return base.ReadAsync(buffer,ct); }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Public_export_writer_preserves_previous_file_on_partial_failure_or_cancellation(bool cancel)
    {
        Directory.CreateDirectory(root); var path=Path.Combine(root,"export.txt"); await File.WriteAllTextAsync(path,"original"); using var cancellation=new CancellationTokenSource();
        var error=await Record.ExceptionAsync(()=>ExportFile.WriteAsync(path,async(stream,ct)=>
        {
            await stream.WriteAsync("partial"u8.ToArray(),ct); if(cancel)cancellation.Cancel(); else throw new IOException("Fixture write error");
        },cancellation.Token));
        if(cancel)Assert.IsAssignableFrom<OperationCanceledException>(error); else Assert.IsType<IOException>(error);
        Assert.Equal("original",await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(root));
    }
    [Fact] public async Task Export_staging_and_completed_files_are_owner_only_on_Unix()
    {
        if(OperatingSystem.IsWindows())return;
        Directory.CreateDirectory(root); var path=Path.Combine(root,"export.txt");
        await ExportFile.WriteAsync(path,async(stream,ct)=>
        {
            var staged=Assert.Single(Directory.GetFiles(root,"*.part")); if(!OperatingSystem.IsWindows())Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite,File.GetUnixFileMode(staged));
            await stream.WriteAsync("fixture"u8.ToArray(),ct);
        });
        Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite,File.GetUnixFileMode(path));
    }
    [Theory] [InlineData("{")] [InlineData("null")] [InlineData("[null]")] [InlineData("oversized")]
    public async Task Corrupt_history_and_collection_indexes_are_not_silently_overwritten(string original)
    {
        if(original=="oversized")original=new string('x',600001);
        var repo=await Repository(); var vault=new Vault(); var store=new NetworkToolsStore(repo,vault);
        await repo.SetSettingAsync("networkTools.history.v1",original); await repo.SetSettingAsync("networkTools.collections.v1",original);
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.AddHistoryAsync(new() { Target="example.test" }));
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.SaveCollectionAsync("Fixture",new() { Target="example.test" },new()));
        Assert.Equal(original,await repo.SettingAsync("networkTools.history.v1")); Assert.Equal(original,await repo.SettingAsync("networkTools.collections.v1")); Assert.Empty(vault.Values);
    }
    [Fact] public async Task Duplicate_collection_identifiers_are_rejected_before_touching_the_vault()
    {
        var repo=await Repository(); var vault=new Vault(); var store=new NetworkToolsStore(repo,vault); var id=Guid.NewGuid(); vault.Values[id]="preserve";
        var json=JsonSerializer.Serialize(new[]{new NetworkCollection(id,"One",NetworkToolKind.Ping),new NetworkCollection(id,"Two",NetworkToolKind.Ping)});
        await repo.SetSettingAsync("networkTools.collections.v1",json); await Assert.ThrowsAsync<InvalidDataException>(()=>store.DeleteCollectionAsync(id)); Assert.Equal("preserve",vault.Values[id]); Assert.Equal(json,await repo.SettingAsync("networkTools.collections.v1"));
    }
    [Theory] [InlineData("null")] [InlineData("{\"Request\":null,\"Secrets\":{}}")] [InlineData("{\"Request\":{\"Target\":null},\"Secrets\":{}}")] [InlineData("{\"Request\":{\"Target\":\"example.test\"},\"Secrets\":null}")] [InlineData("wrong-tool")]
    public async Task Invalid_saved_collection_payload_is_rejected_without_deleting_original(string json)
    {
        var repo=await Repository(); var vault=new Vault(); var store=new NetworkToolsStore(repo,vault); await store.SaveCollectionAsync("Fixture",new() { Target="example.test" },new()); var id=Assert.Single(await store.CollectionsAsync()).Id;
        if(json=="wrong-tool")json=JsonSerializer.Serialize(new SavedNetworkRequest(new() { Tool=NetworkToolKind.Subnet,Target="192.0.2.0/24" },new()));
        vault.Values[id]=json; await Assert.ThrowsAsync<InvalidDataException>(()=>store.LoadCollectionAsync(id)); Assert.Equal(json,vault.Values[id]); Assert.Single(await store.CollectionsAsync());
    }
    [Theory] [InlineData("[null]")] [InlineData("[\"\"]")] [InlineData("[12]")]
    public async Task Corrupt_remembered_values_do_not_crash_or_get_replaced_by_remembering(string json)
    {
        var repo=await Repository(); var store=new NetworkToolsStore(repo,new Vault()); await repo.SetSettingAsync("networkTools.resolvers",json);
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.ValuesAsync("resolvers")); await Assert.ThrowsAsync<InvalidDataException>(()=>store.RememberAsync("resolvers","192.0.2.1")); Assert.Equal(json,await repo.SettingAsync("networkTools.resolvers"));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
