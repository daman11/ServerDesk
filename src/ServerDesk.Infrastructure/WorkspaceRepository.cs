using Microsoft.Data.Sqlite;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    // Read the preference and profile identities under the same write transaction
    // as the snapshot. A queued save cannot resurrect an opted-out workspace.
    public async Task SaveWorkspaceSnapshotAsync(WorkspaceSnapshot snapshot,CancellationToken ct=default)
    {
        await using var db=await Open(ct);
        using var transaction=db.BeginTransaction();
        using var command=db.CreateCommand(); command.Transaction=transaction;
        command.CommandText="SELECT value FROM settings WHERE key='rememberWorkspace'";
        var enabled=await command.ExecuteScalarAsync(ct) as string!="False";
        var json="";
        if(enabled)
        {
            command.CommandText="SELECT id FROM profiles";
            var known=new HashSet<Guid>();
            await using(var reader=await command.ExecuteReaderAsync(ct))
                while(await reader.ReadAsync(ct))known.Add(Guid.Parse(reader.GetString(0)));
            var available=snapshot.AvailableProfiles(known);
            available=(available with { Sessions=available.Sessions.Take(WorkspaceSnapshot.MaximumSessions).ToList() }).AvailableProfiles(known);
            json=available.Serialize();
        }
        command.CommandText="INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        async Task Write(string key,string value)
        {
            command.Parameters.Clear(); command.Parameters.AddWithValue("$key",key); command.Parameters.AddWithValue("$value",value);
            await command.ExecuteNonQueryAsync(ct);
        }
        await Write("lastWorkspace",json);
        await Write("workspaceMode",snapshot.Mode.ToString());
        await Write("splitRatio",snapshot.Ratio.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await transaction.CommitAsync(ct);
    }
}
