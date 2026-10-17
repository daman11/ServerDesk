using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class NetworkCollectionAtomicityTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-collection-atomicity-"+Guid.NewGuid().ToString("N"));
    private sealed class Vault : ICredentialStore
    {
        internal readonly Dictionary<Guid,string> Values=[];
        internal bool RejectDelete;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Values.GetValueOrDefault(id));
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { ct.ThrowIfCancellationRequested(); Values[id]=value; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default)
        { ct.ThrowIfCancellationRequested(); if(RejectDelete)throw new IOException("fixture: vault cleanup unavailable"); Values.Remove(id); return Task.CompletedTask; }
    }
    private async Task Execute(string sql)
    {
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.Combine(root,"serverdesk.db"),Pooling=false }.ToString());
        await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText=sql; await command.ExecuteNonQueryAsync();
    }
    private async Task<(ProfileRepository Repo,Vault Vault,NetworkToolsStore Store,Guid Id)> Fixture()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var vault=new Vault(); var store=new NetworkToolsStore(repo,vault);
        await store.SaveCollectionAsync("Keep",new() { Target="127.0.0.1" },new(Token:"disposable-fixture-token"));
        return (repo,vault,store,Assert.Single(await store.CollectionsAsync()).Id);
    }
    private Task RejectIndexWrites()=>Execute("CREATE TRIGGER fail_index BEFORE UPDATE ON settings WHEN NEW.key='networkTools.collections.v1' BEGIN SELECT RAISE(ABORT,'fixture: index write failed'); END");

    [Fact] public async Task Failed_deletion_preserves_the_index_and_secret_and_retry_works()
    {
        var (_,vault,store,id)=await Fixture(); var original=vault.Values[id]; await RejectIndexWrites();
        var error=await Assert.ThrowsAsync<SqliteException>(()=>store.DeleteCollectionAsync(id)); Assert.Equal(19,error.SqliteErrorCode);
        Assert.Equal(original,vault.Values[id]); Assert.Equal(id,Assert.Single(await store.CollectionsAsync()).Id);
        Assert.Equal("disposable-fixture-token",(await store.LoadCollectionAsync(id)).Secrets.Token);
        await Execute("DROP TRIGGER fail_index"); await store.DeleteCollectionAsync(id);
        Assert.Empty(await store.CollectionsAsync()); Assert.Empty(vault.Values);
    }

    [Fact] public async Task Unavailable_vault_cleanup_does_not_undo_a_committed_deletion()
    {
        var (repo,vault,store,id)=await Fixture(); vault.RejectDelete=true; await store.DeleteCollectionAsync(id);
        Assert.Empty(await new NetworkToolsStore(repo,vault).CollectionsAsync()); Assert.True(vault.Values.ContainsKey(id));
        await Assert.ThrowsAsync<ArgumentException>(()=>store.LoadCollectionAsync(id));
        vault.RejectDelete=false; await store.SaveCollectionAsync("New",new() { Target="127.0.0.1" },new());
        Assert.NotEqual(id,Assert.Single(await store.CollectionsAsync()).Id);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Failed_save_preserves_existing_collections_and_the_original_error(bool rejectCleanup)
    {
        var (_,vault,store,id)=await Fixture(); vault.RejectDelete=rejectCleanup; var original=vault.Values[id]; await RejectIndexWrites();
        var error=await Assert.ThrowsAsync<SqliteException>(()=>store.SaveCollectionAsync("Rejected",new() { Target="localhost" },new()));
        Assert.Equal(19,error.SqliteErrorCode); Assert.Equal(id,Assert.Single(await store.CollectionsAsync()).Id); Assert.Equal(original,vault.Values[id]);
        Assert.Equal(rejectCleanup?2:1,vault.Values.Count);
        await Execute("DROP TRIGGER fail_index"); vault.RejectDelete=false;
        await store.SaveCollectionAsync("Retry",new() { Target="localhost" },new()); Assert.Equal(2,(await store.CollectionsAsync()).Count);
        Assert.Equal("disposable-fixture-token",(await store.LoadCollectionAsync(id)).Secrets.Token);
    }

    [Fact] public async Task Cancelled_or_unknown_deletion_does_not_remove_vault_entries()
    {
        var (_,vault,store,id)=await Fixture(); using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.DeleteCollectionAsync(id,cancelled.Token));
        await store.DeleteCollectionAsync(Guid.NewGuid()); Assert.Equal(id,Assert.Single(await store.CollectionsAsync()).Id); Assert.True(vault.Values.ContainsKey(id));
    }

    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
