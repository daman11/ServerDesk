using System.Net;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ConnectionControlChecks
{
    private static T Field<T>(Window w,string name) where T:Control=>w.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal static void Until(Func<bool> condition)
    {
        var deadline=DateTime.UtcNow.AddSeconds(12);
        while(!condition()) { if(DateTime.UtcNow>deadline)throw new TimeoutException("Connection-control check timed out"); RedesignChecks.Frames(1); }
        RedesignChecks.Frames(2);
    }
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        foreach(var protocol in new[]{Protocol.SSH,Protocol.VNC,Protocol.RDP})
        {
            using var server=new TcpListener(IPAddress.Loopback,0); server.Start();
            var p=new ServerProfile { Name="Anulowanie "+protocol,Protocol=protocol,Host="127.0.0.1",Port=((IPEndPoint)server.LocalEndpoint).Port,Username="fixture",Vnc=new() { Authentication=VncAuthentication.None } };
            using var pane=new SessionPane(main,p,new SshConnectionFactory(repo),new PendingRdp(),repo);
            var w=new Window { Width=780,Height=500,Content=pane }; w.Show(); RedesignChecks.Frames();
            var accepted=server.AcceptTcpClientAsync(); var task=pane.ConnectAsync("",CancellationToken.None); Until(()=>Field<Button>(w,"CancelConnection").IsEffectivelyVisible);
            if(protocol!=Protocol.RDP)Until(()=>accepted.IsCompleted); using var socket=accepted.IsCompleted?accepted.Result:null;
            w.CaptureRenderedFrame()!.Save(Path.Combine(root,"connecting-"+protocol+".png"));
            Click(Field<Button>(w,"CancelConnection")); Until(()=>task.IsCompleted); task.GetAwaiter().GetResult();
            if(pane.IsBusy || pane.HasActiveClient || pane.VisualState!=SessionVisualState.Idle || Field<Button>(w,"CancelConnection").IsVisible)throw new Exception(protocol+" cancellation left a busy or failed pane");
            if(repo.HistoryAsync(profileId:p.Id).GetAwaiter().GetResult().Count!=0)throw new Exception("Cancelled attempt entered history");
            using var outer=new CancellationTokenSource(); task=pane.ConnectAsync("",outer.Token); Until(()=>Field<Button>(w,"CancelConnection").IsVisible); outer.Cancel(); Until(()=>task.IsCompleted);
            try { task.GetAwaiter().GetResult(); throw new Exception("Outer cancellation was swallowed"); } catch(OperationCanceledException) { }
            if(pane.VisualState!=SessionVisualState.Idle)throw new Exception("Outer cancellation marked pane failed");
            if(protocol==Protocol.VNC)
            {
                server.Stop(); using var retry=new VncTestServer(port:p.Port); RedesignChecks.Wait(pane.ReconnectAsync());
                if(!pane.IsConnected || repo.HistoryAsync(profileId:p.Id).GetAwaiter().GetResult().Count!=1)throw new Exception("VNC retry after cancellation did not connect and record once");
            }
            else
            {
                task=pane.ConnectAsync("",CancellationToken.None); Until(()=>Field<Button>(w,"CancelConnection").IsVisible); var closing=pane.CloseAsync(); Until(()=>closing.IsCompleted && task.IsCompleted); closing.GetAwaiter().GetResult();
                try { task.GetAwaiter().GetResult(); throw new Exception("Closing pending attempt did not propagate cancellation"); } catch(OperationCanceledException) { }
            }
            RedesignChecks.Wait(pane.CloseAsync()); w.Close();
        }
        Console.WriteLine("PASS SSH/VNC stalled loopback handshake and RDP pending preflight: cancel button, prompt completion, no history/error state, outer token propagation, clean close");
        using(var cancellation=new CancellationTokenSource())
        {
            var confirmation=Dialogs.Confirm(main,"Test anulowania klucza hosta","Fingerprint fixture",cancellation.Token); RedesignChecks.Frames(); cancellation.Cancel(); RedesignChecks.Wait(confirmation);
            if(confirmation.Result || main.OwnedWindows.Count!=0)throw new Exception("Cancelled host-key confirmation remained visible or trusted key");
        }
        using(var server=new VncTestServer())
        {
            var p=new ServerProfile { Name="Żółty pulpit",Protocol=Protocol.VNC,Host="127.0.0.1",Port=server.Port,Vnc=new() { Authentication=VncAuthentication.None } };
            RedesignChecks.Wait(repo.SaveAsync(p)); using var pane=new SessionPane(main,p,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); var w=new Window { Width=780,Height=500,Content=pane }; w.Show(); RedesignChecks.Frames(); RedesignChecks.Wait(pane.ConnectAsync("",CancellationToken.None));
            var history=repo.HistoryAsync(profileId:p.Id).GetAwaiter().GetResult(); if(history.Count!=1 || !history[0].CanReconnect || !repo.ListAsync().Result.Single(x=>x.Id==p.Id).LastConnected.HasValue)throw new Exception("Actual VNC success did not enter history exactly once");
            RedesignChecks.Wait(pane.CloseAsync()); w.Close();
            RedesignChecks.Wait(repo.SaveAsync(p with { Host="changed.test" }));
        }
        History(main,repo,root);
    }
    private static void History(MainWindow main,ProfileRepository repo,string root)
    {
        var serial=new ServerProfile { Name="Konsola switcha",Protocol=Protocol.Serial,Serial=new() { PortName="COM123" } };
        RedesignChecks.Wait(repo.SaveAsync(serial)); RedesignChecks.Wait(repo.ConnectedAsync(serial));
        var open=Dialogs.History(main,repo); RedesignChecks.Frames(); var w=main.OwnedWindows.Single();
        Until(()=>Field<ListBox>(w,"HistoryEntries").ItemCount>=2);
        var list=Field<ListBox>(w,"HistoryEntries"); list.SelectedItem=list.Items.OfType<ConnectionHistoryEntry>().Single(e=>e.Name=="Żółty pulpit");
        if(Field<Button>(w,"HistoryConnect").IsEnabled)throw new Exception("Changed destination reconnect enabled");
        var previous=DesignSystem.Preferences;
        foreach(var theme in new[]{"dark","light"})foreach(var width in new[]{600,780})
        {
            DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(previous with { InterfaceFontSize=16 }); w.Width=width; w.Height=460; RedesignChecks.Frames();
            foreach(var name in new[]{"HistorySearch","HistoryProtocol","HistoryMore","HistoryConnect"})
            { var c=Field<Control>(w,name); var point=c.TranslatePoint(default,w)!.Value; if(point.X<0 || point.Y<0 || point.X+c.Bounds.Width>w.ClientSize.Width+1 || point.Y+c.Bounds.Height>w.ClientSize.Height+1)throw new Exception("History control escapes window: "+name); }
            w.CaptureRenderedFrame()!.Save(Path.Combine(root,$"history-{theme}-{width}.png"));
        }
        Field<TextBox>(w,"HistorySearch").Text="żÓŁTY"; Field<ComboBox>(w,"HistoryProtocol").SelectedIndex=3; Until(()=>list.ItemCount==1);
        if(list.Items.OfType<ConnectionHistoryEntry>().Single().Protocol!=Protocol.VNC)throw new Exception("History filter returned wrong protocol");
        Field<TextBox>(w,"HistorySearch").Text="missing"; Field<TextBox>(w,"HistorySearch").Text="COM123"; Field<ComboBox>(w,"HistoryProtocol").SelectedIndex=4; Until(()=>list.ItemCount==1 && list.Items.OfType<ConnectionHistoryEntry>().Single().Protocol==Protocol.Serial);
        list.SelectedIndex=0; if(!Field<Button>(w,"HistoryConnect").IsEnabled)throw new Exception("Unchanged destination cannot reconnect"); Click(Field<Button>(w,"HistoryConnect")); RedesignChecks.Wait(open); if(open.Result?.Id!=serial.Id)throw new Exception("History returned incorrect profile");
        open=Dialogs.History(main,repo); RedesignChecks.Frames(); w=main.OwnedWindows.Single(); Field<TextBox>(w,"HistorySearch").Text="close during debounce"; w.Close(); RedesignChecks.Wait(open); RedesignChecks.Frames(15);
        DesignSystem.Apply("dark"); DesignSystem.ApplyPreferences(previous);
        Console.WriteLine("PASS unified history: actual VNC success/recency, changed-target block, Unicode search, protocol filter, rapid filter changes, safe reconnect, close during debounce; dark/light at 600/780px and font16");
    }
    private sealed class PendingRdp:IRdpClient
    {
        public RdpLaunch Prepare(ServerProfile p,RdpOptions o)=>throw new Exception("Cancelled RDP preflight launched a client");
        public async Task<RdpEndpointInfo?> CheckAsync(ServerProfile p,CancellationToken ct) { await Task.Delay(Timeout.Infinite,ct); return null; }
    }
}
