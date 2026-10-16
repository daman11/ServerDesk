using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class MonitoringCompatibilityTests
{
    private const string Links="""
        1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 state UNKNOWN mode DEFAULT\       link/loopback 00:00:00:00:00:00 brd 00:00:00:00:00:00
        2: eth0@if7: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 state UP mode DEFAULT\       link/ether aa:bb:cc:dd:ee:ff brd ff:ff:ff:ff:ff:ff
        3: eth1: <BROADCAST,MULTICAST> mtu 1500 state DOWN mode DEFAULT\       link/ether 02:00:00:00:00:01 brd ff:ff:ff:ff:ff:ff
        """;
    private const string Addresses="""
        1: lo    inet 127.0.0.1/8 scope host lo\       valid_lft forever preferred_lft forever
        2: eth0@if7    inet 192.0.2.5/24 brd 192.0.2.255 scope global eth0\       valid_lft forever preferred_lft forever
        2: eth0@if7    inet6 2001:db8::5/64 scope global\       valid_lft forever preferred_lft forever
        2: eth0@if7    inet6 fe80::5/64 scope link\       valid_lft forever preferred_lft forever
        """;

    [Fact] public void LegacyIpFallbackCombinesAddressesLinkStateMacAndBothGatewayFamilies()
    {
        var details=NetworkDetails.Parse("ip: unknown option -json","","nameserver 192.0.2.53",addressText:Addresses,linkText:Links,
            routeText:"default via 192.0.2.1 dev eth0 proto dhcp metric 100",route6Text:"default via fe80::1 dev eth0 proto ra metric 100");
        Assert.Equal(3,details.Interfaces.Count);
        Assert.Equal("00:00:00:00:00:00",Assert.Single(details.Interfaces,n=>n.Name=="lo").Mac);
        var nic=Assert.Single(details.Interfaces,n=>n.Name=="eth0");
        Assert.Equal("UP",nic.State); Assert.Equal("aa:bb:cc:dd:ee:ff",nic.Mac); Assert.Equal("192.0.2.5/24",nic.IPv4);
        Assert.Equal("2001:db8::5/64, fe80::5/64",nic.IPv6); Assert.Equal("DOWN",Assert.Single(details.Interfaces,n=>n.Name=="eth1").State);
        Assert.Equal("192.0.2.1 · eth0; fe80::1 · eth0",details.Gateways); Assert.Equal("192.0.2.53",details.Dns);
    }
    [Fact] public void JsonRoutesIncludeIPv6AndDeduplicateWithoutRepeatingInterfaces()
    {
        var details=NetworkDetails.Parse("""[{"ifname":"eth0","operstate":"UP","addr_info":[]}]""",
            """[{"gateway":"192.0.2.1","dev":"eth0"}]""","",route6Json:"""[{"gateway":"fe80::1","dev":"eth0"},{"gateway":"fe80::1","dev":"eth0"}]""",
            addressText:Addresses,linkText:Links);
        Assert.Single(details.Interfaces); Assert.Equal("192.0.2.1 · eth0; fe80::1 · eth0",details.Gateways);
    }
    [Fact] public void EachRouteFamilyCanFallBackIndependently()
    {
        var details=NetworkDetails.Parse("[]","""[{"gateway":"192.0.2.1","dev":"eth0"}]""","",route6Json:"unsupported",
            routeText:"default via 192.0.2.99 dev ignored",route6Text:"default via inet6 fe80::1 dev eth0");
        Assert.Equal("192.0.2.1 · eth0; fe80::1 · eth0",details.Gateways);
    }
    [Fact] public void MalformedTextDoesNotInventAddressesOrGateways()
    {
        const string bad=" \nerror: Permission denied\n2: eth0 inet nope/24\n2: eth0 inet 192.0.2.5/33\n2: eth0 inet6 2001:db8::5/-1\n2: eth0 inet 2001:db8::5/24\n0: invalid inet 192.0.2.5/24\n2: eth0 inet6 2001:db8::5/129";
        var details=NetworkDetails.Parse("","","nameserver 192.0.2.53",addressText:bad,linkText:bad,
            routeText:" \ndefault dev eth0\n192.0.2.0/24 via 192.0.2.1 dev eth0\ndefault via broken dev eth0");
        Assert.Empty(details.Interfaces); Assert.Equal("",details.Gateways); Assert.Equal("192.0.2.53",details.Dns);
    }
    [Fact] public void FallbackSectionsPreserveOtherMetricsAndResetInconsistentCpuBaseline()
    {
        const string sample="""
            __CPU__
            cpu 10 0 10 80
            __MEM__
            MemTotal: 1000 kB
            MemAvailable: 600 kB
            __IPJSON__
            __IPTEXT__
            """;
        var text=sample+"\n"+Addresses+"\n__LINKTEXT__\n"+Links+"\n__ROUTES__\n__ROUTETEXT__\ndefault via 192.0.2.1 dev eth0\n__ROUTES6__\n__ROUTETEXT6__\ndefault via fe80::1 dev eth0\n__CLOCK__\n12:34";
        var parser=new MonitoringSampleParser(); var initial=parser.Parse(text,1);
        Assert.DoesNotContain(MonitoringSection.Addresses,initial.UnavailableSections);
        Assert.Equal(40,initial.Memory.Percent); Assert.Equal("12:34",initial.RemoteTime); Assert.Contains("fe80::1",initial.NetworkDetails.Gateways);
        var inconsistent=parser.Parse(text.Replace("cpu 10 0 10 80","cpu 0 0 0 110"),1);
        Assert.Null(inconsistent.CpuPercent); Assert.True(inconsistent.CpuAvailable);
        Assert.Equal(20,parser.Parse(text.Replace("cpu 10 0 10 80","cpu 10 0 10 190"),1).CpuPercent!.Value,6);
    }
    [Theory]
    [InlineData(100UL,80UL,110UL,110UL)] // Idle delta exceeds total delta.
    [InlineData(100UL,80UL,90UL,70UL)] // Reboot/reset.
    [InlineData(100UL,80UL,120UL,79UL)] // iowait can decrease.
    [InlineData(100UL,101UL,120UL,110UL)] // Invalid source baseline.
    public void UnusableCpuIntervalsAreUnavailableInsteadOfFakeZero(ulong total,ulong idle,ulong nextTotal,ulong nextIdle)
        =>Assert.Null(new CpuCounters(nextTotal,nextIdle).Usage(new(total,idle)));
    [Fact] public void GenuineIdleAndBusyCpuIntervalsStillReportZeroAndHundred()
    {
        Assert.Equal(0,new CpuCounters(200,180).Usage(new(100,80)));
        Assert.Equal(100,new CpuCounters(200,80).Usage(new(100,80)));
    }
}
