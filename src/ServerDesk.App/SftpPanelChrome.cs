using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private readonly Dictionary<bool,(Border Surface,TextBlock Active)> filePanelHeadings=[];
    private Control FilePanelHeading(bool remote)
    {
        var title=DesignSystem.Label(remote?L.T("Zdalny"):L.T("Lokalny komputer")); title.FontWeight=FontWeight.SemiBold; title.TextTrimming=TextTrimming.CharacterEllipsis;
        if(remote)title.Bind(TextBlock.TextProperty,new Binding(nameof(PresentationProfile)+".Name") { Source=this,StringFormat=L.T("Zdalny")+" · {0}" });
        var active=DesignSystem.Label(L.T("Aktywny panel"),true,DesignTokens.Micro); active.TextTrimming=TextTrimming.CharacterEllipsis;
        var content=new Grid { ColumnDefinitions=new("20,*,Auto"),ColumnSpacing=8,Children={DeskIcons.Create(remote?"server":"monitor",16,"ForegroundSecondary"),title,active} }; Grid.SetColumn(title,1); Grid.SetColumn(active,2);
        var button=new Button { Name=remote?"SftpRemotePanel":"SftpLocalPanel",Content=content,Classes={"chrome"},HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(8,5),MinHeight=0 };
        AutomationProperties.SetName(button,L.T(remote?"Aktywuj panel zdalny":"Aktywuj panel lokalny")); ToolTip.SetTip(button,remote?Profile.Protocol+" · "+Profile.Endpoint:L.T("Pliki na tym komputerze"));
        button.Click+=(_,_)=> { fileTarget.SelectedIndex=remote?1:0; (remote?remoteList:localList).Focus(); };
        var surface=new Border { Child=button,CornerRadius=new CornerRadius(5),BorderThickness=new Thickness(2,0,0,0) }; filePanelHeadings[remote]=(surface,active); UpdateFilePanelHeadings(); return surface;
    }
    private void UpdateFilePanelHeadings()
    {
        foreach(var (remote,heading) in filePanelHeadings)
        {
            var active=RemoteSelected==remote;
            heading.Active.IsVisible=active && files.Bounds.Width>=900;
            DesignSystem.Resource(heading.Surface,Border.BackgroundProperty,active?"SurfaceSelected":"BackgroundSecondary");
            DesignSystem.Resource(heading.Surface,Border.BorderBrushProperty,active?"AccentPrimary":"BorderDefault");
        }
    }
}
