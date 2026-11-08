using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    public async Task ApplyTagChangesAsync(IReadOnlyList<ProfileTagChange> changes,CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var snapshot=changes.ToArray(); var ids=new HashSet<Guid>();
        if(snapshot.Any(c=>c==null || c.ProfileId==Guid.Empty || c.OriginalTags==null || c.UpdatedTags==null || !ids.Add(c.ProfileId)))
            throw new ArgumentException(L.T("Niepoprawna lista zmian tagów."));
        ct.ThrowIfCancellationRequested(); if(snapshot.Length==0)return;
        await using var db=await Open(ct); using var transaction=db.BeginTransaction(); using var command=db.CreateCommand(); command.Transaction=transaction;
        // Update only Tags in the current JSON. Keep concurrent non-tag edits,
        // unknown fields and credential bindings; never insert missing profiles.
        command.CommandText="UPDATE profiles SET json=json_set(json,'$.Tags',$after) WHERE id=$id AND COALESCE(json_extract(json,'$.Tags'),'')=$before";
        var id=command.Parameters.Add("$id",Microsoft.Data.Sqlite.SqliteType.Text);
        var before=command.Parameters.Add("$before",Microsoft.Data.Sqlite.SqliteType.Text);
        var after=command.Parameters.Add("$after",Microsoft.Data.Sqlite.SqliteType.Text);
        foreach(var change in snapshot)
        {
            ct.ThrowIfCancellationRequested(); id.Value=change.ProfileId.ToString(); before.Value=change.OriginalTags; after.Value=change.UpdatedTags;
            if(await command.ExecuteNonQueryAsync(ct)!=1)
                throw new ProfileTagConflictException();
        }
        await transaction.CommitAsync(ct);
    }
}
