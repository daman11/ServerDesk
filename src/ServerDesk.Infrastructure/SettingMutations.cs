using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    // Read/modify/write under one SQLite writer lock, also across windows and
    // processes. The callback must be bounded and must never access the vault.
    internal Task UpdateSettingAsync(string key,Func<string?,string> update,CancellationToken ct)=>Task.Run(async()=>
    {
        await using var db=await Open(ct); using var transaction=db.BeginTransaction(); using var command=db.CreateCommand(); command.Transaction=transaction;
        command.CommandText="SELECT value FROM settings WHERE key=$key"; command.Parameters.AddWithValue("$key",key);
        var replacement=update(await command.ExecuteScalarAsync(ct) as string); ct.ThrowIfCancellationRequested();
        command.CommandText="INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        command.Parameters.AddWithValue("$value",replacement); await command.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
    },ct);
    internal Task CompareExchangeSettingAsync(string key,string? expected,string value,CancellationToken ct)=>UpdateSettingAsync(key,current=>
    {
        if(current!=expected)throw new InvalidOperationException(L.T("Zapisane dane zmieniły się w innym oknie. Wczytaj je ponownie przed zapisem. Dotychczasowa zawartość pozostaje zachowana."));
        return value;
    },ct);
}
