using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class CoreTests
{
    [Theory]
    [InlineData("ssh://admin@192.168.1.10:2222",Protocol.SSH,2222,"admin")]
    [InlineData("ssh://admin@[::1]",Protocol.SSH,22,"admin")]
    [InlineData("rdp://server",Protocol.RDP,3389,"")]
    public void QuickConnectRecognizesProtocol(string text,Protocol protocol,int port,string user)
    { var p=QuickConnect.Parse(text); Assert.Equal(protocol,p.Protocol); Assert.Equal(port,p.Port); Assert.Equal(user,p.Username); }
    [Theory]
    [InlineData("ssh://admin:password@host")]
    [InlineData("https://host")]
    [InlineData("ssh://host")]
    [InlineData("ssh://admin@host/?password=x")]
    [InlineData("ssh://admin@host/command")]
    public void QuickConnectRejectsUnsafeInput(string input) => Assert.Throws<ArgumentException>(()=>QuickConnect.Parse(input));
    [Theory]
    [InlineData("/","plik ą.txt","/plik ą.txt")]
    [InlineData("/home/a/","data","/home/a/data")]
    public void RemotePathsUsePosixSeparators(string path,string name,string expected)=>Assert.Equal(expected,RemotePath.Join(path,name));
    [Theory][InlineData("../etc")][InlineData("..")] [InlineData(".")] [InlineData("a/b")]
    public void RemotePathsRejectTraversal(string name)=>Assert.Throws<ArgumentException>(()=>RemotePath.Join("/home",name));
    [Fact] public void CpuRequiresTwoSamplesAndIgnoresGuestCounters()
    {
        var a=CpuCounters.Parse("cpu 10 0 10 80 0 0 0 0 10 0"); var b=CpuCounters.Parse("cpu 20 0 20 160 0 0 0 0 20 0");
        Assert.Equal(100UL,a.Total); Assert.Null(a.Usage(null)); Assert.Equal(20,b.Usage(a)!.Value,5); Assert.Null(a.Usage(b));
    }
    [Fact] public void MemoryUsesAvailableRatherThanFree()
    {
        var m=MemoryInfo.Parse("MemTotal: 1000 kB\nMemAvailable: 600 kB\nMemFree: 100 kB\nSwapTotal: 200 kB\nSwapFree: 150 kB\n");
        Assert.Equal(1024000,m.Total); Assert.Equal(40,m.Percent); Assert.Equal(51200,m.SwapTotal-m.SwapFree);
    }
    [Fact] public void ProfilesRejectControlCharactersAndFolderTraversal()
    {
        var profile=new ServerProfile { Name="test",Host="localhost",Username="admin" };
        Assert.Throws<ArgumentException>(()=>(profile with { Username="a\rb" }).Validate());
        Assert.Throws<ArgumentException>(()=>(profile with { Folder="../prod" }).Validate());
        Assert.Throws<ArgumentException>(()=>(profile with { Host="-option" }).Validate());
    }
}
