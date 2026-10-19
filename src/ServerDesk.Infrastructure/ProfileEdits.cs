using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    // Commit the new local vault binding with the new identity. A database error
    // must leave the previous profile AND its credential usable. No vault I/O is
    // performed until this transaction succeeds.
    public Task<ServerProfile> SaveEditedProfileAsync(ServerProfile? original,ServerProfile edited,CancellationToken ct=default)=>Task.Run(async()=>
    {
        edited.Validate();
        if(original!=null && original.Id!=edited.Id)throw new ArgumentException(L.T("Niepoprawne dane profilu."));
        await using var db=await Open(ct); using var transaction=db.BeginTransaction(); using var command=db.CreateCommand(); command.Transaction=transaction;
        command.CommandText="SELECT json FROM profiles WHERE id=$id"; command.Parameters.AddWithValue("$id",edited.Id.ToString());
        var current=await command.ExecuteScalarAsync(ct) as string;
        if(original==null?current!=null:current==null || JsonSerializer.Deserialize<ServerProfile>(current)!=original)
            throw new InvalidOperationException(L.T("Połączenie zostało zmienione lub usunięte w innym oknie. Odśwież listę i otwórz edytor ponownie. Twoje zmiany nie zostały zapisane."));
        var changed=original!=null && !CredentialReference.CanReuse(original,edited);
        var saved=edited with { CredentialId=changed?Guid.NewGuid():original?.CredentialId,LastConnected=changed?null:edited.LastConnected };
        command.CommandText="INSERT INTO profiles(id,json) VALUES($id,$json) ON CONFLICT(id) DO UPDATE SET json=excluded.json";
        command.Parameters.AddWithValue("$json",JsonSerializer.Serialize(saved)); await command.ExecuteNonQueryAsync(ct);
        if(saved.Folder.Length>0)await AddAncestors(db,transaction,saved.Folder,ct);
        await transaction.CommitAsync(ct); return saved;
    },ct);
}
