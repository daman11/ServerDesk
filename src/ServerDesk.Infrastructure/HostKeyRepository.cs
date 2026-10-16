using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    public async Task<IReadOnlyList<TrustedHostKey>> TrustedHostKeysAsync(CancellationToken ct=default)
    {
        await using var db=await Open(ct); using var cmd=db.CreateCommand();
        cmd.CommandTimeout=3;
        cmd.CommandText="SELECT host,port,fingerprint FROM hostkeys ORDER BY host COLLATE NOCASE,port";
        await using var reader=await cmd.ExecuteReaderAsync(ct); var result=new List<TrustedHostKey>();
        while(await reader.ReadAsync(ct))result.Add(new(reader.GetString(0),reader.GetInt32(1),reader.GetString(2)));
        return result;
    }

    public async Task<bool> ForgetHostKeyAsync(TrustedHostKey expected,CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        await using var db=await Open(ct); using var cmd=db.CreateCommand();
        cmd.CommandTimeout=3;
        // Remove exactly what the operator reviewed. A stale dialog cannot delete
        // a different key trusted by another window/process in the meantime.
        cmd.CommandText="DELETE FROM hostkeys WHERE host=$host AND port=$port AND fingerprint=$fingerprint";
        cmd.Parameters.AddWithValue("$host",expected.Host.ToLowerInvariant()); cmd.Parameters.AddWithValue("$port",expected.Port); cmd.Parameters.AddWithValue("$fingerprint",expected.Fingerprint);
        return await cmd.ExecuteNonQueryAsync(ct)==1;
    }
}
