using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class StabilityChecks
{
    private static void Click(Window window,string action)
    { window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==action).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(2); }
    internal static void Run(ProfileRepository repository,string root)
    {
        var originalLanguage=AppText.Language;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var store=new RecoveryStore();
            var owner=new MainWindow(new MainViewModel(repository),repository,store,new SshConnectionFactory(repository),new ExternalRdpClient()); owner.Show(); RedesignChecks.Frames();
            var profile=new ServerProfile { Name="Recovery fixture",Host="fixture.invalid",Username="test" };
            Task<string?> Prompt()=>(Task<string?>)typeof(MainWindow).GetMethod("SessionSecretAsync",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,[profile])!;
            Window Dialog()
            {
                RedesignChecks.Frames(); var dialog=owner.OwnedWindows.Single();
                var warning=dialog.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="CredentialRecoveryNotice");
                if(!warning.IsEffectivelyVisible || warning.Text!=new CredentialReadException().Message || dialog.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked==true)throw new Exception("Credential recovery did not explain safe manual entry");
                return dialog;
            }
            try
            {
                var cancel=Prompt(); var dialog=Dialog(); dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"recovery-"+language+".png")); Click(dialog,"Anuluj"); RedesignChecks.Wait(cancel);
                if(cancel.Result!=null || store.Saves!=0)throw new Exception("Cancelling credential recovery changed the store");
                var manual=Prompt(); dialog=Dialog(); var input=dialog.GetVisualDescendants().OfType<TextBox>().Single();
                if(input.PasswordChar=='\0')throw new Exception("Recovery password is not masked"); input.Text="manual-fixture"; Click(dialog,"Połącz"); RedesignChecks.Wait(manual);
                if(manual.Result!="manual-fixture" || store.Saves!=0)throw new Exception("Manual recovery unexpectedly persisted the password");
                var replace=Prompt(); dialog=Dialog(); dialog.GetVisualDescendants().OfType<TextBox>().Single().Text="replacement-fixture"; dialog.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=true; Click(dialog,"Połącz"); RedesignChecks.Wait(replace);
                if(replace.Result!="replacement-fixture" || store.Saves!=1)throw new Exception("Explicit recovery save did not update the store");
                var subsequent=Prompt(); RedesignChecks.Wait(subsequent); if(subsequent.Result!="replacement-fixture" || owner.OwnedWindows.Count!=0)throw new Exception("Recovered saved password cannot be read");
            }
            finally { owner.Close(); RedesignChecks.Frames(); }
        }
        AppText.SetLanguage(originalLanguage);
        Console.WriteLine("PASS PL/EN credential recovery: visible explanation, masked input, cancel preserves store, manual use does not save, explicit remember replaces and can be read again");

        var main=new MainWindow(new MainViewModel(repository),repository,new RecoveryStore(),new SshConnectionFactory(repository),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames();
        try
        {
            foreach(var fault in new[]{VncTestFault.SilentRaw,VncTestFault.CompressedLength})
            {
                using var server=new VncTestServer(fault:fault); var profile=new ServerProfile { Name="VNC stability",Host="127.0.0.1",Port=server.Port,Protocol=Protocol.VNC,Vnc=new() { Authentication=VncAuthentication.None } };
                using var pane=new SessionPane(main,profile,new SshConnectionFactory(repository),new ExternalRdpClient(),repository);
                var window=new Window { Content=pane,Width=850,Height=550 }; window.Show(); RedesignChecks.Frames();
                try
                {
                    RedesignChecks.Wait(pane.ConnectAsync("",CancellationToken.None));
                    if(fault==VncTestFault.SilentRaw)
                    {
                        RedesignChecks.Wait(server.PartialFrameSent.Task);
                        var session=(VncSession)typeof(SessionPane).GetField("vncSession",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(pane)!;
                        var sync=typeof(VncFramebuffer).GetField("sync",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session.Framebuffer)!;
                        bool Busy() { if(!Monitor.TryEnter(sync))return true; Monitor.Exit(sync); return false; }
                        Until(Busy); var heartbeat=false; Dispatcher.UIThread.Post(()=>heartbeat=true); window.Width=720; RedesignChecks.Frames();
                        if(!heartbeat)throw new Exception("Silent VNC peer blocked UI dispatcher");
                        Click(window,"Rozłącz"); Until(()=>!pane.IsConnected && !pane.IsBusy && pane.VisualState==SessionVisualState.Closed);
                    }
                    else { Until(()=>pane.VisualState==SessionVisualState.Failed); if(pane.IsConnected || pane.IsBusy)throw new Exception("Malformed VNC retained live/busy state"); }
                    RedesignChecks.Wait(pane.CloseAsync());
                }
                finally { window.Close(); }
            }
        }
        finally { main.Close(); RedesignChecks.Frames(); }
        Console.WriteLine("PASS actual VNC silent mid-frame: receive lock occupied, UI heartbeat/resize/disconnect remain responsive; malformed packet ends in Failed instead of connected/normal close");
    }
    private static void Until(Func<bool> ready)
    { var end=DateTime.UtcNow.AddSeconds(5); while(!ready() && DateTime.UtcNow<end)RedesignChecks.Frames(1); if(!ready())throw new TimeoutException("UI stability condition not reached"); }
    private sealed class RecoveryStore : ICredentialStore
    {
        private string? secret;
        internal int Saves;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>secret==null?Task.FromException<string?>(new CredentialReadException(new InvalidDataException("Fixture corruption"))):Task.FromResult<string?>(secret);
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { Saves++; secret=value; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>Task.CompletedTask;
    }
}
