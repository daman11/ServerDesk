using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SecurityChecks
{
    internal static void Run(ProfileRepository repository)
    {
        var credentials=new TestCredentials();
        var vm=new MainViewModel(repository); var window=new MainWindow(vm,repository,credentials,new SshConnectionFactory(repository),new ExternalRdpClient()); window.Show(); RedesignChecks.Frames();
        try
        {
            var profile=new ServerProfile { Name="Credential security fixture",Host="old.example",Username="admin" };
            RedesignChecks.Wait(repository.SaveAsync(profile)); credentials.Secrets[profile.Id]="fixture-secret";
            var renamed=profile with { Name="Renamed fixture",Color="#112233" };
            RedesignChecks.Wait(window.SaveEditedProfileAsync(profile,renamed));
            if(!credentials.Secrets.ContainsKey(profile.Id) || credentials.Deleted.Count!=0)throw new Exception("Appearance edit removed credentials");
            var changed=renamed with { Host="new.example" };
            RedesignChecks.Wait(window.SaveEditedProfileAsync(renamed,changed));
            if(credentials.Secrets.Count!=0 || !credentials.Deleted.Contains(profile.Id) || !credentials.Deleted.Contains(CredentialReference.Vnc(profile.Id)))throw new Exception("Destination edit retained credentials");
            credentials.FailDeletion=true;
            try { RedesignChecks.Wait(window.SaveEditedProfileAsync(changed,changed with { Host="unsafe.example" })); throw new Exception("Failed credential removal allowed destination change"); }
            catch(InvalidOperationException) { }
            var saved=repository.ListAsync(); RedesignChecks.Wait(saved);
            if(saved.Result.Single(p=>p.Id==profile.Id).Host!="new.example")throw new Exception("Profile changed despite credential store failure");
            Console.WriteLine("PASS appearance edits preserve credentials; destination changes clear both credential references; unavailable store blocks destination save");

            using var pane=new SessionPane(window,profile,new SshConnectionFactory(repository),new ExternalRdpClient(),repository);
            var terminalWindow=new Window { Content=pane,Width=800,Height=500 }; terminalWindow.Show(); RedesignChecks.Frames();
            try
            {
                var terminal=pane.GetVisualDescendants().OfType<TerminalControl>().Single(); terminal.ApplyTemplate();
                var defaults=new XTerm.Options.TerminalOptions(); Console.WriteLine($"INFO upstream terminal defaults: clipboard read={defaults.ClipboardReadEnabled}, write={defaults.ClipboardWriteEnabled}; production explicitly disables both remote requests");
                var reads=0; var writes=0; terminal.Terminal.ClipboardReadRequested+=(_,_)=>reads++; terminal.Terminal.ClipboardWriteRequested+=(_,_)=>writes++;
                RedesignChecks.Wait(terminalWindow.Clipboard!.SetTextAsync("local-security-sentinel"));
                terminal.Terminal.Write("\x1b]52;c;?\a\x1b]52;c;cmVtb3RlLW92ZXJ3cml0ZQ==\a"); RedesignChecks.Frames();
                var clip=terminalWindow.Clipboard!.TryGetTextAsync(); RedesignChecks.Wait(clip);
                if(reads!=0 || writes!=0 || clip.Result!="local-security-sentinel")throw new Exception("Remote terminal escape sequence accessed clipboard");
                Console.WriteLine("PASS production terminal blocks OSC 52 clipboard read and overwrite without user interaction");
            }
            finally { terminalWindow.Close(); }
        }
        finally { window.Close(); RedesignChecks.Frames(); }
    }
    private sealed class TestCredentials : ICredentialStore
    {
        internal readonly Dictionary<Guid,string> Secrets=[];
        internal readonly List<Guid> Deleted=[];
        internal bool FailDeletion;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Secrets.GetValueOrDefault(id));
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Secrets[id]=secret; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { if(FailDeletion)throw new InvalidOperationException("Fixture store unavailable"); Deleted.Add(id); Secrets.Remove(id); return Task.CompletedTask; }
    }
}
