using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class ProfileIconTests
{
    [Fact]
    public void Old_profiles_get_protocol_icons_without_schema_changes()
    {
        var old=JsonSerializer.Deserialize<ServerProfile>("{\"Name\":\"Legacy\",\"Host\":\"host\",\"Username\":\"admin\"}")!;
        Assert.Equal("auto",old.IconId); Assert.Equal("ssh",ProfileIcons.Resolve(old)); Assert.Equal("rdp",ProfileIcons.Resolve(old with { Protocol=Protocol.RDP }));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../server")]
    [InlineData("https://example.org/icon.svg")]
    [InlineData("DATABASE")]
    public void Malformed_ids_are_rejected(string? icon)=>Assert.Throws<ArgumentException>(()=>ProfileIcons.Validate(icon));
    [Fact]
    public void Unknown_future_icon_is_preserved_with_safe_protocol_fallback()
    {
        var profile=new ServerProfile { Name="Future",Host="host",Username="admin",IconId="future-icon-2" }; profile.Validate();
        Assert.Equal("ssh",ProfileIcons.Resolve(profile)); Assert.Equal("future-icon-2",profile.IconId);
    }
    [Fact]
    public async Task Icons_persist_duplicate_and_export_without_credential_reuse()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-icon-test-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync();
            var profile=new ServerProfile { Name="Database",Host="localhost",Username="admin",IconId="database" }; await repo.SaveAsync(profile);
            Assert.Equal("database",(await repo.ListAsync()).Single().IconId);
            var duplicate=profile with { Id=Guid.NewGuid() }; Assert.Equal(profile.IconId,duplicate.IconId);
            using var export=new MemoryStream(); await repo.ExportAsync(export); export.Position=0; await repo.ImportAsync(export);
            var imported=(await repo.ListAsync()).Single(p=>p.Id!=profile.Id); Assert.Equal(profile.IconId,imported.IconId); Assert.Null(imported.LastConnected);
        }
        finally { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
    }
}
