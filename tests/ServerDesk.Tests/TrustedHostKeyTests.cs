using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class TrustedHostKeyTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-trust-"+Guid.NewGuid().ToString("N"));
    private ProfileRepository Repo()=>new(new AppPaths(root));

    [Fact] public async Task ListIncludesUnreferencedHostsAndSeparatePorts()
    {
        var repo=Repo(); await repo.InitializeAsync();
        await repo.TrustHostAsync("Z-HOST",22,"SHA256:z"); await repo.TrustHostAsync("a-host",2222,"SHA256:a2"); await repo.TrustHostAsync("A-HOST",22,"SHA256:a1");
        Assert.Equal(new[]{new TrustedHostKey("a-host",22,"SHA256:a1"),new TrustedHostKey("a-host",2222,"SHA256:a2"),new TrustedHostKey("z-host",22,"SHA256:z")},await Repo().TrustedHostKeysAsync());
        Assert.Empty(await repo.ListAsync());
    }
    [Fact] public async Task ForgetOnlyRemovesReviewedHostPortAndFingerprint()
    {
        var repo=Repo(); await repo.InitializeAsync();
        await repo.TrustHostAsync("host",22,"SHA256:first"); await repo.TrustHostAsync("host",2222,"SHA256:other"); await repo.TrustHostAsync("other",22,"SHA256:first");
        Assert.False(await repo.ForgetHostKeyAsync(new("host",22,"SHA256:stale")));
        Assert.Equal("SHA256:first",await repo.HostKeyAsync("host",22));
        Assert.True(await repo.ForgetHostKeyAsync(new("HOST",22,"SHA256:first")));
        Assert.Null(await repo.HostKeyAsync("host",22)); Assert.Equal(2,(await repo.TrustedHostKeysAsync()).Count);
        Assert.False(await repo.ForgetHostKeyAsync(new("host",22,"SHA256:first")));
    }
    [Fact] public async Task StaleReviewCannotDeleteReplacementKey()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.TrustHostAsync("host",22,"SHA256:old");
        var reviewed=Assert.Single(await repo.TrustedHostKeysAsync()); Assert.True(await Repo().ForgetHostKeyAsync(reviewed));
        await Repo().TrustHostAsync("host",22,"SHA256:new");
        Assert.False(await repo.ForgetHostKeyAsync(reviewed)); Assert.Equal("SHA256:new",await repo.HostKeyAsync("host",22));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.TrustHostAsync("host",22,"SHA256:other"));
    }
    [Fact] public async Task ForgetDoesNotRemoveProfileSettingsOrHistoryAndTrustIsNotExported()
    {
        var repo=Repo(); await repo.InitializeAsync(); var profile=new ServerProfile { Name="Fixture",Host="host",Username="fixture" };
        await repo.SaveAsync(profile); await repo.SetSettingAsync("theme","light"); await repo.ConnectedAsync(profile); await repo.TrustHostAsync("host",22,"SHA256:host-key-fixture");
        using var json=new MemoryStream(); await repo.ExportAsync(json); Assert.DoesNotContain("SHA256:host-key-fixture",System.Text.Encoding.UTF8.GetString(json.ToArray()));
        Assert.True(await repo.ForgetHostKeyAsync(new("host",22,"SHA256:host-key-fixture")));
        Assert.Single(await repo.ListAsync()); Assert.Single(await repo.HistoryAsync()); Assert.Equal("light",await repo.SettingAsync("theme"));
    }
    [Fact] public async Task CancelledForgetDoesNotChangeTrust()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.TrustHostAsync("host",22,"SHA256:keep");
        using var cancel=new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repo.ForgetHostKeyAsync(new("host",22,"SHA256:keep"),cancel.Token));
        Assert.Equal("SHA256:keep",await repo.HostKeyAsync("host",22));
    }
    [Theory] [InlineData("host",22,"host:22")] [InlineData("2001:db8::1",2222,"[2001:db8::1]:2222")]
    public void EndpointKeepsIpv6Unambiguous(string host,int port,string endpoint)=>Assert.Equal(endpoint,new TrustedHostKey(host,port,"fixture").Endpoint);

    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
