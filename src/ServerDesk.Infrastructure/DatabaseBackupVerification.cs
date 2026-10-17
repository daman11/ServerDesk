using Microsoft.Data.Sqlite;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public static partial class DatabaseBackupCatalog
{
    public static Task<int> VerifyAsync(DatabaseBackupEntry entry,CancellationToken ct=default)=>Task.Run(()=>Verify(entry,ct),ct);
    private static int Verify(DatabaseBackupEntry entry,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path=Path.GetFullPath(entry.Path); var match=BackupName().Match(Path.GetFileName(path));
        if(!match.Success || !int.TryParse(match.Groups["schema"].Value,out var namedVersion) || namedVersion!=entry.SchemaVersion)
            throw new InvalidDataException(L.T("Nazwa kopii nie jest zgodna z wybranym wpisem. Odśwież listę i wybierz plik ponownie."));
        var info=new FileInfo(path); info.Refresh();
        if(!info.Exists)throw new FileNotFoundException();
        if((info.Attributes & (FileAttributes.ReparsePoint|FileAttributes.Directory))!=0 || info.Length==0 || info.Length!=entry.Bytes)
            throw new InvalidDataException(L.T("Plik kopii zmienił się lub jest niepoprawny. Odśwież listę i spróbuj ponownie."));
        var length=info.Length; var modified=info.LastWriteTimeUtc;
        RejectJournals(path);
        // Completed snapshots are standalone files. Immutable read-only mode avoids
        // creating WAL/SHM beside them; never apply this to a live database.
        using var guard=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        try
        {
            using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=new Uri(path).AbsoluteUri+"?immutable=1",Mode=SqliteOpenMode.ReadOnly,Pooling=false,DefaultTimeout=2 }.ToString());
            db.Open(); ct.ThrowIfCancellationRequested();
            using var command=db.CreateCommand(); command.CommandText="PRAGMA trusted_schema=OFF; PRAGMA query_only=ON"; command.ExecuteNonQuery();
            command.CommandText="PRAGMA quick_check(1)";
            if(!string.Equals(command.ExecuteScalar() as string,"ok",StringComparison.Ordinal))throw new InvalidDataException(L.T("Kontrola SQLite wykryła uszkodzenie kopii. Plik pozostawiono bez zmian."));
            ct.ThrowIfCancellationRequested(); command.CommandText="PRAGMA user_version"; var version=Convert.ToInt32(command.ExecuteScalar());
            if(version is <0 or >3)throw new InvalidDataException(L.T("Kopia pochodzi z nowszej lub nieobsługiwanej wersji ServerDesk. Plik pozostawiono bez zmian."));
            if(version!=namedVersion)throw new InvalidDataException(L.T("Schemat zapisany w bazie różni się od nazwy kopii. Plik pozostawiono bez zmian."));
            ct.ThrowIfCancellationRequested(); info.Refresh(); RejectJournals(path);
            if(!info.Exists || info.Length!=length || info.LastWriteTimeUtc!=modified)throw new InvalidDataException(L.T("Plik kopii zmienił się lub jest niepoprawny. Odśwież listę i spróbuj ponownie."));
            return version;
        }
        catch(SqliteException ex) { throw new InvalidDataException(L.T("Nie można potwierdzić poprawności SQLite w tej kopii. Plik pozostawiono bez zmian."),ex); }
    }
    private static void RejectJournals(string path)
    {
        if(new[]{"-wal","-shm","-journal"}.Any(suffix=>File.Exists(path+suffix)))
            throw new InvalidDataException(L.T("Obok kopii znaleziono pliki dziennika SQLite. Nie sprawdzamy jej jako samodzielnej kopii; utwórz nową kopię w ServerDesk."));
    }
}
