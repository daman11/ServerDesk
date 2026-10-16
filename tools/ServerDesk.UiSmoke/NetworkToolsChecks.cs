using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class NetworkToolsChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static object? Call(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,args);
    internal static void Run(string root)
    {
        var original=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N"))); var repository=new ProfileRepository(paths); RedesignChecks.Wait(repository.InitializeAsync()); var vm=new MainViewModel(repository);
            var main=new MainWindow(vm,repository,new CredentialStore(paths),new SshConnectionFactory(repository),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            if(vm.AllServers.Count!=0)throw new Exception("Network standalone fixture requires no saved servers");
            RedesignChecks.Wait((Task)Call(main,"SelectSectionAsync","network")!); RedesignChecks.Frames();
            if(!main.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="NetworkToolsSidebar").IsEffectivelyVisible || main.GetVisualDescendants().OfType<StackPanel>().Single(s=>s.Name=="ConnectionBrowserTools").IsEffectivelyVisible)throw new Exception("Network Tools navigation overlaps connection browser");
            var panes=Enum.GetValues<NetworkToolKind>().Select(tool=>main.OpenNetworkTool(tool)).ToArray();
            if(Field<TabControl>(main,"tabs").ItemCount!=9 || main.OpenSessions.Count!=0)throw new Exception("Network Tools bypassed workspace tabs or created fake connection sessions");
            foreach(var pane in panes)
            {
                main.FocusSession(pane);
                foreach(var theme in new[]{"graphite","light"})
                {
                    DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(original with { InterfaceFontSize=16 }); RedesignChecks.Frames();
                    if(pane.ReadRequest().Tool!=pane.Tool)throw new Exception("Tool form lost its type");
                    var start=pane.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Start"); var point=start.TranslatePoint(default,pane)!.Value;
                    if(!start.IsEffectivelyVisible || point.Y<0 || point.Y+start.Bounds.Height>pane.Bounds.Height || Field<ListBox>(pane,"resultList").Bounds.Height<40)throw new Exception("Tool actions or results disappear in normal layout");
                    main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"network-{pane.Tool}-{language}-{theme}.png"));
                }
                var expander=pane.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="NetworkParameters"); expander.IsExpanded=true; RedesignChecks.Frames();
                main.SetWorkspaceMode(WorkspaceMode.Columns); main.DockSession(pane,SessionDock.Secondary); main.Width=1000; main.Height=650; RedesignChecks.Frames();
                var run=pane.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Start"); var p=run.TranslatePoint(default,pane)!.Value; if(p.X<0 || p.Y<0 || p.Y+run.Bounds.Height>pane.Bounds.Height+1)throw new Exception("Split view hides fixed Start action");
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"network-{pane.Tool}-{language}-split.png")); main.DockSession(pane,SessionDock.Primary); main.SetWorkspaceMode(WorkspaceMode.Single); main.Width=1320; main.Height=840; expander.IsExpanded=false;
            }
            DesignSystem.ApplyPreferences(original); DesignSystem.Apply("dark");
            var http=panes.Single(p=>p.Tool==NetworkToolKind.Http); main.FocusSession(http); RedesignChecks.Frames(); http.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex=1; Field<TextBox>(http,"response").Text="{\"name\":\"fixture\",\"number\":42,\"enabled\":true}"; RedesignChecks.Frames();
            if(!http.GetVisualDescendants().OfType<SelectableTextBlock>().Any(text=>text.Inlines?.Count>3))throw new Exception("JSON syntax preview did not produce colored text runs");
            if(language=="en" && http.GetVisualDescendants().OfType<Expander>().Any(e=>e.Header as string is "Parametry" or "Historia i kolekcje"))throw new Exception("Network Tools localization left Polish section labels");
            var a=panes.Single(p=>p.Tool==NetworkToolKind.Ping); var b=main.OpenNetworkTool(NetworkToolKind.Ping);
            a.ApplyRequest(new() { Tool=NetworkToolKind.Ping,Target="127.0.0.1",Count=2,IntervalMs=100 }); b.ApplyRequest(new() { Tool=NetworkToolKind.Ping,Target="localhost",Count=3,IntervalMs=100 });
            RedesignChecks.Wait(Task.WhenAll(a.StartAsync(),b.StartAsync()));
            if(Field<ObservableCollection<NetworkRow>>(a,"rows").Count!=2 || Field<ObservableCollection<NetworkRow>>(b,"rows").Count!=3 || a.ReadRequest().Target==b.ReadRequest().Target)throw new Exception("Concurrent Ping tabs share state or failed actual ICMP");
            var subnet=panes.Single(p=>p.Tool==NetworkToolKind.Subnet); main.FocusSession(subnet); Field<TextBox>(subnet,"target").Text="192.0.2.10/24"; RedesignChecks.Frames(25);
            if(!Field<ObservableCollection<NetworkRow>>(subnet,"rows").Any(r=>r.Cells.Contains("192.0.2.0/24")))throw new Exception("Subnet did not recalculate after typing");
            a.ApplyRequest(new() { Tool=NetworkToolKind.Ping,Target="127.0.0.1",Continuous=true,IntervalMs=100 }); _=a.StartAsync(); RedesignChecks.Frames(8); main.RemovePane(a); RedesignChecks.Wait(a.Shutdown); if(a.IsRunning)throw new Exception("Closing tab did not cancel Ping");
            var saved=new ServerProfile { Name="Context server",Host="127.0.0.1",Protocol=Protocol.RDP,Port=3389,Username="unchanged" }; RedesignChecks.Wait(repository.SaveAsync(saved)); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames();
            foreach(var kind in Enum.GetValues<NetworkToolKind>())
            { var pane=main.OpenNetworkTool(kind,saved); if(kind is not NetworkToolKind.Wake && !pane.ReadRequest().Target.Contains(saved.Host))throw new Exception("Context target not passed"); main.RemovePane(pane); }
            if(repository.ListAsync().GetAwaiter().GetResult().Single()!=saved)throw new Exception("Tools mutated saved connection");
            RedesignChecks.Wait((Task)Call(main,"SelectSectionAsync","connections")!); RedesignChecks.Frames();
            var node=Field<TreeView>(main,"tree").Items.OfType<TreeViewItem>().Single(); node.ContextMenu!.Open(node); RedesignChecks.Frames(3); var network=node.ContextMenu.Items.OfType<MenuItem>().Single(m=>m.Header as string=="Network Tools"); if(network.Items.Count!=9)throw new Exception("Context menu does not expose all tools"); node.ContextMenu.Close();
            main.FocusSession(b); main.DockSession(b,SessionDock.Floating); RedesignChecks.Frames(); if(main.Workspace.Location(b.WorkspaceId)!=SessionDock.Floating)throw new Exception("Network tool cannot detach"); main.DockSession(b,SessionDock.Primary);
            RedesignChecks.Wait(main.SaveSessionSnapshotAsync()); if(WorkspaceSnapshot.Parse(repository.SettingAsync("lastWorkspace").GetAwaiter().GetResult()!).Sessions.Count!=0)throw new Exception("Tool snapshot was saved as a connection profile");
            foreach(var pane in Field<List<NetworkToolPane>>(main,"networkPanes").ToArray())main.RemovePane(pane); main.Close(); RedesignChecks.Frames();
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(original); DesignSystem.Apply("dark");
        Console.WriteLine("PASS Network Tools standalone with zero hosts, all nine forms/tabs, editable context targets and untouched profile, concurrent real ICMP instances, automatic subnet calculation, cancel/dispose on close, existing split/floating docking and snapshot separation, PL/EN Graphite/Light font16 and narrow split actions");
    }
}
