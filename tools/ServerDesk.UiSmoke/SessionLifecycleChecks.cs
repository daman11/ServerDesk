using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SessionLifecycleChecks
{
    private static void Wait(Task task)
    {
        var limit=DateTime.UtcNow.AddSeconds(40);
        while(!task.IsCompleted && DateTime.UtcNow<limit)RedesignChecks.Frames(1);
        if(!task.IsCompleted)throw new TimeoutException("Owned session lifecycle task did not finish");
        task.GetAwaiter().GetResult();
    }
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(owner)!;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Pane,WeakReference ConnectionLifetime) FailedPane(MainWindow main,ProfileRepository repo,SshConnectionFactory factory,CancellationToken parent)
    {
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var pane=new SessionPane(main,new ServerProfile { Name="Owned failed session",Host="127.0.0.1",Port=port,Username="fixture" },factory,new ExternalRdpClient(),repo);
        main.RegisterPane(pane);
        try { Wait(pane.ConnectAsync("",parent)); throw new Exception("Closed fixture port unexpectedly connected"); }
        catch(Exception ex) when(ex is SocketException or Renci.SshNet.Common.SshConnectionException) { }
        var reference=new WeakReference(pane); var linked=new WeakReference(Field<CancellationTokenSource>(pane,"connectionLifetime"));
        main.RemovePane(pane); Wait(pane.CloseAsync()); return (reference,linked);
    }
    internal static void Run(string root)
    {
        var paths=new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        var factory=new SshConnectionFactory(repo); var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        using var parent=new CancellationTokenSource();
        var closed=Enumerable.Range(0,8).Select(_=>FailedPane(main,repo,factory,parent.Token)).ToArray();
        main.Focus(); RedesignChecks.Frames(60);
        for(var i=0;i<3;i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); RedesignChecks.Frames(3); }
        var panes=closed.Count(p=>p.Pane.IsAlive); var sources=closed.Count(p=>p.ConnectionLifetime.IsAlive);
        Console.WriteLine($"INFO closed failed sessions: panes alive={panes}, linked cancellation sources alive={sources}/8");
        GC.KeepAlive(parent); main.Close(); RedesignChecks.Frames();
        if(sources!=0)throw new Exception("Closed sessions leave linked cancellation sources registered on the parent lifetime");
        if(panes>1)throw new Exception("Closed session controls remain retained after background work should have stopped");
        Console.WriteLine("PASS failed-session close unregisters linked lifetimes and closed controls can be collected");
    }
}
