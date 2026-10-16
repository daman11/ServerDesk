using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class FolderIconTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-folder-icons-"+Guid.NewGuid());
    private ProfileRepository Repo()=>new(new AppPaths(root));
    [Fact] public async Task Migration_from_version_one_keeps_profiles_and_folders()
    {
        var paths=new AppPaths(root);
        await using(var db=new SqliteConnection($"Data Source={Path.Combine(paths.Data,"serverdesk.db")}"))
        {
            await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText="CREATE TABLE folders(path TEXT PRIMARY KEY); INSERT INTO folders VALUES('Prod/Linux'); CREATE TABLE profiles(id TEXT PRIMARY KEY,json TEXT NOT NULL); INSERT INTO profiles VALUES($id,$json); PRAGMA user_version=1";
            var profile=new ServerProfile { Name="Legacy",Host="localhost",Username="user",Folder="Prod/Linux" };
            cmd.Parameters.AddWithValue("$id",profile.Id.ToString()); cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(profile)); await cmd.ExecuteNonQueryAsync();
        }
        var repo=Repo(); await repo.InitializeAsync(); Assert.Contains("Prod/Linux",await repo.FoldersAsync());
        Assert.Equal("Legacy",Assert.Single(await repo.ListAsync()).Name);
        await repo.SetFolderIconAsync("Prod/Linux","database"); Assert.Equal("database",(await Repo().FolderIconsAsync())["Prod/Linux"]);
    }
    [Fact] public async Task Recursive_rename_and_delete_move_and_clean_up_icons()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.AddFolderAsync("Prod/Linux");
        await repo.SetFolderIconAsync("Prod","cloud"); await repo.SetFolderIconAsync("Prod/Linux","container");
        await repo.RenameFolderAsync("Prod","Production"); var read=await Repo().FolderIconsAsync();
        Assert.Equal("cloud",read["Production"]); Assert.Equal("container",read["Production/Linux"]); Assert.False(read.ContainsKey("Prod"));
        await repo.DeleteFolderAsync("Production"); Assert.Empty(await repo.FolderIconsAsync());
        await repo.AddFolderAsync("Production"); Assert.Empty(await repo.FolderIconsAsync());
    }
    [Fact] public async Task Export_import_preserves_icons_and_existing_local_choices()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.AddFolderAsync("Prod/Linux"); await repo.SetFolderIconAsync("Prod/Linux","database");
        using var export=new MemoryStream(); await repo.ExportAsync(export);
        await repo.SetFolderIconAsync("Prod/Linux","shield"); export.Position=0; await repo.ImportAsync(export);
        Assert.Equal("shield",(await repo.FolderIconsAsync())["Prod/Linux"]);
        await repo.DeleteFolderAsync("Prod"); export.Position=0; await repo.ImportAsync(export);
        Assert.Equal("database",(await repo.FolderIconsAsync())["Prod/Linux"]); Assert.Contains("Prod",await repo.FoldersAsync());
    }
    [Fact] public async Task Invalid_metadata_does_not_partially_import()
    {
        var repo=Repo(); await repo.InitializeAsync();
        var data=new ProfileRepository.ConfigurationExport(2,[new ServerProfile { Name="new",Host="host",Username="user" }],["Prod"]) { FolderIcons=new() { ["Prod"]="bad/icon" } };
        using var stream=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(data)); await Assert.ThrowsAsync<ArgumentException>(()=>repo.ImportAsync(stream));
        Assert.Empty(await repo.FoldersAsync()); Assert.Empty(await repo.ListAsync());
    }
    [Fact] public async Task Legacy_import_and_unknown_icon_roundtrip_work()
    {
        var repo=Repo(); await repo.InitializeAsync();
        using var legacy=new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":1,\"Servers\":[],\"Folders\":[\"Prod/Linux\"]}")); await repo.ImportAsync(legacy);
        await repo.SetFolderIconAsync("Prod/Linux","future-symbol"); Assert.Equal("folder",FolderIcons.Resolve((await repo.FolderIconsAsync())["Prod/Linux"]));
        using var export=new MemoryStream(); await repo.ExportAsync(export); Assert.Contains("future-symbol",Encoding.UTF8.GetString(export.ToArray()));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.SetFolderIconAsync("missing","cloud"));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
