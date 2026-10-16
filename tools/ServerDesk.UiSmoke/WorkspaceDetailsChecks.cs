using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Iciclecreek.Terminal;

internal static class WorkspaceDetailsChecks
{
    private static void Wait(Task task)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(!task.IsCompleted) { if(watch.Elapsed.TotalSeconds>35)throw new TimeoutException("Workspace details UI timed out"); RedesignChecks.Frames(1); }
        task.GetAwaiter().GetResult();
    }
    internal static void Preview(MainWindow main,ProfileRepository repo,string root)
    {
        var titlebar=main.GetVisualDescendants().OfType<Grid>().Single(g=>g.Name=="WorkspaceTitleBar");
        if(!main.ExtendClientAreaToDecorationsHint || main.WindowDecorations!=WindowDecorations.BorderOnly || WindowDecorationProperties.GetElementRole(titlebar)!=WindowDecorationsElementRole.TitleBar)throw new Exception("Window does not use a single custom title bar");
        var folder=main.GetVisualDescendants().OfType<TreeViewItem>().Single(t=>t.Tag as string=="Production/Linux");
        if(!folder.ContextMenu!.Items.OfType<MenuItem>().Any(m=>m.Header as string=="Zmień ikonę folderu"))throw new Exception("Folder icon command missing");
        var edit=main.EditFolderIconAsync("Production/Linux"); RedesignChecks.Frames();
        var dialog=main.OwnedWindows.Single(); var picker=dialog.GetVisualDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Ikona folderu");
        picker.SelectedItem=FolderIcons.Choices.Single(c=>c.Id=="database"); RedesignChecks.Frames(); dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"folder-icon-picker.png"));
        dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(edit); RedesignChecks.Frames();
        folder=main.GetVisualDescendants().OfType<TreeViewItem>().Single(t=>t.Tag as string=="Production/Linux");
        if(!((Control)folder.Header!).GetVisualDescendants().OfType<DeskIcon>().Any(i=>i.Glyph=="database"))throw new Exception("Saved folder icon did not reach tree");
        edit=main.EditFolderIconAsync("Production/Linux"); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single();
        dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Domyślna ikona").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(edit);
        if(repo.FolderIconsAsync().GetAwaiter().GetResult()["Production/Linux"]!="database")throw new Exception("Cancelled folder icon edit changed data");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"workspace-folders.png"));
        var snapshot=new ServerSnapshot("",new(100,50),23,new(8L<<30,5L<<30,0,0),"","","") { SystemDetails=new("test","Ubuntu 24.04 LTS","","","",TimeSpan.FromDays(27),""),Traffic=new(12345,67890),RemoteTime="14:32",DiskDetails=[new("/dev/sda","ext4","/",100,45,55,45)] };
        var bar=new ServerMetricsBar(); var metricsWindow=new Window { Width=900,Height=80,Content=bar }; metricsWindow.Show(); RedesignChecks.Frames();
        foreach(var theme in new[]{"midnight","light"})
        {
            DesignSystem.Apply(theme); bar.Render(snapshot,DateTimeOffset.UtcNow,"",true,900); RedesignChecks.Frames();
            if(bar.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.IsEffectivelyVisible && (t.Bounds.Width<=0 || t.TranslatePoint(default,metricsWindow)!.Value.X+t.Bounds.Width>metricsWindow.Bounds.Width)))throw new Exception("Metrics exceed narrow window bounds");
            metricsWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"metrics-narrow-"+theme+".png"));
        }
        bar.Render(null,null,"",false,900); RedesignChecks.Frames(); if(bar.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("23%") == true))throw new Exception("Disconnected metrics still show old values");
        metricsWindow.Close(); DesignSystem.Apply("dark");
        Console.WriteLine("PASS folder icon dialog/save/tree/cancel and merged titlebar role; compact metrics bounds in Midnight/Light and clearing disconnected values");
    }
    internal static void Network(MainWindow main,ProfileRepository repo,string root)
    {
        var pane=main.GetVisualDescendants().OfType<SessionPane>().Last(p=>p.IsConnected);
        main.FocusSession(pane);
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(pane.LatestSnapshot?.Traffic==null && DateTime.UtcNow<deadline)RedesignChecks.Frames(1);
        var snapshot=pane.LatestSnapshot??throw new Exception("No automatic metrics while terminal is active: "+pane.MetricsError);
        if(snapshot.CpuPercent==null || snapshot.Traffic==null || snapshot.Memory.Total<=0 || snapshot.SystemDetails.Uptime==null || snapshot.RemoteTime.Length==0)throw new Exception("Automatic metrics sample is incomplete");
        if(!pane.GetVisualDescendants().OfType<TerminalControl>().Single().IsVisible)throw new Exception("Metrics opened a panel instead of keeping terminal");
        DesignSystem.Apply("midnight"); main.Width=1320; main.Height=840; RedesignChecks.Frames();
        Wait(repo.SetFolderIconAsync("Production","cloud")); Wait(repo.SetFolderIconAsync("Production/Linux","database"));
        Wait(((MainViewModel)main.DataContext!).LoadAsync()); RedesignChecks.Frames();
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"workspace-live-metrics.png"));
        var bar=main.GetVisualDescendants().OfType<ServerMetricsBar>().Single();
        if(!bar.IsVisible || bar.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="Metric_ram").Text?.Contains("GiB")!=true)throw new Exception("Active metrics did not render");
        main.Width=900; main.Height=600; RedesignChecks.Frames();
        var status=main.GetVisualDescendants().OfType<Grid>().Single(g=>g.Name=="WorkspaceStatusBar");
        foreach(var text in status.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.IsEffectivelyVisible))
        {
            var point=text.TranslatePoint(default,status)!.Value;
            if(text.Bounds.Width<=0 || point.X<0 || point.X+text.Bounds.Width>status.Bounds.Width+1)throw new Exception("900px status bar overlaps or hides connection status: "+text.Name);
        }
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"workspace-live-metrics-900.png")); main.Width=1320; main.Height=840;
        Console.WriteLine("PASS real Linux metrics without opening Monitoring: CPU/RAM/network rates/uptime/server clock; active-session status and 900px layout");
    }
}
