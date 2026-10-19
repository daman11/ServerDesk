using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

// Opt-in acceptance only. Every run owns a new directory; children require its
// unguessable marker. Never point this harness at an existing application store.
if(args.Length==4 && args[0]=="--child")
{
    var childRoot=Path.GetFullPath(args[2]);
    if(File.ReadAllText(Path.Combine(childRoot,".data-probe"))!=args[3])throw new InvalidOperationException("Invalid fixture marker");
    if(args[1]=="export")
        await ExportFile.WriteAsync(Path.Combine(childRoot,"config.json"),async(stream,ct)=>
        {
            await stream.WriteAsync(new byte[32768],ct); await stream.FlushAsync(ct);
            File.WriteAllText(Path.Combine(childRoot,"ready"),"export staged");
            await Task.Delay(TimeSpan.FromMinutes(2),ct);
        });
    else if(args[1]=="database")
    {
        var childRepo=new ProfileRepository(new(childRoot)); await childRepo.InitializeAsync();
        var stop=Stopwatch.StartNew(); var source="A"; var destination="B";
        while(stop.Elapsed<TimeSpan.FromMinutes(2))
        {
            await childRepo.RenameFolderAsync(source,destination);
            File.WriteAllText(Path.Combine(childRoot,"ready"),"rename committed");
            (source,destination)=(destination,source);
        }
    }
    else throw new ArgumentException("Unknown child mode");
    return;
}

var diskFull=args.Length==2 && args[0]=="--disk-full";
if(args.Length!=1 && !diskFull)throw new ArgumentException("Usage: ServerDesk.DataProbe <new-output-directory> | --disk-full <new-directory-on-isolated-tmpfs>");
var runRoot=Path.GetFullPath(args[diskFull?1:0]);
if(diskFull)
{
    if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Disk-full fixture requires bounded Linux tmpfs");
    var mount=File.ReadLines("/proc/self/mountinfo").Select(line=>line.Split(' ')).Where(parts=>parts.Length>7 && runRoot.StartsWith(parts[4]+"/",StringComparison.Ordinal)).OrderByDescending(parts=>parts[4].Length).FirstOrDefault();
    if(mount==null || mount[Array.IndexOf(mount,"-")+1]!="tmpfs" || !Path.GetFileName(mount[4]).StartsWith("serverdesk-disk-full-",StringComparison.Ordinal))throw new InvalidOperationException("Not an owned tmpfs mount");
    var options=string.Join(',',mount.Skip(Array.IndexOf(mount,"-")+3));
    var size=options.Split(',').FirstOrDefault(value=>value.StartsWith("size=",StringComparison.Ordinal))?[5..];
    if(size==null || !size.EndsWith('k') || !int.TryParse(size[..^1],out var kib) || kib is <1024 or >65536)throw new InvalidOperationException("Fixture tmpfs must be explicitly bounded to 1–64 MiB");
}
if(Directory.Exists(runRoot)||File.Exists(runRoot))throw new IOException("Output must be a new directory");
Directory.CreateDirectory(runRoot);
var fixtureRoot=Path.Combine(runRoot,"fixtures"); Directory.CreateDirectory(fixtureRoot);
var results=new List<object>();
try
{
    if(diskFull)await Check("real-enospc-preservation-and-retry",DiskFull);
    else
    {
        for(var version=0;version<ProfileRepository.CurrentSchemaVersion;version++)await Check("historical-schema-"+version,()=>Migration(version));
        await Check("terminated-export-and-retry",ExportCrash);
        for(var attempt=0;attempt<3;attempt++)await Check("terminated-folder-transaction-"+attempt,()=>DatabaseCrash(attempt));
    }
}
finally
{
    await File.WriteAllTextAsync(Path.Combine(runRoot,"results.json"),JsonSerializer.Serialize(new
    {
        commit=Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        platform=System.Runtime.InteropServices.RuntimeInformation.OSDescription,results,
        limits="Owned fixtures only; process termination is not a power-loss test. No OS keyring or hardware acceptance."
    },new JsonSerializerOptions { WriteIndented=true }));
    SqliteConnection.ClearAllPools();
    // The output directory was required to be new, and this fixed child contains
    // only fixtures created by this run. Keep results/logs, not databases or blobs.
    if((File.GetAttributes(fixtureRoot)&FileAttributes.ReparsePoint)!=0)throw new IOException("Fixture root became a link");
    Directory.Delete(fixtureRoot,recursive:true);
}

async Task Check(string name,Func<Task> action)
{
    var timer=Stopwatch.StartNew();
    try { await action(); results.Add(new { name,passed=true,milliseconds=timer.ElapsedMilliseconds }); Console.WriteLine("PASS "+name); }
    catch(Exception error) { results.Add(new { name,passed=false,error=error.GetType().Name,milliseconds=timer.ElapsedMilliseconds }); throw; }
}
string Fixture(string name)
{
    var path=Path.Combine(fixtureRoot,name); Directory.CreateDirectory(path);
    File.WriteAllText(Path.Combine(path,".data-probe"),Guid.NewGuid().ToString("N")); return path;
}
void Require(bool condition,string message) { if(!condition)throw new InvalidDataException(message); }
async Task Execute(string path,string sql,params (string,object)[] parameters)
{
    using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path,Pooling=false }.ToString());
    await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText=sql;
    foreach(var (key,value) in parameters)command.Parameters.AddWithValue(key,value);
    await command.ExecuteNonQueryAsync();
}
async Task<object?> Scalar(string path,string sql)
{
    using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path,Pooling=false }.ToString());
    await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText=sql; return await command.ExecuteScalarAsync();
}
async Task Migration(int version)
{
    var path=Fixture("schema-"+version); var dbPath=Path.Combine(path,"serverdesk.db");
    var id=Guid.NewGuid();
    // Older profile JSON deliberately omits the later protocol/appearance fields
    // and includes an opaque future field; migration must not rewrite its bytes.
    var original=JsonSerializer.Serialize(new { Id=id,Name="Żółty serwer 10",Host="example.test",Port=22,Username="fixture",Folder="Lab/Zażółć/SSH",Favorite=true,FutureField="preserve" });
    await Execute(dbPath,"CREATE TABLE profiles(id TEXT PRIMARY KEY,json TEXT NOT NULL); CREATE TABLE folders(path TEXT PRIMARY KEY); CREATE TABLE hostkeys(host TEXT NOT NULL,port INTEGER NOT NULL,fingerprint TEXT NOT NULL,PRIMARY KEY(host,port)); CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT NOT NULL); CREATE TABLE history(id INTEGER PRIMARY KEY,profile TEXT NOT NULL,time TEXT NOT NULL);"+
        "INSERT INTO profiles VALUES($id,$json); INSERT INTO folders VALUES('Lab/Zażółć/SSH'); INSERT INTO folders VALUES('Empty'); INSERT INTO hostkeys VALUES('example.test',22,'SHA256:fixture'); INSERT INTO settings VALUES('theme','light'); INSERT INTO settings VALUES('commands.vault.v1',$pointer); INSERT INTO settings VALUES('networkTools.collections.v1','[]'); INSERT INTO history VALUES(7,$id,'2026-01-01T00:00:00Z'); PRAGMA user_version="+version,
        ("$id",id.ToString()),("$json",original),("$pointer","{\"version\":1,\"parts\":[\"11111111-1111-1111-1111-111111111111\"]}"));
    var repo=new ProfileRepository(new(path)); await repo.InitializeAsync();
    var backup=Directory.GetFiles(repo.DatabaseBackupDirectory,"*.db").Single();
    Require(Convert.ToInt64(await Scalar(backup,"PRAGMA user_version"))==version,"Backup schema changed");
    Require((string?)await Scalar(backup,"SELECT json FROM profiles")==original,"Backup lost original profile JSON");
    Require((string?)await Scalar(dbPath,"SELECT json FROM profiles")==original,"Migration rewrote profile JSON");
    Require((string?)await Scalar(dbPath,"PRAGMA quick_check(1)")=="ok","Migrated database is invalid");
    Require(Convert.ToInt64(await Scalar(dbPath,"PRAGMA user_version"))==ProfileRepository.CurrentSchemaVersion,"Migration did not reach the current schema");
    var profile=(await repo.ListAsync()).Single(); profile.Validate();
    Require(profile.Id==id && profile.Favorite && profile.Folder=="Lab/Zażółć/SSH","Profile identity or folder lost");
    Require(await repo.HostKeyAsync("example.test",22)=="SHA256:fixture","Host trust lost");
    Require(await repo.SettingAsync("theme")=="light" && (await repo.FoldersAsync()).Contains("Empty"),"Settings/empty folder lost");
    Require(await Scalar(dbPath,"SELECT snapshot FROM history WHERE id=7")==DBNull.Value,"Legacy history was rebound to a new identity");
    await repo.InitializeAsync(); Require(Directory.GetFiles(repo.DatabaseBackupDirectory,"*.db").Length==1,"Repeated initialization created another migration");
    var restore=Fixture("restored-"+version); File.Copy(backup,Path.Combine(restore,"serverdesk.db"));
    var restored=new ProfileRepository(new(restore)); await restored.InitializeAsync();
    Require((await restored.ListAsync()).Single()==profile,"Restoring old backup changed profile");
    Require(await restored.SettingAsync("commands.vault.v1")==await repo.SettingAsync("commands.vault.v1"),"Vault reference changed");
    // A database-only restore must report an unavailable external blob, not reset
    // or replace the saved library with an empty one.
    var pointer=await restored.SettingAsync("commands.vault.v1");
    try { await new CommandStore(restored,new MissingVault()).ReadLibraryAsync(); throw new InvalidDataException("Missing vault was silently accepted"); }
    catch(InvalidOperationException) { }
    Require(await restored.SettingAsync("commands.vault.v1")==pointer,"Missing vault reset its pointer");
    using var export=new MemoryStream(); await restored.ExportAsync(export); export.Position=0;
    var imported=new ProfileRepository(new(Fixture("imported-"+version))); await imported.InitializeAsync(); await imported.ImportAsync(export);
    Require((await imported.ListAsync()).Single().Id!=id,"Import reused a credential identity");
    Require(await imported.SettingAsync("commands.vault.v1")==null,"Profile export included a vault reference");
}
async Task Terminate(string mode,string path)
{
    var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true };
    if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    foreach(var value in new[]{"--child",mode,path,File.ReadAllText(Path.Combine(path,".data-probe"))})start.ArgumentList.Add(value);
    using var child=Process.Start(start)??throw new IOException("Could not start owned child");
    var output=child.StandardOutput.ReadToEndAsync(); var error=child.StandardError.ReadToEndAsync();
    try
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while(!File.Exists(Path.Combine(path,"ready")))
        {
            if(child.HasExited)throw new IOException("Child exited before readiness: "+await error);
            await Task.Delay(20,timeout.Token);
        }
        if(mode=="database")await Task.Delay(75,timeout.Token);
        child.Kill(entireProcessTree:true); await child.WaitForExitAsync(timeout.Token);
    }
    finally { if(!child.HasExited) { child.Kill(entireProcessTree:true); await child.WaitForExitAsync(); } }
    await File.WriteAllTextAsync(Path.Combine(runRoot,Path.GetFileName(path)+"-child.log"),(await output)+(await error));
}
async Task ExportCrash()
{
    var path=Fixture("export-crash"); var target=Path.Combine(path,"config.json"); var original=Encoding.UTF8.GetBytes("{\"previous\":\"preserve\"}");
    await File.WriteAllBytesAsync(target,original); await Terminate("export",path);
    Require((await File.ReadAllBytesAsync(target)).SequenceEqual(original),"Interrupted export destroyed its previous destination");
    var partials=Directory.GetFiles(path,"*.part"); Require(partials.Length==1,"Interrupted export did not leave the expected staging file");
    var partialHash=SHA256.HashData(await File.ReadAllBytesAsync(partials[0]));
    await ExportFile.WriteAsync(target,(stream,ct)=>stream.WriteAsync(Encoding.UTF8.GetBytes("{\"retry\":true}"),ct).AsTask());
    Require(await File.ReadAllTextAsync(target)=="{\"retry\":true}","Export retry failed");
    Require(SHA256.HashData(await File.ReadAllBytesAsync(partials[0])).SequenceEqual(partialHash),"Retry modified an earlier partial file");
}
async Task DatabaseCrash(int attempt)
{
    var path=Fixture("database-crash-"+attempt); var repo=new ProfileRepository(new(path)); await repo.InitializeAsync();
    var ids=new HashSet<Guid>();
    for(var i=0;i<32;i++) { var profile=new ServerProfile { Name="Fixture "+i,Host="example.test",Username="fixture",Folder="A/Child" }; await repo.SaveAsync(profile); ids.Add(profile.Id); }
    await repo.SetFolderIconAsync("A","shield"); await repo.SetFolderIconAsync("A/Child","database"); await repo.SetSettingAsync("marker","preserve");
    SqliteConnection.ClearAllPools(); await Terminate("database",path);
    var reopened=new ProfileRepository(new(path)); await reopened.InitializeAsync(); var profiles=await reopened.ListAsync();
    Require(profiles.Count==32 && profiles.Select(p=>p.Id).ToHashSet().SetEquals(ids),"Crash lost a profile identity");
    var folders=await reopened.FoldersAsync(); var parent=profiles[0].Folder.Split('/')[0];
    Require(parent is "A" or "B" && profiles.All(p=>p.Folder==parent+"/Child"),"Crash left a partially renamed subtree");
    Require(folders.Order().SequenceEqual(new[]{parent,parent+"/Child"}.Order()),"Crash left partial folders");
    var icons=await reopened.FolderIconsAsync(); Require(icons.Count==2 && icons[parent]=="shield" && icons[parent+"/Child"]=="database","Crash lost or mixed folder icons");
    Require(await reopened.SettingAsync("marker")=="preserve","Crash lost an unrelated setting");
    var backup=await reopened.CreateDatabaseBackupAsync(); Require((string?)await Scalar(backup.Path,"PRAGMA quick_check(1)")=="ok","Post-crash backup invalid");
    await reopened.RenameFolderAsync(parent,"Retry"); Require((await reopened.ListAsync()).All(p=>p.Folder=="Retry/Child"),"Post-crash retry failed");
}
async Task DiskFull()
{
    var path=Fixture("disk-full"); var repo=new ProfileRepository(new(path)); await repo.InitializeAsync();
    var profile=new ServerProfile { Name="Original",Host="example.test",Username="fixture",Folder="Keep" }; await repo.SaveAsync(profile);
    await repo.SetFolderIconAsync("Keep","shield"); var target=Path.Combine(path,"config.json"); await repo.ExportFileAsync(target);
    var original=SHA256.HashData(await File.ReadAllBytesAsync(target)); var filler=Path.Combine(path,"owned-filler.bin");
    var exportFailed=false; var databaseFailed=false; var backupFailed=false;
    try
    {
        using(var fill=new FileStream(filler,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {
            var block=new byte[32768]; var full=false;
            // At most 65 MiB even if the fixture guard were misconfigured.
            for(var i=0;i<2080;i++)try { fill.Write(block); fill.Flush(true); } catch(IOException ex) when((ex.HResult&0xffff)==28) { full=true; break; }
            Require(full,"Bounded fixture did not produce ENOSPC");
        }
        try { await repo.ExportFileAsync(target); } catch(IOException ex) when((ex.HResult&0xffff)==28) { exportFailed=true; }
        Require(exportFailed && SHA256.HashData(await File.ReadAllBytesAsync(target)).SequenceEqual(original),"ENOSPC export did not preserve destination");
        Require(Directory.GetFiles(path,"*.part").Length==0,"Failed export retained its staging file");
        try { await repo.SaveAsync(profile with { Name="Edited",Folder="New/Child",Description=new string('x',500000) }); }
        catch(SqliteException ex) when(ex.SqliteErrorCode==13) { databaseFailed=true; }
        Require(databaseFailed,"Expected SQLITE_FULL");
        Require((await repo.ListAsync()).Single()==profile && !(await repo.FoldersAsync()).Contains("New"),"SQLITE_FULL partially saved profile/folders");
        try { await repo.CreateDatabaseBackupAsync(); } catch(DatabaseBackupException) { backupFailed=true; }
        Require(backupFailed,"Backup succeeded on an exhausted fixture unexpectedly");
    }
    finally { File.Delete(filler); }
    await repo.SaveAsync(profile with { Name="Retry",Folder="New/Child" }); await repo.ExportFileAsync(target);
    Require((await repo.ListAsync()).Single().Name=="Retry","Retry after ENOSPC failed");
    var backup=await repo.CreateDatabaseBackupAsync(); Require((string?)await Scalar(backup.Path,"PRAGMA quick_check(1)")=="ok","Backup retry failed");
    Require(!Directory.EnumerateFiles(repo.DatabaseBackupDirectory,"*.part*").Any(),"Backup left partial files");
}

sealed class MissingVault : ICredentialStore
{
    public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<string?>(null);
    public Task SaveAsync(Guid id,string value,CancellationToken ct=default)=>throw new InvalidOperationException("Read-only missing fixture vault");
    public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new InvalidOperationException("Read-only missing fixture vault");
}
