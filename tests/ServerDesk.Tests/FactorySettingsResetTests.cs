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
    [Fact] public async Task Startup_reset_survives_late_shutdown_writes_and_preserves_user_data()
    {
        var repo=await Modified(); var profile=new ServerProfile { Name="Keep me",Host="fixture.invalid",Username="fixture",Folder="LAB",Description="private note",Tags="production" };
        await repo.SaveAsync(profile); await repo.SetFolderIconAsync("LAB","server"); await repo.ConnectedAsync(profile);
        var userSettings=new Dictionary<string,string> { ["connectionTagColors"]="{\"production\":\"#123456\"}",["commandFavorites"]="[\"fixture\"]",["networkTools.custom"]="private fixture",["future.user.data"]="keep unknown data" };
        foreach(var (key,value) in userSettings)await repo.SetSettingAsync(key,value);
        var files=new[]{Path.Combine(root,"credentials","keep.bin"),Path.Combine(root,"logs","keep.log"),Path.Combine(root,"notes","keep.txt")};
        foreach(var path in files) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path,"unchanged fixture"); }
        var before=await repo.ListAsync(); var history=await repo.HistoryAsync(); var icons=await repo.FolderIconsAsync();
        await repo.ScheduleFactorySettingsResetAsync();
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
        var repo=await Modified(); var before=await repo.ApplicationSettingsAsync(); await repo.ScheduleFactorySettingsResetAsync();
        using(var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()))
        {
            await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText="CREATE TRIGGER reject_settings_reset BEFORE DELETE ON settings WHEN OLD.key='terminalFont' BEGIN SELECT RAISE(ABORT,'Fixture reset failure'); END"; await command.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<SqliteException>(()=>repo.ApplyPendingFactorySettingsResetAsync()); Assert.Equal(before,await repo.ApplicationSettingsAsync()); Assert.True(await repo.IsFactorySettingsResetPendingAsync());
        using(var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString())) { await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText="DROP TRIGGER reject_settings_reset"; await command.ExecuteNonQueryAsync(); }
        Assert.True(await Repository().ApplyPendingFactorySettingsResetAsync()); Assert.Equal(ApplicationSettings.Defaults,await repo.ApplicationSettingsAsync());
    }
    [Fact] public async Task An_unknown_request_version_does_not_clear_any_settings()
    {
        var repo=await Modified(); var before=await repo.ApplicationSettingsAsync(); await repo.SetSettingAsync("pendingFactorySettingsReset","2"); Assert.False(await repo.ApplyPendingFactorySettingsResetAsync()); Assert.Equal(before,await repo.ApplicationSettingsAsync()); Assert.Equal("2",await repo.SettingAsync("pendingFactorySettingsReset"));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
