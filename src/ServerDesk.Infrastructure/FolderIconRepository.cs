using Microsoft.Data.Sqlite;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    public async Task<Dictionary<string,string>> FolderIconsAsync(CancellationToken ct=default)
    {
        await using var db=await Open(ct); return await ReadFolderIconsAsync(db,null,ct);
    }
    private static async Task<Dictionary<string,string>> ReadFolderIconsAsync(SqliteConnection db,SqliteTransaction? transaction,CancellationToken ct)
    {
        using var cmd=db.CreateCommand(); cmd.Transaction=transaction;
        cmd.CommandText="SELECT path,icon FROM folder_icons";
        await using var reader=await cmd.ExecuteReaderAsync(ct); var result=new Dictionary<string,string>(StringComparer.Ordinal);
        while(await reader.ReadAsync(ct))result.Add(reader.GetString(0),reader.GetString(1));
        return result;
    }
    public async Task SetFolderIconAsync(string path,string icon,CancellationToken ct=default)
    {
        ProfileIcons.Validate(icon);
        await using var db=await Open(ct); using var cmd=db.CreateCommand();
        cmd.CommandText="INSERT INTO folder_icons(path,icon) SELECT path,$icon FROM folders WHERE path=$path ON CONFLICT(path) DO UPDATE SET icon=excluded.icon";
        cmd.Parameters.AddWithValue("$path",path); cmd.Parameters.AddWithValue("$icon",icon);
        if(await cmd.ExecuteNonQueryAsync(ct)==0)throw new InvalidOperationException(L.T("Folder nie istnieje."));
    }
}
