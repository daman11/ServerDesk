using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Iciclecreek.Terminal;

internal static class VisualSurfaceChecks
{
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static void Call(SessionPane pane,string name,params object[] args)=>typeof(SessionPane).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,args);
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        var saved=DesignSystem.Preferences;
        using var pane=new SessionPane(main,new() { Name="Visual fixture",Host="fixture.invalid",Username="test" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
        var w=new Window { Width=1200,Height=760,Content=pane }; w.Show(); RedesignChecks.Frames();
        // Visual fixtures exercise actual SFTP/monitoring controls without a server or filesystem mutations.
        pane.Children.OfType<TerminalControl>().Single().IsVisible=false;
        var files=Field<Grid>(pane,"files"); var info=Field<ScrollViewer>(pane,"info");
        var entries=new List<FileEntry> {
            new("documents","/fixture/documents",true,false,0,DateTime.Now,"755"),
            new("nginx-production.conf","/fixture/nginx-production.conf",false,false,12345,DateTime.Now,"644"),
            new("serverdesk.log","/fixture/serverdesk.log",false,false,4567890,DateTime.Now,"640")
        };
        Field<List<FileEntry>>(pane,"localEntries").AddRange(entries); Field<List<FileEntry>>(pane,"remoteEntries").AddRange(entries);
        Field<TextBox>(pane,"remotePath").Text="/srv/production/config"; Call(pane,"FilterFiles");
        var snapshot=new ServerSnapshot("fixture",new(120,36),31,new(8L<<30,5L<<30,0,0),"","","") { SystemDetails=new("fixture","Debian GNU/Linux 13 (trixie)","6.12", "test", "",TimeSpan.FromDays(38),""),DiskDetails=[new("/dev/sda","ext4","/",100L<<30,45L<<30,55L<<30,45)] };
        Call(pane,"RenderDetails",snapshot);
        Field<TextBlock>(pane,"system").Text="fixture · Debian GNU/Linux 13 (trixie)\nLinux 6.12 · Uptime 38 d";
        Field<TextBlock>(pane,"cpu").Text="CPU: 31% · Load 0.8"; Field<TextBlock>(pane,"ram").Text="3 GiB / 8 GiB";
        Field<ProgressBar>(pane,"cpuBar").Value=31; Field<ProgressBar>(pane,"ramBar").Value=37;
        Field<Avalonia.Controls.Shapes.Polyline>(pane,"cpuChart").Points=new Avalonia.Collections.AvaloniaList<Point>(Enumerable.Range(0,60).Select(i=>new Point(i*8,40+Math.Sin(i*.3)*20)));
        foreach(var theme in new[]{"dark","light","graphite"})foreach(var width in new[]{650,1200})
        {
            DesignSystem.ApplyPreferences(saved with { InterfaceFontSize=16 }); DesignSystem.Apply(theme); w.Width=width;
            files.IsVisible=true; info.IsVisible=false; RedesignChecks.Frames();
            var toolbar=w.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="SessionToolbarSurface");
            if(toolbar.Bounds.Bottom>files.Bounds.Y+1)throw new Exception("Session toolbar overlaps SFTP");
            foreach(var list in new[]{Field<ListBox>(pane,"localList"),Field<ListBox>(pane,"remoteList")}.Where(c=>c.IsEffectivelyVisible))
            { var p=list.TranslatePoint(default,w)!.Value; if(p.X<0 || p.X+list.Bounds.Width>w.Bounds.Width+1)throw new Exception("SFTP list escapes viewport"); }
            w.CaptureRenderedFrame()!.Save(Path.Combine(root,$"sftp-{theme}-{width}.png"));
            files.IsVisible=false; info.IsVisible=true; RedesignChecks.Frames();
            foreach(var card in info.GetVisualDescendants().OfType<Border>().Where(b=>b.ClipToBounds && b.BorderThickness.Top==1))
            { var p=card.TranslatePoint(default,w)!.Value; if(p.X<0 || p.X+card.Bounds.Width>w.Bounds.Width+1)throw new Exception("Monitoring card escapes viewport"); }
            w.CaptureRenderedFrame()!.Save(Path.Combine(root,$"monitoring-{theme}-{width}.png"));
        }
        w.Close(); DesignSystem.ApplyPreferences(saved); DesignSystem.Apply("dark");
        var menu=main.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Menu");
        foreach(var theme in new[]{"dark","light","graphite"})
        {
            DesignSystem.Apply(theme); menu.ContextMenu!.Open(menu); RedesignChecks.Frames();
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"menu-{theme}.png")); menu.ContextMenu.Close();
        }
        DesignSystem.Apply("dark");
        Console.WriteLine("PASS visual surfaces: actual SFTP and monitoring controls with explicit fixtures, toolbar/list/card bounds, 650/1200px, font16, Dark/Light/Graphite; native application menu rendering; no remote connection");
    }
}
