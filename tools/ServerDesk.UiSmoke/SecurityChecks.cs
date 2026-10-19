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
            using(var db=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource=repository.DatabasePath,Pooling=false }.ToString()))
            {
                db.Open(); using var command=db.CreateCommand(); command.CommandText="CREATE TRIGGER reject_credential_edit BEFORE UPDATE ON profiles BEGIN SELECT RAISE(ABORT,'fixture edit failure'); END"; command.ExecuteNonQuery();
                try { RedesignChecks.Wait(window.SaveEditedProfileAsync(renamed,renamed with { Host="blocked.example" })); throw new Exception("Edit fixture did not fail"); } catch(Microsoft.Data.Sqlite.SqliteException) { }
                if(credentials.Deleted.Count!=0 || !credentials.Secrets.ContainsKey(profile.Id))throw new Exception("Failed profile write removed its password");
                command.CommandText="DROP TRIGGER reject_credential_edit"; command.ExecuteNonQuery();
            }
            var changed=renamed with { Host="new.example" };
            RedesignChecks.Wait(window.SaveEditedProfileAsync(renamed,changed));
            if(credentials.Secrets.Count!=0 || !credentials.Deleted.Contains(profile.Id) || !credentials.Deleted.Contains(CredentialReference.Vnc(profile.Id)))throw new Exception("Destination edit retained credentials");
            credentials.FailDeletion=true;
            var previous=repository.ListAsync(); RedesignChecks.Wait(previous); var bound=previous.Result.Single(p=>p.Id==profile.Id); credentials.Secrets[CredentialReference.ForProfile(bound)]="previous-target-fixture";
            RedesignChecks.Wait(window.SaveEditedProfileAsync(bound,bound with { Host="safe-new.example" }));
            var saved=repository.ListAsync(); RedesignChecks.Wait(saved);
            var safe=saved.Result.Single(p=>p.Id==profile.Id);
            if(safe.Host!="safe-new.example" || CredentialReference.ForProfile(safe)==CredentialReference.ForProfile(bound) || credentials.Secrets.ContainsKey(CredentialReference.ForProfile(safe)))throw new Exception("New destination can reuse an old secret after cleanup failure");
            Console.WriteLine("PASS appearance edits preserve credentials; destination changes atomically rotate credential binding; unavailable cleanup never exposes a previous target's secret");

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
