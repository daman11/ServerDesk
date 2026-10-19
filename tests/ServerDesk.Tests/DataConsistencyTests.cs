using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class DataConsistencyTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-consistency-"+Guid.NewGuid().ToString("N"));
    private sealed class Vault:ICredentialStore
    {
        internal readonly ConcurrentDictionary<Guid,string> Data=new();
        internal Func<int,CancellationToken,Task>? BeforeWrite;
        private int writes;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Data.GetValueOrDefault(id));
        public async Task SaveAsync(Guid id,string value,CancellationToken ct=default) { var n=Interlocked.Increment(ref writes); if(BeforeWrite!=null)await BeforeWrite(n,ct); ct.ThrowIfCancellationRequested(); Data[id]=value; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Data.TryRemove(id,out _); return Task.CompletedTask; }
    }
    private async Task<ProfileRepository> Repository() { var repo=new ProfileRepository(new(root)); await repo.InitializeAsync(); return repo; }
    private static async Task Sql(ProfileRepository repo,string sql)
    { using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()); await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText=sql; await command.ExecuteNonQueryAsync(); }
    [Fact] public async Task Failed_profile_identity_edit_preserves_original_password_binding_and_retry_rotates_it_atomically()
    {
        var repo=await Repository(); var original=new ServerProfile { Name="Fixture",Host="old.invalid",Username="fixture" }; await repo.SaveAsync(original);
        var vault=new Vault(); await vault.SaveAsync(CredentialReference.ForProfile(original),"old-secret-fixture");
        await Sql(repo,"CREATE TRIGGER deny_edit BEFORE UPDATE ON profiles BEGIN SELECT RAISE(ABORT,'fixture write denied'); END");
        var edited=original with { Host="new.invalid",Folder="New/Folder" };
        await Assert.ThrowsAsync<SqliteException>(()=>repo.SaveEditedProfileAsync(original,edited));
        Assert.Equal(original,(await repo.ListAsync()).Single()); Assert.Empty(await repo.FoldersAsync()); Assert.Equal("old-secret-fixture",await vault.GetAsync(CredentialReference.ForProfile(original)));
        await Sql(repo,"DROP TRIGGER deny_edit"); var saved=await repo.SaveEditedProfileAsync(original,edited);
        Assert.Equal(original.Id,saved.Id); Assert.NotNull(saved.CredentialId); Assert.NotEqual(CredentialReference.ForProfile(original),CredentialReference.ForProfile(saved)); Assert.Null(await vault.GetAsync(CredentialReference.ForProfile(saved))); Assert.Equal(saved,(await repo.ListAsync()).Single());
        var renamed=await repo.SaveEditedProfileAsync(saved,saved with { Name="Renamed" }); Assert.Equal(saved.CredentialId,renamed.CredentialId);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.SaveEditedProfileAsync(original,original with { Host="stale.invalid" })); Assert.Equal(renamed,(await repo.ListAsync()).Single());
    }
    [Fact] public async Task Local_vault_bindings_never_cross_exports_imports_or_new_profile_creation()
    {
        var repo=await Repository(); var p=new ServerProfile { Name="Fixture",Host="host.invalid",Username="fixture" }; await repo.SaveAsync(p);
        var rotated=await repo.SaveEditedProfileAsync(p,p with { Username="other" }); using var export=new MemoryStream(); await repo.ExportAsync(export);
        Assert.DoesNotContain("CredentialId",System.Text.Encoding.UTF8.GetString(export.ToArray()));
        var hostile=JsonSerializer.SerializeToUtf8Bytes(new ProfileRepository.ConfigurationExport(2,[rotated],[])); await repo.ImportAsync(new MemoryStream(hostile));
        var imported=(await repo.ListAsync()).Single(x=>x.Id!=rotated.Id); Assert.Null(imported.CredentialId); Assert.NotEqual(CredentialReference.ForProfile(rotated),CredentialReference.ForProfile(imported));
        var duplicate=await repo.SaveEditedProfileAsync(null,rotated with { Id=Guid.NewGuid() }); Assert.Null(duplicate.CredentialId);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task A_partial_or_cancelled_command_vault_write_preserves_the_complete_previous_library(bool cancel)
    {
        var repo=await Repository(); var vault=new Vault(); var store=new CommandStore(repo,vault); var old=new CommandUserLibrary { Commands=[CommandEngineTests.Snippet()] }; await store.SaveLibraryAsync(old);
        var pointer=await repo.SettingAsync("commands.vault.v1"); var before=vault.Data.ToArray(); using var stop=new CancellationTokenSource();
        vault.BeforeWrite=(n,_)=> { if(n==3) { if(cancel)stop.Cancel(); else throw new IOException("fixture chunk failure"); } return Task.CompletedTask; };
        var large=old with { Commands=Enumerable.Range(0,30).Select(i=>CommandEngineTests.Snippet() with { Id="user."+i,Explanation=new string('x',5000) }).ToArray() };
        Assert.NotNull(await Record.ExceptionAsync(()=>store.SaveLibraryAsync(large,stop.Token)));
        Assert.Equal(pointer,await repo.SettingAsync("commands.vault.v1")); Assert.Equal(before.OrderBy(p=>p.Key),vault.Data.OrderBy(p=>p.Key)); Assert.Single((await store.ReadLibraryAsync()).Commands);
        vault.BeforeWrite=null; await store.SaveLibraryAsync(large); Assert.Equal(30,(await store.ReadLibraryAsync()).Commands.Length);
    }
    [Fact] public async Task A_slow_or_stale_command_writer_cannot_overwrite_another_window_or_remove_its_blobs()
    {
        var repo=await Repository(); var vault=new Vault(); var first=new CommandStore(repo,vault); var other=new CommandStore(new(new(root)),vault);
        var library=new CommandUserLibrary { Commands=[CommandEngineTests.Snippet()] }; await first.SaveLibraryAsync(library); await other.ReadLibraryAsync();
        var blocked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vault.BeforeWrite=async(n,ct)=> { if(n==2) { blocked.SetResult(); await release.Task.WaitAsync(ct); } };
        var slow=first.SaveLibraryAsync(library with { Categories=["Slow"] }); await blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { await other.SaveLibraryAsync(library with { Categories=["Winner"] }); } finally { release.SetResult(); }
        await Assert.ThrowsAsync<InvalidOperationException>(()=>slow); Assert.Equal(new[]{"Winner"},(await other.ReadLibraryAsync()).Categories);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>first.SaveLibraryAsync(library));
        await first.ReadLibraryAsync(); await first.SaveLibraryAsync(library with { Categories=["Reloaded"] }); Assert.Equal(new[]{"Reloaded"},(await first.ReadLibraryAsync()).Categories);
    }
    [Fact] public async Task Independent_network_stores_merge_simultaneous_history_and_collection_commits()
    {
        var repo=await Repository(); var vault=new Vault { BeforeWrite=async(_,_)=>await Task.Yield() };
        var request=new NetworkRequest { Tool=NetworkToolKind.Ping,Target="127.0.0.1" };
        var stores=Enumerable.Range(0,12).Select(_=>new NetworkToolsStore(new(new(root)),vault)).ToArray();
        await Task.WhenAll(stores.Select((store,i)=>store.SaveCollectionAsync("Fixture "+i,request,new()))).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(12,(await stores[0].CollectionsAsync()).Count); Assert.Equal(12,vault.Data.Count);
        await Task.WhenAll(stores.Select(store=>store.AddHistoryAsync(request))).WaitAsync(TimeSpan.FromSeconds(15)); Assert.Equal(12,(await stores[0].HistoryAsync()).Count);
        var rows=await stores[0].CollectionsAsync(); await Task.WhenAll(stores.Select((store,i)=>store.DeleteCollectionAsync(rows[i].Id))).WaitAsync(TimeSpan.FromSeconds(15)); Assert.Empty(await stores[0].CollectionsAsync()); Assert.Empty(vault.Data);
    }
    [Fact] public async Task A_real_writer_lock_returns_control_promptly_and_preserves_profile_until_retry()
    {
        var repo=await Repository(); var profile=new ServerProfile { Name="Locked fixture",Host="old.invalid",Username="fixture" }; await repo.SaveAsync(profile);
        using(var blocker=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()))
        {
            await blocker.OpenAsync(); using var transaction=blocker.BeginTransaction(); var clock=System.Diagnostics.Stopwatch.StartNew();
            var writing=repo.SaveEditedProfileAsync(profile,profile with { Host="new.invalid" }); Assert.True(clock.Elapsed<TimeSpan.FromMilliseconds(500));
            var error=await Assert.ThrowsAsync<SqliteException>(()=>writing.WaitAsync(TimeSpan.FromSeconds(8))); Assert.Equal(5,error.SqliteErrorCode);
        }
        Assert.Equal(profile,(await repo.ListAsync()).Single()); var retry=await repo.SaveEditedProfileAsync(profile,profile with { Host="new.invalid" }); Assert.Equal("new.invalid",retry.Host); Assert.NotNull(retry.CredentialId);
    }
    [Fact] public async Task A_real_Unix_directory_permission_failure_preserves_the_original_export_and_allows_retry()
    {
        if(OperatingSystem.IsWindows() || Environment.UserName=="root")return;
        Directory.CreateDirectory(root); var directory=Path.Combine(root,"readonly"); Directory.CreateDirectory(directory); var file=Path.Combine(directory,"fixture.json"); await File.WriteAllTextAsync(file,"original fixture");
        var mode=File.GetUnixFileMode(directory); File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserExecute);
        Task Write()=>ExportFile.WriteAsync(file,(stream,ct)=>stream.WriteAsync("replacement"u8.ToArray(),ct).AsTask());
        try { await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(Write); }
        finally { File.SetUnixFileMode(directory,mode); }
        Assert.Equal("original fixture",await File.ReadAllTextAsync(file)); Assert.Single(Directory.GetFiles(directory));
        await Write(); Assert.Equal("replacement",await File.ReadAllTextAsync(file));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
