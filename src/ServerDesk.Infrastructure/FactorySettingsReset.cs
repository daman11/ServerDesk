using Microsoft.Data.Sqlite;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public enum FactorySettingsResetScope { None,Preferences,Application }

public sealed partial class ProfileRepository
{
    private const string FactoryResetPendingKey="pendingFactorySettingsReset";
    private const string FullFactoryResetRequest="application-v1";
    // Explicit allowlist: settings also contains plugin documents, command
    // favorites and connection tag colors, which are user data, not preferences.
    private static readonly string[] FactorySettingKeys=[
        "theme","terminalPalette","terminalFont","monitorInterval","rememberWorkspace","applicationPreferences","skippedUpdateVersion","sidebar",
        "lastWorkspace","mainWindowPlacement","x","y","width","height","sidebarHidden","collapsedFolders",
        "workspaceMode","splitRatio","rowSplitRatio","sftpLocalShare","serverSort","serverSortDescending"
    ];
    public async Task<FactorySettingsResetScope> PendingFactorySettingsResetAsync(CancellationToken ct=default)=>(await SettingAsync(FactoryResetPendingKey,ct)) switch {
        "1"=>FactorySettingsResetScope.Preferences,
        FullFactoryResetRequest=>FactorySettingsResetScope.Application,
        _=>FactorySettingsResetScope.None };
    public async Task<bool> IsFactorySettingsResetPendingAsync(CancellationToken ct=default)=>await PendingFactorySettingsResetAsync(ct)!=FactorySettingsResetScope.None;
    public async Task<DatabaseBackup> ScheduleFactorySettingsResetAsync(CancellationToken ct=default)
    {
        // Do not queue anything unless a coherent, verified database copy exists.
        // Active windows may still save their layout at shutdown: consume the
        // request at startup, after those writers have finished.
        if(await SettingAsync(FactoryResetPendingKey,ct)!=null)throw new InvalidOperationException(L.T("Istnieje już plan resetu. Anuluj go przed zaplanowaniem nowego resetu."));
        var backup=await CreateDatabaseBackupAsync(ct);
        await using var db=await Open(ct); using var command=db.CreateCommand();
        command.CommandText="INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO NOTHING";
        command.Parameters.AddWithValue("$key",FactoryResetPendingKey); command.Parameters.AddWithValue("$value",FullFactoryResetRequest);
        if(await command.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException(L.T("Istnieje już plan resetu. Anuluj go przed zaplanowaniem nowego resetu."));
        return backup;
    }
    public Task CancelFactorySettingsResetAsync(CancellationToken ct=default)=>Execute("DELETE FROM settings WHERE key=$key",ct,("$key",FactoryResetPendingKey));
    public Task<bool> ApplyPendingFactorySettingsResetAsync(CancellationToken ct=default)=>Task.Run(()=>ApplyFactoryResetCoreAsync(ct),ct);
    private async Task<bool> ApplyFactoryResetCoreAsync(CancellationToken ct)
    {
        await using var db=await Open(ct); using var command=db.CreateCommand();
        command.CommandText="SELECT value FROM settings WHERE key=$key"; command.Parameters.AddWithValue("$key",FactoryResetPendingKey);
        var request=await command.ExecuteScalarAsync(ct) as string;
        if(request is not ("1" or FullFactoryResetRequest))return false;
        // Retain one read snapshot through backup and deletion. A competing
        // writer makes the read-to-write upgrade fail, never erases newer data.
        using var transaction=db.BeginTransaction(deferred:true); command.Transaction=transaction;
        request=await command.ExecuteScalarAsync(ct) as string;
        if(request is not ("1" or FullFactoryResetRequest))return false;
        if(request==FullFactoryResetRequest)
        {
            // Include final shutdown writes, not just the earlier scheduling
            // snapshot. Keep encrypted vault blobs in place for local recovery;
            // remove every active reference to them from the application state.
            command.CommandText="PRAGMA user_version";
            var schema=Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            if(schema!=CurrentSchemaVersion)throw new InvalidDataException(L.T("Baza pochodzi z nowszej wersji ServerDesk."));
            DatabaseBackups.Create(db,DatabaseBackupDirectory,schema,"before-reset",ct);
            command.Parameters.Clear();
            command.CommandText="DELETE FROM history; DELETE FROM folder_icons; DELETE FROM folders; DELETE FROM hostkeys; DELETE FROM profiles; DELETE FROM settings";
            await command.ExecuteNonQueryAsync(ct);
        }
        else
        {
            // Old preferences-only requests never acquire a destructive scope
            // merely because the application was upgraded.
            command.CommandText="DELETE FROM settings WHERE key=$key";
            foreach(var key in FactorySettingKeys.Append(FactoryResetPendingKey)) { command.Parameters["$key"].Value=key; await command.ExecuteNonQueryAsync(ct); }
        }
        await transaction.CommitAsync(ct); return true;
    }
}
