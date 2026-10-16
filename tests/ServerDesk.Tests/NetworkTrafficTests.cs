using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class NetworkTrafficTests
{
    [Fact] public void Parses_proc_net_dev_and_ignores_loopback_and_invalid_rows()
    {
        var read=NetworkTraffic.Parse("Inter-| Receive | Transmit\n lo: 99 0 0 0 0 0 0 0 99 0 0 0 0 0 0 0\n eth0: 100 0 0 0 0 0 0 0 40 0 0 0 0 0 0 0\n bad: broken\n eth1: -1 0 0 0 0 0 0 0 40 0 0 0 0 0 0 0");
        Assert.Equal(new NetworkCounter(100,40),Assert.Single(read).Value);
    }
    [Fact] public void Rates_use_elapsed_time_and_matching_interfaces()
    {
        var old=new Dictionary<string,NetworkCounter> { ["eth0"]=new(100,20),["removed"]=new(300,200) };
        var current=new Dictionary<string,NetworkCounter> { ["eth0"]=new(300,120),["new"]=new(90000,100000) };
        Assert.Equal(new NetworkRate(100,50),NetworkTraffic.Rate(current,old,2));
        Assert.Null(NetworkTraffic.Rate(current,null,2)); Assert.Null(NetworkTraffic.Rate(current,old,0));
        Assert.Null(NetworkTraffic.Rate(current,old,double.NaN));
    }
    [Fact] public void Counter_reset_does_not_create_underflow_spike()
    {
        var old=new Dictionary<string,NetworkCounter> { ["eth0"]=new(100,20) };
        var current=new Dictionary<string,NetworkCounter> { ["eth0"]=new(1,1) };
        Assert.Null(NetworkTraffic.Rate(current,old,1));
    }
}
