using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class MonitoringSampleTests
{
    private const string Sample="""
        __SYSTEM__
        node__CPU__name
        NAME=Fixture
        Linux 1 x86_64
        fixture
        123.0 0
        1
        __CPU__
        cpu 10 0 10 80 0 0 0 0
        __MEM__
        MemTotal: 1000 kB
        MemAvailable: 600 kB
        SwapTotal: 200 kB
        SwapFree: 150 kB
        __DISKS__
        device ext4 1000 400 600 40% /
        __NET__
        __LOAD__
        0.10 0.20 0.30 1/100 1
        __TOPOLOGY__
        0,0,0
        __IPJSON__
        [{"ifname":"eth0","addr_info":[]}]
        __ROUTES__
        []
        __DNS__
        nameserver 192.0.2.53
        __TRAFFIC__
        eth0: 100 0 0 0 0 0 0 0 20 0 0 0 0 0 0 0
        __CLOCK__
        12:34
        """;
    [Fact] public void CompleteSampleAndCrlfAreParsedWithExactSectionBoundaries()
    {
        var parser=new MonitoringSampleParser(); var first=parser.Parse(Sample.Replace("\n","\r\n"),0);
        Assert.Empty(first.UnavailableSections); Assert.Null(first.CpuPercent); Assert.Null(first.Traffic);
        Assert.Equal("node__CPU__name",first.SystemDetails.Hostname); Assert.Equal("12:34",first.RemoteTime);
        var next=parser.Parse(Sample.Replace("10 0 10 80","20 0 20 160").Replace("eth0: 100","eth0: 300"),2);
        Assert.Equal(20,next.CpuPercent!.Value,6); Assert.Equal(100,next.Traffic!.ReceivedBytesPerSecond);
    }
    [Theory][InlineData("")][InlineData("head: Permission denied")][InlineData("cpu broken 0 0 0")][InlineData("cpu 18446744073709551615 1 1 1")]
    public void MissingOrMalformedCpuDoesNotDiscardMemoryAndDisks(string cpu)
    {
        var result=new MonitoringSampleParser().Parse(Sample.Replace("cpu 10 0 10 80 0 0 0 0",cpu),1);
        Assert.False(result.CpuAvailable); Assert.Null(result.CpuPercent); Assert.Equal(1024000,result.Memory.Total); Assert.Single(result.DiskDetails);
        Assert.Equal([MonitoringSection.Cpu],result.UnavailableSections);
    }
    [Fact] public void MissingOptionalCommandsPreserveTheOtherSections()
    {
        var text=Sample.Replace("device ext4 1000 400 600 40% /","").Replace("0,0,0","").Replace("[{\"ifname\":\"eth0\",\"addr_info\":[]}]","");
        var result=new MonitoringSampleParser().Parse(text,1);
        Assert.True(result.CpuAvailable); Assert.Equal(40,result.Memory.Percent); Assert.Equal("192.0.2.53",result.NetworkDetails.Dns);
        Assert.Equal([MonitoringSection.Disks,MonitoringSection.Topology,MonitoringSection.Addresses],result.UnavailableSections);
    }
    [Fact] public void AGapResetsCpuAndTrafficBaselines()
    {
        var parser=new MonitoringSampleParser(); parser.Parse(Sample,1); var empty=parser.Parse("",1);
        Assert.Equal(7,empty.UnavailableSections.Count); Assert.Equal(0,empty.Memory.Total);
        var recovered=parser.Parse(Sample.Replace("10 0 10 80","20 0 20 160"),1);
        Assert.True(recovered.CpuAvailable); Assert.Null(recovered.CpuPercent); Assert.Null(recovered.Traffic);
        Assert.NotNull(parser.Parse(Sample.Replace("10 0 10 80","30 0 30 240"),1).CpuPercent);
    }
    [Theory][InlineData("MemTotal: 1000 kB\nMemAvailable: 2000 kB")][InlineData("MemTotal: 1000 kB")][InlineData("MemTotal: 9223372036854775807 kB\nMemFree: 1 kB")][InlineData("MemTotal: -1 kB\nMemAvailable: 0 kB")]
    public void UnavailableMemoryDoesNotLookLikeZeroUsage(string text)
    { var memory=MemoryInfo.Parse(text); Assert.Equal(0,memory.Total); Assert.False(memory.HasSwapData); }
    [Fact] public void UnknownAndMalformedMemoryFieldsAreIgnoredAndOldKernelFallbackIsMarked()
    {
        var memory=MemoryInfo.Parse("MemTotal: 1000 kB\nMemTotal: 1001 kB\nMemFree: 300 kB\nHugePages_Total: 2\nUnknown: bad\nSwapTotal: bad kB");
        Assert.Equal(1024000,memory.Total); Assert.Equal(70,memory.Percent); Assert.True(memory.AvailableEstimated); Assert.False(memory.HasSwapData);
    }
}
