using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Text;
using Xunit;

namespace ServerDesk.Tests;
public sealed class RepositoryTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-tests-"+Guid.NewGuid());
    private ProfileRepository Repo()=>new(new AppPaths(root));
    private static ServerProfile Profile()=>new() { Name="Żółty serwer",Host="localhost",Username="admin",Folder="Prod/Linux",Tags="test,ą",Favorite=true };
    [Fact] public async Task SaveUpdateDeleteAndReopen()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(); await repo.SaveAsync(p); await repo.SaveAsync(p with { Port=2222 });
        var read=Assert.Single(await Repo().ListAsync()); Assert.Equal(2222,read.Port); Assert.Equal(p.Id,read.Id); Assert.Contains("Prod/Linux",await repo.FoldersAsync());
        await repo.DeleteAsync(p.Id); Assert.Empty(await repo.ListAsync());
    }
    [Fact] public async Task ExportImportUsesNewCredentialIdentityAndUnicode()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(); await repo.SaveAsync(p); using var stream=new MemoryStream(); await repo.ExportAsync(stream);
        var json=Encoding.UTF8.GetString(stream.ToArray()); Assert.DoesNotContain("Password",json); Assert.DoesNotContain("secret",json,StringComparison.OrdinalIgnoreCase); stream.Position=0;
        await repo.ImportAsync(stream); var imported=(await repo.ListAsync()).Single(x=>x.Id!=p.Id); Assert.Equal(p.Name,imported.Name); Assert.Equal(p.Tags,imported.Tags); Assert.Null(imported.LastConnected);
    }
    [Fact] public async Task InvalidImportIsAtomic()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SaveAsync(Profile());
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":1,\"Servers\":[{\"Name\":\"ok\",\"Host\":\"localhost\",\"Username\":\"user\"},{\"Name\":\"bad\",\"Host\":\"\"}],\"Folders\":[]}"));
        await Assert.ThrowsAsync<ArgumentException>(()=>repo.ImportAsync(stream)); Assert.Single(await repo.ListAsync());
    }
    [Fact] public async Task HostKeysAreScopedToPortAndCannotBeSilentlyReplaced()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.TrustHostAsync("HOST",22,"first"); Assert.Equal("first",await repo.HostKeyAsync("host",22)); Assert.Null(await repo.HostKeyAsync("host",2222));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.TrustHostAsync("host",22,"changed")); Assert.Equal("first",await repo.HostKeyAsync("host",22));
    }
    [Fact] public async Task SettingsAndLastConnectionPersist()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(); await repo.SaveAsync(p); await repo.ConnectedAsync(p); await repo.SetSettingAsync("theme","light");
        Assert.Equal("light",await Repo().SettingAsync("theme")); Assert.NotNull(Assert.Single(await repo.ListAsync()).LastConnected);
    }
    [Fact] public async Task Reconnecting_old_session_does_not_overwrite_saved_visuals_or_edits()
    {
        var repo=Repo(); await repo.InitializeAsync(); var old=Profile(); await repo.SaveAsync(old);
        await repo.SaveAsync(old with { Name="Updated",IconId="database",Tags="new tags" }); await repo.ConnectedAsync(old);
        var current=Assert.Single(await repo.ListAsync()); Assert.Equal("Updated",current.Name); Assert.Equal("database",current.IconId); Assert.Equal("new tags",current.Tags); Assert.NotNull(current.LastConnected);
    }
    [Fact] public async Task FolderRenameIsRecursiveAndNonemptyDeleteIsBlocked()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(); await repo.SaveAsync(p); Assert.Contains("Prod",await repo.FoldersAsync());
        await repo.RenameFolderAsync("Prod","Production"); Assert.Equal("Production/Linux",Assert.Single(await repo.ListAsync()).Folder);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.DeleteFolderAsync("Production")); await repo.DeleteAsync(p.Id); await repo.DeleteFolderAsync("Production"); Assert.Empty(await repo.FoldersAsync());
    }
    [Fact] public async Task WindowsCredentialsAreEncryptedAndRemovable()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid(); const string testSecret="test-secret-123";
        await store.SaveAsync(id,testSecret); Assert.Equal(testSecret,await store.GetAsync(id));
        var bytes=await File.ReadAllBytesAsync(Path.Combine(root,"credentials",id+".bin")); Assert.DoesNotContain(testSecret,Encoding.UTF8.GetString(bytes));
        await store.DeleteAsync(id); Assert.Null(await store.GetAsync(id));
    }
    [Fact] public async Task RdpOptionsPersistAndExportAcrossImport()
    {
        var repo=Repo(); await repo.InitializeAsync(); var profile=new ServerProfile { Name="RDP",Protocol=Protocol.RDP,Host="localhost",Port=3389,Username="ACME\\admin",Rdp=new() { DisplayMode=RdpDisplayMode.Embedded,Width=1920,Height=1080,Clipboard=false,Fullscreen=true,KeyboardMode=RdpKeyboardMode.Remote,AutoReconnect=true } }; await repo.SaveAsync(profile);
        Assert.Equal(profile.Rdp,Assert.Single(await repo.ListAsync()).Rdp); using var stream=new MemoryStream(); await repo.ExportAsync(stream); stream.Position=0; await repo.ImportAsync(stream); Assert.All(await repo.ListAsync(),p=>Assert.Equal(profile.Rdp,p.Rdp));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
    [Fact] public async Task MovingDraggedSnapshotPreservesLatestProfileEdits()
    {
        var repo=Repo(); await repo.InitializeAsync(); var original=new ServerProfile { Name="Original",Host="localhost",Username="admin" }; await repo.SaveAsync(original);
        await repo.SaveAsync(original with { Name="Edited",Color="#112233",Description="New description" });
        await repo.MoveToFolderAsync(original.Id,"Production/Linux"); var saved=Assert.Single(await repo.ListAsync());
        Assert.Equal("Edited",saved.Name); Assert.Equal("#112233",saved.Color); Assert.Equal("New description",saved.Description); Assert.Equal("Production/Linux",saved.Folder);
    }
}
