using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class FactorySettingsResetTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-factory-settings-"+Guid.NewGuid().ToString("N"));
    private ProfileRepository Repository()=>new(new AppPaths(root));
    private async Task<ProfileRepository> Modified()
    {
        var repo=Repository(); await repo.InitializeAsync();
        await repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Theme="nord",TerminalFont=19,Preferences=new() { Language="en",InterfaceFontSize=16,DisabledPlugins=new([PluginCatalog.Commands]) } });
        foreach(var (key,value) in new[]{("lastWorkspace","fixture snapshot"),("mainWindowPlacement","fixture placement"),("collapsedFolders","[\"LAB\"]"),("serverSort","Host"),("serverSortDescending","True"),("sftpLocalShare","0.7"),("workspaceMode","Grid"),("sidebarHidden","True"),("x","100"),("y","200"),("width","1440"),("height","900")})await repo.SetSettingAsync(key,value);
        return repo;
    }
    [Fact] public async Task Scheduling_creates_a_verified_copy_and_keeps_live_preferences_until_next_start()
    {
        var repo=await Modified(); var before=await repo.ApplicationSettingsAsync(); var copy=await repo.ScheduleFactorySettingsResetAsync();
        Assert.True(await repo.IsFactorySettingsResetPendingAsync()); Assert.Equal(before,await Repository().ApplicationSettingsAsync()); Assert.True(File.Exists(copy.Path));
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=copy.Path,Mode=SqliteOpenMode.ReadOnly,Pooling=false }.ToString()); await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText="PRAGMA quick_check"; Assert.Equal("ok",await command.ExecuteScalarAsync());
        command.CommandText="SELECT value FROM settings WHERE key='theme'"; Assert.Equal("nord",await command.ExecuteScalarAsync());
        command.CommandText="SELECT value FROM settings WHERE key='pendingFactorySettingsReset'"; Assert.Null(await command.ExecuteScalarAsync());
    }
    [Fact] public async Task Legacy_preferences_only_request_never_becomes_a_full_reset_after_upgrade()
    {
        var repo=await Modified(); var profile=new ServerProfile { Name="Keep me",Host="fixture.invalid",Username="fixture",Folder="LAB",Description="private note",Tags="production" };
        await repo.SaveAsync(profile); await repo.SetFolderIconAsync("LAB","server"); await repo.ConnectedAsync(profile);
        var userSettings=new Dictionary<string,string> { ["connectionTagColors"]="{\"production\":\"#123456\"}",["commandFavorites"]="[\"fixture\"]",["networkTools.custom"]="private fixture",["future.user.data"]="keep unknown data" };
        foreach(var (key,value) in userSettings)await repo.SetSettingAsync(key,value);
        var files=new[]{Path.Combine(root,"credentials","keep.bin"),Path.Combine(root,"logs","keep.log"),Path.Combine(root,"notes","keep.txt")};
        foreach(var path in files) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path,"unchanged fixture"); }
        var before=await repo.ListAsync(); var history=await repo.HistoryAsync(); var icons=await repo.FolderIconsAsync();
        await repo.SetSettingAsync("pendingFactorySettingsReset","1");
        await repo.SetSettingAsync("lastWorkspace","late shutdown snapshot"); await repo.SetSettingAsync("mainWindowPlacement","late shutdown placement"); await repo.SetSettingAsync("sidebar","450");
        var restarted=Repository(); await restarted.InitializeAsync(); Assert.True(await restarted.ApplyPendingFactorySettingsResetAsync());
        Assert.Equal(ApplicationSettings.Defaults,await restarted.ApplicationSettingsAsync()); Assert.False(await restarted.IsFactorySettingsResetPendingAsync());
        foreach(var key in new[]{"lastWorkspace","mainWindowPlacement","collapsedFolders","serverSort","serverSortDescending","sftpLocalShare","workspaceMode","sidebarHidden","x","y","width","height"})Assert.Null(await restarted.SettingAsync(key));
        Assert.Equal(before,await restarted.ListAsync()); Assert.Equal(history,await restarted.HistoryAsync()); Assert.Equal(icons,await restarted.FolderIconsAsync()); Assert.Contains("LAB",await restarted.FoldersAsync());
        foreach(var (key,value) in userSettings)Assert.Equal(value,await restarted.SettingAsync(key)); foreach(var path in files)Assert.Equal("unchanged fixture",await File.ReadAllTextAsync(path));
        await restarted.SetSettingAsync("theme","light"); Assert.False(await restarted.ApplyPendingFactorySettingsResetAsync()); Assert.Equal("light",await restarted.SettingAsync("theme"));
    }
    [Fact] public async Task Cancelling_a_scheduled_reset_preserves_settings_and_the_backup()
    {
        var repo=await Modified(); var before=await repo.ApplicationSettingsAsync(); var backup=await repo.ScheduleFactorySettingsResetAsync();
        await repo.CancelFactorySettingsResetAsync(); await repo.CancelFactorySettingsResetAsync(); Assert.False(await Repository().ApplyPendingFactorySettingsResetAsync()); Assert.Equal(before,await repo.ApplicationSettingsAsync()); Assert.True(File.Exists(backup.Path));
    }
    [Fact] public async Task Backup_failure_and_cancellation_do_not_schedule_a_reset()
    {
        var repo=await Modified(); var before=await repo.ApplicationSettingsAsync(); await File.WriteAllTextAsync(repo.DatabaseBackupDirectory,"fixture blocks directory");
        await Assert.ThrowsAsync<DatabaseBackupException>(()=>repo.ScheduleFactorySettingsResetAsync()); Assert.False(await repo.IsFactorySettingsResetPendingAsync()); Assert.Equal(before,await repo.ApplicationSettingsAsync());
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repo.ScheduleFactorySettingsResetAsync(cancelled.Token)); Assert.False(await repo.IsFactorySettingsResetPendingAsync());
    }
    [Fact] public async Task A_write_failure_rolls_back_all_setting_removals_and_keeps_the_request_retryable()
    {
        var repo=await Modified(); var before=await repo.ApplicationSettingsAsync(); var profile=new ServerProfile { Name="Keep all fixture data",Host="fixture.invalid",Username="fixture",Folder="LAB" };
        await repo.SaveAsync(profile); await repo.ConnectedAsync(profile); await repo.TrustHostAsync("fixture.invalid",22,"fixture fingerprint"); await repo.SetSettingAsync("future.plugin","fixture data"); await repo.ScheduleFactorySettingsResetAsync();
        using(var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()))
        {
            await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText="CREATE TRIGGER reject_settings_reset BEFORE DELETE ON settings WHEN OLD.key='terminalFont' BEGIN SELECT RAISE(ABORT,'Fixture reset failure'); END"; await command.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<SqliteException>(()=>repo.ApplyPendingFactorySettingsResetAsync()); Assert.Equal(before,await repo.ApplicationSettingsAsync()); Assert.True(await repo.IsFactorySettingsResetPendingAsync());
        Assert.Equal(profile.Id,(await repo.ListAsync()).Single().Id); Assert.Single(await repo.HistoryAsync()); Assert.Contains("LAB",await repo.FoldersAsync()); Assert.Equal("fixture fingerprint",await repo.HostKeyAsync("fixture.invalid",22)); Assert.Equal("fixture data",await repo.SettingAsync("future.plugin"));
        using(var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString())) { await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText="DROP TRIGGER reject_settings_reset"; await command.ExecuteNonQueryAsync(); }
        Assert.True(await Repository().ApplyPendingFactorySettingsResetAsync()); Assert.Equal(ApplicationSettings.Defaults,await repo.ApplicationSettingsAsync());
    }
    [Fact] public async Task An_unknown_request_version_does_not_clear_any_settings()
    {
        var repo=await Modified(); var before=await repo.ApplicationSettingsAsync(); await repo.SetSettingAsync("pendingFactorySettingsReset","2"); Assert.False(await repo.ApplyPendingFactorySettingsResetAsync()); Assert.Equal(before,await repo.ApplicationSettingsAsync()); Assert.Equal("2",await repo.SettingAsync("pendingFactorySettingsReset"));
    }
    private sealed class Vault:ICredentialStore
    {
        internal readonly Dictionary<Guid,string> Entries=[];
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Entries.GetValueOrDefault(id));
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { Entries[id]=value; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Entries.Remove(id); return Task.CompletedTask; }
    }
    [Fact] public async Task Full_reset_clears_all_application_state_and_its_latest_backup_restores_it_without_replaying_reset()
    {
        var repo=await Modified(); var vault=new Vault();
        var profile=new ServerProfile { Name="Saved fixture",Host="fixture.invalid",Username="fixture",Folder="LAB",Description="private note",Tags="production" };
        await repo.SaveAsync(profile); await repo.ConnectedAsync(profile); await repo.SetFolderIconAsync("LAB","server"); await repo.TrustHostAsync("fixture.invalid",22,"fixture fingerprint");
        await new CommandStore(repo,vault).SaveLibraryAsync(new() { Commands=[CommandEngineTests.Snippet()] });
        var network=new NetworkToolsStore(repo,vault); var request=new NetworkRequest { Tool=NetworkToolKind.Ping,Target="127.0.0.1" };
        await network.AddHistoryAsync(request); await network.SaveCollectionAsync("Fixture",request,new());
        await repo.SetSettingAsync("future.plugin.document","private fixture"); await repo.SetSettingAsync("connectionTagColors","fixture colors");
        var retainedFiles=new[]{Path.Combine(root,"credentials","existing.bin"),Path.Combine(root,"logs","existing.log"),Path.Combine(root,"external-notes.txt")};
        foreach(var path in retainedFiles) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path,"retained fixture bytes"); }
        var encrypted=vault.Entries.ToDictionary();
        var scheduleCopy=await repo.ScheduleFactorySettingsResetAsync(); Assert.Equal(FactorySettingsResetScope.Application,await repo.PendingFactorySettingsResetAsync());
        var late=profile with { Id=Guid.NewGuid(),Name="Written after scheduling" }; await repo.SaveAsync(late); await repo.SetSettingAsync("lastWorkspace","late shutdown layout");
        Assert.True(await Repository().ApplyPendingFactorySettingsResetAsync());
        Assert.Equal(ApplicationSettings.Defaults,await repo.ApplicationSettingsAsync()); Assert.Empty(await repo.ListAsync()); Assert.Empty(await repo.HistoryAsync()); Assert.Empty(await repo.FoldersAsync()); Assert.Empty(await repo.FolderIconsAsync()); Assert.Null(await repo.HostKeyAsync("fixture.invalid",22));
        Assert.Null(await repo.SettingAsync("future.plugin.document")); Assert.Null(await repo.SettingAsync("connectionTagColors")); Assert.Empty((await new CommandStore(repo,vault).ReadLibraryAsync()).Commands); Assert.Empty(await network.CollectionsAsync()); Assert.Empty(await network.HistoryAsync());
        Assert.Equal(encrypted.OrderBy(e=>e.Key),vault.Entries.OrderBy(e=>e.Key)); Assert.False(await Repository().ApplyPendingFactorySettingsResetAsync());
        foreach(var path in retainedFiles)Assert.Equal("retained fixture bytes",await File.ReadAllTextAsync(path));
        var copies=(await DatabaseBackupCatalog.ReadAsync(repo.DatabaseBackupDirectory)).Entries;
        Assert.Equal(2,copies.Count); var latest=Assert.Single(copies,e=>e.BeforeReset); Assert.False(latest.BeforeMigration); Assert.True(File.Exists(scheduleCopy.Path));
        var restoredPaths=new AppPaths(Path.Combine(root,"odtworzone żółć")); File.Copy(latest.Path,Path.Combine(restoredPaths.Data,"serverdesk.db"));
        var restored=new ProfileRepository(restoredPaths); await restored.InitializeAsync(); Assert.False(await restored.ApplyPendingFactorySettingsResetAsync());
        Assert.Equal(2,(await restored.ListAsync()).Count); Assert.Contains(await restored.ListAsync(),p=>p.Id==late.Id); Assert.Single(await restored.HistoryAsync()); Assert.Equal("private fixture",await restored.SettingAsync("future.plugin.document")); Assert.Equal("late shutdown layout",await restored.SettingAsync("lastWorkspace"));
        Assert.Single((await new CommandStore(restored,vault).ReadLibraryAsync()).Commands); Assert.Single(await new NetworkToolsStore(restored,vault).CollectionsAsync());
        Assert.Equal("fixture fingerprint",await restored.HostKeyAsync("fixture.invalid",22)); Assert.Null(await restored.SettingAsync("pendingFactorySettingsReset"));
    }
    [Fact] public async Task Startup_backup_failure_or_cancellation_preserves_the_entire_state_and_retry_request()
    {
        var repo=await Modified(); var profile=new ServerProfile { Name="Preserve",Host="fixture.invalid",Username="fixture" }; await repo.SaveAsync(profile); await repo.ConnectedAsync(profile);
        var settings=await repo.ApplicationSettingsAsync(); await repo.ScheduleFactorySettingsResetAsync();
        using(var cancelled=new CancellationTokenSource()) { cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repo.ApplyPendingFactorySettingsResetAsync(cancelled.Token)); }
        var retained=Path.Combine(root,"retained-copies"); Directory.Move(repo.DatabaseBackupDirectory,retained); await File.WriteAllTextAsync(repo.DatabaseBackupDirectory,"blocks directory creation");
        await Assert.ThrowsAsync<DatabaseBackupException>(()=>repo.ApplyPendingFactorySettingsResetAsync());
        Assert.True(await repo.IsFactorySettingsResetPendingAsync()); Assert.Equal(settings,await repo.ApplicationSettingsAsync()); Assert.Equal(profile.Id,(await repo.ListAsync()).Single().Id); Assert.Single(await repo.HistoryAsync());
        File.Delete(repo.DatabaseBackupDirectory); Directory.Move(retained,repo.DatabaseBackupDirectory);
        Assert.True(await repo.ApplyPendingFactorySettingsResetAsync()); Assert.Empty(await repo.ListAsync()); Assert.Empty(await repo.HistoryAsync());
    }
    [Fact] public async Task Any_manual_backup_omits_a_pending_destructive_request()
    {
        var repo=await Modified(); await repo.ScheduleFactorySettingsResetAsync(); var backup=await repo.CreateDatabaseBackupAsync();
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=backup.Path,Mode=SqliteOpenMode.ReadOnly,Pooling=false }.ToString()); await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText="SELECT value FROM settings WHERE key='pendingFactorySettingsReset'"; Assert.Null(await command.ExecuteScalarAsync());
        Assert.True(await repo.IsFactorySettingsResetPendingAsync());
    }
    [Theory][InlineData("1")][InlineData("2")][InlineData("application-v1")]
    public async Task Scheduling_never_silently_promotes_or_replaces_an_existing_request(string value)
    {
        var repo=await Modified(); await repo.SetSettingAsync("pendingFactorySettingsReset",value);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.ScheduleFactorySettingsResetAsync());
        Assert.Equal(value,await repo.SettingAsync("pendingFactorySettingsReset")); Assert.False(Directory.Exists(repo.DatabaseBackupDirectory));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); DatabaseTestCleanup.Clear(Path.Combine(root,"odtworzone żółć")); if(Directory.Exists(root))Directory.Delete(root,true); }
}
