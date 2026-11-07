using System.Text.Json;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class MonitoringMalformedDataTests
{
    [Theory] [InlineData("NaN")] [InlineData("Infinity")] [InlineData("-Infinity")] [InlineData("1e309")]
    public void Nonfinite_disk_percentages_do_not_reach_progress_controls(string percent)
    {
        var rows=DiskInfo.Parse($"bad ext4 1000 400 500 {percent}% /bad\ngood ext4 1000 400 500 45% /mnt/team files");
        var valid=Assert.Single(rows); Assert.Equal("/mnt/team files",valid.MountPoint); Assert.Equal(45,valid.Percent);
        Assert.True(double.IsFinite(valid.Percent));
    }

    [Theory]
    [InlineData("inet","not-an-ip",24)] [InlineData("inet","2001:db8::5",24)]
    [InlineData("inet6","192.0.2.5",64)] [InlineData("inet6","broken",64)]
    [InlineData("inet","192.0.2.5",-1)] [InlineData("inet","192.0.2.5",33)]
    [InlineData("inet6","2001:db8::5",-1)] [InlineData("inet6","2001:db8::5",129)]
    public void Json_addresses_require_the_declared_family_and_a_valid_prefix(string family,string local,int prefix)
    {
        var json=JsonSerializer.Serialize(new[] { new { ifname="eth0",addr_info=new object[] { new { family,local,prefixlen=prefix },new { family="inet",local="192.0.2.10",prefixlen=24 },new { family="inet6",local="2001:db8::10",prefixlen=64 } } } });
        var nic=Assert.Single(NetworkDetails.Parse(json,"[]","").Interfaces);
        Assert.Equal("192.0.2.10/24",nic.IPv4); Assert.Equal("2001:db8::10/64",nic.IPv6);
    }

    [Theory] [InlineData("\"24\"")] [InlineData("null")] [InlineData("24.5")] [InlineData("2147483648")]
    public void An_invalid_explicit_prefix_does_not_turn_into_an_apparently_valid_bare_address(string prefix)
    {
        var json="[{\"ifname\":\"eth0\",\"addr_info\":[{\"family\":\"inet\",\"local\":\"192.0.2.5\",\"prefixlen\":"+prefix+"}]}]";
        Assert.Empty(Assert.Single(NetworkDetails.Parse(json,"[]","").Interfaces).IPv4);
    }

    [Fact] public void A_missing_prefix_keeps_valid_bare_addresses_without_inventing_a_network_mask()
    {
        const string json="""[{"ifname":"eth0","addr_info":[{"family":"inet","local":"192.0.2.5"},{"family":"inet6","local":"2001:db8::5"}]}]""";
        var nic=Assert.Single(NetworkDetails.Parse(json,"[]","").Interfaces);
        Assert.Equal("192.0.2.5",nic.IPv4); Assert.Equal("2001:db8::5",nic.IPv6);
    }

    [Theory] [InlineData("broken")] [InlineData("192.0.2.1/24")] [InlineData("https://192.0.2.1")]
    public void Invalid_json_gateways_do_not_suppress_the_valid_text_fallback(string gateway)
    {
        var routes=JsonSerializer.Serialize(new[] { new { gateway,dev="eth0" } });
        var info=NetworkDetails.Parse("[]",routes,"",routeText:"default via 192.0.2.1 dev eth0",route6Json:routes,route6Text:"default via fe80::1 dev eth0");
        Assert.Equal("192.0.2.1 · eth0; fe80::1 · eth0",info.Gateways);
    }

    [Fact] public void A_valid_gateway_without_a_device_has_no_empty_separator()
    {
        var info=NetworkDetails.Parse("[]","""[{"gateway":"192.0.2.1"},{"gateway":"broken"}]""","",route6Json:"""[{"gateway":"fe80::1"}]""");
        Assert.Equal("192.0.2.1; fe80::1",info.Gateways);
    }

    [Fact] public void Malformed_disk_and_address_fields_preserve_other_sections_and_can_recover()
    {
        const string text="""
            __CPU__
            cpu 10 0 10 80
            __MEM__
            MemTotal: 1000 kB
            MemAvailable: 600 kB
            __DISKS__
            disk ext4 1000 400 500 NaN% /
            __IPJSON__
            [{"ifname":"eth0","addr_info":[{"family":"inet","local":"broken","prefixlen":24}]}]
            __CLOCK__
            12:34
            """;
        var parser=new MonitoringSampleParser(); var first=parser.Parse(text,1);
        Assert.True(first.CpuAvailable); Assert.Equal(40,first.Memory.Percent); Assert.Equal("12:34",first.RemoteTime);
        Assert.Empty(first.DiskDetails); Assert.Contains(MonitoringSection.Disks,first.UnavailableSections); Assert.Empty(Assert.Single(first.NetworkDetails.Interfaces).IPv4);
        var recovered=parser.Parse(text.Replace("NaN%","45%").Replace("broken","192.0.2.5").Replace("cpu 10 0 10 80","cpu 20 0 20 160"),1);
        Assert.Single(recovered.DiskDetails); Assert.DoesNotContain(MonitoringSection.Disks,recovered.UnavailableSections);
        Assert.Equal("192.0.2.5/24",Assert.Single(recovered.NetworkDetails.Interfaces).IPv4); Assert.Equal(20,recovered.CpuPercent!.Value,6);
    }
}
