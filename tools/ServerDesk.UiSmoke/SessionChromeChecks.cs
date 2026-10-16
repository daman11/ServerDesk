using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Avalonia.Media;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SessionChromeChecks
{
    internal static void Preview(MainWindow main,ProfileRepository repo,string root)
    {
        var originalTheme=DesignSystem.CurrentId;
        var profile=new ServerProfile { Name="test",Host="design-test.invalid",Username="admin",Color="#E06C75",IconId="security" };
        var pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane);
        var busy=pane.WaitForCredentials();
        var status=pane.GetVisualDescendants().OfType<Grid>().Single(g=>g.Name=="SessionOperationStatus");
        var indicator=status.Children.OfType<BusyIndicator>().Single(); var label=status.Children.OfType<TextBlock>().Single();
        void VerifyStatus()
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); RedesignChecks.Frames(1);
            var a=indicator.TranslatePoint(default,pane)!.Value; var b=label.TranslatePoint(default,pane)!.Value;
            if(!status.IsVisible || !status.ClipToBounds || !pane.ClipToBounds || indicator.Bounds.Width>16.5 || a.X<0 || a.Y<0 || a.X+indicator.Bounds.Width>b.X || a.X+indicator.Bounds.Width>pane.Bounds.Width || a.Y+indicator.Bounds.Height>pane.Bounds.Height)throw new Exception("Loading indicator escapes its session or overlaps status text");
            if(pane.GetVisualDescendants().OfType<ProgressBar>().Any(p=>p.IsIndeterminate))throw new Exception("Oversized indeterminate track still present");
            if(main.GetVisualDescendants().OfType<Border>().Any(b=>b.Name=="PART_SelectedPipe" && b.IsVisible))throw new Exception("Active tab underline still visible");
        }
        RedesignChecks.Frames(20);
        foreach(var theme in new[]{"midnight","dark","light"})
        {
            DesignSystem.Apply(theme); RedesignChecks.Frames(); var start=indicator.GetValue(BusyIndicator.AngleProperty);
            for(var i=0;i<12;i++)VerifyStatus(); if(Math.Abs(indicator.GetValue(BusyIndicator.AngleProperty)-start)<.01)throw new Exception("Loading indicator is not animated");
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,"session-loading-"+theme+".png"));
        }
        var tab=main.GetVisualDescendants().OfType<TabItem>().Single(t=>t.Tag==pane);
        var marker=tab.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="SessionColorMarker"); pane.UpdatePresentation(profile with { Color="#69CFB6" }); RedesignChecks.Frames();
        if(marker.Background is not ISolidColorBrush brush || brush.Color!=Color.Parse("#69CFB6") || marker.Bounds.Width>6.5 || marker.Bounds.Height>6.5)throw new Exception("Profile color marker lost live appearance or became a bar");
        var other=new SessionPane(main,profile with { Id=Guid.NewGuid(),Name="Druga sesja",Color="#E5B65C" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(other); var otherBusy=other.WaitForCredentials();
        main.SetWorkspaceMode(WorkspaceMode.Columns); main.FocusSession(pane); main.Width=900; main.Height=600; RedesignChecks.Frames();
        for(var i=0;i<12;i++)VerifyStatus(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"session-loading-split-900.png"));
        var tabs=main.GetVisualDescendants().OfType<TabItem>().ToArray(); if(tabs.Count(t=>t.Classes.Contains("current"))!=1 || !tabs.Single(t=>t.Tag==pane).Classes.Contains("current"))throw new Exception("Focused group has no distinct active tab");
        main.FocusSession(other); RedesignChecks.Frames(); if(!tabs.Single(t=>t.Tag==other).Classes.Contains("current") || tabs.Single(t=>t.Tag==pane).Classes.Contains("current"))throw new Exception("Focus does not update tab emphasis");
        main.SetRenderScaling(2); RedesignChecks.Frames(); VerifyStatus(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"session-loading-dpi200.png")); main.SetRenderScaling(1);
        busy.Dispose(); otherBusy.Dispose(); RedesignChecks.Frames(); if(status.IsVisible || indicator.Classes.Contains("busy"))throw new Exception("Finished operation kept loading indicator active");
        pane.UpdatePresentation(profile with { Color="" }); RedesignChecks.Frames(); if(marker.IsVisible)throw new Exception("Cleared profile color kept marker");
        main.Width=1320; main.Height=840; main.SetWorkspaceMode(WorkspaceMode.Single); DesignSystem.Apply(originalTheme);
        Console.WriteLine("PASS animated loading indicator stays inside session without text overlap across frames, Midnight/Dark/Light, 900px split and 200% rendering; no tab underline, correct focused-group emphasis, live/cleared color marker and cleanup after operation");
        main.Close(); RedesignChecks.Frames(); var confirmation=main.OwnedWindows.Single(); confirmation.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(20);
    }
}
