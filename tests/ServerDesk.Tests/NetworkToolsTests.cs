using System.Numerics;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class NetworkToolsTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-network-"+Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("192.0.2.10/24","192.0.2.0","192.0.2.255","192.0.2.1","192.0.2.254",256,254)]
    [InlineData("192.0.2.5/30","192.0.2.4","192.0.2.7","192.0.2.5","192.0.2.6",4,2)]
    [InlineData("192.0.2.5/31","192.0.2.4","192.0.2.5","192.0.2.4","192.0.2.5",2,2)]
    [InlineData("192.0.2.5/32","192.0.2.5","192.0.2.5","192.0.2.5","192.0.2.5",1,1)]
    public void Ipv4_special_prefixes(string input,string network,string last,string firstHost,string lastHost,int count,int hosts)
    { var value=SubnetCalculation.Calculate(input); Assert.Equal(network,value.Network); Assert.Equal(last,value.Last); Assert.Equal(firstHost,value.FirstUsable); Assert.Equal(lastHost,value.LastUsable); Assert.Equal(new BigInteger(count),value.Addresses); Assert.Equal(new BigInteger(hosts),value.Hosts); }
    [Fact] public void Dotted_mask_wildcard_and_subdivision()
    { var value=SubnetCalculation.Calculate("192.0.2.10 255.255.255.0",26); Assert.Equal(24,value.Prefix); Assert.Equal("0.0.0.255",value.Wildcard); Assert.Equal(new[]{"192.0.2.0/26","192.0.2.64/26","192.0.2.128/26","192.0.2.192/26"},value.Subnets); }
    [Fact] public void Ipv6_big_integer_range_and_compressed_expanded_forms()
    { var value=SubnetCalculation.Calculate("2001:db8::1/64",66); Assert.True(value.IPv6); Assert.Equal("2001:db8::",value.Network); Assert.Equal("2001:db8::ffff:ffff:ffff:ffff",value.Last); Assert.Equal(BigInteger.One<<64,value.Addresses); Assert.Equal("2001:0db8:0000:0000:0000:0000:0000:0001",value.Expanded); Assert.Equal(4,value.Subnets.Count); Assert.Equal("—",value.Mask); Assert.Equal(BigInteger.One<<128,SubnetCalculation.Calculate("::1/0").Addresses); }
    [Theory] [InlineData("192.0.2.1/33",-1)] [InlineData("2001:db8::1/129",-1)] [InlineData("192.0.2.1 255.0.255.0",-1)] [InlineData("192.0.2.1/24",23)] [InlineData("::1/0",128)] [InlineData("bad/24",-1)]
    public void Subnet_rejects_invalid_or_unbounded_expansion(string input,int split)=>Assert.Throws<ArgumentException>(()=>SubnetCalculation.Calculate(input,split));
    [Fact] public void Port_ranges_are_sorted_distinct_and_bounded()
    { Assert.Equal(new[]{22,80,81,82,443},NetworkRequest.ParsePorts("443,80-82,22,80")); Assert.Equal(65535,NetworkRequest.ParsePorts("1-65535").Length); }
    [Theory] [InlineData("0")] [InlineData("65536")] [InlineData("80-1")] [InlineData("22;whoami")] [InlineData("")] [InlineData("1--3")]
    public void Bad_port_ranges_are_rejected(string value)=>Assert.Throws<ArgumentException>(()=>NetworkRequest.ParsePorts(value));
    [Theory] [InlineData("127.0.0.1")] [InlineData("::1")] [InlineData("fixture.example")] [InlineData("żółć.example")]
    public void Host_accepts_ip_and_idn(string value)=>Assert.NotEmpty(NetworkRequest.Host(value));
    [Theory] [InlineData("127.0.0.1;whoami")] [InlineData("https://host.test")] [InlineData("host.test:22")] [InlineData("bad host")] [InlineData("-o ProxyCommand=sh")]
    public void Host_never_accepts_shell_commands_or_urls(string value)=>Assert.Throws<ArgumentException>(()=>NetworkRequest.Host(value));
    [Fact] public void Magic_packet_contains_six_ff_and_sixteen_exact_mac_repetitions()
    { var packet=NetworkRequest.MagicPacket("02:12:34:56:78:9A"); Assert.Equal(102,packet.Length); Assert.All(packet.Take(6),b=>Assert.Equal(255,b)); for(var i=0;i<16;i++)Assert.Equal(Convert.FromHexString("02123456789A"),packet.Skip(6+6*i).Take(6)); }
    [Theory] [InlineData("00:00:00:00:00:00")] [InlineData("FF:FF:FF:FF:FF:FF")] [InlineData("bad")] [InlineData("01:12:34:56:78:9A")]
    public void Invalid_or_group_mac_is_rejected(string mac)=>Assert.Throws<ArgumentException>(()=>NetworkRequest.MagicPacket(mac));
    [Fact] public void Csv_quotes_and_spreadsheet_formula_injection_are_handled()
    { var csv=NetworkExport.Csv(new[]{new[]{"=cmd()","+test","@data","-1","a,\"b\"\nc"}}); Assert.Contains("\"'=cmd()\"",csv); Assert.Contains("\"a,\"\"b\"\"\nc\"",csv); }
    [Fact] public void Http_history_removes_path_query_headers_body_and_credentials()
    { var request=new NetworkRequest { Tool=NetworkToolKind.Http,Target="https://example.test/secret-path?token=private-token",Headers="Authorization: Bearer private-token",Body="private-password",Query="password=private-password",Authentication="Bearer",Username="private-user" }; var json=JsonSerializer.Serialize(NetworkToolsStore.SafeHistory(request)); Assert.DoesNotContain("private",json); Assert.DoesNotContain("secret-path",json); Assert.Contains("https://example.test",json); }
    [Fact] public void Redaction_masks_known_secrets_json_tokens_and_passwords()
    { var text=NetworkToolsService.Redact("{\"access_token\":\"unknown\",\"nested\":{\"password\":\"hidden\"},\"echo\":\"known-password\",\"data\":123}",new(Password:"known-password")); Assert.DoesNotContain("unknown",text); Assert.DoesNotContain("hidden",text); Assert.DoesNotContain("known-password",text); Assert.Contains("123",text); }
    [Fact] public async Task History_and_collections_have_separate_safe_storage_and_deletion()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var credentials=new MemoryCredentials(); var store=new NetworkToolsStore(repo,credentials);
        var request=new NetworkRequest { Tool=NetworkToolKind.Http,Target="https://fixture.test/path?token=private-token",Body="{\"password\":\"private-password\"}",Headers="Authorization: Bearer private-token" }; var secrets=new NetworkSecrets(Password:"private-password",Token:"private-token");
        await store.AddHistoryAsync(request); await store.SaveCollectionAsync("Request",request,secrets);
        Assert.DoesNotContain("private",(await repo.SettingAsync("networkTools.history.v1"))!); Assert.DoesNotContain("private",(await repo.SettingAsync("networkTools.collections.v1"))!);
        var collection=Assert.Single(await store.CollectionsAsync()); var restored=await store.LoadCollectionAsync(collection.Id); Assert.Equal(request,restored.Request); Assert.Equal(secrets,restored.Secrets);
        using var export=new MemoryStream(); await repo.ExportAsync(export); Assert.DoesNotContain("private",System.Text.Encoding.UTF8.GetString(export.ToArray()));
        await store.DeleteCollectionAsync(collection.Id); Assert.Empty(credentials.Values); await store.DeleteHistoryAsync((await store.HistoryAsync()).Single().Id); Assert.Empty(await store.HistoryAsync());
    }
    [Fact] public async Task Concurrent_history_writes_are_bounded_and_do_not_lose_latest_entries()
    { var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var store=new NetworkToolsStore(repo,new MemoryCredentials()); await Task.WhenAll(Enumerable.Range(0,110).Select(i=>store.AddHistoryAsync(new() { Tool=NetworkToolKind.Ping,Target="host-"+i+".test" }))); Assert.Equal(100,(await store.HistoryAsync()).Count); await store.ClearHistoryAsync(); Assert.Empty(await store.HistoryAsync()); }
    [Fact] public async Task Unavailable_secure_store_does_not_fall_back_to_plaintext()
    { var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var store=new NetworkToolsStore(repo,new FailingCredentials()); await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveCollectionAsync("Request",new() { Target="127.0.0.1" },new(Password:"private-password"))); Assert.Empty(await store.CollectionsAsync()); }
    [Fact] public void Ipv6_reverse_dns_nibbles_are_correct()
    { Assert.Equal("1.0.0.127.in-addr.arpa",NetworkToolsService.ReverseName(System.Net.IPAddress.Loopback)); Assert.Equal("1."+string.Join(".",Enumerable.Repeat("0",31))+".ip6.arpa",NetworkToolsService.ReverseName(System.Net.IPAddress.IPv6Loopback)); }
    private sealed class MemoryCredentials : ICredentialStore
    { internal readonly Dictionary<Guid,string> Values=[]; public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Values.GetValueOrDefault(id)); public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Values[id]=secret; return Task.CompletedTask; } public Task DeleteAsync(Guid id,CancellationToken ct=default) { Values.Remove(id); return Task.CompletedTask; } }
    private sealed class FailingCredentials : ICredentialStore
    { public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>throw new InvalidOperationException("Locked store"); public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new InvalidOperationException("Locked store"); public Task DeleteAsync(Guid id,CancellationToken ct=default)=>Task.CompletedTask; }
    public void Dispose()
    { DatabaseTestCleanup.Clear(root); var full=Path.GetFullPath(root); if(!full.StartsWith(Path.Combine(Path.GetTempPath(),"serverdesk-network-"),StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe test cleanup"); if(Directory.Exists(full))Directory.Delete(full,true); }
}
