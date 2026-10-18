using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using System.Text.Json;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    private readonly string connectionString;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string databasePath;
    public string DatabasePath=>databasePath;
    public string DatabaseBackupDirectory { get; }
    public string LogDirectory { get; }
    private readonly SemaphoreSlim initializationGate=new(1);
    public ProfileRepository(AppPaths paths)
    {
        databasePath=Path.Combine(paths.Data,"serverdesk.db");
        DatabaseBackupDirectory=Path.Combine(paths.Data,"backups");
        LogDirectory=paths.Logs;
        connectionString=new SqliteConnectionStringBuilder { DataSource=databasePath }.ToString();
    }
    private async Task<SqliteConnection> Open(CancellationToken ct)
    {
        var db = new SqliteConnection(connectionString);
        try { await db.OpenAsync(ct); return db; }
        catch { await db.DisposeAsync(); throw; }
    }
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await initializationGate.WaitAsync(ct).ConfigureAwait(false);
        try { await Task.Run(()=>InitializeDatabaseAsync(ct),ct).ConfigureAwait(false); }
        finally { initializationGate.Release(); }
    }
    private async Task InitializeDatabaseAsync(CancellationToken ct)
    {
        await using var db = await Open(ct);
        // Take a coherent read snapshot before acquiring a migration write lock.
        // SQLite's backup API cannot copy from a source with a write transaction.
        using(var inspection=db.BeginTransaction(deferred:true))
        {
            using var inspect=db.CreateCommand(); inspect.Transaction=inspection;
            inspect.CommandText="PRAGMA user_version";
            var previous=Convert.ToInt32(await inspect.ExecuteScalarAsync(ct));
            if(previous>3)throw new InvalidDataException(L.T("Baza pochodzi z nowszej wersji ServerDesk."));
            inspect.CommandText="PRAGMA quick_check(1)";
            if(!string.Equals(await inspect.ExecuteScalarAsync(ct) as string,"ok",StringComparison.Ordinal))
                throw new InvalidDataException(L.T("Kontrola bazy wykryła uszkodzenie. Zamknij aplikację i przywróć sprawdzoną kopię danych. Oryginalna baza nie została usunięta."));
            if(previous==3)return;
            inspect.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            if(Convert.ToInt64(await inspect.ExecuteScalarAsync(ct))>0)
                DatabaseBackups.Create(db,DatabaseBackupDirectory,previous,"before-migration",ct);
            inspection.Commit();
        }
        ct.ThrowIfCancellationRequested();
        using var transaction=db.BeginTransaction();
        using var cmd = db.CreateCommand();
        cmd.Transaction=transaction;
        cmd.CommandText="PRAGMA user_version";
        var version=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if(version>3) throw new InvalidDataException(L.T("Baza pochodzi z nowszej wersji ServerDesk."));
        if(version==3)return; // Another instance may have completed the migration.
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS profiles(id TEXT PRIMARY KEY, json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS folders(path TEXT PRIMARY KEY); CREATE TABLE IF NOT EXISTS folder_icons(path TEXT PRIMARY KEY,icon TEXT NOT NULL); CREATE TABLE IF NOT EXISTS hostkeys(host TEXT NOT NULL,port INTEGER NOT NULL,fingerprint TEXT NOT NULL,PRIMARY KEY(host,port)); CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY,value TEXT NOT NULL); CREATE TABLE IF NOT EXISTS history(id INTEGER PRIMARY KEY,profile TEXT NOT NULL,time TEXT NOT NULL);";
        await cmd.ExecuteNonQueryAsync(ct);
        cmd.CommandText="PRAGMA table_info(history)";
        var hasSnapshot=false;
        await using(var columns=await cmd.ExecuteReaderAsync(ct))while(await columns.ReadAsync(ct))if(columns.GetString(1)=="snapshot")hasSnapshot=true;
        if(!hasSnapshot) { cmd.CommandText="ALTER TABLE history ADD COLUMN snapshot TEXT"; await cmd.ExecuteNonQueryAsync(ct); }
        cmd.CommandText="PRAGMA user_version=3"; await cmd.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task<List<ServerProfile>> ListAsync(CancellationToken ct = default)
    {
        await using var db=await Open(ct); return await ReadProfilesAsync(db,null,ct);
    }
    private static async Task<List<ServerProfile>> ReadProfilesAsync(SqliteConnection db,SqliteTransaction? transaction,CancellationToken ct)
    {
        using var cmd=db.CreateCommand(); cmd.Transaction=transaction; cmd.CommandText="SELECT json FROM profiles";
        await using var reader = await cmd.ExecuteReaderAsync(ct); var list = new List<ServerProfile>();
        while (await reader.ReadAsync(ct)) list.Add(JsonSerializer.Deserialize<ServerProfile>(reader.GetString(0))!);
        return list.OrderByDescending(x => x.Favorite).ThenBy(x => x.Folder).ThenBy(x => x.Name).ToList();
    }
    public async Task SaveAsync(ServerProfile profile, CancellationToken ct = default)
    {
        profile.Validate();
        await using var db=await Open(ct); using var transaction=db.BeginTransaction(); using var cmd=db.CreateCommand(); cmd.Transaction=transaction;
        cmd.CommandText="INSERT INTO profiles VALUES($id,$json) ON CONFLICT(id) DO UPDATE SET json=excluded.json";
        cmd.Parameters.AddWithValue("$id",profile.Id.ToString()); cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(profile)); await cmd.ExecuteNonQueryAsync(ct);
        if(profile.Folder.Length>0)await AddAncestors(db,transaction,profile.Folder,ct);
        await transaction.CommitAsync(ct);
    }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Execute("DELETE FROM profiles WHERE id=$id", ct, ("$id", id.ToString()));
    public async Task MoveToFolderAsync(Guid id,string path,CancellationToken ct=default)
    {
        ProfileFolder.Validate(path,allowEmpty:true);
        await using var db=await Open(ct); using var transaction=db.BeginTransaction(); using var cmd=db.CreateCommand(); cmd.Transaction=transaction;
        cmd.CommandText="UPDATE profiles SET json=json_set(json,'$.Folder',$folder) WHERE id=$id";
        cmd.Parameters.AddWithValue("$folder",path); cmd.Parameters.AddWithValue("$id",id.ToString());
        if(await cmd.ExecuteNonQueryAsync(ct)==0)throw new InvalidOperationException(L.T("Serwer już nie istnieje. Odśwież listę połączeń i spróbuj ponownie."));
        if(path.Length>0)await AddAncestors(db,transaction,path,ct);
        await transaction.CommitAsync(ct);
    }
    public async Task AddFolderAsync(string path, CancellationToken ct = default)
    {
        ProfileFolder.Validate(path);
        await using var db=await Open(ct); using var transaction=db.BeginTransaction();
        await AddAncestors(db,transaction,path,ct); await transaction.CommitAsync(ct);
    }
    private static async Task AddAncestors(SqliteConnection db,SqliteTransaction transaction,string path,CancellationToken ct)
    {
        using var cmd=db.CreateCommand(); cmd.Transaction=transaction; cmd.CommandText="INSERT OR IGNORE INTO folders VALUES($path)"; var value=cmd.Parameters.Add("$path",SqliteType.Text);
        foreach(var ancestor in ProfileFolder.Ancestors(path)) { value.Value=ancestor; await cmd.ExecuteNonQueryAsync(ct); }
    }
    public async Task DeleteFolderAsync(string path,CancellationToken ct=default)
    {
        ProfileFolder.Validate(path);
        await using var db=await Open(ct); using var transaction=db.BeginTransaction();
        // Lock before inspecting contents, so another writer cannot add a profile
        // between the empty-folder check and the recursive deletion.
        var profiles=await ReadProfilesAsync(db,transaction,ct);
        if(profiles.Any(x=>x.Folder==path || x.Folder.StartsWith(path+"/",StringComparison.Ordinal)))throw new InvalidOperationException(L.T("Folder zawiera serwery. Przenieś je przed usunięciem."));
        var allFolders=await ReadFoldersAsync(db,transaction,ct);
        var folders=allFolders.Where(x=>x==path || x.StartsWith(path+"/",StringComparison.Ordinal)).ToArray();
        foreach(var folder in folders) { using var cmd=db.CreateCommand(); cmd.Transaction=(SqliteTransaction)transaction; cmd.CommandText="DELETE FROM folder_icons WHERE path=$path; DELETE FROM folders WHERE path=$path"; cmd.Parameters.AddWithValue("$path",folder); await cmd.ExecuteNonQueryAsync(ct); } await transaction.CommitAsync(ct);
    }
    public async Task RenameFolderAsync(string path,string target,CancellationToken ct=default)
    {
        ProfileFolder.Validate(path); ProfileFolder.Validate(target);
        if(string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(target) || target.Split('/').Any(x=>string.IsNullOrWhiteSpace(x)||x is "." or "..") || target.StartsWith(path+"/",StringComparison.Ordinal)) throw new ArgumentException(L.T("Niepoprawny folder docelowy."));
        await using var db=await Open(ct); using var transaction=db.BeginTransaction();
        var folders=await ReadFoldersAsync(db,transaction,ct); if(!folders.Contains(path))throw new InvalidOperationException(L.T("Folder nie istnieje."));
        if(path==target) { await transaction.CommitAsync(ct); return; }
        if(folders.Contains(target))throw new InvalidOperationException(L.T("Folder docelowy już istnieje."));
        var profiles=(await ReadProfilesAsync(db,transaction,ct)).Where(x=>x.Folder==path || x.Folder.StartsWith(path+"/",StringComparison.Ordinal)).ToArray();
        foreach(var folder in folders.Where(x=>x==path || x.StartsWith(path+"/",StringComparison.Ordinal)))ProfileFolder.Validate(target+folder[path.Length..]);
        foreach(var profile in profiles)ProfileFolder.Validate(target+profile.Folder[path.Length..]);
        foreach(var folder in folders.Where(x=>x==path || x.StartsWith(path+"/",StringComparison.Ordinal)))
        { using var cmd=db.CreateCommand(); cmd.Transaction=(SqliteTransaction)transaction; cmd.CommandText="UPDATE folder_icons SET path=$target WHERE path=$old; UPDATE folders SET path=$target WHERE path=$old"; cmd.Parameters.AddWithValue("$target",target+folder[path.Length..]); cmd.Parameters.AddWithValue("$old",folder); await cmd.ExecuteNonQueryAsync(ct); }
        foreach(var profile in profiles) { using var cmd=db.CreateCommand(); cmd.Transaction=transaction; cmd.CommandText="UPDATE profiles SET json=json_set(json,'$.Folder',$folder) WHERE id=$id"; cmd.Parameters.AddWithValue("$id",profile.Id.ToString()); cmd.Parameters.AddWithValue("$folder",target+profile.Folder[path.Length..]); await cmd.ExecuteNonQueryAsync(ct); }
        await AddAncestors(db,(SqliteTransaction)transaction,target,ct); await transaction.CommitAsync(ct);
    }
    public async Task<List<string>> FoldersAsync(CancellationToken ct = default)
    {
        await using var db=await Open(ct); return await ReadFoldersAsync(db,null,ct);
    }
    private static async Task<List<string>> ReadFoldersAsync(SqliteConnection db,SqliteTransaction? transaction,CancellationToken ct)
    {
        using var cmd=db.CreateCommand(); cmd.Transaction=transaction; cmd.CommandText="SELECT path FROM folders ORDER BY path";
        await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<string>(); while (await r.ReadAsync(ct)) result.Add(r.GetString(0)); return result;
    }
    public async Task ConnectedAsync(ServerProfile profile, CancellationToken ct = default)
    {
        var snapshot=ConnectionHistorySnapshot.Capture(profile); var time=DateTimeOffset.UtcNow.ToString("O");
        await using var db=await Open(ct); using var transaction=db.BeginTransaction(); using var cmd=db.CreateCommand(); cmd.Transaction=transaction;
        cmd.CommandText="INSERT INTO history(profile,time,snapshot) VALUES($id,$time,$snapshot)"; cmd.Parameters.AddWithValue("$id",profile.Id.ToString()); cmd.Parameters.AddWithValue("$time",time); cmd.Parameters.AddWithValue("$snapshot",JsonSerializer.Serialize(snapshot)); await cmd.ExecuteNonQueryAsync(ct);
        cmd.CommandText="SELECT json FROM profiles WHERE id=$id"; var stored=await cmd.ExecuteScalarAsync(ct) as string;
        // A live session can retain an old destination after the profile is edited.
        // Preserve that history without marking a different destination as connected.
        if(stored!=null && snapshot.Matches(JsonSerializer.Deserialize<ServerProfile>(stored)!))
        { cmd.CommandText="UPDATE profiles SET json=json_set(json,'$.LastConnected',$time) WHERE id=$id"; await cmd.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct);
    }
    public async Task<List<ConnectionHistoryEntry>> HistoryAsync(int limit=100,long? beforeId=null,Guid? profileId=null,CancellationToken ct=default,Protocol? protocol=null,string? search=null)
    {
        if(limit is <1 or >1000)throw new ArgumentOutOfRangeException(nameof(limit));
        if(protocol!=null && !Enum.IsDefined(protocol.Value))throw new ArgumentOutOfRangeException(nameof(protocol));
        var terms=(search??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        await using var db=await Open(ct); using var cmd=db.CreateCommand();
        db.CreateFunction<string?,bool>("sd_history_matches",text=>terms.All(t=>(text??"").Contains(t,StringComparison.OrdinalIgnoreCase)),isDeterministic:true);
        cmd.CommandText="""
            SELECT h.id,h.profile,h.time,p.json,h.snapshot FROM history h LEFT JOIN profiles p ON p.id=h.profile
            WHERE ($before IS NULL OR h.id<$before) AND ($profile IS NULL OR h.profile=$profile)
              AND ($protocol IS NULL OR COALESCE(json_extract(h.snapshot,'$.Protocol'),json_extract(p.json,'$.Protocol'))=$protocol)
              AND ($unfiltered OR sd_history_matches(
                COALESCE(json_extract(h.snapshot,'$.Name'),json_extract(p.json,'$.Name'),'')||' '||
                COALESCE(json_extract(h.snapshot,'$.Endpoint'),json_extract(p.json,'$.Host'),'')||' '||
                COALESCE(json_extract(h.snapshot,'$.User'),json_extract(p.json,'$.Username'),'')||' '||
                CASE COALESCE(json_extract(h.snapshot,'$.Protocol'),json_extract(p.json,'$.Protocol')) WHEN 0 THEN 'SSH' WHEN 1 THEN 'RDP' WHEN 2 THEN 'VNC' WHEN 3 THEN 'Serial' ELSE '' END))
            ORDER BY h.id DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$before",(object?)beforeId??DBNull.Value); cmd.Parameters.AddWithValue("$profile",(object?)profileId?.ToString()??DBNull.Value); cmd.Parameters.AddWithValue("$limit",limit);
        cmd.Parameters.AddWithValue("$protocol",protocol.HasValue?(int)protocol.Value:DBNull.Value); cmd.Parameters.AddWithValue("$unfiltered",terms.Length==0);
        await using var reader=await cmd.ExecuteReaderAsync(ct); var result=new List<ConnectionHistoryEntry>();
        while(await reader.ReadAsync(ct))result.Add(new(reader.GetInt64(0),Guid.Parse(reader.GetString(1)),DateTimeOffset.Parse(reader.GetString(2),System.Globalization.CultureInfo.InvariantCulture),reader.IsDBNull(3)?null:JsonSerializer.Deserialize<ServerProfile>(reader.GetString(3))) { Snapshot=reader.IsDBNull(4)?null:JsonSerializer.Deserialize<ConnectionHistorySnapshot>(reader.GetString(4)) });
        return result;
    }
    public Task<string?> HostKeyAsync(string host, int port, CancellationToken ct = default) => Scalar("SELECT fingerprint FROM hostkeys WHERE host=$host AND port=$port", ct, ("$host", host.ToLowerInvariant()), ("$port", port));
    public async Task TrustHostAsync(string host, int port, string fingerprint, CancellationToken ct = default)
    {
        await Execute("INSERT OR IGNORE INTO hostkeys VALUES($host,$port,$fp)",ct,("$host",host.ToLowerInvariant()),("$port",port),("$fp",fingerprint));
        if(await HostKeyAsync(host,port,ct)!=fingerprint) throw new InvalidOperationException(L.T("Klucz hosta zmienił się podczas nawiązywania równoległego połączenia."));
    }
    public Task<string?> SettingAsync(string key, CancellationToken ct = default) => Scalar("SELECT value FROM settings WHERE key=$key", ct, ("$key", key));
    public Task SetSettingAsync(string key, string value, CancellationToken ct = default) => Execute("INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value", ct, ("$key", key), ("$value", value));
    private async Task Execute(string sql, CancellationToken ct, params (string, object)[] parameters)
    {
        await using var db = await Open(ct); using var cmd = db.CreateCommand(); cmd.CommandText = sql;
        foreach (var (key,value) in parameters) cmd.Parameters.AddWithValue(key,value); await cmd.ExecuteNonQueryAsync(ct);
    }
    private async Task<string?> Scalar(string sql, CancellationToken ct, params (string, object)[] parameters)
    {
        await using var db = await Open(ct); using var cmd = db.CreateCommand(); cmd.CommandText = sql;
        foreach (var (key,value) in parameters) cmd.Parameters.AddWithValue(key,value); return await cmd.ExecuteScalarAsync(ct) as string;
    }
    public async Task ExportAsync(Stream stream, CancellationToken ct = default)
    {
        ConfigurationExport data;
        await using(var db=await Open(ct))
        using(var snapshot=db.BeginTransaction(deferred:true))
        {
            data=new(2,await ReadProfilesAsync(db,snapshot,ct),await ReadFoldersAsync(db,snapshot,ct)) { FolderIcons=await ReadFolderIconsAsync(db,snapshot,ct),TagColors=await ReadTagColorsAsync(db,snapshot,ct) };
            await snapshot.CommitAsync(ct);
        }
        var bytes=JsonSerializer.SerializeToUtf8Bytes(data,Json);
        if(bytes.Length>StreamLimits.ConfigurationBytes)throw new InvalidDataException(L.T("Eksport przekracza limit 32 MiB. Zmniejsz liczbę własnych grafik."));
        await stream.WriteAsync(bytes,ct);
    }
    public Task ExportFileAsync(string path,CancellationToken ct=default)=>Task.Run(async()=>
    {
        var target=Path.GetFullPath(path);
        using var prepared=new MemoryStream(); await ExportAsync(prepared,ct); prepared.Position=0;
        // Prepare everything before touching the destination. The staged file is
        // flushed and closed before replacing an existing export on the same volume.
        await AtomicFile.ReplaceAsync(target,(output,token)=>prepared.CopyToAsync(output,token),ct);
    },ct);
    public Task ImportAsync(Stream stream,CancellationToken ct=default)=>Task.Run(()=>ImportCoreAsync(stream,ct),ct);
    private async Task ImportCoreAsync(Stream stream,CancellationToken ct)
    {
        var bytes=await StreamLimits.ReadAsync(stream,StreamLimits.ConfigurationBytes,ct);
        ConfigurationExport data;
        try
        {
            using var document=JsonDocument.Parse(bytes); RejectDuplicateConfigurationProperties(document.RootElement,ct);
            data=document.RootElement.Deserialize<ConfigurationExport>(Json)??throw new JsonException();
        }
        catch(JsonException ex) { throw new InvalidDataException(L.T("Niepoprawny JSON konfiguracji lub powtórzone pola. Dane nie zostały zaimportowane."),ex); }
        if (data.Version is not (1 or 2) || data.Servers == null || data.Folders == null || data.Servers.Count > 10000 || data.Folders.Count>10000 || data.FolderIcons is { Count:>10000 }) throw new InvalidDataException(L.T("Nieobsługiwany eksport."));
        var tagColors=ProfileTags.ValidateColors(data.TagColors);
        foreach (var p in data.Servers) { ct.ThrowIfCancellationRequested(); if(p==null)throw new InvalidDataException(L.T("Niepoprawne dane profilu.")); p.Validate(); }
        foreach (var folder in data.Folders)
        { try { ProfileFolder.Validate(folder); } catch(ArgumentException ex) { throw new InvalidDataException(ex.Message,ex); } }
        var importedFolders=new HashSet<string>(StringComparer.Ordinal);
        foreach(var folder in data.Folders.Concat(data.Servers.Select(x=>x.Folder)).Where(x=>x.Length>0))
        {
            ct.ThrowIfCancellationRequested();
            foreach(var ancestor in ProfileFolder.Ancestors(folder))
                if(importedFolders.Add(ancestor) && importedFolders.Count>10000)throw new InvalidDataException(L.T("Import może zawierać do 10 000 folderów, łącznie z folderami nadrzędnymi."));
        }
        foreach(var (path,icon) in data.FolderIcons??[]) { if(!importedFolders.Contains(path))throw new InvalidDataException(L.T("Ikona odwołuje się do nieznanego folderu.")); ProfileIcons.Validate(icon); }
        await using var db = await Open(ct); await using var transaction = await db.BeginTransactionAsync(ct);
        foreach (var p in data.Servers)
        {
            // Imported profiles get new identities; never reuse a local credential reference.
            var imported = p with { Id = Guid.NewGuid(), LastConnected = null };
            using var cmd = db.CreateCommand(); cmd.Transaction = (SqliteTransaction)transaction; cmd.CommandText = "INSERT INTO profiles VALUES($id,$json)"; cmd.Parameters.AddWithValue("$id", imported.Id.ToString()); cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(imported)); await cmd.ExecuteNonQueryAsync(ct);
        }
        foreach (var folder in importedFolders)
        { using var cmd = db.CreateCommand(); cmd.Transaction = (SqliteTransaction)transaction; cmd.CommandText = "INSERT OR IGNORE INTO folders VALUES($p)"; cmd.Parameters.AddWithValue("$p", folder); await cmd.ExecuteNonQueryAsync(ct); }
        // A merge into an existing folder keeps its explicitly chosen local icon.
        foreach(var (path,icon) in data.FolderIcons??[]) { using var cmd=db.CreateCommand(); cmd.Transaction=(SqliteTransaction)transaction; cmd.CommandText="INSERT OR IGNORE INTO folder_icons VALUES($p,$icon)"; cmd.Parameters.AddWithValue("$p",path); cmd.Parameters.AddWithValue("$icon",icon); await cmd.ExecuteNonQueryAsync(ct); }
        if(tagColors.Count>0)await MergeTagColorsAsync(db,(SqliteTransaction)transaction,tagColors,ct);
        await transaction.CommitAsync(ct);
    }
    public sealed record ConfigurationExport(int Version, List<ServerProfile> Servers, List<string> Folders)
    {
        public Dictionary<string,string>? FolderIcons { get; init; }
        public Dictionary<string,string>? TagColors { get; init; }
    }
    private static void RejectDuplicateConfigurationProperties(JsonElement element,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(element.ValueKind==JsonValueKind.Object)
        {
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in element.EnumerateObject()) { if(!keys.Add(property.Name))throw new JsonException(); RejectDuplicateConfigurationProperties(property.Value,ct); }
        }
        else if(element.ValueKind==JsonValueKind.Array)foreach(var value in element.EnumerateArray())RejectDuplicateConfigurationProperties(value,ct);
    }
}
