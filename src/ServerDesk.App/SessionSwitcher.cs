using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ServerDesk.Core;

namespace ServerDesk.App;
internal sealed record SessionChoice(WorkspacePane Pane,SessionSearchEntry Entry);
internal sealed class SessionChoiceSurface : Border
{
    public static readonly StyledProperty<bool> SelectedProperty=AvaloniaProperty.Register<SessionChoiceSurface,bool>(nameof(Selected));
    public bool Selected { get=>GetValue(SelectedProperty); set=>SetValue(SelectedProperty,value); }
    internal SessionChoiceSurface()
    {
        Classes.Add("session-choice"); Padding=new Thickness(6); CornerRadius=new CornerRadius(6); Update();
        Bind(SelectedProperty,new Binding(nameof(ListBoxItem.IsSelected)) { RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType=typeof(ListBoxItem) } });
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); if(change.Property==SelectedProperty)Update(); }
    private void Update()=>DesignSystem.Resource(this,BackgroundProperty,Selected?"SurfaceSelected":"BackgroundSecondary");
}
internal static partial class Dialogs
{
    internal static string SessionStateName(SessionVisualState state)=>state switch { SessionVisualState.Connected=>L.T("Połączono"),SessionVisualState.Connecting=>L.T("Łączenie"),SessionVisualState.Closed=>L.T("Zakończono"),SessionVisualState.Failed=>L.T("Błąd"),SessionVisualState.ExternalClient=>L.T("Klient RDP uruchomiony"),_=>L.T("Oczekuje na Połącz") };
    internal static async Task<Guid?> Sessions(Window owner,Func<IReadOnlyList<SessionChoice>> getChoices,Guid? active)
    {
        var search=new TextBox { Name="SessionSwitcherSearch",PlaceholderText=L.T("Nazwa, adres, login, panel lub stan…") }; AutomationProperties.SetName(search,L.T("Szukaj otwartych sesji"));
        var list=new ListBox { Name="SessionSwitcherResults" }; AutomationProperties.SetName(list,L.T("Otwarte sesje"));
        list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        list.Styles.Add(new Style(s=>s.OfType<ListBoxItem>()) { Setters={new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch)} });
        list.ItemTemplate=new FuncDataTemplate<SessionChoice>((choice,_)=>
        {
            if(choice==null)return new TextBlock();
            var row=new Grid { ColumnDefinitions=new ColumnDefinitions("24,*"),ColumnSpacing=DesignTokens.Medium,Margin=new Thickness(4) };
            row.Children.Add(choice.Pane is SessionPane profilePane ? new ProfileVisual(profilePane.PresentationProfile,20) : DeskIcons.Create("network",20));
            var name=DesignSystem.Label(choice.Entry.Name); name.TextTrimming=TextTrimming.CharacterEllipsis; name.TextWrapping=TextWrapping.NoWrap; name.FontWeight=FontWeight.SemiBold;
            var detail=DesignSystem.Label($"{choice.Entry.Protocol} · {(choice.Entry.User.Length>0?choice.Entry.User+"@":"")}{choice.Entry.Host}",true,12); detail.TextTrimming=TextTrimming.CharacterEllipsis; detail.TextWrapping=TextWrapping.NoWrap;
            var text=new StackPanel { Spacing=DesignTokens.Small,Children={name,detail} }; ToolTip.SetTip(text,$"{choice.Entry.Name}\n{choice.Entry.Protocol} · {choice.Entry.User}@{choice.Entry.Host} · {choice.Entry.Location}"); Grid.SetColumn(text,1); row.Children.Add(text);
            var state=new StackPanel { Orientation=Orientation.Horizontal,Spacing=DesignTokens.Small,VerticalAlignment=VerticalAlignment.Center }; var icon=new SessionStatusIcon(); if(choice.Pane is SessionPane)icon.Bind(SessionStatusIcon.StateProperty,new Binding(nameof(SessionPane.VisualState)) { Source=choice.Pane }); state.Children.Add(icon);
            var label=DesignSystem.Label(choice.Entry.State,true,11); if(choice.Pane is SessionPane)label.Bind(TextBlock.TextProperty,new Binding(nameof(SessionPane.VisualState)) { Source=choice.Pane,Converter=new FuncValueConverter<SessionVisualState,string>(SessionStateName) }); state.Children.Add(label);
            var location=DesignSystem.Badge(choice.Entry.Location);
            var meta=new WrapPanel { ItemSpacing=8,LineSpacing=4,Children={location,state} }; if(choice.Pane.WorkspaceId==active)meta.Children.Add(DesignSystem.Badge(L.T("Aktywna"))); text.Children.Add(meta);
            return new SessionChoiceSurface { Child=row };
        });
        var empty=DesignSystem.Hint(""); empty.Name="SessionSwitcherEmpty"; empty.Margin=new Thickness(14); empty.VerticalAlignment=VerticalAlignment.Center;
        var count=DesignSystem.Label("",true,12); count.Name="SessionSwitcherCount"; var choose=Button("Przejdź",()=> { }); choose.Name="ChooseSession"; choose.Classes.Add("primary");
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium,Children={choose} };
        var root=new Grid { Margin=new Thickness(20),RowDefinitions=new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto"),RowSpacing=12 };
        root.Children.Add(DesignSystem.PageHeading("layers",L.T("Otwarte sesje"),L.T("Znajdź kartę po nazwie, adresie lub panelu i wróć do pracy.")));
        Grid.SetRow(search,1); root.Children.Add(search); Grid.SetRow(count,2); root.Children.Add(count);
        var resultHost=new Grid { Children={list,empty} }; Grid.SetRow(resultHost,3); root.Children.Add(resultHost);
        var hint=DesignSystem.Hint(L.T("Enter — przejdź · ↑ / ↓ — wybierz · Esc — zamknij")); Grid.SetRow(hint,4); root.Children.Add(hint); Grid.SetRow(actions,5); root.Children.Add(actions);
        var dialog=new Window { Title=L.T("Otwarte sesje"),Name="SessionSwitcherDialog",Width=720,Height=590,MinWidth=420,MinHeight=380,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=root }; FitToOwnerScreen(dialog,owner);
        var clear=DeskIcons.Button("close","Wyczyść wyszukiwanie",()=> { search.Text=""; search.Focus(); },26); search.InnerRightContent=clear;
        string? appliedSearch=null;
        void Filter()
        {
            var selected=(list.SelectedItem as SessionChoice)?.Pane.WorkspaceId??active; var choices=getChoices(); var ids=SessionSearch.Filter(choices.Select(c=>c.Entry),search.Text).Select(e=>e.Key).ToHashSet(); var visible=choices.Where(c=>ids.Contains(c.Entry.Key)).ToArray();
            list.ItemsSource=visible; list.SelectedItem=visible.FirstOrDefault(c=>c.Pane.WorkspaceId==selected)??visible.FirstOrDefault(); empty.IsVisible=visible.Length==0; choose.IsEnabled=visible.Length>0; count.Text=L.F($"Widoczne: {visible.Length} / {choices.Count}");
            empty.Text=choices.Count==0?L.T("Nie masz otwartych sesji. Otwórz zapisane połączenie lub skorzystaj z szybkiego połączenia."):L.T("Brak pasujących sesji. Zmień lub wyczyść wyszukiwanie."); clear.IsVisible=!string.IsNullOrEmpty(search.Text); appliedSearch=search.Text??"";
            if(list.SelectedIndex>=0)list.ScrollIntoView(list.SelectedIndex);
        }
        search.TextChanged+=(_,_)=> { if(appliedSearch!=(search.Text??""))Filter(); }; Filter(); list.SelectionChanged+=(_,_)=>choose.IsEnabled=list.SelectedItem!=null;
        void Pick() { Filter(); if(list.SelectedItem is SessionChoice choice)dialog.Close((Guid?)choice.Pane.WorkspaceId); }
        choose.Click+=(_,_)=>Pick(); list.DoubleTapped+=(_,_)=>Pick(); actions.Children.Add(Button("Anuluj",()=>dialog.Close()));
        dialog.KeyDown+=(_,e)=>
        {
            if(e.Key==Key.Escape) { dialog.Close(); e.Handled=true; }
            else if(e.Key==Key.Enter) { Pick(); e.Handled=true; }
            else if(e.Key==Key.F && e.KeyModifiers==KeyModifiers.Control) { search.Focus(); search.SelectAll(); e.Handled=true; }
            else if(e.Key is Key.Down or Key.Up) { if(appliedSearch!=(search.Text??""))Filter(); if(list.ItemCount>0) { list.SelectedIndex=Math.Clamp(list.SelectedIndex+(e.Key==Key.Down?1:-1),0,list.ItemCount-1); list.ScrollIntoView(list.SelectedIndex); } e.Handled=true; }
        };
        dialog.Opened+=(_,_)=>search.Focus(); return await dialog.ShowDialog<Guid?>(owner);
    }
}
public sealed partial class MainWindow
{
    internal async Task SwitchSessionAsync(Window? caller=null)
    {
        IReadOnlyList<SessionChoice> Choices()=>dockState.All.Select(id=>Panes.Single(p=>p.WorkspaceId==id)).Select(p=>new SessionChoice(p,new SessionSearchEntry(p.WorkspaceId,p.DisplayName,p is SessionPane session?session.Profile.Protocol==Protocol.Serial?session.Profile.Serial.PortName:session.Profile.Host:"",p is SessionPane user?user.Profile.Username:"",p is SessionPane protocol?protocol.Profile.Protocol.ToString():L.T("Narzędzia sieciowe"),DockName(dockState.Location(p.WorkspaceId)),p is SessionPane state?Dialogs.SessionStateName(state.VisualState):L.T("Narzędzie sieciowe")))).ToArray();
        var chosen=await Dialogs.Sessions(caller??this,Choices,dockState.Focused); if(chosen==null || Panes.FirstOrDefault(p=>p.WorkspaceId==chosen) is not {} pane)return;
        FocusSession(pane); if(floatingDocks.TryGetValue(pane.WorkspaceId,out var floating))floating.Window.Activate(); else Activate(); pane.FocusContent();
    }
}
