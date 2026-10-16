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
    private readonly TabControl secondaryTabs=new();
    private readonly Dictionary<Guid,FloatingDock> floatingDocks=[];
    private readonly Border primaryWelcome=new(),secondaryWelcome=new();
    private readonly GridSplitter dockSplitter=new() { ResizeBehavior=GridResizeBehavior.PreviousAndNext };
    private Control primaryStrip=null!,secondaryStrip=null!;
    private bool refreshingTabs;
    private SessionDock focusedDock=SessionDock.Primary;
    private WorkspaceMode? renderedMode;
    private double renderedRatio;
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
    private void BuildDockWorkspace()
    {
        primaryStrip=TabStrip(tabs,SessionDock.Primary); secondaryStrip=TabStrip(secondaryTabs,SessionDock.Secondary);
        sessionHost.Children.Add(primaryStrip); sessionHost.Children.Add(secondaryStrip); sessionHost.Children.Add(dockSplitter);
        primaryWelcome.Child=BuildWelcome();
        secondaryWelcome.Child=new StackPanel { VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(20),Spacing=DesignTokens.Section,Children={DeskIcons.Create("layers",32),DesignSystem.Label(L.T("Drugi panel"),false,18),DesignSystem.Label(L.T("Przeciągnij tutaj zakładkę\nlub wybierz „Dokuj do panelu 2” w jej menu."),true)} };
        sessionHost.Children.Add(primaryWelcome); sessionHost.Children.Add(secondaryWelcome);
        primaryWelcome.PointerPressed+=(_,_)=>focusedDock=SessionDock.Primary; secondaryWelcome.PointerPressed+=(_,_)=>focusedDock=SessionDock.Secondary;
        dockSplitter.DragCompleted+=(_,_)=>
        {
            var a=dockState.Mode==WorkspaceMode.Columns?sessionHost.ColumnDefinitions[0].ActualWidth:sessionHost.RowDefinitions[1].ActualHeight;
            var b=dockState.Mode==WorkspaceMode.Columns?sessionHost.ColumnDefinitions[2].ActualWidth:sessionHost.RowDefinitions[4].ActualHeight;
            if(a+b>0)dockState.SetRatio(a/(a+b)); renderedMode=null; LayoutDockWorkspace(); QueueWorkspaceSave();
        };
        DragDrop.SetAllowDrop(sessionHost,true);
        DragDrop.AddDragOverHandler(sessionHost,(_,e)=> { e.DragEffects=e.DataTransfer.TryGetValue(TabDrag) is {} pane && Panes.Contains(pane)?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; });
        DragDrop.AddDropHandler(sessionHost,(_,e)=>
        {
            if(e.DataTransfer.TryGetValue(TabDrag) is {} pane && Panes.Contains(pane))
            {
                var point=e.GetPosition(sessionHost); var target=dockState.Mode switch { WorkspaceMode.Columns when point.X>sessionHost.ColumnDefinitions[0].ActualWidth=>SessionDock.Secondary,WorkspaceMode.Rows when point.Y>35+sessionHost.RowDefinitions[1].ActualHeight=>SessionDock.Secondary,_=>SessionDock.Primary }; DockSession(pane,target);
            }
            e.Handled=true;
        });
        LayoutDockWorkspace();
    }
    private Control TabStrip(TabControl control,SessionDock location)
    {
        control.Height=35; AutomationProperties.SetName(control,location==SessionDock.Primary?L.T("Sesje panelu 1"):L.T("Sesje panelu 2"));
        control.SelectionChanged+=(_,_)=> { if(!refreshingTabs && control.SelectedItem is TabItem { Tag:WorkspacePane pane })FocusSession(pane); };
        var scroller=new ScrollViewer { Content=control,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled };
        scroller.PointerWheelChanged+=(_,e)=> { scroller.Offset=new Vector(Math.Max(0,scroller.Offset.X-(e.Delta.X+e.Delta.Y)*100),0); e.Handled=true; };
        control.SelectionChanged+=(_,_)=>Dispatcher.UIThread.Post(()=> { if(control.SelectedItem is Control selected)selected.BringIntoView(); });
        return new Border { Child=scroller };
    }
    private void LayoutDockWorkspace()
    {
        var mode=dockState.Mode; var ratio=dockState.Ratio;
        if(renderedMode!=mode || renderedRatio!=ratio)
        {
            renderedMode=mode; renderedRatio=ratio; var a=new GridLength(ratio,GridUnitType.Star); var b=new GridLength(1-ratio,GridUnitType.Star);
            sessionHost.ColumnDefinitions.Clear(); sessionHost.RowDefinitions.Clear();
            if(mode==WorkspaceMode.Columns)
            {
                sessionHost.ColumnDefinitions.Add(new ColumnDefinition(a) { MinWidth=200 }); sessionHost.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(4))); sessionHost.ColumnDefinitions.Add(new ColumnDefinition(b) { MinWidth=200 });
                sessionHost.RowDefinitions.Add(new RowDefinition(new GridLength(35))); sessionHost.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            }
            else
            {
                sessionHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star)); sessionHost.RowDefinitions.Add(new RowDefinition(new GridLength(35))); sessionHost.RowDefinitions.Add(new RowDefinition(mode==WorkspaceMode.Rows?a:GridLength.Star) { MinHeight=140 });
                if(mode==WorkspaceMode.Rows) { sessionHost.RowDefinitions.Add(new RowDefinition(new GridLength(4))); sessionHost.RowDefinitions.Add(new RowDefinition(new GridLength(35))); sessionHost.RowDefinitions.Add(new RowDefinition(b) { MinHeight=140 }); }
            }
            Place(primaryStrip,0,0); Place(secondaryStrip,mode==WorkspaceMode.Rows?3:0,mode==WorkspaceMode.Columns?2:0);
            Place(dockSplitter,mode==WorkspaceMode.Rows?2:0,mode==WorkspaceMode.Columns?1:0,mode==WorkspaceMode.Columns?2:1);
            dockSplitter.ResizeDirection=mode==WorkspaceMode.Rows?GridResizeDirection.Rows:GridResizeDirection.Columns;
            dockSplitter.HorizontalAlignment=HorizontalAlignment.Stretch; dockSplitter.VerticalAlignment=VerticalAlignment.Stretch;
        }
        secondaryStrip.IsVisible=dockSplitter.IsVisible=mode!=WorkspaceMode.Single;
        Place(primaryWelcome,1,0); Place(secondaryWelcome,mode==WorkspaceMode.Rows?4:1,mode==WorkspaceMode.Columns?2:0);
        primaryWelcome.IsVisible=dockState.Primary.Count==0; secondaryWelcome.IsVisible=mode!=WorkspaceMode.Single && dockState.Secondary.Count==0;
        foreach(var pane in Panes)
        {
            var location=dockState.Location(pane.WorkspaceId);
            if(location!=SessionDock.Floating)Place(pane,location==SessionDock.Secondary && mode==WorkspaceMode.Rows?4:1,location==SessionDock.Secondary && mode==WorkspaceMode.Columns?2:0);
        }
        ActivateSession();
    }
    private static void Place(Control control,int row,int column,int rowSpan=1) { Grid.SetRow(control,row); Grid.SetColumn(control,column); Grid.SetRowSpan(control,rowSpan); Grid.SetColumnSpan(control,1); }
    internal void SetWorkspaceMode(WorkspaceMode mode)
    {
        dockState.SetMode(mode);
        if(mode!=WorkspaceMode.Single && dockState.Secondary.Count==0 && dockState.Primary.Count>1 && ActivePane is {} active && dockState.Location(active.WorkspaceId)==SessionDock.Primary)dockState.Move(active.WorkspaceId,SessionDock.Secondary);
        focusedDock=ActivePane is {} focused?dockState.Location(focused.WorkspaceId):SessionDock.Primary;
        RefreshTabs(); LayoutDockWorkspace(); QueueWorkspaceSave();
    }
    internal async Task SaveWorkspaceAsync()
    {
        await repository.SetSettingAsync("workspaceMode",dockState.Mode.ToString()); await repository.SetSettingAsync("splitRatio",dockState.Ratio.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    internal void RegisterPane(WorkspacePane pane,SessionDock? requested=null)
    {
        if(Panes.Contains(pane))throw new InvalidOperationException(L.T("Sesja jest już w obszarze pracy."));
        var target=requested??(focusedDock==SessionDock.Secondary && dockState.Mode!=WorkspaceMode.Single?SessionDock.Secondary:SessionDock.Primary);
        dockState.Add(pane.WorkspaceId,target); focusedDock=target;
        if(pane is SessionPane session) { session.StatusChanged+=PaneStatusChanged; sessions.Add(session); } else networkPanes.Add((NetworkToolPane)pane); sessionHost.Children.Add(pane);
        pane.AddHandler(PointerPressedEvent,(_,_)=>FocusSession(pane),RoutingStrategies.Tunnel); pane.GotFocus+=(_,_)=>FocusSession(pane);
        RefreshTabs(); LayoutDockWorkspace(); QueueWorkspaceSave();
    }
    internal void FocusSession(WorkspacePane pane)
    {
        if(!Panes.Contains(pane)||closing)return; var changed=dockState.Focused!=pane.WorkspaceId; dockState.Select(pane.WorkspaceId); focusedDock=dockState.Location(pane.WorkspaceId);
        refreshingTabs=true;
        try { var control=focusedDock==SessionDock.Secondary?secondaryTabs:tabs; if(focusedDock!=SessionDock.Floating)control.SelectedIndex=IndexOf(focusedDock,pane.WorkspaceId); }
        finally { refreshingTabs=false; }
        ActivateSession();
        if(changed)QueueWorkspaceSave();
    }
    private int IndexOf(SessionDock dock,Guid id)=>(dock==SessionDock.Primary?dockState.Primary:dockState.Secondary).ToList().IndexOf(id);
    private void RefreshTabs()
    {
        refreshingTabs=true;
        try
        {
            void Build(TabControl control,IReadOnlyList<Guid> ids,SessionDock location,Guid? selected)
            {
                control.ItemsSource=ids.Select(id=>
                {
                    var pane=Panes.Single(p=>p.WorkspaceId==id); var tab=new TabItem { Header=WorkspaceTabHeader(pane),Tag=pane,Classes={"session-tab"} };
                    tab.ContextMenu=new ContextMenu { ItemsSource=DockMenu(pane) };
                    DragSupport.Source(tab,TabDrag,()=>pane,DragDropEffects.Move); DragDrop.SetAllowDrop(tab,true);
                    DragDrop.AddDragOverHandler(tab,(_,e)=> { e.DragEffects=e.DataTransfer.TryGetValue(TabDrag) is {} source && Panes.Contains(source)?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; });
                    DragDrop.AddDropHandler(tab,(_,e)=> { if(e.DataTransfer.TryGetValue(TabDrag) is {} source && Panes.Contains(source))DockSession(source,location,pane.WorkspaceId); e.Handled=true; });
                    return tab;
                }).ToArray(); control.SelectedIndex=selected==null?-1:ids.ToList().IndexOf(selected.Value);
            }
            Build(tabs,dockState.Primary,SessionDock.Primary,dockState.SelectedPrimary); Build(secondaryTabs,dockState.Secondary,SessionDock.Secondary,dockState.SelectedSecondary);
        }
        finally { refreshingTabs=false; }
    }
    private MenuItem[] DockMenu(WorkspacePane pane)=>[
        Menu("Dokuj do panelu 1",()=> { DockSession(pane,SessionDock.Primary); return Task.CompletedTask; }),
        Menu("Dokuj do panelu 2",()=> { DockSession(pane,SessionDock.Secondary); return Task.CompletedTask; }),
        Menu("Odłącz do osobnego okna",()=> { DockSession(pane,SessionDock.Floating); return Task.CompletedTask; }),
        Menu("Zamknij sesję",()=>CloseWorkspacePaneAsync(pane))];
    private void ActivateSession()
    {
        foreach(var tab in tabs.Items.Concat(secondaryTabs.Items).OfType<TabItem>())tab.Classes.Set("current",tab.Tag is WorkspacePane current && current.WorkspaceId==dockState.Focused);
        foreach(var pane in Panes)
        {
            var location=dockState.Location(pane.WorkspaceId); var visible=location==SessionDock.Floating || location==SessionDock.Primary && dockState.SelectedPrimary==pane.WorkspaceId || location==SessionDock.Secondary && dockState.Mode!=WorkspaceMode.Single && dockState.SelectedSecondary==pane.WorkspaceId;
            pane.IsVisible=visible; pane.IsActive=visible && (location!=SessionDock.Floating || floatingDocks.TryGetValue(pane.WorkspaceId,out var floating) && floating.Window.WindowState!=WindowState.Minimized);
        }
        UpdateStatus(); QueueTree();
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
        var content=new Grid { RowDefinitions=new RowDefinitions("Auto,*") }; var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=DesignTokens.Medium,Margin=new Thickness(8),Children={WorkspaceTabHeader(pane),Action("Dokuj do panelu 1",()=> { DockSession(pane,SessionDock.Primary); return Task.CompletedTask; }),Action("Dokuj do panelu 2",()=> { DockSession(pane,SessionDock.Secondary); return Task.CompletedTask; })} };
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
        dockState.Remove(pane.WorkspaceId); if(pane is SessionPane removed)sessions.Remove(removed); else networkPanes.Remove((NetworkToolPane)pane); focusedDock=ActivePane is {} active?dockState.Location(active.WorkspaceId):SessionDock.Primary; RefreshTabs(); LayoutDockWorkspace(); QueueWorkspaceSave();
        if(transferViews.Count>0)UpdateTransferOverview();
        UpdateResumeNotice();
    }
    private void CycleSession(bool reverse)
    {
        var dock=ActivePane is {} pane?dockState.Location(pane.WorkspaceId):focusedDock; var ids=dock==SessionDock.Secondary?dockState.Secondary:dockState.Primary;
        if(dock==SessionDock.Floating)return;
        if(ids.Count==0)return; var index=dockState.Focused==null?0:ids.ToList().IndexOf(dockState.Focused.Value); var next=(Math.Max(0,index)+(reverse?ids.Count-1:1))%ids.Count; var target=Panes.Single(p=>p.WorkspaceId==ids[next]); FocusSession(target);
    }
    internal void Reorder(WorkspacePane pane,int offset)
    {
        var location=dockState.Location(pane.WorkspaceId); if(location==SessionDock.Floating)return; var ids=(location==SessionDock.Primary?dockState.Primary:dockState.Secondary).ToList(); var index=ids.IndexOf(pane.WorkspaceId); var target=index+offset; if(target<0 || target>=ids.Count)return;
        ids.Remove(pane.WorkspaceId); ids.Insert(target,pane.WorkspaceId); var before=target+1<ids.Count?ids[target+1]:(Guid?)null; DockSession(pane,location,before);
    }
}
