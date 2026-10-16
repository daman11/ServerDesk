using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class ProfileAppearanceTests
{
    private static byte[] Fixture()
    {
        using var stream=typeof(ProfileAppearanceTests).Assembly.GetManifestResourceStream("ServerDesk.Tests.Fixtures.server-icon.png")!; using var bytes=new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray();
    }
    [Theory]
    [InlineData(null)] [InlineData("blue")] [InlineData("#fff")] [InlineData("#FF000000")] [InlineData("#XX8080")]
    public void Invalid_colors_are_rejected(string? value)=>Assert.Throws<ArgumentException>(()=>ProfileAppearance.Validate(value,""));
    [Fact]
    public void Defaults_custom_color_and_real_png_are_supported()
    {
        var old=JsonSerializer.Deserialize<ServerProfile>("{\"Name\":\"Legacy\",\"Host\":\"host\",\"Username\":\"admin\"}")!; old.Validate(); Assert.Equal("",old.CustomIconPng);
        ProfileAppearance.Validate("#aBcD01",Convert.ToBase64String(Fixture())); ProfileAppearance.Validate("","");
    }
    [Fact]
    public void Corrupt_oversized_or_non_png_icon_is_rejected()
    {
        Assert.Throws<ArgumentException>(()=>ProfileAppearance.Validate("#123456","not base64"));
        Assert.Throws<ArgumentException>(()=>ProfileAppearance.Validate("#123456",Convert.ToBase64String(new byte[ProfileAppearance.MaximumIconBytes+1])));
        var damaged=Fixture(); damaged[^5]^=1; Assert.Throws<ArgumentException>(()=>ProfileAppearance.ValidatePng(damaged));
        Assert.Throws<ArgumentException>(()=>ProfileAppearance.ValidatePng(Fixture()[..40]));
    }
    [Fact]
    public async Task Visuals_round_trip_and_reconnecting_old_snapshot_cannot_replace_them()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-appearance-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var old=new ServerProfile { Name="Database",Host="localhost",Username="admin" }; await repo.SaveAsync(old);
            var edited=old with { Color="#E5B65C",CustomIconPng=Convert.ToBase64String(Fixture()) }; await repo.SaveAsync(edited); await repo.ConnectedAsync(old);
            var saved=Assert.Single(await repo.ListAsync()); Assert.Equal(edited.Color,saved.Color); Assert.Equal(edited.CustomIconPng,saved.CustomIconPng);
            using var export=new MemoryStream(); await repo.ExportAsync(export); export.Position=0; await repo.ImportAsync(export); var imported=(await repo.ListAsync()).Single(p=>p.Id!=old.Id); Assert.Equal(edited.CustomIconPng,imported.CustomIconPng); Assert.Equal(edited.Color,imported.Color);
            var valid=edited with { Id=Guid.NewGuid() }; var invalid=valid with { Id=Guid.NewGuid(),CustomIconPng="invalid" };
            using var bad=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new ProfileRepository.ConfigurationExport(1,[valid,invalid],[]))); await Assert.ThrowsAsync<ArgumentException>(()=>repo.ImportAsync(bad)); Assert.Equal(2,(await repo.ListAsync()).Count);
        }
        finally { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
    }
    [Fact]
    public async Task Input_limits_reject_a_stream_before_reading_unbounded_data()
    {
        using var stream=new MemoryStream(new byte[1025]); await Assert.ThrowsAsync<InvalidDataException>(()=>StreamLimits.ReadAsync(stream,1024)); Assert.Equal(1025,stream.Position);
        stream.Position=0; Assert.Equal(1025,(await StreamLimits.ReadAsync(stream,1025)).Length);
    }
}
