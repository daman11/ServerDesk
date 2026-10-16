using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Text.Json;
using Xunit;

namespace ServerDesk.Tests;
public sealed class ConnectionHistoryTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-history-0190-"+Guid.NewGuid());
    private ProfileRepository Repo()=>new(new AppPaths(root));
    private static ServerProfile Profile(Protocol protocol=Protocol.SSH)=>new() { Name="Żółty serwer",Protocol=protocol,Host="example.test",Port=protocol switch { Protocol.RDP=>3389,Protocol.VNC=>5900,_=>22 },Username="admin",Serial=new() { PortName="COM3" } };
    [Theory] [InlineData(Protocol.SSH)] [InlineData(Protocol.RDP)] [InlineData(Protocol.VNC)] [InlineData(Protocol.Serial)]
    public async Task Snapshot_and_recent_timestamp_survive_reopening(Protocol protocol)
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(protocol); await repo.SaveAsync(p); await repo.ConnectedAsync(p);
        var entry=Assert.Single(await Repo().HistoryAsync()); Assert.Equal(ConnectionHistorySnapshot.Capture(p),entry.Snapshot); Assert.True(entry.CanReconnect); Assert.Equal(protocol,entry.Protocol);
        Assert.Equal(entry.Time,Assert.Single(await repo.ListAsync()).LastConnected);
    }
    [Fact] public async Task Historical_target_survives_profile_edit_and_deletion()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(); await repo.SaveAsync(p); await repo.ConnectedAsync(p);
        await repo.SaveAsync(p with { Host="other.test",Name="Renamed",Protocol=Protocol.VNC });
        var changed=Assert.Single(await repo.HistoryAsync()); Assert.Equal(p.Name,changed.Name); Assert.Contains(p.Endpoint,changed.Detail); Assert.Equal(Protocol.SSH,changed.Protocol); Assert.False(changed.CanReconnect);
        await repo.DeleteAsync(p.Id); var deleted=Assert.Single(await repo.HistoryAsync()); Assert.Null(deleted.Profile); Assert.Equal(changed.Snapshot,deleted.Snapshot); Assert.False(deleted.CanReconnect);
        Assert.Single(await repo.HistoryAsync(protocol:Protocol.SSH,search:"ŻÓŁTY example.test")); Assert.Empty(await repo.HistoryAsync(search:"Renamed"));
    }
    [Fact] public async Task Old_live_session_does_not_touch_new_target_recency()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(); await repo.SaveAsync(p with { Host="new.test" }); await repo.ConnectedAsync(p);
        Assert.Null(Assert.Single(await repo.ListAsync()).LastConnected); Assert.Single(await repo.HistoryAsync());
    }
    [Fact] public async Task Cosmetic_edits_keep_reconnect_available()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(); await repo.SaveAsync(p); await repo.ConnectedAsync(p);
        await repo.SaveAsync(p with { Name="New name",IconId="database",Folder="New",Color="#112233" });
        var entry=Assert.Single(await repo.HistoryAsync()); Assert.True(entry.CanReconnect); Assert.Equal(p.Name,entry.Name); Assert.Equal("New name",entry.Profile!.Name);
    }
    [Fact] public async Task Search_and_protocol_filters_apply_before_pagination()
    {
        var repo=Repo(); await repo.InitializeAsync(); var older=Profile(Protocol.Serial); await repo.SaveAsync(older); await repo.ConnectedAsync(older);
        for(var i=0;i<105;i++)await repo.ConnectedAsync(Profile(Protocol.VNC) with { Name="Noise" });
        var match=Assert.Single(await repo.HistoryAsync(search:"żÓŁtY com3 serial")); Assert.Equal(older.Id,match.ProfileId);
        Assert.Single(await repo.HistoryAsync(protocol:Protocol.Serial));
        var first=await repo.HistoryAsync(100,protocol:Protocol.VNC); var rest=await repo.HistoryAsync(100,first.Last().Id,protocol:Protocol.VNC);
        Assert.Equal(100,first.Count); Assert.Equal(5,rest.Count); Assert.Empty(first.Select(e=>e.Id).Intersect(rest.Select(e=>e.Id)));
        Assert.Empty(await repo.HistoryAsync(search:"COM3 missing"));
    }
    [Fact] public async Task Schema_two_migration_preserves_old_rows_and_is_idempotent()
    {
        _=new AppPaths(root); var p=Profile();
        await using(var db=new SqliteConnection("Data Source="+Path.Combine(root,"serverdesk.db")))
        {
            await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText="CREATE TABLE history(id INTEGER PRIMARY KEY,profile TEXT NOT NULL,time TEXT NOT NULL); CREATE TABLE profiles(id TEXT PRIMARY KEY,json TEXT NOT NULL); INSERT INTO profiles VALUES($id,$json); INSERT INTO history VALUES(17,$id,$time); PRAGMA user_version=2";
            cmd.Parameters.AddWithValue("$id",p.Id.ToString()); cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(p)); cmd.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.ToString("O")); await cmd.ExecuteNonQueryAsync();
        }
        var repo=Repo(); await repo.InitializeAsync(); await repo.InitializeAsync(); var legacy=Assert.Single(await repo.HistoryAsync()); Assert.Equal(17,legacy.Id); Assert.Null(legacy.Snapshot); Assert.True(legacy.CanReconnect);
        await repo.ConnectedAsync(p); Assert.Equal(2,(await repo.HistoryAsync()).Count); Assert.Single(await repo.HistoryAsync(search:"ŻÓŁTY" ,beforeId:18));
    }
    [Fact] public async Task Newer_schema_is_rejected_without_overwriting_version()
    {
        var repo=Repo(); await repo.InitializeAsync();
        await using var db=new SqliteConnection("Data Source="+Path.Combine(root,"serverdesk.db")); await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText="PRAGMA user_version=4"; await cmd.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.InitializeAsync()); cmd.CommandText="PRAGMA user_version"; Assert.Equal(4L,await cmd.ExecuteScalarAsync());
    }
    [Fact] public void Identity_comparison_respects_protocol_username_and_linux_device_case()
    {
        var p=Profile(); var snapshot=ConnectionHistorySnapshot.Capture(p); Assert.True(snapshot.Matches(p with { Host="EXAMPLE.TEST" })); Assert.False(snapshot.Matches(p with { Username="Admin" })); Assert.False(snapshot.Matches(p with { Port=2222 }));
        p=Profile(Protocol.RDP) with { Username="ACME\\admin" }; Assert.True(ConnectionHistorySnapshot.Capture(p).Matches(p with { Username="ADMIN",Domain="acme" }));
        p=Profile(Protocol.Serial) with { Serial=new() { PortName="/dev/ttyUSB0" } }; Assert.False(ConnectionHistorySnapshot.Capture(p).Matches(p with { Serial=p.Serial with { PortName="/dev/ttyusb0" } }));
    }
    [Fact] public async Task Cancelled_write_leaves_no_history_or_recency()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile(); await repo.SaveAsync(p); using var cancel=new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repo.ConnectedAsync(p,cancel.Token)); Assert.Empty(await repo.HistoryAsync()); Assert.Null(Assert.Single(await repo.ListAsync()).LastConnected);
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
