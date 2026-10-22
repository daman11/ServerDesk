using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class TerminalFocusChecks
{
    private static TerminalControl Terminal(SessionPane pane)=>pane.Children.OfType<TerminalControl>().Single();
    private static TabControl Tabs(MainWindow main,SessionPane pane)=>main.GetVisualDescendants().OfType<TabControl>()
        .Single(t=>t.Items.OfType<TabItem>().Any(tab=>ReferenceEquals(tab.Tag,pane)));
    private static void ClickTab(MainWindow main,SessionPane pane)
    {
        var tab=Tabs(main,pane).Items.OfType<TabItem>().Single(t=>ReferenceEquals(t.Tag,pane));
        var point=tab.TranslatePoint(new Point(12,tab.Bounds.Height/2),main)!.Value;
        main.MouseDown(point,MouseButton.Left); main.MouseUp(point,MouseButton.Left); RedesignChecks.Frames(4);
    }
    private static void AssertFocus(MainWindow main,SessionPane pane)
    {
        if(main.Workspace.Focused!=pane.WorkspaceId || !Terminal(pane).GetVisualDescendants().OfType<TerminalView>().Any(v=>v.IsFocused))
            throw new Exception("Clicking a tab did not focus its terminal input");
    }
    internal static void Run(string root)
    {
        var repo=new ProfileRepository(new AppPaths()); RedesignChecks.Wait(repo.InitializeAsync());
        var main=new MainWindow(new(repo),repo,new CredentialStore(new AppPaths()),new(repo),new ExternalRdpClient());
        main.Show(); RedesignChecks.Frames(12);
        var panes=Enumerable.Range(0,3).Select(i=>new SessionPane(main,new() { Name="Focus "+i,Host="fixture.invalid" },new(repo),new ExternalRdpClient(),repo)).ToArray();
        foreach(var pane in panes)main.RegisterPane(pane); RedesignChecks.Frames(6);
        for(var i=0;i<12;i++) { ClickTab(main,panes[i%3]); AssertFocus(main,panes[i%3]); }
        var tabs=Tabs(main,panes[0]);
        tabs.SelectedIndex=0; tabs.SelectedIndex=1; tabs.SelectedIndex=2; RedesignChecks.Frames(4); AssertFocus(main,panes[2]);
        RedesignChecks.Wait(main.OpenCommandsPanelAsync()); RedesignChecks.Frames(4);
        var search=main.GetVisualDescendants().OfType<TextBox>().Single(t=>t.Name=="CommandSearch");
        tabs.SelectedIndex=0; search.Focus(); RedesignChecks.Frames(4);
        if(!search.IsKeyboardFocusWithin)throw new Exception("Deferred tab focus stole input from a later command-field click");
        ClickTab(main,panes[0]); AssertFocus(main,panes[0]);
        main.KeyPress(Key.Tab,RawInputModifiers.Control,PhysicalKey.Tab,"\t");
        main.KeyRelease(Key.Tab,RawInputModifiers.Control,PhysicalKey.Tab,"\t"); RedesignChecks.Frames(4); AssertFocus(main,panes[1]);
        main.CloseCommandsPanel(); main.SetWorkspaceMode(WorkspaceMode.Columns); RedesignChecks.Frames(4);
        for(var i=0;i<9;i++) { ClickTab(main,panes[i%3]); AssertFocus(main,panes[i%3]); }
        main.FocusSession(panes[0]);
        var path=panes[0].GetVisualDescendants().OfType<TextBox>().Single(t=>t.Name=="SftpRemotePath");
        Terminal(panes[0]).IsVisible=false;
        var files=(Grid)typeof(SessionPane).GetField("files",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(panes[0])!;
        files.IsVisible=true; RedesignChecks.Frames(3); path.Focus(); RedesignChecks.Frames(4);
        if(!path.IsKeyboardFocusWithin)throw new Exception("Ordinary pane activation stole focus from an SFTP path field");
        main.SetWorkspaceMode(WorkspaceMode.Single); RedesignChecks.Frames(4);
        panes[2].PrepareRestored(0); ClickTab(main,panes[2]);
        var connect=panes[2].GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Połącz"));
        if(!connect.IsKeyboardFocusWithin || panes.Any(p=>p.IsConnected))throw new Exception("Restored tab focus connected or targeted a hidden terminal");
        main.CaptureRenderedFrame()!.Save(System.IO.Path.Combine(root,"terminal-focus.png"));
        main.Close(); RedesignChecks.Frames(4);
        var confirm=main.OwnedWindows.Single();
        confirm.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Potwierdź")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        SshAcceptanceChecks.Until(()=>!main.IsVisible,"Focus fixture did not close");
        Console.WriteLine("PASS terminal focus: mouse tabs, selected-tab click, rapid switching, Ctrl+Tab, split panels, later command-field focus, SFTP typing and restored-tab isolation; no server or credentials");
    }
}
