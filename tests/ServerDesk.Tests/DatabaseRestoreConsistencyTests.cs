using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

// Every database and vault entry belongs to this disposable fixture. No OS
// credential service, saved user profile or remote server is accessed.
public sealed class DatabaseRestoreConsistencyTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-restore-consistency-"+Guid.NewGuid().ToString("N"));
    private readonly List<string> directories=[];
    private sealed class Vault : ICredentialStore
    {
        internal readonly Dictionary<Guid,string> Values=[];
        internal readonly List<Guid> Reads=[];
        internal bool Locked;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)
        {
            ct.ThrowIfCancellationRequested(); Reads.Add(id);
            if(Locked)throw new CredentialReadException(new IOException("Disposable fixture vault is locked"));
            return Task.FromResult(Values.GetValueOrDefault(id));
        }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default)
        { ct.ThrowIfCancellationRequested(); CredentialLimits.Validate(value); Values[id]=value; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default)
        { ct.ThrowIfCancellationRequested(); Values.Remove(id); return Task.CompletedTask; }
    }
    private async Task<ProfileRepository> Repository(string name)
    {
        var directory=Path.Combine(root,name); directories.Add(directory);
        var repository=new ProfileRepository(new AppPaths(directory)); await repository.InitializeAsync(); return repository;
    }
    private async Task<ProfileRepository> Restore(DatabaseBackup backup)
    {
        var directory=Path.Combine(root,"odtworzone żółć "+Guid.NewGuid().ToString("N")); directories.Add(directory);
        var paths=new AppPaths(directory); File.Copy(backup.Path,Path.Combine(paths.Data,"serverdesk.db"));
        var restored=new ProfileRepository(paths); await restored.InitializeAsync(); return restored;
    }
    private static CommandUserLibrary Library(string marker)=>new() { Commands=[CommandEngineTests.Snippet() with { Explanation=marker+new string('x',5000),ExplanationEn=new string('x',5000),Examples=Enumerable.Repeat(new string('x',2000),12).ToArray() }] };
    private static Guid[] Parts(string pointer)
    { using var json=JsonDocument.Parse(pointer); return json.RootElement.GetProperty("parts").EnumerateArray().Select(p=>p.GetGuid()).ToArray(); }
    private static void Unchanged(Vault vault,Dictionary<Guid,string> before)=>Assert.Equal(before.OrderBy(p=>p.Key),vault.Values.OrderBy(p=>p.Key));

    [Fact] public async Task Restoring_unchanged_references_recovers_profiles_commands_and_collections_with_the_matching_vault()
    {
        var repo=await Repository("source"); var vault=new Vault();
        var profile=new ServerProfile { Name="Fixture SSH",Host="fixture.invalid",Username="fixture",CredentialId=Guid.NewGuid(),Folder="Lab/Linux",Description="Fixture note" };
        await repo.SaveAsync(profile); await vault.SaveAsync(CredentialReference.ForProfile(profile),"disposable-profile-secret"); await repo.ConnectedAsync(profile);
        var commands=new CommandStore(repo,vault); var library=Library("original-commands"); await commands.SaveLibraryAsync(library);
        await commands.SaveStateAsync(new() { Favorites=[library.Commands[0].Id],Hosts=new() { [profile.Id]=new() { System="ubuntu" } } });
        var network=new NetworkToolsStore(repo,vault); var request=new NetworkRequest { Target="127.0.0.1" };
        await network.SaveCollectionAsync("Fixture collection",request,new(Token:"disposable-network-token")); await network.AddHistoryAsync(request);
        var collection=Assert.Single(await network.CollectionsAsync()); var backup=await repo.CreateDatabaseBackupAsync(); var before=new Dictionary<Guid,string>(vault.Values);
        var restored=await Restore(backup); var restoredProfile=Assert.Single(await restored.ListAsync());
        Assert.Equal(profile.CredentialId,restoredProfile.CredentialId); Assert.Equal(profile.Description,restoredProfile.Description);
        Assert.Equal("disposable-profile-secret",await vault.GetAsync(CredentialReference.ForProfile(restoredProfile)));
        Assert.Single(await restored.HistoryAsync()); Assert.Contains("Lab/Linux",await restored.FoldersAsync());
        var restoredCommands=new CommandStore(restored,vault); Assert.Equal(library.Commands[0].Explanation,Assert.Single((await restoredCommands.ReadLibraryAsync()).Commands).Explanation);
        Assert.Equal("ubuntu",(await restoredCommands.ReadStateAsync()).Hosts[profile.Id].System);
        Assert.Contains(library.Commands[0].Id,(await restoredCommands.ReadStateAsync()).Favorites);
        var restoredNetwork=new NetworkToolsStore(restored,vault); Assert.Equal(collection,Assert.Single(await restoredNetwork.CollectionsAsync()));
        Assert.Equal("disposable-network-token",(await restoredNetwork.LoadCollectionAsync(collection.Id)).Secrets.Token); Assert.Single(await restoredNetwork.HistoryAsync());
        Unchanged(vault,before);
    }

    [Theory] [InlineData(Protocol.SSH)] [InlineData(Protocol.RDP)] [InlineData(Protocol.VNC)]
    public async Task An_old_profile_binding_never_falls_back_to_the_new_identity_or_another_vault_entry(Protocol protocol)
    {
        var repo=await Repository("source"); var vault=new Vault();
        var profile=new ServerProfile { Name="Fixture",Host="old.invalid",Username="fixture",Protocol=protocol,Port=QuickConnect.DefaultPort(protocol),CredentialId=Guid.NewGuid() };
        await repo.SaveAsync(profile); var oldId=CredentialReference.ForProfile(profile); await vault.SaveAsync(oldId,"old-identity");
        var backup=await repo.CreateDatabaseBackupAsync(); var edited=await repo.SaveEditedProfileAsync(profile,profile with { Host="new.invalid" });
        var newId=CredentialReference.ForProfile(edited); Assert.NotEqual(oldId,newId);
        await vault.SaveAsync(newId,"new-identity"); await vault.DeleteAsync(oldId);
        await vault.SaveAsync(profile.Id,"unrelated-legacy-entry"); await vault.SaveAsync(CredentialReference.Vnc(profile.Id),"unrelated-vnc-entry");
        var before=new Dictionary<Guid,string>(vault.Values); var restored=await Restore(backup); var saved=Assert.Single(await restored.ListAsync()); vault.Reads.Clear();
        Assert.Equal("old.invalid",saved.Host); Assert.Equal(oldId,CredentialReference.ForProfile(saved));
        Assert.Null(await vault.GetAsync(CredentialReference.ForProfile(saved))); Assert.Equal(new[]{oldId},vault.Reads);
        Assert.Equal("new-identity",await vault.GetAsync(newId)); Unchanged(vault,before);
    }

    [Fact] public async Task A_database_snapshot_restores_the_reference_not_an_older_password_version()
    {
        var repo=await Repository("source"); var vault=new Vault();
        var profile=new ServerProfile { Name="Same identity",Host="fixture.invalid",Username="fixture",CredentialId=Guid.NewGuid() };
        await repo.SaveAsync(profile); var id=CredentialReference.ForProfile(profile); await vault.SaveAsync(id,"before-snapshot");
        var backup=await repo.CreateDatabaseBackupAsync(); await vault.SaveAsync(id,"after-snapshot");
        var before=new Dictionary<Guid,string>(vault.Values); var restored=await Restore(backup); var saved=Assert.Single(await restored.ListAsync());
        Assert.Equal(id,CredentialReference.ForProfile(saved)); Assert.Equal("after-snapshot",await vault.GetAsync(CredentialReference.ForProfile(saved)));
        Unchanged(vault,before);
    }

    [Fact] public async Task Restoring_an_older_command_pointer_reports_missing_content_without_replacing_the_current_library()
    {
        var repo=await Repository("source"); var vault=new Vault(); var current=new CommandStore(repo,vault);
        var original=Library("before-edit"); await current.SaveLibraryAsync(original);
        var oldPointer=(await repo.SettingAsync("commands.vault.v1"))!; var protectedCopy=new Dictionary<Guid,string>(vault.Values); var backup=await repo.CreateDatabaseBackupAsync();
        var updated=Library("after-edit"); await current.SaveLibraryAsync(updated); Assert.DoesNotContain(vault.Values.Keys,id=>protectedCopy.ContainsKey(id));
        var before=new Dictionary<Guid,string>(vault.Values); var restored=await Restore(backup); var reader=new CommandStore(restored,vault);
        var error=await Assert.ThrowsAsync<ProtectedContentUnavailableException>(()=>reader.ReadLibraryAsync()); Assert.Contains("SQLite",OperationErrorMessages.Describe(error));
        Assert.Equal(oldPointer,await restored.SettingAsync("commands.vault.v1")); Unchanged(vault,before);
        Assert.Equal(updated.Commands[0].Explanation,Assert.Single((await current.ReadLibraryAsync()).Commands).Explanation);
        // Simulate restoring the matching protected content, then retry the same reader.
        foreach(var (id,value) in protectedCopy)vault.Values.Add(id,value);
        Assert.Equal(original.Commands[0].Explanation,Assert.Single((await reader.ReadLibraryAsync()).Commands).Explanation);
        Assert.Equal(updated.Commands[0].Explanation,Assert.Single((await current.ReadLibraryAsync()).Commands).Explanation);
    }

    [Fact] public async Task A_missing_later_command_chunk_does_not_publish_a_partial_or_empty_library()
    {
        var repo=await Repository("source"); var vault=new Vault(); var commands=new CommandStore(repo,vault); var library=Library("multi-part");
        await commands.SaveLibraryAsync(library); var pointer=(await repo.SettingAsync("commands.vault.v1"))!; var parts=Parts(pointer); Assert.True(parts.Length>1);
        var backup=await repo.CreateDatabaseBackupAsync(); var last=parts[^1]; var retained=vault.Values[last]; vault.Values.Remove(last);
        var before=new Dictionary<Guid,string>(vault.Values); var restored=await Restore(backup); var reader=new CommandStore(restored,vault);
        await Assert.ThrowsAsync<ProtectedContentUnavailableException>(()=>reader.ReadLibraryAsync());
        Assert.Equal(pointer,await restored.SettingAsync("commands.vault.v1")); Unchanged(vault,before);
        vault.Values[last]=retained; Assert.Equal(library.Commands[0].Explanation,Assert.Single((await reader.ReadLibraryAsync()).Commands).Explanation);
    }

    [Fact] public async Task A_deleted_collection_stays_indexed_after_restore_without_damaging_other_collections()
    {
        var repo=await Repository("source"); var vault=new Vault(); var network=new NetworkToolsStore(repo,vault);
        await network.SaveCollectionAsync("Old",new() { Target="old.invalid" },new(Token:"old-token")); var old=Assert.Single(await network.CollectionsAsync());
        await network.SaveCollectionAsync("Keep",new() { Target="keep.invalid" },new(Token:"keep-token")); var keep=(await network.CollectionsAsync()).Single(c=>c.Id!=old.Id);
        var backup=await repo.CreateDatabaseBackupAsync(); await network.DeleteCollectionAsync(old.Id); var before=new Dictionary<Guid,string>(vault.Values);
        var restored=await Restore(backup); var reader=new NetworkToolsStore(restored,vault); var pointer=await restored.SettingAsync("networkTools.collections.v1");
        var error=await Assert.ThrowsAsync<ProtectedContentUnavailableException>(()=>reader.LoadCollectionAsync(old.Id)); Assert.Contains("SQLite",NetworkToolsService.Explain(error));
        Assert.Equal(pointer,await restored.SettingAsync("networkTools.collections.v1")); Assert.Equal(2,(await reader.CollectionsAsync()).Count); Unchanged(vault,before);
        Assert.Equal("keep-token",(await reader.LoadCollectionAsync(keep.Id)).Secrets.Token);
        await reader.SaveCollectionAsync("Added after restore",new() { Target="new.invalid" },new()); Assert.Equal(3,(await reader.CollectionsAsync()).Count);
        Assert.Contains(await reader.CollectionsAsync(),c=>c.Id==old.Id); Assert.Equal("keep-token",(await reader.LoadCollectionAsync(keep.Id)).Secrets.Token);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task An_unavailable_Secret_Service_tool_offers_retry_without_claiming_that_the_content_was_deleted(bool collection)
    {
        var repo=await Repository("source"); var vault=new Vault(); await new CommandStore(repo,vault).SaveLibraryAsync(Library("fixture"));
        var network=new NetworkToolsStore(repo,vault); await network.SaveCollectionAsync("Fixture",new() { Target="fixture.invalid" },new());
        var id=Assert.Single(await network.CollectionsAsync()).Id; var restored=await Restore(await repo.CreateDatabaseBackupAsync());
        // This path does not exist. No real secret-tool, session bus or OS vault
        // can be reached, even on the Linux desktop where the tests may run.
        var unavailable=new SecretServiceCredentialStore(Path.Combine(root,"absent-fixture-tool-"+Guid.NewGuid().ToString("N")));
        var key=collection?"networkTools.collections.v1":"commands.vault.v1"; var pointer=await restored.SettingAsync(key);
        async Task Read() { if(collection)await new NetworkToolsStore(restored,unavailable).LoadCollectionAsync(id); else await new CommandStore(restored,unavailable).ReadLibraryAsync(); }
        var error=await Assert.ThrowsAsync<ProtectedContentUnavailableException>(Read); var english=AppText.T(error.Message,"en");
        Assert.Contains("did not provide",english); Assert.Contains("Unlock it and retry",english); Assert.Contains("SQLite",english);
        Assert.Equal(error.Message,collection?NetworkToolsService.Explain(error):OperationErrorMessages.Describe(error));
        Assert.Equal(pointer,await restored.SettingAsync(key));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Reported_vault_read_errors_retain_their_cause_and_retry_preserves_the_restored_references(bool collection)
    {
        var repo=await Repository("source"); var vault=new Vault(); var library=Library("locked-fixture");
        await new CommandStore(repo,vault).SaveLibraryAsync(library); var network=new NetworkToolsStore(repo,vault);
        await network.SaveCollectionAsync("Keep",new() { Target="fixture.invalid" },new(Token:"keep-token")); var id=Assert.Single(await network.CollectionsAsync()).Id;
        var restored=await Restore(await repo.CreateDatabaseBackupAsync()); var commands=new CommandStore(restored,vault); var reader=new NetworkToolsStore(restored,vault);
        var pointer=await restored.SettingAsync(collection?"networkTools.collections.v1":"commands.vault.v1"); var before=new Dictionary<Guid,string>(vault.Values); vault.Locked=true;
        async Task Read() { if(collection)await reader.LoadCollectionAsync(id); else await commands.ReadLibraryAsync(); }
        var error=await Assert.ThrowsAsync<CredentialReadException>(Read); Assert.IsType<IOException>(error.InnerException);
        Unchanged(vault,before); Assert.Equal(pointer,await restored.SettingAsync(collection?"networkTools.collections.v1":"commands.vault.v1"));
        vault.Locked=false; await Read(); Unchanged(vault,before);
    }

    public void Dispose()
    {
        foreach(var directory in directories)DatabaseTestCleanup.Clear(directory);
        if(Directory.Exists(root))Directory.Delete(root,true);
    }
}
