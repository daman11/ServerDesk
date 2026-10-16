using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;
public sealed partial class MainWindow
{
    private readonly StackPanel welcomeRecentRows=new() { Spacing=4 };
    private readonly TextBlock welcomeSummary=DesignSystem.Label("",true,DesignTokens.Caption);
    private Control? welcomeRecent;
    private ServerProfile[] welcomeRecentSnapshot=[];
    private Control BuildWelcome()
    {
        var body=new StackPanel { Name="WorkspaceWelcome",Spacing=20,MaxWidth=800,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(28) };
        var shortcuts=new List<Control>();
        var title=DesignSystem.Label(L.T("Gotowy do pracy"),false,DesignTokens.Title); title.FontWeight=FontWeight.SemiBold; title.TextWrapping=TextWrapping.Wrap;
        var description=DesignSystem.Label(L.T("Twoje serwery, terminal i pliki w jednym miejscu.\nWybierz zapisane połączenie lub rozpocznij nową sesję."),true); description.TextWrapping=TextWrapping.Wrap;
        var intro=new StackPanel { Spacing=8,Children={title,description} };
        var brand=new Border { Width=58,Height=58,CornerRadius=new CornerRadius(14),Child=new BrandMark { Width=38,Height=38 },VerticalAlignment=VerticalAlignment.Top };
        DesignSystem.Resource(brand,Border.BackgroundProperty,"SurfaceSelected");
        var hero=new Grid { ColumnDefinitions=new("Auto,*"),ColumnSpacing=18,Children={brand,intro} }; Grid.SetColumn(intro,1); body.Children.Add(hero);
        welcomeSummary.Name="WelcomeSummary"; welcomeSummary.TextWrapping=TextWrapping.Wrap; body.Children.Add(welcomeSummary);
        var actions=new Grid { Name="WelcomeActions",ColumnDefinitions=new("*,*"),RowDefinitions=new("Auto,Auto"),ColumnSpacing=12,RowSpacing=12 };
        Button Entry(string icon,string title,string subtitle,string? shortcut,Func<Task> action)
        {
            title=L.T(title); subtitle=L.T(subtitle);
            var row=new Grid { ColumnDefinitions=new("30,*,Auto"),ColumnSpacing=10 }; row.Children.Add(DeskIcons.Create(icon,22,"AccentPrimary"));
            var text=new StackPanel { Spacing=4 }; var heading=DesignSystem.Label(title); heading.FontWeight=FontWeight.SemiBold; heading.TextWrapping=TextWrapping.Wrap; text.Children.Add(heading);
            var hint=DesignSystem.Label(subtitle,true,DesignTokens.Caption); hint.TextWrapping=TextWrapping.Wrap; text.Children.Add(hint); Grid.SetColumn(text,1); row.Children.Add(text);
            var end=DeskIcons.Create("arrow-right",14,"ForegroundSecondary"); Grid.SetColumn(end,2); row.Children.Add(end);
            if(shortcut!=null) { var key=DesignSystem.Label(shortcut,true,DesignTokens.Micro); key.Margin=new Thickness(0,6,0,0); text.Children.Add(key); shortcuts.Add(key); }
            var button=Action(title,action); button.Content=row; button.IsDefault=false; button.VerticalAlignment=VerticalAlignment.Stretch; button.Classes.Add("welcome-action"); AutomationProperties.SetName(button,title+(shortcut==null?"":" · "+shortcut)); return button;
        }
        actions.Children.Add(Entry("plus","Nowe połączenie","Dodaj SSH, RDP, VNC lub Serial","Ctrl+Shift+N",()=>EditAsync(null)));
        actions.Children.Add(Entry("connect","Szybkie połączenie","Adres ssh://, rdp://, vnc:// lub serial://",null,QuickAsync));
        actions.Children.Add(Entry("network","Network Tools","Diagnostyka sieci bez zapisanego serwera",null,()=>SelectSectionAsync("network")));
        actions.Children.Add(Entry("square-terminal","Paleta poleceń","Wyszukaj akcję lub ustawienie","Ctrl+Shift+P",CommandsAsync)); body.Children.Add(actions);
        var recentHeading=DesignSystem.Label(L.T("Ostatnio używane połączenia")); recentHeading.FontWeight=FontWeight.SemiBold;
        welcomeRecent=new StackPanel { Name="WelcomeRecent",IsVisible=false,Spacing=10,Children={recentHeading,welcomeRecentRows} }; body.Children.Add(welcomeRecent);
        var footer=DesignSystem.Label(L.T("Panel połączeń  Ctrl+B     ·     Otwarte sesje  Ctrl+Shift+L"),true,DesignTokens.Micro); footer.TextWrapping=TextWrapping.Wrap; body.Children.Add(footer);
        var scroll=new ScrollViewer { Content=body,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var renderedColumns=0;
        scroll.SizeChanged+=(_,_)=>
        {
            body.Width=Math.Min(800,Math.Max(0,scroll.Bounds.Width-56)); var columns=body.Width>=500?2:1;
            if(columns!=renderedColumns)
            {
                renderedColumns=columns; actions.ColumnDefinitions=new(columns==2?"*,*":"*"); actions.RowDefinitions=new(columns==2?"Auto,Auto":"Auto,Auto,Auto,Auto");
                for(var i=0;i<actions.Children.Count;i++) { Grid.SetColumn(actions.Children[i],i%columns); Grid.SetRow(actions.Children[i],i/columns); }
            }
            foreach(var hint in shortcuts)hint.IsVisible=body.Width>=680;
            brand.IsVisible=body.Width>=400; hero.ColumnDefinitions[0].Width=brand.IsVisible?GridLength.Auto:new GridLength(0); hero.ColumnSpacing=brand.IsVisible?18:0;
        };
        UpdateWelcome(); return scroll;
    }
    private void UpdateWelcome()
    {
        if(welcomeRecent==null)return;
        welcomeSummary.Text=L.F($"Zapisane: {vm.AllServers.Count} · ulubione: {vm.AllServers.Count(p=>p.Favorite)} · protokoły: {vm.AllServers.Select(p=>p.Protocol).Distinct().Count()}");
        var recent=vm.AllServers.Where(p=>p.LastConnected!=null).OrderByDescending(p=>p.LastConnected).ThenBy(p=>p.Id).Take(3).ToArray();
        if(recent.SequenceEqual(welcomeRecentSnapshot))return; welcomeRecentSnapshot=recent; welcomeRecentRows.Children.Clear(); welcomeRecent.IsVisible=recent.Length>0;
        foreach(var profile in recent)
        {
            var name=DesignSystem.Label(profile.Name); name.TextTrimming=TextTrimming.CharacterEllipsis;
            var endpoint=DesignSystem.Label(profile.Protocol+" · "+SortAddress(profile),true,DesignTokens.Micro); endpoint.TextTrimming=TextTrimming.CharacterEllipsis;
            var text=new StackPanel { Spacing=2,Children={name,endpoint} };
            var content=new Grid { ColumnDefinitions=new("24,*,Auto"),ColumnSpacing=10,Children={new ProfileVisual(profile,20),text} }; Grid.SetColumn(text,1);
            var arrow=DeskIcons.Create("connect",16,"ForegroundSecondary"); Grid.SetColumn(arrow,2); content.Children.Add(arrow);
            var button=Action(profile.Name,()=>ConnectAsync(vm.AllServers.FirstOrDefault(p=>p.Id==profile.Id))); button.Name="WelcomeRecentConnection"; button.Content=content; button.IsDefault=false; button.IsCancel=false; button.Classes.Add("chrome"); button.HorizontalContentAlignment=HorizontalAlignment.Stretch; button.HorizontalAlignment=HorizontalAlignment.Stretch;
            ToolTip.SetTip(button,profile.Name+"\n"+profile.Protocol+" · "+SortAddress(profile)); AutomationProperties.SetName(button,L.F($"Połącz z {profile.Name}")); welcomeRecentRows.Children.Add(button);
        }
    }
}
