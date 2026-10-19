using Microsoft.Data.Sqlite;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed record DatabaseBackup(string Path,long Bytes,int SchemaVersion);

public sealed class DatabaseBackupException(Exception innerException) : IOException(
    L.T("Nie udało się utworzyć sprawdzonej kopii bazy. Sprawdź wolne miejsce i uprawnienia katalogu danych. Operacja wymagająca kopii nie została rozpoczęta."),innerException);

internal static class DatabaseBackups
{
    internal static DatabaseBackup Create(SqliteConnection source,string directory,int version,string reason,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var name=$"serverdesk-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-v{version}-{reason}-{Guid.NewGuid():N}.db";
        var path=System.IO.Path.Combine(directory,name);
        // SQLite appends journal/WAL suffixes while copying. Do not spend that
        // native path budget on the longer timestamp/schema/reason filename.
        var temporary=System.IO.Path.Combine(directory,$".{Guid.NewGuid():N}.part");
        try
        {
            Directory.CreateDirectory(directory);
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            var options=new FileStreamOptions { Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None };
            if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
            using(new FileStream(temporary,options)) { }
            using(var destination=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=temporary,Pooling=false,DefaultTimeout=5 }.ToString()))
            {
                destination.Open();
                ct.ThrowIfCancellationRequested();
                // Includes committed WAL pages; never copy the live database file directly.
                // This synchronous native operation runs only on a worker, not the UI thread.
                source.BackupDatabase(destination);
                ct.ThrowIfCancellationRequested();
                using var check=destination.CreateCommand(); check.CommandText="PRAGMA quick_check(1)";
                if(!string.Equals(check.ExecuteScalar() as string,"ok",StringComparison.Ordinal))throw new InvalidDataException();
                check.CommandText="PRAGMA user_version";
                if(Convert.ToInt32(check.ExecuteScalar())!=version)throw new InvalidDataException();
                if(version>=3)
                {
                    // Restoring a backup must never replay a scheduled reset.
                    check.CommandText="DELETE FROM settings WHERE key='pendingFactorySettingsReset'";
                    check.ExecuteNonQuery();
                    check.CommandText="PRAGMA quick_check(1)";
                    if(!string.Equals(check.ExecuteScalar() as string,"ok",StringComparison.Ordinal))throw new InvalidDataException();
                }
            }
            using(var file=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))file.Flush(flushToDisk:true);
            ct.ThrowIfCancellationRequested();
            File.Move(temporary,path); // Unique name; no replacement of a previous backup.
            return new(path,new FileInfo(path).Length,version);
        }
        catch(OperationCanceledException) { throw; }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or SqliteException)
        { throw new DatabaseBackupException(ex); }
        finally
        {
            // Only files belonging to this one attempt; keep all completed snapshots.
            foreach(var suffix in new[]{"","-journal","-wal","-shm"})
                try { File.Delete(temporary+suffix); } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
    }
}

public sealed partial class ProfileRepository
{
    public Task<DatabaseBackup> CreateDatabaseBackupAsync(CancellationToken ct=default)=>Task.Run(()=>
    {
        ct.ThrowIfCancellationRequested();
        using var source=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=databasePath,Mode=SqliteOpenMode.ReadOnly,Pooling=false,DefaultTimeout=5 }.ToString());
        source.Open();
        using var snapshot=source.BeginTransaction(deferred:true);
        using var version=source.CreateCommand(); version.Transaction=snapshot; version.CommandText="PRAGMA user_version";
        var schema=Convert.ToInt32(version.ExecuteScalar());
        if(schema>CurrentSchemaVersion)throw new InvalidDataException(L.T("Baza pochodzi z nowszej wersji ServerDesk."));
        return DatabaseBackups.Create(source,DatabaseBackupDirectory,schema,"manual",ct);
    },ct);
}
