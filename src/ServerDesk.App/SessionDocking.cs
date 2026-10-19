using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ServerDesk.Core;

namespace ServerDesk.App;
public sealed partial class MainWindow
{
    private readonly WorkspaceState dockState=new();
    private readonly TabControl secondaryTabs=new(),tertiaryTabs=new(),quaternaryTabs=new();
    private readonly Dictionary<SessionDock,TabControl> dockTabs=[];
    private readonly Dictionary<SessionDock,Control> dockStrips=[];
    private readonly Dictionary<SessionDock,Border> dockWelcomes=[];
    private readonly Dictionary<SessionDock,Button> dockNumbers=[];
    private readonly Dictionary<Guid,FloatingDock> floatingDocks=[];
    private readonly GridSplitter dockSplitter=new() { Name="DockColumnSeparator",ResizeBehavior=GridResizeBehavior.PreviousAndNext };
    private readonly Border rowDockSplitter=new() { Name="DockRowSeparator",Focusable=true,Cursor=new Cursor(StandardCursorType.SizeNorthSouth) };
    private bool refreshingTabs;
    private SessionDock focusedDock=SessionDock.Primary;
    private (WorkspaceMode Mode,double Ratio,double Rows,int Height,int Separator)? renderedLayout;
    private sealed class FloatingDock(Window window,Grid host)
    {
        internal Window Window { get; }=window;
        internal Grid Host { get; }=host;
        internal bool Returning;
        internal WindowGeometry? NormalGeometry;
    }
    internal WorkspaceState Workspace=>dockState;
    internal IReadOnlyList<SessionPane> OpenSessions=>sessions.AsReadOnly();
    private SessionPane? ActiveSession=>sessions.FirstOrDefault(p=>p.WorkspaceId==dockState.Focused);
    private static int DockNumber(SessionDock dock)=>WorkspaceState.DockedGroups.ToList().IndexOf(dock)+1;
    private static string DockName(SessionDock dock)=>dock switch { SessionDock.Primary=>L.T("Panel 1"),SessionDock.Secondary=>L.T("Panel 2"),SessionDock.Tertiary=>L.T("Panel 3"),SessionDock.Quaternary=>L.T("Panel 4"),_=>L.T("Osobne okno") };
    private void BuildDockWorkspace()
    {
        dockTabs[SessionDock.Primary]=tabs; dockTabs[SessionDock.Secondary]=secondaryTabs; dockTabs[SessionDock.Tertiary]=tertiaryTabs; dockTabs[SessionDock.Quaternary]=quaternaryTabs;
        foreach(var dock in WorkspaceState.DockedGroups)
        {
            var strip=TabStrip(dockTabs[dock],dock); dockStrips[dock]=strip; sessionHost.Children.Add(strip);
            var message=DesignSystem.Label(L.T("Przeciągnij tutaj kartę lub wybierz ten panel i otwórz połączenie."),true); message.TextWrapping=TextWrapping.Wrap;
            var welcome=new Border { Name="EmptyDock"+DockNumber(dock),Child=dock==SessionDock.Primary?BuildWelcome():new StackPanel { VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(20),Spacing=DesignTokens.Section,Children={DeskIcons.Create("layers",28),DesignSystem.Label(DockName(dock),false,18),message} } };
            welcome.PointerPressed+=(_,_)=>FocusDock(dock); dockWelcomes[dock]=welcome; sessionHost.Children.Add(welcome);
        }
        sessionHost.Children.Add(dockSplitter); sessionHost.Children.Add(rowDockSplitter);
        dockSplitter.DragCompleted+=(_,_)=>CaptureSplit(false); ConfigureRowSplitter();
        dockSplitter.AddHandler(KeyDownEvent,(_,e)=> { if(e.Key is Key.Left or Key.Right)Dispatcher.UIThread.Post(()=>CaptureSplit(false)); },RoutingStrategies.Bubble,true);
        DragDrop.SetAllowDrop(sessionHost,true);
        DragDrop.AddDragOverHandler(sessionHost,(_,e)=> { e.DragEffects=e.DataTransfer.TryGetValue(TabDrag) is {} pane && Panes.Contains(pane)?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; });
        DragDrop.AddDropHandler(sessionHost,(_,e)=> { if(e.DataTransfer.TryGetValue(TabDrag) is {} pane && Panes.Contains(pane))DockSession(pane,DockTargetAt(e.GetPosition(sessionHost))); e.Handled=true; });
        LayoutDockWorkspace();
    }
    private void ConfigureRowSplitter()
    {
        // A standard GridSplitter would resize the adjacent bottom tab-header
        // row, rather than its content row. Resize the two content rows directly.
        (double Origin,double Top,double Total)? drag=null;
        DesignSystem.Resource(rowDockSplitter,Border.BackgroundProperty,"BorderDefault"); AutomationProperties.SetName(rowDockSplitter,L.T("Zmień wysokość paneli"));
        ToolTip.SetTip(rowDockSplitter,L.T("Przeciągnij separator lub użyj klawiszy ↑ i ↓. Home wyrównuje wysokość paneli."));
        void Resize(double ratio)
        { if(dockState.Mode==WorkspaceMode.Rows)dockState.SetRatio(ratio); else dockState.SetRowRatio(ratio); LayoutDockWorkspace(); }
        rowDockSplitter.PointerPressed+=(_,e)=>
        {
            if(!e.GetCurrentPoint(rowDockSplitter).Properties.IsLeftButtonPressed)return;
            var top=sessionHost.RowDefinitions[1].ActualHeight; var bottom=sessionHost.RowDefinitions[4].ActualHeight;
            drag=(e.GetPosition(sessionHost).Y,top,top+bottom); rowDockSplitter.Focus(); e.Pointer.Capture(rowDockSplitter); e.Handled=true;
        };
        rowDockSplitter.PointerMoved+=(_,e)=> { if(drag is {} start && start.Total>0) { Resize((start.Top+e.GetPosition(sessionHost).Y-start.Origin)/start.Total); e.Handled=true; } };
        rowDockSplitter.PointerReleased+=(_,e)=> { if(drag==null)return; drag=null; e.Pointer.Capture(null); QueueWorkspaceSave(); e.Handled=true; };
        rowDockSplitter.PointerCaptureLost+=(_,_)=> { if(drag!=null) { drag=null; QueueWorkspaceSave(); } };
        rowDockSplitter.KeyDown+=(_,e)=>
        {
            if(e.Key is not (Key.Up or Key.Down or Key.Home))return;
            var ratio=dockState.Mode==WorkspaceMode.Rows?dockState.Ratio:dockState.RowRatio;
            Resize(e.Key==Key.Home?.5:ratio+(e.Key==Key.Down?.01:-.01)); QueueWorkspaceSave(); e.Handled=true;
        };
    }
    private void FocusDock(SessionDock dock)
    {
        focusedDock=dock;
        if(dockState.Selected(dock) is {} id && Panes.FirstOrDefault(p=>p.WorkspaceId==id) is {} pane) { FocusSession(pane); pane.FocusContent(); }
        else ActivateSession();
    }
    private Control TabStrip(TabControl control,SessionDock location)
    {
        var name=location switch { SessionDock.Primary=>"Sesje panelu 1",SessionDock.Secondary=>"Sesje panelu 2",SessionDock.Tertiary=>"Sesje panelu 3",_=>"Sesje panelu 4" };
        AutomationProperties.SetName(control,L.T(name)); control.Name="DockTabs"+DockNumber(location);
        control.SelectionChanged+=(_,_)=> { if(!refreshingTabs && control.SelectedItem is TabItem { Tag:WorkspacePane pane })FocusSession(pane); };
        var scroller=new ScrollViewer { Content=control,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled };
        scroller.PointerWheelChanged+=(_,e)=> { scroller.Offset=new Vector(Math.Max(0,scroller.Offset.X-(e.Delta.X+e.Delta.Y)*100),0); e.Handled=true; };
        control.SelectionChanged+=(_,_)=>Dispatcher.UIThread.Post(()=> { if(control.SelectedItem is Control selected)selected.BringIntoView(); });
        var number=new Button { Name="DockNumber"+DockNumber(location),Content=DockNumber(location).ToString(),Width=26,Padding=new Thickness(2),Margin=new Thickness(3),Classes={"segment","chrome"} }; AutomationProperties.SetName(number,DockName(location)); ToolTip.SetTip(number,DockName(location)); number.Click+=(_,_)=>FocusDock(location); dockNumbers[location]=number;
        var strip=new Grid { Name="DockStrip"+DockNumber(location),ColumnDefinitions=new ColumnDefinitions("Auto,*"),ClipToBounds=true,Children={number,scroller} }; Grid.SetColumn(scroller,1);
        var rule=new Border { Name="DockStripRule"+DockNumber(location),Height=1,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false }; DesignSystem.Resource(rule,Border.BackgroundProperty,"BorderDefault"); Grid.SetColumnSpan(rule,2); strip.Children.Add(rule);
        DesignSystem.Resource(strip,Grid.BackgroundProperty,"BackgroundPrimary"); return strip;
    }
    private void CaptureSplit(bool rows)
    {
        var vertical=rows || dockState.Mode==WorkspaceMode.Rows;
        var a=vertical?sessionHost.RowDefinitions[1].ActualHeight:sessionHost.ColumnDefinitions[0].ActualWidth;
        var b=vertical?sessionHost.RowDefinitions[4].ActualHeight:sessionHost.ColumnDefinitions[2].ActualWidth;
        if(a+b>0) { if(rows)dockState.SetRowRatio(a/(a+b)); else dockState.SetRatio(a/(a+b)); }
        renderedLayout=null; LayoutDockWorkspace(); QueueWorkspaceSave();
    }
    internal SessionDock DockTargetAt(Point point)
    {
        var right=sessionHost.ColumnDefinitions.Count>1 && point.X>sessionHost.ColumnDefinitions[0].ActualWidth+preferences.PaneSeparatorSize/2d;
        var bottom=sessionHost.RowDefinitions.Count>2 && point.Y>sessionHost.RowDefinitions[0].ActualHeight+sessionHost.RowDefinitions[1].ActualHeight+preferences.PaneSeparatorSize/2d;
        return dockState.Mode switch {
            WorkspaceMode.Columns=>right?SessionDock.Secondary:SessionDock.Primary,
            WorkspaceMode.Rows=>bottom?SessionDock.Secondary:SessionDock.Primary,
            WorkspaceMode.Three=>!right?SessionDock.Primary:bottom?SessionDock.Tertiary:SessionDock.Secondary,
            WorkspaceMode.Grid=>bottom?right?SessionDock.Quaternary:SessionDock.Tertiary:right?SessionDock.Secondary:SessionDock.Primary,
            _=>SessionDock.Primary
        };
    }
    private (int Row,int Column,int Span) DockSlot(SessionDock dock)=>dock switch {
        SessionDock.Primary=>(1,0,dockState.Mode==WorkspaceMode.Three?4:1),
        SessionDock.Secondary=>dockState.Mode==WorkspaceMode.Rows?(4,0,1):(1,2,1),
        SessionDock.Tertiary=>(4,dockState.Mode==WorkspaceMode.Three?2:0,1),
        _=>(4,2,1)
    };
    private void LayoutDockWorkspace()
    {
        var mode=dockState.Mode; var multiRow=mode is WorkspaceMode.Rows or WorkspaceMode.Three or WorkspaceMode.Grid; var multiColumn=mode is WorkspaceMode.Columns or WorkspaceMode.Three or WorkspaceMode.Grid;
        var signature=(mode,dockState.Ratio,dockState.RowRatio,preferences.TabStripHeight,preferences.PaneSeparatorSize);
        if(renderedLayout!=signature)
        {
            renderedLayout=signature;
            var a=new GridLength(dockState.Ratio,GridUnitType.Star); var b=new GridLength(1-dockState.Ratio,GridUnitType.Star);
            var rowRatio=mode==WorkspaceMode.Rows?dockState.Ratio:dockState.RowRatio;
            sessionHost.ColumnDefinitions.Clear(); sessionHost.RowDefinitions.Clear();
            sessionHost.ColumnDefinitions.Add(new ColumnDefinition(multiColumn?a:GridLength.Star) { MinWidth=200 });
            if(multiColumn) { sessionHost.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(preferences.PaneSeparatorSize))); sessionHost.ColumnDefinitions.Add(new ColumnDefinition(b) { MinWidth=200 }); }
            sessionHost.RowDefinitions.Add(new RowDefinition(new GridLength(preferences.TabStripHeight))); sessionHost.RowDefinitions.Add(new RowDefinition(multiRow?new GridLength(rowRatio,GridUnitType.Star):GridLength.Star) { MinHeight=140 });
            if(multiRow) { sessionHost.RowDefinitions.Add(new RowDefinition(new GridLength(preferences.PaneSeparatorSize))); sessionHost.RowDefinitions.Add(new RowDefinition(new GridLength(preferences.TabStripHeight))); sessionHost.RowDefinitions.Add(new RowDefinition(new GridLength(1-rowRatio,GridUnitType.Star)) { MinHeight=140 }); }
            Place(dockSplitter,mode==WorkspaceMode.Rows?2:0,mode==WorkspaceMode.Rows?0:1,multiColumn?(multiRow?5:2):1);
            dockSplitter.ResizeDirection=mode==WorkspaceMode.Rows?GridResizeDirection.Rows:GridResizeDirection.Columns;
            Place(rowDockSplitter,2,mode==WorkspaceMode.Three?2:0,1,mode==WorkspaceMode.Grid?3:1);
            dockSplitter.HorizontalAlignment=rowDockSplitter.HorizontalAlignment=HorizontalAlignment.Stretch; dockSplitter.VerticalAlignment=rowDockSplitter.VerticalAlignment=VerticalAlignment.Stretch;
        }
        dockSplitter.IsVisible=multiColumn; rowDockSplitter.IsVisible=multiRow;
        foreach(var dock in WorkspaceState.DockedGroups)
        {
            var visible=dockState.VisibleDocks.Contains(dock); dockStrips[dock].IsVisible=visible; dockNumbers[dock].IsVisible=preferences.ShowPanelNumbers && mode!=WorkspaceMode.Single;
            dockTabs[dock].Height=preferences.TabStripHeight;
            dockWelcomes[dock].IsVisible=visible && dockState.Items(dock).Count==0;
            if(!visible)continue;
            var slot=DockSlot(dock); Place(dockStrips[dock],slot.Row-1,slot.Column); Place(dockWelcomes[dock],slot.Row,slot.Column,slot.Span);
        }
        foreach(var pane in Panes)
        {
            var location=dockState.Location(pane.WorkspaceId);
            if(location==SessionDock.Floating)continue;
            var slot=DockSlot(location); Place(pane,slot.Row,slot.Column,slot.Span);
        }
        ActivateSession();
    }
    private static void Place(Control control,int row,int column,int rowSpan=1,int columnSpan=1) { Grid.SetRow(control,row); Grid.SetColumn(control,column); Grid.SetRowSpan(control,rowSpan); Grid.SetColumnSpan(control,columnSpan); }
    internal void SetWorkspaceMode(WorkspaceMode mode)
    {
        var focused=dockState.Focused; dockState.SetMode(mode);
        // Fill empty panels from spare docked tabs. A floating session stays floating.
        foreach(var dock in dockState.VisibleDocks.Skip(1).Where(d=>dockState.Items(d).Count==0))
        {
            var donor=dockState.VisibleDocks.FirstOrDefault(d=>dockState.Items(d).Count>1);
            if(dockState.Items(donor).Count<=1)break;
            var candidate=focused is {} id && dockState.Items(donor).Contains(id)?id:dockState.Items(donor)[^1]; dockState.Move(candidate,dock);
        }
        if(focused is {} keep)dockState.Select(keep);
        focusedDock=ActivePane is {} active?dockState.Location(active.WorkspaceId):SessionDock.Primary;
        RefreshTabs(); LayoutDockWorkspace(); QueueWorkspaceSave();
    }
    internal void EqualizeWorkspace()
    { dockState.SetRatio(.5); dockState.SetRowRatio(.5); LayoutDockWorkspace(); QueueWorkspaceSave(); }
    internal async Task SaveWorkspaceAsync()
    {
        await repository.SetSettingAsync("workspaceMode",dockState.Mode.ToString()); await repository.SetSettingAsync("splitRatio",dockState.Ratio.ToString(System.Globalization.CultureInfo.InvariantCulture)); await repository.SetSettingAsync("rowSplitRatio",dockState.RowRatio.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    internal void RegisterPane(WorkspacePane pane,SessionDock? requested=null)
    {
        if(Panes.Contains(pane))throw new InvalidOperationException(L.T("Sesja jest już w obszarze pracy."));
        var target=requested??(preferences.NewTabTarget switch { NewTabPlacement.FirstPanel=>SessionDock.Primary,NewTabPlacement.LeastPopulated=>dockState.VisibleDocks.MinBy(d=>dockState.Items(d).Count),_=>dockState.VisibleDocks.Contains(focusedDock)?focusedDock:SessionDock.Primary });
        dockState.Add(pane.WorkspaceId,target); focusedDock=target;
        if(pane is SessionPane session) { session.StatusChanged+=PaneStatusChanged; sessions.Add(session); } else if(pane is HelpPane help)helpPane=help; else networkPanes.Add((NetworkToolPane)pane); sessionHost.Children.Add(pane);
        pane.AddHandler(PointerPressedEvent,(_,_)=>FocusSession(pane),RoutingStrategies.Tunnel); pane.GotFocus+=(_,_)=>FocusSession(pane);
        RefreshTabs(); LayoutDockWorkspace(); QueueWorkspaceSave();
    }
    internal void FocusSession(WorkspacePane pane)
    {
        if(!Panes.Contains(pane)||closing)return; var changed=dockState.Focused!=pane.WorkspaceId; dockState.Select(pane.WorkspaceId); focusedDock=dockState.Location(pane.WorkspaceId);
        refreshingTabs=true;
        try { if(dockTabs.TryGetValue(focusedDock,out var control))control.SelectedIndex=dockState.Items(focusedDock).ToList().IndexOf(pane.WorkspaceId); }
        finally { refreshingTabs=false; }
        ActivateSession(); if(changed)QueueWorkspaceSave();
    }
    private void RefreshTabs()
    {
        refreshingTabs=true;
        try
        {
            foreach(var location in WorkspaceState.DockedGroups)
            {
                var control=dockTabs[location]; var ids=dockState.Items(location);
                control.ItemsSource=ids.Select(id=>
                {
                    var pane=Panes.Single(p=>p.WorkspaceId==id); var tab=new TabItem { Header=WorkspaceTabHeader(pane),Tag=pane,Classes={"session-tab"} }; tab.ContextMenu=new ContextMenu { ItemsSource=DockMenu(pane) };
                    DragSupport.Source(tab,TabDrag,()=>pane,DragDropEffects.Move); DragDrop.SetAllowDrop(tab,true);
                    DragDrop.AddDragOverHandler(tab,(_,e)=> { e.DragEffects=e.DataTransfer.TryGetValue(TabDrag) is {} source && Panes.Contains(source)?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; });
                    DragDrop.AddDropHandler(tab,(_,e)=> { if(e.DataTransfer.TryGetValue(TabDrag) is {} source && Panes.Contains(source))DockSession(source,location,pane.WorkspaceId); e.Handled=true; });
                    return tab;
                }).ToArray(); control.SelectedIndex=dockState.Selected(location) is {} selected?ids.ToList().IndexOf(selected):-1;
            }
        }
        finally { refreshingTabs=false; }
    }
    private MenuItem[] DockMenu(WorkspacePane pane)=>WorkspaceState.DockedGroups.Select(dock=>Menu(DockAction(dock),()=> { DockSession(pane,dock); return Task.CompletedTask; })).Concat(new[]{
        Menu("Odłącz do osobnego okna",()=> { DockSession(pane,SessionDock.Floating); return Task.CompletedTask; }),Menu("Zamknij sesję",()=>CloseWorkspacePaneAsync(pane)) }).ToArray();
    private static string DockAction(SessionDock dock)=>dock switch { SessionDock.Primary=>"Dokuj do panelu 1",SessionDock.Secondary=>"Dokuj do panelu 2",SessionDock.Tertiary=>"Dokuj do panelu 3",_=>"Dokuj do panelu 4" };
    private void ActivateSession()
    {
        foreach(var tab in dockTabs.Values.SelectMany(t=>t.Items).OfType<TabItem>())tab.Classes.Set("current",tab.Tag is WorkspacePane current && current.WorkspaceId==dockState.Focused);
        foreach(var (dock,number) in dockNumbers)number.Classes.Set("active",dock==focusedDock);
        foreach(var pane in Panes)
        {
            var location=dockState.Location(pane.WorkspaceId); var visible=location==SessionDock.Floating || dockState.VisibleDocks.Contains(location) && dockState.Selected(location)==pane.WorkspaceId;
            pane.IsVisible=visible; pane.IsActive=visible && (location!=SessionDock.Floating || floatingDocks.TryGetValue(pane.WorkspaceId,out var floating) && floating.Window.WindowState!=WindowState.Minimized);
        }
        UpdateActivity(); UpdateStatus(); QueueTree();
    }
    internal void DockSession(WorkspacePane pane,SessionDock target,Guid? before=null)
    {
        if(!Panes.Contains(pane)||closing)return; var previous=dockState.Location(pane.WorkspaceId);
        if(target==SessionDock.Floating && previous==SessionDock.Floating) { floatingDocks[pane.WorkspaceId].Window.Activate(); return; }
        dockState.Move(pane.WorkspaceId,target,before); focusedDock=target;
        if(previous==SessionDock.Floating || target==SessionDock.Floating)
        {
            var previousRoot=TopLevel.GetTopLevel(pane);
            pane.BeginReparent();
            try
            {
                if(previous==SessionDock.Floating) { var old=floatingDocks[pane.WorkspaceId]; old.Host.Children.Remove(pane); old.Returning=true; old.Window.Close(); floatingDocks.Remove(pane.WorkspaceId); }
                else sessionHost.Children.Remove(pane);
                // Drain invalidations while the pane is detached. Otherwise the old
                // window may try to arrange controls already attached to the new one.
                previousRoot?.UpdateLayout();
                if(target==SessionDock.Floating)OpenFloatingDock(pane); else sessionHost.Children.Add(pane);
            }
            finally { pane.EndReparent(); }
        }
        RefreshTabs(); LayoutDockWorkspace(); QueueWorkspaceSave();
    }
    private void OpenFloatingDock(WorkspacePane pane)
    {
        var host=new Grid(); Place(pane,0,0); host.Children.Add(pane);
        var content=new Grid { RowDefinitions=new RowDefinitions("Auto,*") }; var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=DesignTokens.Medium,Margin=new Thickness(8),Children={WorkspaceTabHeader(pane)} };
        foreach(var dock in WorkspaceState.DockedGroups)actions.Children.Add(Action(DockAction(dock),()=> { DockSession(pane,dock); return Task.CompletedTask; }));
        content.Children.Add(new ScrollViewer { Content=actions,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled }); Grid.SetRow(host,1); content.Children.Add(host);
        var window=new Window { Title="ServerDesk — "+pane.DisplayName,Width=1000,Height=720,MinWidth=520,MinHeight=380,Content=content,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        if(pane is SessionPane)window.Bind(Window.TitleProperty,new Binding(nameof(SessionPane.PresentationProfile)+".Name") { Source=pane,StringFormat="ServerDesk — {0}" });
        ToolTip.SetTip(actions,L.T("Zamknięcie okna dokuje sesję z powrotem do panelu 1.")); var floating=new FloatingDock(window,host); floatingDocks.Add(pane.WorkspaceId,floating);
        window.Closing+=(_,e)=> { if(!closing && !floating.Returning) { e.Cancel=true; DockSession(pane,SessionDock.Primary); } };
        window.Activated+=(_,_)=>FocusSession(pane); window.PropertyChanged+=(_,e)=> { if(e.Property==Window.WindowStateProperty)ActivateSession(); };
        void RememberGeometry() { if(window.WindowState==WindowState.Normal && window.Bounds.Width>0 && window.Bounds.Height>0) { var geometry=new WindowGeometry(window.Position.X,window.Position.Y,window.Bounds.Width,window.Bounds.Height); if(floating.NormalGeometry!=geometry) { floating.NormalGeometry=geometry; QueueWorkspaceSave(); } } }
        window.PositionChanged+=(_,_)=>RememberGeometry(); window.PropertyChanged+=(_,e)=> { if(e.Property==Window.BoundsProperty)RememberGeometry(); };
        window.ScalingChanged+=(_,_)=>Dispatcher.UIThread.Post(()=>RepairFloatingPlacement(floating));
        window.AddHandler(KeyDownEvent,HandleKeys,RoutingStrategies.Tunnel); window.Show(this); RepairFloatingPlacement(floating);
    }
    internal void RemovePane(WorkspacePane pane)
    {
        if(pane is SessionPane session)session.StatusChanged-=PaneStatusChanged; pane.Dispose();
        if(pane is SessionPane closingSession)_=TrackSessionCloseAsync(closingSession);
        if(floatingDocks.Remove(pane.WorkspaceId,out var floating)) { floating.Returning=true; floating.Host.Children.Remove(pane); floating.Window.Close(); }
        else sessionHost.Children.Remove(pane);
        dockState.Remove(pane.WorkspaceId); if(pane is SessionPane removed)sessions.Remove(removed); else if(pane is HelpPane)helpPane=null; else networkPanes.Remove((NetworkToolPane)pane); focusedDock=ActivePane is {} active?dockState.Location(active.WorkspaceId):SessionDock.Primary; RefreshTabs(); LayoutDockWorkspace(); QueueWorkspaceSave();
        if(transferViews.Count>0)UpdateTransferOverview();
        UpdateResumeNotice();
    }
    private void CycleSession(bool reverse)
    {
        var dock=ActivePane is {} pane?dockState.Location(pane.WorkspaceId):focusedDock; var ids=dockState.Items(dock);
        if(dock==SessionDock.Floating)return;
        if(ids.Count==0)return; var index=dockState.Focused==null?0:ids.ToList().IndexOf(dockState.Focused.Value); var next=(Math.Max(0,index)+(reverse?ids.Count-1:1))%ids.Count; var target=Panes.Single(p=>p.WorkspaceId==ids[next]); FocusSession(target);
    }
    internal void Reorder(WorkspacePane pane,int offset)
    {
        var location=dockState.Location(pane.WorkspaceId); if(location==SessionDock.Floating)return; var ids=dockState.Items(location).ToList(); var index=ids.IndexOf(pane.WorkspaceId); var target=index+offset; if(target<0 || target>=ids.Count)return;
        ids.Remove(pane.WorkspaceId); ids.Insert(target,pane.WorkspaceId); var before=target+1<ids.Count?ids[target+1]:(Guid?)null; DockSession(pane,location,before);
    }
}
