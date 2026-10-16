using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private readonly Dictionary<Button,Protocol?> protocolButtons=[];
    private readonly TextBlock browserCount=DesignSystem.Label("",true,11);
    private readonly StackPanel browserEmpty=new() { Spacing=10,Margin=new Thickness(18),VerticalAlignment=VerticalAlignment.Center };
    private readonly TextBlock browserEmptyTitle=DesignSystem.Label("",false,16);
    private readonly TextBlock browserEmptyHint=DesignSystem.Label("",true,12);
    private Button clearBrowserFilters=null!,emptyBrowserAction=null!;
    private bool HasBrowserFilter=>!string.IsNullOrWhiteSpace(vm.Search) || vm.ProtocolFilter!=null;
    // Check the live filter too: Enter can arrive before the queued tree rebuild.
    private bool IsBrowserProfileVisible(ServerProfile profile)=>
        ProfileSearch.Matches(profile,vm.Search) && (vm.ProtocolFilter==null || profile.Protocol==vm.ProtocolFilter) &&
        (section=="connections" || section=="favorite" && profile.Favorite || section=="recent" && profile.LastConnected!=null);
    private void ClearBrowserFilters() { vm.Search=""; vm.ProtocolFilter=null; QueueTree(); }
    private void BuildBrowserFilters()
    {
        var body=new StackPanel { Margin=new Thickness(10,0,10,8),Spacing=6 };
        var choices=new Grid { ColumnDefinitions=new ColumnDefinitions("1.8*,*,*"),RowDefinitions=new RowDefinitions("Auto,Auto"),ColumnSpacing=4,RowSpacing=4 };
        var options=new (string Name,Protocol? Protocol)[]{(L.T("Wszystkie"),null),("SSH",Protocol.SSH),("RDP",Protocol.RDP),("VNC",Protocol.VNC),("Serial",Protocol.Serial)};
        for(var i=0;i<options.Length;i++)
        {
            var option=options[i]; var button=Dialogs.Button(option.Name,()=> { vm.ProtocolFilter=option.Protocol; QueueTree(); });
            button.Classes.Add("chrome"); button.Classes.Add("segment"); button.HorizontalAlignment=HorizontalAlignment.Stretch;
            button.Padding=new Thickness(6,3);
            AutomationProperties.SetName(button,L.T("Filtr protokołu: ")+option.Name); protocolButtons[button]=option.Protocol; Grid.SetColumn(button,i<3?i:i-3); Grid.SetRow(button,i<3?0:1); if(i==4)Grid.SetColumnSpan(button,2); choices.Children.Add(button);
        }
        var filterSurface=new Border { Child=choices,Padding=new Thickness(3),CornerRadius=new CornerRadius(6),BorderThickness=new Thickness(1) };
        DesignSystem.Resource(filterSurface,Border.BackgroundProperty,"BackgroundPrimary"); DesignSystem.Resource(filterSurface,Border.BorderBrushProperty,"BorderDefault"); body.Children.Add(filterSurface);
        var stats=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=8 }; browserCount.Name="ConnectionResultCount"; browserCount.TextTrimming=TextTrimming.CharacterEllipsis; stats.Children.Add(browserCount);
        clearBrowserFilters=DeskIcons.Button("close","Wyczyść filtry",ClearBrowserFilters,24); clearBrowserFilters.Classes.Add("chrome"); Grid.SetColumn(clearBrowserFilters,1); stats.Children.Add(clearBrowserFilters); body.Children.Add(stats);
        connectionTools.Children.Add(body);
        browserEmptyTitle.TextWrapping=browserEmptyHint.TextWrapping=TextWrapping.Wrap;
        browserEmpty.Children.Add(DeskIcons.Create("search",26,"ForegroundSecondary")); browserEmpty.Children.Add(browserEmptyTitle); browserEmpty.Children.Add(browserEmptyHint);
        emptyBrowserAction=Dialogs.Button("Wyczyść filtry",()=> { if(HasBrowserFilter)ClearBrowserFilters(); else _=Safe(()=>EditAsync(null)); }); emptyBrowserAction.HorizontalAlignment=HorizontalAlignment.Left; browserEmpty.Children.Add(emptyBrowserAction);
    }
    private void UpdateBrowserState(int count)
    {
        foreach(var (button,filter) in protocolButtons)button.Classes.Set("active",filter==vm.ProtocolFilter);
        browserCount.Text=L.F($"Widoczne: {count} / {vm.AllServers.Count}");
        clearBrowserFilters.IsVisible=HasBrowserFilter;
        browserEmpty.IsVisible=section!="transfers" && count==0;
        browserEmptyTitle.Text=HasBrowserFilter?L.T("Brak wyników"):section=="favorite"?L.T("Brak ulubionych"):section=="recent"?L.T("Brak ostatnich połączeń"):L.T("Dodaj pierwszy serwer");
        browserEmptyHint.Text=HasBrowserFilter?L.T("Zmień zapytanie lub wyczyść filtry."):section=="favorite"?L.T("Oznacz serwer gwiazdką w jego menu."):section=="recent"?L.T("Tutaj pojawią się potwierdzone połączenia SSH, RDP, VNC i Serial."):L.T("Zapisz połączenie SSH, RDP, VNC lub Serial.");
        emptyBrowserAction.Content=DeskIcons.Labelled(HasBrowserFilter?"close":"plus",HasBrowserFilter?L.T("Wyczyść filtry"):L.T("Nowe połączenie"));
        AutomationProperties.SetName(emptyBrowserAction,HasBrowserFilter?L.T("Wyczyść filtry pustej listy"):L.T("Dodaj pierwszy serwer"));
        emptyBrowserAction.IsVisible=HasBrowserFilter || section=="connections";
    }
}
