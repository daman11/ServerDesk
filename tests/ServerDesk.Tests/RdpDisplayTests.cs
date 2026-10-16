using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class RdpDisplayTests
{
    [Theory][InlineData(1,100,800,600)][InlineData(1.25,125,1000,750)][InlineData(1.5,150,1200,900)][InlineData(1.75,175,1400,1050)][InlineData(2,200,1600,1200)]
    public void PhysicalViewportAndRemoteDpiFollowMonitor(double scale,int dpi,int width,int height)
        =>Assert.Equal(new RdpDisplaySettings(width,height,dpi,100),RdpDisplaySettings.FromViewport(800,600,scale));
    [Fact] public void SamePixelsWithDifferentDpiStillRequireUpdate()
        =>Assert.NotEqual(RdpDisplaySettings.FromViewport(1200,900,1),RdpDisplaySettings.FromViewport(800,600,1.5));
    [Fact] public void SplitViewportHasEvenWidthAndProtocolBounds()
    { var small=RdpDisplaySettings.FromViewport(311,467,1.25); Assert.Equal(390,small.Width); Assert.Equal(0,small.Width%2); Assert.Equal(200,RdpDisplaySettings.FromViewport(1,1,1).Width); Assert.Equal(7680,RdpDisplaySettings.FromViewport(10000,5000,2).Width); }
    [Theory][InlineData(0,600,1)][InlineData(800,-1,1)][InlineData(800,600,0)][InlineData(800,600,double.NaN)][InlineData(800,600,double.PositiveInfinity)]
    public void InvalidViewportDoesNotOverflow(double width,double height,double scale)
        =>Assert.Throws<ArgumentException>(()=>RdpDisplaySettings.FromViewport(width,height,scale));
}
