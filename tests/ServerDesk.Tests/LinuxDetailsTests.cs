using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class LinuxDetailsTests
{
    [Fact] public void SystemDetailsReadDistributionUptimeUserAndArchitecture()
    {
        var info=SystemInfo.Parse("host\nNAME=Ubuntu\nPRETTY_NAME=\"Ubuntu 24.04 LTS\"\nLinux 6.6.0 x86_64 GNU/Linux\nadmin\n86461.5 2000.0\n4\nCPU: Test CPU\n");
        Assert.Equal("host",info.Hostname); Assert.Equal("Ubuntu 24.04 LTS",info.Distribution); Assert.Equal("x86_64",info.Architecture); Assert.Equal("admin",info.User); Assert.Equal(TimeSpan.FromSeconds(86461.5),info.Uptime); Assert.Equal("Test CPU",info.CpuModel);
    }
    [Fact] public void EmptySystemInformationRemainsUnavailable()
    { var info=SystemInfo.Parse(""); Assert.Equal("",info.Hostname); Assert.Null(info.Uptime); }
    [Fact] public void DiskParserPreservesMountpointSpacesAndConvertsUnits()
    {
        var disks=DiskInfo.Parse("Filesystem Type 1024-blocks Used Available Capacity Mounted on\n/dev/sda1 ext4 1000 400 500 45% /mnt/team files\n");
        var disk=Assert.Single(disks); Assert.Equal("/mnt/team files",disk.MountPoint); Assert.Equal(1024000,disk.Total); Assert.Equal(409600,disk.Used); Assert.Equal(512000,disk.Available); Assert.Equal(45,disk.Percent);
    }
    [Theory][InlineData("")][InlineData("df: Permission denied")][InlineData("device fs - - - - /mount")][InlineData("device fs 9223372036854775807 0 0 0% /mount")]
    public void UnavailableOrOverflowDiskDataIsSkipped(string text)=>Assert.Empty(DiskInfo.Parse(text));
    [Fact] public void TopologyDistinguishesRepeatedCoreIdsAcrossSockets()
    {
        var topology=CpuTopology.Parse("# Socket,Core,CPU\n0,0,0\n0,0,1\n1,0,2\n1,0,3\n"); Assert.Equal(new CpuTopology(2,2,4),topology);
    }
    [Fact] public void MissingCpuTopologyIsUnavailable()=>Assert.Null(CpuTopology.Parse("# no data\n-, -, -\n"));
    [Fact] public void NetworkReadsIPv4IPv6MacGatewayAndDns()
    {
        var info=NetworkDetails.Parse("""[{"ifname":"eth0","operstate":"UP","address":"aa:bb:cc:dd:ee:ff","addr_info":[{"family":"inet","local":"192.0.2.5","prefixlen":24},{"family":"inet6","local":"2001:db8::5","prefixlen":64}]}]""","""[{"gateway":"192.0.2.1","dev":"eth0"}]""","# nameserver ignored\nnameserver 1.1.1.1 # comment\nnameserver 2001:db8::53\nnameserver 1.1.1.1\n");
        var network=Assert.Single(info.Interfaces); Assert.Equal("192.0.2.5/24",network.IPv4); Assert.Equal("2001:db8::5/64",network.IPv6); Assert.Equal("aa:bb:cc:dd:ee:ff",network.Mac); Assert.Equal("UP",network.State); Assert.Equal("192.0.2.1 · eth0",info.Gateways); Assert.Equal("1.1.1.1, 2001:db8::53",info.Dns);
    }
    [Theory][InlineData("")][InlineData("ip: command not found")][InlineData("{}")] [InlineData("[null,{},7]")]
    public void MissingNetworkCommandsDoNotBreakMonitoring(string text)
    { var info=NetworkDetails.Parse(text,text,""); Assert.Empty(info.Interfaces); Assert.Equal("",info.Gateways); }
}
