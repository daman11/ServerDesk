using Microsoft.Data.Sqlite;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    public async Task<(ProfileSort Mode,bool Descending)> ProfileOrderingAsync(CancellationToken ct=default)
    {
        await using var db=await Open(ct); using var cmd=db.CreateCommand(); cmd.CommandText="SELECT key,value FROM settings WHERE key IN ('serverSort','serverSortDescending')";
        var settings=new Dictionary<string,string>(); await using(var reader=await cmd.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))settings[reader.GetString(0)]=reader.GetString(1);
        var mode=Enum.TryParse<ProfileSort>(settings.GetValueOrDefault("serverSort"),out var stored) && Enum.IsDefined(stored)?stored:ProfileSort.Name;
        var descending=bool.TryParse(settings.GetValueOrDefault("serverSortDescending"),out var direction)?direction:mode==ProfileSort.LastConnected;
        return (mode,descending);
    }
    public async Task SaveProfileOrderingAsync(ProfileSort mode,bool descending,CancellationToken ct=default)
    {
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        await using var db=await Open(ct); await using var transaction=await db.BeginTransactionAsync(ct);
        foreach(var (key,value) in new[]{("serverSort",mode.ToString()),("serverSortDescending",descending.ToString())})
        {
            using var cmd=db.CreateCommand(); cmd.Transaction=(SqliteTransaction)transaction;
            cmd.CommandText="INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            cmd.Parameters.AddWithValue("$key",key); cmd.Parameters.AddWithValue("$value",value); await cmd.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
