using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using System.Text.Json;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    private const string TagColorsKey="connectionTagColors";
    public async Task<Dictionary<string,string>> TagColorsAsync(CancellationToken ct=default)
    {
        await using var db=await Open(ct); return await ReadTagColorsAsync(db,null,ct);
    }
    private static async Task<Dictionary<string,string>> ReadTagColorsAsync(SqliteConnection db,SqliteTransaction? transaction,CancellationToken ct)
    {
        using var cmd=db.CreateCommand(); cmd.Transaction=transaction; cmd.CommandText="SELECT value FROM settings WHERE key=$key"; cmd.Parameters.AddWithValue("$key",TagColorsKey);
        var json=await cmd.ExecuteScalarAsync(ct) as string;
        return ProfileTags.ValidateColors(json==null?null:JsonSerializer.Deserialize<Dictionary<string,string>>(json));
    }
    public Task SaveTagColorsAsync(IReadOnlyDictionary<string,string> colors,CancellationToken ct=default)
        =>SetSettingAsync(TagColorsKey,JsonSerializer.Serialize(ProfileTags.ValidateColors(colors)),ct);
    private static async Task MergeTagColorsAsync(SqliteConnection db,SqliteTransaction transaction,Dictionary<string,string> imported,CancellationToken ct)
    {
        var colors=await ReadTagColorsAsync(db,transaction,ct);
        foreach(var (name,color) in imported)colors.TryAdd(name,color); // Explicit local colors win.
        ProfileTags.ValidateColors(colors);
        using var cmd=db.CreateCommand(); cmd.Transaction=transaction; cmd.CommandText="INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$key",TagColorsKey); cmd.Parameters.AddWithValue("$value",JsonSerializer.Serialize(colors)); await cmd.ExecuteNonQueryAsync(ct);
    }
}
