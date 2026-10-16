namespace ServerDesk.Core;

public sealed record RdpDisplaySettings(int Width,int Height,int DesktopScaleFactor,int DeviceScaleFactor)
{
    public static RdpDisplaySettings FromViewport(double width,double height,double scale)
    {
        if(!double.IsFinite(width)||!double.IsFinite(height)||!double.IsFinite(scale)||width<=0||height<=0||scale<=0||scale>5)throw new ArgumentException(L.T("Niepoprawne wymiary lub skala pulpitu RDP."));
        var pixels=(int)Math.Clamp(Math.Round(width*scale),200,7680);
        pixels=(pixels+1)&~1; // MS-RDPEDISP requires an even width.
        return new(pixels,(int)Math.Clamp(Math.Round(height*scale),200,4320),Math.Clamp((int)Math.Round(scale*100),100,500),100);
    }
}
