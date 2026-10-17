using System.Text.Json;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ConfigurationExportTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-export-"+Guid.NewGuid().ToString("N"));
    private string Target=>Path.Combine(root,"exports","serwery żółć.json");
    private async Task<ProfileRepository> Repository()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); Directory.CreateDirectory(Path.GetDirectoryName(Target)!); return repo;
    }
    private void NoStagingFiles()=>Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Target)!,"*.part"));

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Export_publishes_complete_JSON_and_round_trips_profiles_and_folder_icons(bool replace)
    {
        var repo=await Repository(); var profile=new ServerProfile { Name="Żółty serwer",Host="example.test",Username="fixture",Folder="Lab/Linux",Color="#112233" };
        await repo.SaveAsync(profile); await repo.SetFolderIconAsync(profile.Folder,"database");
        if(replace)await File.WriteAllTextAsync(Target,new string('x',16000));
        await repo.ExportFileAsync(Target); var bytes=await File.ReadAllBytesAsync(Target);
        var exported=JsonSerializer.Deserialize<ProfileRepository.ConfigurationExport>(bytes)!;
        Assert.Equal(profile,Assert.Single(exported.Servers)); Assert.Equal("database",exported.FolderIcons![profile.Folder]); NoStagingFiles();
        var restored=new ProfileRepository(new AppPaths(Path.Combine(root,"restored"))); await restored.InitializeAsync();
        using var input=new MemoryStream(bytes); await restored.ImportAsync(input); var imported=Assert.Single(await restored.ListAsync());
        Assert.NotEqual(profile.Id,imported.Id); Assert.Equal(profile with { Id=imported.Id },imported); Assert.Equal("database",(await restored.FolderIconsAsync())[profile.Folder]);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Cancelled_export_never_creates_or_replaces_the_destination(bool existing)
    {
        var repo=await Repository(); if(existing)await File.WriteAllTextAsync(Target,"previous export"); using var cancellation=new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repo.ExportFileAsync(Target,cancellation.Token));
        if(existing)Assert.Equal("previous export",await File.ReadAllTextAsync(Target)); else Assert.False(File.Exists(Target)); NoStagingFiles();
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Invalid_source_data_is_reported_before_touching_the_destination(bool existing)
    {
        var repo=await Repository(); if(existing)await File.WriteAllTextAsync(Target,"previous export");
        using(var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.Combine(root,"serverdesk.db"),Pooling=false }.ToString()))
        {
            await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText="INSERT INTO profiles VALUES($id,'not JSON')"; cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString()); await cmd.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<JsonException>(()=>repo.ExportFileAsync(Target));
        if(existing)Assert.Equal("previous export",await File.ReadAllTextAsync(Target)); else Assert.False(File.Exists(Target)); NoStagingFiles();
    }

    [Fact] public async Task Publication_failure_preserves_an_existing_directory_and_removes_only_its_own_staging_file()
    {
        var repo=await Repository(); Directory.CreateDirectory(Target); var keep=Path.Combine(Target,"keep.txt"); await File.WriteAllTextAsync(keep,"keep");
        var unrelated=Path.Combine(Path.GetDirectoryName(Target)!,"older-export.part"); await File.WriteAllTextAsync(unrelated,"unrelated");
        var error=await Record.ExceptionAsync(()=>repo.ExportFileAsync(Target)); Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal("keep",await File.ReadAllTextAsync(keep)); Assert.Equal("unrelated",await File.ReadAllTextAsync(unrelated));
        Assert.Equal(new[]{unrelated},Directory.GetFiles(Path.GetDirectoryName(Target)!,"*.part"));
    }

    [Fact] public async Task Locked_export_is_preserved_and_can_be_replaced_after_unlocking()
    {
        if(!OperatingSystem.IsWindows())return;
        var repo=await Repository(); await File.WriteAllTextAsync(Target,"previous export");
        using(var locked=new FileStream(Target,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            var error=await Record.ExceptionAsync(()=>repo.ExportFileAsync(Target)); Assert.True(error is IOException or UnauthorizedAccessException); NoStagingFiles();
        }
        Assert.Equal("previous export",await File.ReadAllTextAsync(Target)); await repo.ExportFileAsync(Target);
        using var json=JsonDocument.Parse(await File.ReadAllTextAsync(Target)); Assert.Equal(2,json.RootElement.GetProperty("Version").GetInt32()); NoStagingFiles();
    }

    public void Dispose()
    {
        DatabaseTestCleanup.Clear(root); DatabaseTestCleanup.Clear(Path.Combine(root,"restored"));
        if(Directory.Exists(root))Directory.Delete(root,true);
    }
}
