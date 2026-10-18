using Microsoft.Data.Sqlite;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    private const string FactoryResetPendingKey="pendingFactorySettingsReset";
    // Explicit allowlist: settings also contains plugin documents, command
    // favorites and connection tag colors, which are user data, not preferences.
    private static readonly string[] FactorySettingKeys=[
        "theme","terminalPalette","terminalFont","monitorInterval","rememberWorkspace","applicationPreferences","sidebar",
        "lastWorkspace","mainWindowPlacement","x","y","width","height","sidebarHidden","collapsedFolders",
        "workspaceMode","splitRatio","rowSplitRatio","sftpLocalShare","serverSort","serverSortDescending"
    ];
    public async Task<bool> IsFactorySettingsResetPendingAsync(CancellationToken ct=default)=>await SettingAsync(FactoryResetPendingKey,ct)=="1";
    public async Task<DatabaseBackup> ScheduleFactorySettingsResetAsync(CancellationToken ct=default)
    {
        // Do not queue anything unless a coherent, verified database copy exists.
        // Active windows may still save their layout at shutdown: consume the
        // request at startup, after those writers have finished.
        var backup=await CreateDatabaseBackupAsync(ct);
        await SetSettingAsync(FactoryResetPendingKey,"1",ct);
        return backup;
    }
    public Task CancelFactorySettingsResetAsync(CancellationToken ct=default)=>Execute("DELETE FROM settings WHERE key=$key",ct,("$key",FactoryResetPendingKey));
    public async Task<bool> ApplyPendingFactorySettingsResetAsync(CancellationToken ct=default)
    {
        await using var db=await Open(ct); using var command=db.CreateCommand();
        command.CommandText="SELECT value FROM settings WHERE key=$key"; command.Parameters.AddWithValue("$key",FactoryResetPendingKey);
        if(await command.ExecuteScalarAsync(ct) as string!="1")return false;
        // Ordinary startup needs no writer lock. Recheck under the write lock
        // before consuming a request that another instance may have cancelled.
        using var transaction=db.BeginTransaction(); command.Transaction=transaction;
        if(await command.ExecuteScalarAsync(ct) as string!="1")return false;
        command.CommandText="DELETE FROM settings WHERE key=$key";
        foreach(var key in FactorySettingKeys.Append(FactoryResetPendingKey)) { command.Parameters["$key"].Value=key; await command.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct); return true;
    }
}
