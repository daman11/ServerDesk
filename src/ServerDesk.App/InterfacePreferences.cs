using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

internal static partial class DesignSystem
{
    internal static ApplicationPreferences Preferences { get; private set; }=new();
    internal static void ApplyPreferences(ApplicationPreferences value)
    {
        value.Validate(); Preferences=value;
        var resources=Application.Current!.Resources; var font=value.InterfaceFontSize;
        var fallback=OperatingSystem.IsWindows()?"Segoe UI,Inter":"Inter,Noto Sans,DejaVu Sans";
        resources["InterfaceFont"]=new FontFamily(value.InterfaceFontFamily=="Domyślna"?fallback:value.InterfaceFontFamily+","+fallback);
        resources["RadiusSmall"]=new CornerRadius(value.ControlCornerRadius);
        resources["ConnectionRowPadding"]=new Thickness(4,value.ConnectionRowPadding);
        resources["MenuItemPadding"]=new Thickness(10,value.MenuItemPadding);
        resources["ShowTabIcons"]=value.ShowTabIcons;
        resources["ShowTabCloseButtons"]=value.ShowTabCloseButtons;
        resources["FontBody"]=(double)font; resources["FontCaption"]=(double)Math.Max(11,font-1); resources["FontMicro"]=(double)Math.Max(10,font-2);
        var height=value.Density switch { InterfaceDensity.Compact=>26d,InterfaceDensity.Comfortable=>34d,_=>30d };
        var spacing=value.Density switch { InterfaceDensity.Compact=>5d,InterfaceDensity.Comfortable=>12d,_=>8d };
        resources["ControlHeight"]=height; resources["SpacingMedium"]=spacing;
        resources["ButtonPadding"]=new Thickness(10,value.Density==InterfaceDensity.Compact?2:5);
        resources["InputPadding"]=new Thickness(8,value.Density==InterfaceDensity.Compact?3:6);
        resources["RowPadding"]=new Thickness(6,value.Density==InterfaceDensity.Compact?2:5);
        resources["FileRowHeight"]=Math.Max(26d,font+(value.Density==InterfaceDensity.Compact?10:14));
        resources["TabHeight"]=34d;
        resources["SessionTabMaxWidth"]=(double)value.TabLabelWidth;
        Apply(CurrentId);
    }
    private static void SetAccent(ResourceDictionary resources,string fallback,bool light)
    {
        var color=Color.Parse(Preferences.AccentColor.Length>0?Preferences.AccentColor:fallback);
        var brush=new SolidColorBrush(color);
        resources["AccentPrimary"]=brush; resources["AccentHover"]=brush; resources["SystemAccentColor"]=color;
        resources["TabItemHeaderSelectedPipeFill"]=brush;
        static double Linear(byte b) { var s=b/255d; return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4); }
        var luminance=.2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);
        resources["AccentForeground"]=new SolidColorBrush(luminance>.179?Colors.Black:Colors.White);
        foreach(var state in new[]{"Checked","Indeterminate"})foreach(var suffix in new[]{"","PointerOver","Pressed"})
        {
            resources["CheckBoxCheckBackgroundFill"+state+suffix]=brush;
            resources["CheckBoxCheckBackgroundStroke"+state+suffix]=brush;
            resources["CheckBoxCheckGlyphForeground"+state+suffix]=resources["AccentForeground"];
        }
    }
}
