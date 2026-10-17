using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class NetworkCancellationChecks
{
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Verify(bool condition,string message) { if(!condition)throw new Exception(message); }
    private static void Until(Func<bool> condition)
    { var deadline=DateTime.UtcNow.AddSeconds(15); while(!condition() && DateTime.UtcNow<deadline)RedesignChecks.Frames(1); Verify(condition(),"Network cancellation fixture timed out"); }
    private static void Click(NetworkToolPane pane,string action)=>pane.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==action).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void ExpandSaved(NetworkToolPane pane) { pane.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header as string==AppText.T("Historia i kolekcje")).IsExpanded=true; RedesignChecks.Frames(3); }
    private sealed class Vault:ICredentialStore
    {
        internal readonly Dictionary<Guid,string> Values=[];
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Hold;
        public async Task<string?> GetAsync(Guid id,CancellationToken ct=default) { if(Hold) { Entered.TrySetResult(); await Release.Task; } return Values.GetValueOrDefault(id); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Values[id]=secret; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Values.Remove(id); return Task.CompletedTask; }
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var repo=new ProfileRepository(new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N")))); RedesignChecks.Wait(repo.InitializeAsync());
            var vault=new Vault(); var store=new NetworkToolsStore(repo,vault); var owner=new MainWindow(new(repo),repo,vault,new SshConnectionFactory(repo),new ExternalRdpClient()); owner.Show(); RedesignChecks.Frames(8);
            var subnet=new NetworkToolPane(owner,NetworkToolKind.Subnet,store,new NetworkToolsService(),[],""); var window=new Window { Content=subnet,Width=900,Height=700 }; window.Show(); RedesignChecks.Frames(4);
            var runs=0; subnet.PropertyChanged+=(_,e)=> { if(e.Property==NetworkToolPane.IsRunningProperty && subnet.IsRunning)runs++; };
            var target=Field<TextBox>(subnet,"target"); target.Text="192.0.2.0/24"; RedesignChecks.Frames(1);
            var delay=Field<CancellationTokenSource>(subnet,"subnetDelay"); var manual=subnet.StartAsync();
            Verify(delay.IsCancellationRequested,"Manual Start leaves an automatic subnet run queued"); RedesignChecks.Wait(manual); RedesignChecks.Frames(20); Verify(runs==1,"Automatic recalculation replaced an explicit result");
            target.Text="198.51.100.0/24"; RedesignChecks.Frames(1); Verify(Field<Button>(subnet,"stop").IsEnabled,"Stop cannot cancel a pending subnet calculation"); Click(subnet,"Stop"); RedesignChecks.Wait(subnet.Shutdown); RedesignChecks.Frames(16); Verify(runs==1 && subnet.StatusText==AppText.T("Zatrzymano"),"Stop did not cancel the delayed subnet calculation");
            target.Text="203.0.113.0/24"; RedesignChecks.Frames(1); target.Focus(); window.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,null); RedesignChecks.Wait(subnet.Shutdown); RedesignChecks.Frames(16); Verify(runs==1,"Escape left a subnet calculation queued");
            for(var i=0;i<8;i++) { target.Text=$"192.0.{i}.0/24"; RedesignChecks.Frames(1); }
            RedesignChecks.Wait(subnet.Shutdown); Verify(runs==2,"Rapid subnet edits were not coalesced");
            target.Text="198.51.100.1/32"; RedesignChecks.Frames(1); subnet.Dispose(); var draining=subnet.Shutdown; RedesignChecks.Wait(draining); var closedStatus=subnet.StatusText; RedesignChecks.Frames(16); Verify(runs==2 && subnet.StatusText==closedStatus,"Closed subnet pane received a late update"); window.Close();

            RedesignChecks.Wait(store.SaveCollectionAsync("Held collection",new() { Tool=NetworkToolKind.Http,Target="https://example.test" },new("fixture secret")));
            var http=new NetworkToolPane(owner,NetworkToolKind.Http,store,new NetworkToolsService(),[],""); window=new Window { Content=http,Width=900,Height=700 }; window.Show();
            var collections=Field<ComboBox>(http,"collectionChoice"); Until(()=>collections.ItemCount==1); ExpandSaved(http); collections.SelectedIndex=0; vault.Hold=true; Click(http,"Wczytaj kolekcję"); RedesignChecks.Wait(vault.Entered.Task);
            var request=http.ReadRequest(); var status=http.StatusText; http.Dispose(); draining=http.Shutdown;
            try { Verify(!draining.IsCompleted,"Shutdown ignores the pending collection read"); }
            finally { vault.Release.TrySetResult(); RedesignChecks.Wait(draining); }
            Verify(http.ReadRequest()==request && http.StatusText==status && Field<Dictionary<string,TextBox>>(http,"secretFields").Values.All(box=>string.IsNullOrEmpty(box.Text)),"Collection read populated a closed pane"); window.Close();

            var prompt=new NetworkToolPane(owner,NetworkToolKind.Http,store,new NetworkToolsService(),[],""); window=new Window { Content=prompt,Width=900,Height=700 }; window.Show(); RedesignChecks.Frames(4); prompt.ApplyRequest(new() { Tool=NetworkToolKind.Http,Target="https://example.test" });
            ExpandSaved(prompt); Click(prompt,"Zapisz kolekcję"); Until(()=>window.OwnedWindows.Count==1); prompt.Dispose(); RedesignChecks.Wait(prompt.Shutdown); Verify(window.OwnedWindows.Count==0,"Closing a tool left its save prompt open"); window.Close(); Verify(RedesignChecksValue(store.CollectionsAsync()).Count==1,"Cancelling save wrote another collection");

            using(var server=new HeldHttpServer())
            {
                for(var i=0;i<12;i++)
                {
                    var pane=new NetworkToolPane(owner,NetworkToolKind.Http,store,new NetworkToolsService(),[],""); window=new Window { Content=pane,Width=700,Height=500 }; window.Show(); pane.ApplyRequest(new() { Tool=NetworkToolKind.Http,Target=server.Url,TimeoutMs=10000 });
                    _=pane.StartAsync(); Until(()=>server.Accepted>i); if(i%2==0)Click(pane,"Stop"); pane.Dispose(); RedesignChecks.Wait(pane.Shutdown); Verify(!pane.IsRunning,"Closed HTTP request stayed active"); window.Close();
                }
                var retry=new NetworkToolPane(owner,NetworkToolKind.Http,store,new NetworkToolsService(),[],""); window=new Window { Content=retry,Width=700,Height=500 }; window.Show(); retry.ApplyRequest(new() { Tool=NetworkToolKind.Http,Target=server.Url,TimeoutMs=10000 });
                _=retry.StartAsync(); Until(()=>server.Accepted==13); Click(retry,"Restart"); var restarting=Field<Task>(retry,"restarting"); Click(retry,"Restart"); Verify(ReferenceEquals(restarting,Field<Task>(retry,"restarting")),"Repeated Restart queued independent operations"); Click(retry,"Stop"); RedesignChecks.Wait(retry.Shutdown); RedesignChecks.Frames(8); Verify(server.Accepted==13 && !retry.IsRunning,"Stop allowed a pending Restart to start another request");
                Click(retry,"Restart"); Click(retry,"Restart"); Until(()=>server.Accepted==14); Click(retry,"Stop"); RedesignChecks.Wait(retry.Shutdown); Verify(server.Accepted==14,"Repeated Restart started multiple requests"); retry.Dispose(); RedesignChecks.Wait(retry.Shutdown); window.Close();
                Until(()=>server.Active==0);
            }
            owner.Close(); RedesignChecks.Frames(8); Console.WriteLine("PASS "+language+": manual Start supersedes debounce, Stop/Escape cancel pending subnet work, rapid edits coalesce, close drains collection reads without late secrets/UI, prompt cancellation, repeated Restart/Stop and 14 real loopback HTTP runs release sockets");
        }
        AppText.SetLanguage("pl");
    }
    private static T RedesignChecksValue<T>(Task<T> task) { RedesignChecks.Wait(task); return task.GetAwaiter().GetResult(); }
    private sealed class HeldHttpServer:IDisposable
    {
        private readonly TcpListener listener=new(IPAddress.Loopback,0);
        private readonly CancellationTokenSource lifetime=new();
        private readonly ConcurrentBag<Task> clients=[];
        private readonly Task accepting;
        private int accepted,active;
        internal int Accepted=>Volatile.Read(ref accepted);
        internal int Active=>Volatile.Read(ref active);
        internal string Url { get; }
        internal HeldHttpServer()
        {
            listener.Start(); Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;
            // Socket cleanup must not wait for a continuation on the UI dispatcher.
            accepting=Task.Run(AcceptAsync);
        }
        private async Task AcceptAsync()
        {
            try { while(!lifetime.IsCancellationRequested)clients.Add(HoldAsync(await listener.AcceptTcpClientAsync(lifetime.Token))); }
            catch(Exception ex) when(ex is OperationCanceledException or SocketException && lifetime.IsCancellationRequested) { }
        }
        private async Task HoldAsync(TcpClient client)
        {
            Interlocked.Increment(ref active); using(client)
            {
                try { var bytes=new byte[1024]; var stream=client.GetStream(); if(await stream.ReadAsync(bytes,lifetime.Token)>0)Interlocked.Increment(ref accepted); while(await stream.ReadAsync(bytes,lifetime.Token)>0) { } }
                catch(Exception ex) when(ex is IOException or OperationCanceledException) { }
                finally { Interlocked.Decrement(ref active); }
            }
        }
        public void Dispose() { lifetime.Cancel(); listener.Stop(); accepting.GetAwaiter().GetResult(); Task.WhenAll(clients).GetAwaiter().GetResult(); lifetime.Dispose(); }
    }
}
