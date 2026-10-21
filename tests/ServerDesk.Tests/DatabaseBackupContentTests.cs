using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class DatabaseBackupContentTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-backup-content-"+Guid.NewGuid().ToString("N"),"żółć # %");
    private readonly ServerProfile profile=new() { Name="private fixture name",Host="private.example.test",Username="fixture",Folder="Lab/SSH",Description="private fixture notes" };

    private async Task<(ProfileRepository Repository,DatabaseBackupEntry Entry)> Snapshot()
    {
        var repo=new ProfileRepository(new(root)); await repo.InitializeAsync(); await repo.SaveAsync(profile); await repo.SetSettingAsync("marker","preserve");
        var backup=await repo.CreateDatabaseBackupAsync();
        return (repo,new(backup.Path,DateTimeOffset.UtcNow,backup.Bytes,backup.SchemaVersion,false));
    }
    private static async Task Execute(string path,string sql,params (string Name,object Value)[] parameters)
    {
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path,Pooling=false }.ToString()); await db.OpenAsync();
        using var command=db.CreateCommand(); command.CommandText=sql;
        foreach(var (name,value) in parameters)command.Parameters.AddWithValue(name,value);
        await command.ExecuteNonQueryAsync();
    }
    private async Task<InvalidDataException> RejectWithoutChanges(ProfileRepository repo,DatabaseBackupEntry entry)
    {
        entry=entry with { Bytes=new FileInfo(entry.Path).Length }; var bytes=await File.ReadAllBytesAsync(entry.Path); var files=Directory.GetFiles(repo.DatabaseBackupDirectory);
        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>DatabaseBackupCatalog.VerifyAsync(entry));
        Assert.Equal(bytes,await File.ReadAllBytesAsync(entry.Path)); Assert.Equal(files,Directory.GetFiles(repo.DatabaseBackupDirectory));
        Assert.Equal(profile,Assert.Single(await repo.ListAsync())); Assert.Equal("preserve",await repo.SettingAsync("marker"));
        Assert.DoesNotContain(profile.Name,error.Message); Assert.DoesNotContain(profile.Host,error.Message); Assert.DoesNotContain(profile.Description,error.Message);
        return error;
    }

    [Theory]
    [InlineData("DROP TABLE profiles")]
    [InlineData("DROP TABLE folders")]
    [InlineData("DROP TABLE folder_icons")]
    [InlineData("DROP TABLE hostkeys")]
    [InlineData("DROP TABLE settings")]
    [InlineData("DROP TABLE history")]
    [InlineData("ALTER TABLE profiles RENAME COLUMN json TO missing_json")]
    [InlineData("ALTER TABLE settings RENAME COLUMN value TO missing_value")]
    [InlineData("ALTER TABLE history DROP COLUMN snapshot")]
    [InlineData("DROP TABLE profiles; CREATE VIEW profiles AS SELECT 'fixture' AS id, '{}' AS json")]
    public async Task SQLite_integrity_alone_does_not_certify_an_incomplete_application_schema(string sql)
    {
        var (repo,entry)=await Snapshot(); await Execute(entry.Path,sql); await RejectWithoutChanges(repo,entry);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("null")]
    [InlineData("array")]
    [InlineData("missing-id")]
    [InlineData("empty-id")]
    [InlineData("mismatched-id")]
    [InlineData("invalid-host")]
    [InlineData("invalid-port")]
    [InlineData("null-name")]
    [InlineData("duplicate-field")]
    [InlineData("duplicate-nested-field")]
    [InlineData("null-options")]
    [InlineData("invalid-folder")]
    [InlineData("invalid-icon")]
    [InlineData("invalid-credential")]
    [InlineData("binary-json")]
    [InlineData("invalid-row-id")]
    [InlineData("null-row-id")]
    [InlineData("duplicate-identity")]
    public async Task Invalid_saved_profiles_are_reported_without_rewriting_the_backup_or_live_data(string kind)
    {
        var (repo,entry)=await Snapshot(); var json=JsonSerializer.SerializeToNode(profile)!.AsObject();
        switch(kind)
        {
            case "missing-id": json.Remove("Id"); break;
            case "empty-id": json["Id"]=Guid.Empty; break;
            case "mismatched-id": json["Id"]=Guid.NewGuid(); break;
            case "invalid-host": json["Host"]="bad/host"; break;
            case "invalid-port": json["Port"]=0; break;
            case "null-name": json["Name"]=null; break;
            case "null-options": json["Rdp"]=null; break;
            case "invalid-folder": json["Folder"]="Lab/../SSH"; break;
            case "invalid-icon": json["CustomIconPng"]="not a PNG"; break;
            case "invalid-credential": json["CredentialId"]=Guid.Empty; break;
        }
        var text=kind switch
        {
            "malformed"=>"{",
            "null"=>"null",
            "array"=>"[]",
            "duplicate-field"=>json.ToJsonString().Insert(1,"\"Name\":\"ambiguous\","),
            "duplicate-nested-field"=>json.ToJsonString().Replace("\"Rdp\":{","\"Rdp\":{\"Width\":800,"),
            _=>json.ToJsonString()
        };
        await Execute(entry.Path,"UPDATE profiles SET json=$json",("$json",kind=="binary-json"?System.Text.Encoding.UTF8.GetBytes(text):text));
        if(kind=="invalid-row-id")await Execute(entry.Path,"UPDATE profiles SET id='not a GUID'");
        if(kind=="null-row-id")await Execute(entry.Path,"UPDATE profiles SET id=NULL");
        if(kind=="duplicate-identity")await Execute(entry.Path,"INSERT INTO profiles SELECT '{'||id||'}',json FROM profiles");
        await RejectWithoutChanges(repo,entry);
    }

    [Fact] public async Task A_profile_over_the_verification_budget_is_not_loaded_or_rewritten()
    {
        var (repo,entry)=await Snapshot();
        await Execute(entry.Path,"UPDATE profiles SET json=$json",("$json",JsonSerializer.Serialize(profile with { Description=new string('x',StreamLimits.ConfigurationBytes) })));
        var error=await RejectWithoutChanges(repo,entry);
        Assert.Contains("32 MiB",error.Message);
    }

    [Fact] public async Task A_valid_first_profile_does_not_hide_a_later_invalid_record()
    {
        var (repo,entry)=await Snapshot();
        await Execute(entry.Path,"INSERT INTO profiles VALUES($id,'null')",("$id",Guid.NewGuid().ToString()));
        await RejectWithoutChanges(repo,entry);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Historical_profiles_keep_defaults_unknown_fields_and_platform_specific_paths(int schema)
    {
        var (repo,entry)=await Snapshot();
        var legacy=JsonSerializer.Serialize(new { Id=profile.Id,profile.Name,profile.Host,profile.Username,profile.Folder,PrivateKeyPath="/home/fixture/.ssh/key",Authentication=Authentication.PrivateKey,FutureField="preserve" });
        await Execute(entry.Path,"UPDATE profiles SET json=$json; PRAGMA user_version="+schema+(schema<3?"; DROP TABLE folder_icons; ALTER TABLE history DROP COLUMN snapshot":""),("$json",legacy));
        var target=entry.Path.Replace("-v4-","-v"+schema+"-"); if(target!=entry.Path)File.Move(entry.Path,target);
        var bytes=await File.ReadAllBytesAsync(target); var files=Directory.GetFiles(repo.DatabaseBackupDirectory);
        Assert.Equal(schema,await DatabaseBackupCatalog.VerifyAsync(entry with { Path=target,SchemaVersion=schema,Bytes=bytes.Length }));
        Assert.Equal(bytes,await File.ReadAllBytesAsync(target)); Assert.Equal(files,Directory.GetFiles(repo.DatabaseBackupDirectory));
        Assert.Equal(profile,Assert.Single(await repo.ListAsync()));
    }

    [Theory] [InlineData(Protocol.SSH)] [InlineData(Protocol.RDP)] [InlineData(Protocol.VNC)] [InlineData(Protocol.Serial)]
    public async Task All_saved_protocols_are_checked_offline_without_connecting_or_opening_private_keys(Protocol protocol)
    {
        var repo=new ProfileRepository(new(root)); await repo.InitializeAsync();
        await repo.SaveAsync(profile with { Protocol=protocol,PrivateKeyPath="C:\\nonexistent\\fixture.key",Authentication=Authentication.PrivateKey,Serial=new() { PortName="/dev/ttyUSB0" } });
        var backup=await repo.CreateDatabaseBackupAsync(); var entry=new DatabaseBackupEntry(backup.Path,DateTimeOffset.UtcNow,backup.Bytes,backup.SchemaVersion,false);
        var bytes=await File.ReadAllBytesAsync(entry.Path); Assert.Equal(backup.SchemaVersion,await DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Equal(bytes,await File.ReadAllBytesAsync(entry.Path));
    }

    public void Dispose() { DatabaseTestCleanup.Clear(root); var parent=Path.GetDirectoryName(root)!; if(Directory.Exists(parent))Directory.Delete(parent,true); }
}
