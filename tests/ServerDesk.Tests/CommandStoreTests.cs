using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class CommandStoreTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-commands-"+Guid.NewGuid());
    private sealed class Vault : ICredentialStore
    {
        internal Dictionary<Guid,string> Data=[];
        internal bool Fail;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Data.GetValueOrDefault(id));
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { CredentialLimits.Validate(value); if(Fail)throw new IOException("fixture vault unavailable"); Data[id]=value; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Data.Remove(id); return Task.CompletedTask; }
    }
    [Fact] public async Task CRUD_restart_categories_and_large_definitions_use_encrypted_vault_chunks_not_plaintext_SQLite()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var vault=new Vault(); var store=new CommandStore(repo,vault);
        var document=new CommandUserLibrary { Commands=Enumerable.Range(0,90).Select(i=>CommandEngineTests.Snippet() with { Id="user."+i,Explanation=new string('x',5000) }).ToArray(),Categories=["Linux/Work","Linux/Empty"] };
        await store.SaveLibraryAsync(document); Assert.True(vault.Data.Count>1);
        foreach(var part in vault.Data.Values)Assert.True(System.Text.Encoding.UTF8.GetByteCount(part)<CredentialLimits.MaximumSecretBytes);
        var loaded=await new CommandStore(new(new AppPaths(root)),vault).ReadLibraryAsync(); Assert.Equal(90,loaded.Commands.Length); Assert.Equal(document.Categories,loaded.Categories); Assert.Equal(document.Commands[0].Template,loaded.Commands[0].Template);
        var pointer=await repo.SettingAsync("commands.vault.v1"); Assert.DoesNotContain("echo",pointer!); Assert.DoesNotContain("hello",pointer!);
        var edited=loaded with { Commands=loaded.Commands.Skip(1).Select(c=>c with { Category="Moved/Folder" }).ToArray() }; var previous=vault.Data.Keys.ToArray();
        await store.SaveLibraryAsync(edited); Assert.Equal(89,(await store.ReadLibraryAsync()).Commands.Length); Assert.DoesNotContain(vault.Data.Keys,id=>previous.Contains(id));
        vault.Fail=true; await Assert.ThrowsAsync<IOException>(()=>store.SaveLibraryAsync(document)); Assert.Equal(89,(await store.ReadLibraryAsync()).Commands.Length);
    }
    [Fact] public async Task Favorites_history_and_manual_per_host_preferences_persist_as_references_only()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var vault=new Vault(); var store=new CommandStore(repo,vault); var host=Guid.NewGuid();
        var state=new CommandUserState { Favorites=["linux.net.listeners"],Recent=[new("linux.kernel",DateTimeOffset.UtcNow)],Preferences=new() { Automatic=false,Pinned=true,System="ubuntu",Version="24.04",Shell=CommandShell.Zsh,RememberRecent=true },Hosts=new() { [host]=new() { System="windows-server",Automatic=false,Shell=CommandShell.PowerShell } } };
        await store.SaveStateAsync(state); var read=await new CommandStore(new(new AppPaths(root)),vault).ReadStateAsync(); Assert.Equal(state.Favorites,read.Favorites); Assert.Equal(state.Recent,read.Recent); Assert.Equal(state.Preferences,read.Preferences); Assert.Equal(state.Hosts[host],read.Hosts[host]); Assert.Empty(vault.Data);
        var json=await repo.SettingAsync("commands.state.v1"); Assert.DoesNotContain("template",json!); Assert.DoesNotContain("parameters",json!);
    }
    [Fact] public async Task Locked_or_missing_vault_does_not_create_empty_replacement_or_fallback_to_plaintext()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var vault=new Vault(); var store=new CommandStore(repo,vault);
        await store.SaveLibraryAsync(new() { Commands=[CommandEngineTests.Snippet()] }); var pointer=await repo.SettingAsync("commands.vault.v1"); vault.Data.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>store.ReadLibraryAsync()); Assert.Equal(pointer,await repo.SettingAsync("commands.vault.v1"));
        await repo.SetSettingAsync("commands.state.v1","{\"preferences\":{\"system\":\"unsupported\"}}"); await Assert.ThrowsAsync<InvalidDataException>(()=>store.ReadStateAsync());
    }
    [Fact] public void Import_is_bounded_validated_and_does_not_bind_new_commands_to_foreign_hosts()
    {
        var original=new CommandUserLibrary { Commands=[CommandEngineTests.Snippet()] };
        var imported=original with { Commands=[original.Commands[0] with { HostId=Guid.NewGuid(),HostFolder="Production" }] };
        var merged=CommandStore.Merge(original,imported); Assert.Null(merged.Commands[1].HostId); Assert.Empty(merged.Commands[1].HostFolder);
        Assert.Throws<ArgumentException>(()=>CommandStore.Parse(JsonSerializer.Serialize(original with { Commands=[original.Commands[0],original.Commands[0]] },CommandValidation.Json)));
        Assert.Throws<InvalidDataException>(()=>CommandStore.Parse(new string(' ',CommandValidation.MaxDocumentBytes+1)));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
