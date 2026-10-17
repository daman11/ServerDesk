using System.Text.Json;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ConfigurationConsistencyTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-consistency-"+Guid.NewGuid().ToString("N"));
    private SqliteConnection Connection()=>new(new SqliteConnectionStringBuilder { DataSource=Path.Combine(root,"serverdesk.db"),Pooling=false,DefaultTimeout=5 }.ToString());
    private async Task<ProfileRepository> Repository()
    { var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); return repo; }
    private static ServerProfile Profile(string folder="Source/Child")=>new() { Name="Żółty serwer",Host="example.test",Username="fixture",Folder=folder,Color="#112233",Description="Keep",Favorite=true };
    private async Task Execute(string sql)
    { using var db=Connection(); await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText=sql; await cmd.ExecuteNonQueryAsync(); }
    private async Task<string> State(ProfileRepository repo)=>JsonSerializer.Serialize(new { Profiles=await repo.ListAsync(),Folders=await repo.FoldersAsync(),Icons=(await repo.FolderIconsAsync()).OrderBy(p=>p.Key,StringComparer.Ordinal) });

    [Fact] public async Task Moving_a_deleted_profile_does_not_create_destination_folders()
    {
        var repo=await Repository(); var profile=Profile(); await repo.SaveAsync(profile); await repo.DeleteAsync(profile.Id); var before=await State(repo);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.MoveToFolderAsync(profile.Id,"New/Deep/Folder")); Assert.Equal(before,await State(repo));
    }

    [Fact] public async Task Renaming_preserves_unrelated_and_unknown_profile_metadata()
    {
        var repo=await Repository(); var profile=Profile(); await repo.SaveAsync(profile); await repo.SetFolderIconAsync("Source/Child","database");
        await Execute("UPDATE profiles SET json=json_set(json,'$.FutureOptions',json('{\"keep\":[1,2,3]}'),'$.LastConnected','2026-10-17T08:00:00+00:00')");
        var expected=Assert.Single(await repo.ListAsync()) with { Folder="Renamed/Child" }; await repo.RenameFolderAsync("Source","Renamed");
        Assert.Equal(expected,Assert.Single(await repo.ListAsync())); Assert.Equal("database",(await repo.FolderIconsAsync())["Renamed/Child"]);
        using var db=Connection(); await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText="SELECT json_extract(json,'$.FutureOptions') FROM profiles";
        Assert.Equal("{\"keep\":[1,2,3]}",await cmd.ExecuteScalarAsync());
    }

    [Fact] public async Task Renaming_to_the_same_path_performs_no_updates_but_missing_source_is_rejected()
    {
        var repo=await Repository(); await repo.SaveAsync(Profile()); await repo.SetFolderIconAsync("Source","cloud"); var before=await State(repo);
        await Execute("CREATE TRIGGER no_profile_update BEFORE UPDATE ON profiles BEGIN SELECT RAISE(ABORT,'unexpected update'); END; CREATE TRIGGER no_folder_update BEFORE UPDATE ON folders BEGIN SELECT RAISE(ABORT,'unexpected update'); END; CREATE TRIGGER no_icon_update BEFORE UPDATE ON folder_icons BEGIN SELECT RAISE(ABORT,'unexpected update'); END;");
        await repo.RenameFolderAsync("Source","Source"); Assert.Equal(before,await State(repo));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.RenameFolderAsync("Missing","Missing"));
    }

    [Fact] public async Task Renaming_reads_latest_metadata_after_a_competing_writer_commits()
    {
        var repo=await Repository(); var profile=Profile(); await repo.SaveAsync(profile);
        using var writer=Connection(); await writer.OpenAsync(); using var transaction=writer.BeginTransaction(); using var edit=writer.CreateCommand(); edit.Transaction=transaction;
        edit.CommandText="UPDATE profiles SET json=json_set(json,'$.Name','New name','$.Description','New description')"; await edit.ExecuteNonQueryAsync();
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renaming=Task.Run(async()=> { started.SetResult(); return await Record.ExceptionAsync(()=>repo.RenameFolderAsync("Source","Renamed")); });
        Exception? error;
        try { await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); await Task.Delay(250); Assert.False(renaming.IsCompleted); }
        finally { await transaction.CommitAsync(); error=await renaming.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.Null(error); Assert.Equal(profile with { Folder="Renamed/Child",Name="New name",Description="New description" },Assert.Single(await repo.ListAsync()));
    }

    [Fact] public async Task Deleting_a_folder_rechecks_contents_after_a_competing_writer_commits()
    {
        var repo=await Repository(); await repo.AddFolderAsync("Source/Child"); await repo.SetFolderIconAsync("Source","cloud"); var profile=Profile();
        using var writer=Connection(); await writer.OpenAsync(); using var transaction=writer.BeginTransaction(); using var insert=writer.CreateCommand(); insert.Transaction=transaction;
        insert.CommandText="INSERT INTO profiles VALUES($id,$json)"; insert.Parameters.AddWithValue("$id",profile.Id.ToString()); insert.Parameters.AddWithValue("$json",JsonSerializer.Serialize(profile)); await insert.ExecuteNonQueryAsync();
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleting=Task.Run(async()=> { started.SetResult(); return await Record.ExceptionAsync(()=>repo.DeleteFolderAsync("Source")); });
        Exception? error;
        try { await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); await Task.Delay(250); Assert.False(deleting.IsCompleted); }
        finally { await transaction.CommitAsync(); error=await deleting.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.IsType<InvalidOperationException>(error); Assert.Equal(profile,Assert.Single(await repo.ListAsync()));
        Assert.Contains("Source/Child",await repo.FoldersAsync()); Assert.Equal("cloud",(await repo.FolderIconsAsync())["Source"]);
    }

    [Fact] public async Task Deleting_an_implicit_legacy_parent_still_removes_its_empty_descendants_and_icons()
    {
        var repo=await Repository(); await repo.AddFolderAsync("Source/Child"); await repo.SetFolderIconAsync("Source/Child","database");
        await Execute("DELETE FROM folders WHERE path='Source'");
        await repo.DeleteFolderAsync("Source"); Assert.Empty(await repo.FoldersAsync()); Assert.Empty(await repo.FolderIconsAsync());
    }

    [Fact] public async Task Export_uses_one_snapshot_while_another_connection_changes_profiles_folders_and_icons()
    {
        var repo=await Repository(); await repo.SaveAsync(Profile("A/Child")); await repo.SetFolderIconAsync("A/Child","database");
        var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer=Task.Run(async()=>
        {
            using var db=Connection(); await db.OpenAsync(); await start.Task;
            for(var i=0;i<60;i++)
            {
                var from=i%2==0?"A":"B"; var to=i%2==0?"B":"A"; using var tx=db.BeginTransaction(); using var cmd=db.CreateCommand(); cmd.Transaction=tx;
                cmd.CommandText="UPDATE profiles SET json=json_set(json,'$.Folder',$to||'/Child'); UPDATE folder_icons SET path=replace(path,$from,$to); UPDATE folders SET path=replace(path,$from,$to)";
                cmd.Parameters.AddWithValue("$from",from); cmd.Parameters.AddWithValue("$to",to); await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync(); await Task.Yield();
            }
        });
        var reader=Task.Run(async()=>
        {
            start.SetResult();
            for(var i=0;i<80;i++)
            {
                using var stream=new MemoryStream(); await repo.ExportAsync(stream); var data=JsonSerializer.Deserialize<ProfileRepository.ConfigurationExport>(stream.ToArray())!;
                var profile=Assert.Single(data.Servers); Assert.Contains(profile.Folder,data.Folders); Assert.Equal("database",data.FolderIcons![profile.Folder]);
                Assert.Contains(profile.Folder.Split('/')[0],data.Folders);
            }
        });
        await Task.WhenAll(writer,reader).WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Import_rolls_back_real_write_errors_and_retries_without_overwriting_local_icons(bool failIcon)
    {
        var repo=await Repository(); await repo.SaveAsync(Profile("Keep")); await repo.SetFolderIconAsync("Keep","shield"); var before=await State(repo);
        var profile=Profile("New/Child") with { Id=Guid.NewGuid(),Name="Imported" };
        var import=new ProfileRepository.ConfigurationExport(2,[profile],["Keep","New/Child"]) { FolderIcons=new() { ["Keep"]="cloud",["New/Child"]="database" } };
        await Execute(failIcon?"CREATE TRIGGER fail_import BEFORE INSERT ON folder_icons WHEN NEW.path='New/Child' BEGIN SELECT RAISE(ABORT,'fixture icon error'); END":"CREATE TRIGGER fail_import BEFORE INSERT ON folders WHEN NEW.path='New/Child' BEGIN SELECT RAISE(ABORT,'fixture folder error'); END");
        using var stream=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(import)); await Assert.ThrowsAsync<SqliteException>(()=>repo.ImportAsync(stream)); Assert.Equal(before,await State(repo));
        await Execute("DROP TRIGGER fail_import"); stream.Position=0; await repo.ImportAsync(stream);
        var saved=(await repo.ListAsync()).Single(p=>p.Name=="Imported"); Assert.NotEqual(profile.Id,saved.Id); Assert.Equal(profile with { Id=saved.Id },saved);
        var icons=await repo.FolderIconsAsync(); Assert.Equal("shield",icons["Keep"]); Assert.Equal("database",icons["New/Child"]); Assert.Contains("New",await repo.FoldersAsync());
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
