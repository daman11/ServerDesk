using Avalonia;

namespace ServerDesk.App;

internal static class DesignTokens
{
    internal const double Tight=2,Small=4,IconGap=6,Medium=8,Section=12,Large=16,Page=20;
    internal const double Body=13,Caption=12,Micro=11,Heading=20,Title=24;
    internal const double ControlHeight=30,TabHeight=34,RowHeight=26,HeaderHeight=36,StatusHeight=26;
    internal const double ActivityWidth=48,ActivityButton=42,IconSize=18,Radius=6;
    internal static readonly Thickness Border=new(1),ButtonPadding=new(10,Small),InputPadding=new(Medium,5),PagePadding=new(Page);
    internal static readonly CornerRadius Corner=new(Radius);
}
