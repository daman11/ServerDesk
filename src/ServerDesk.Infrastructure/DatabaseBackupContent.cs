using System.Text.Json;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public static partial class DatabaseBackupCatalog
{
    private static void VerifyApplicationContent(SqliteConnection db,int version,CancellationToken ct)
    {
        // Inspect only ordinary stored tables, never a view or a virtual table
        // supplied by the selected file. Historical schemas can lack later fields.
        try
        {
            RequireTable("profiles",["id","json"]);
            RequireTable("folders",["path"]);
            RequireTable("hostkeys",["host","port","fingerprint"]);
            RequireTable("settings",["key","value"]);
            RequireTable("history",version>=3?["id","profile","time","snapshot"]:["id","profile","time"]);
            RequireTable("folder_icons",["path","icon"],required:version>=3);
        }
        catch(SqliteException ex) { throw new InvalidDataException(L.T("Kopia nie zawiera wymaganych tabel lub kolumn ServerDesk. Plik pozostawiono bez zmian."),ex); }

        using var command=db.CreateCommand();
        // Do not materialize an oversized JSON value in managed memory. This is
        // a verification limit, not a write limit or a reason to remove a record.
        command.CommandText="SELECT id,typeof(id),typeof(json),CASE WHEN typeof(json)='text' AND length(CAST(json AS BLOB))<=$maximum THEN json END FROM profiles";
        command.Parameters.AddWithValue("$maximum",StreamLimits.ConfigurationBytes);
        using var reader=command.ExecuteReader(); var identities=new HashSet<Guid>(); var row=0;
        while(reader.Read())
        {
            ct.ThrowIfCancellationRequested(); row++;
            try
            {
                if(reader.GetString(1)!="text" || reader.GetString(2)!="text" || !Guid.TryParse(reader.GetString(0),out var id) || id==Guid.Empty || !identities.Add(id))throw new JsonException();
                if(reader.IsDBNull(3))throw new InvalidDataException(L.T("Profil w kopii przekracza limit kontroli 32 MiB. Plik pozostawiono bez zmian."));
                using var document=JsonDocument.Parse(reader.GetString(3)); var root=document.RootElement;
                if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("Id",out var property) || property.ValueKind!=JsonValueKind.String || !property.TryGetGuid(out var storedId) || storedId!=id)throw new JsonException();
                RejectDuplicateProfileProperties(root,ct);
                var profile=root.Deserialize<ServerProfile>()??throw new JsonException();
                profile.Validate(); // Pure model validation: no network, key files or vault access.
            }
            catch(Exception ex) when(ex is JsonException or ArgumentException)
            { throw new InvalidDataException(L.F($"Kopia zawiera niepoprawny profil połączenia (wiersz {row}). Plik pozostawiono bez zmian."),ex); }
        }
        ct.ThrowIfCancellationRequested();

        void RequireTable(string name,string[] expected,bool required=true)
        {
            ct.ThrowIfCancellationRequested();
            using var check=db.CreateCommand(); check.CommandText="SELECT CASE WHEN type='table' THEN rootpage ELSE 0 END FROM sqlite_schema WHERE name=$name COLLATE NOCASE"; check.Parameters.AddWithValue("$name",name);
            var page=check.ExecuteScalar();
            if(page==null && !required)return;
            if(page==null || Convert.ToInt64(page)<=0)throw InvalidSchema();
            // Names below come only from the fixed internal list above.
            check.CommandText=$"PRAGMA table_info(\"{name}\")";
            using var columns=check.ExecuteReader(); var actual=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while(columns.Read()) { ct.ThrowIfCancellationRequested(); actual.Add(columns.GetString(1)); }
            if(expected.Any(column=>!actual.Contains(column)))throw InvalidSchema();
        }
    }
    private static InvalidDataException InvalidSchema()=>new(L.T("Kopia nie zawiera wymaganych tabel lub kolumn ServerDesk. Plik pozostawiono bez zmian."));
    private static void RejectDuplicateProfileProperties(JsonElement element,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(element.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in element.EnumerateObject())
            {
                if(!names.Add(property.Name))throw new JsonException();
                RejectDuplicateProfileProperties(property.Value,ct);
            }
        }
        else if(element.ValueKind==JsonValueKind.Array)foreach(var value in element.EnumerateArray())RejectDuplicateProfileProperties(value,ct);
    }
}
