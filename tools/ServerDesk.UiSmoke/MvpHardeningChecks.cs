using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class MvpHardeningChecks
{
    private sealed class Vault:ICredentialStore
    {
        private readonly Dictionary<Guid,string> values=[];
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(values.GetValueOrDefault(id));
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { values[id]=secret; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { values.Remove(id); return Task.CompletedTask; }
    }
    private static void Verify(bool valid,string reason) { if(!valid)throw new Exception(reason); }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var settings=repo.ApplicationSettingsAsync().GetAwaiter().GetResult(); RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { Language=language } }));
            var vault=new Vault(); var owner=new MainWindow(new(repo),repo,vault,new SshConnectionFactory(repo),new ExternalRdpClient()); owner.Show(); RedesignChecks.Frames(20);
            var store=new CommandStore(repo,vault); var command=new CommandDefinition { Id="user.fixture",Origin=CommandOrigin.User,Name="Fixture",Category="Lab",Template="echo fixture",Shell=CommandShell.Bash,HostId=Guid.NewGuid(),HostFolder="Private" };
            RedesignChecks.Wait(store.SaveLibraryAsync(new() { Commands=[command] }));
            using var panel=new CommandPanel(owner,store); var commandsWindow=new Window { Width=640,Height=650,Content=panel }; commandsWindow.Show(); RedesignChecks.Wait(panel.InitializeAsync()); RedesignChecks.Frames(5);
            var path=Path.Combine(paths.Data,"commands.json"); RedesignChecks.Wait(panel.SaveExportAsync(path));
            var parsed=CommandStore.Parse(File.ReadAllText(path)); Verify(parsed.Commands.Single().HostId==null && parsed.Commands.Single().HostFolder=="","Command export retained private host binding");
            var original=File.ReadAllBytes(path); var blocked=path+".directory"; Directory.CreateDirectory(blocked);
            var error=RecordError(()=>panel.SaveExportAsync(blocked)); Verify(error is InvalidOperationException && error.Message==AppText.T("Nie udało się zapisać eksportu. Poprzedni plik pozostał zachowany. Sprawdź wolne miejsce, uprawnienia i blokadę pliku, a następnie ponów zapis."),"Command export error is not localized");
            Verify(original.SequenceEqual(File.ReadAllBytes(path)),"Command export changed earlier file on failure");
            Verify(RecordError(()=>panel.SaveExportAsync(null)) is NotSupportedException,"Provider-only command export was accepted"); commandsWindow.Close(); panel.Dispose(); RedesignChecks.Wait(panel.Shutdown);

            var networkStore=new NetworkToolsStore(repo,vault); var network=new NetworkToolPane(owner,NetworkToolKind.Subnet,networkStore,new NetworkToolsService(),[],"192.0.2.0/24");
            // This case exercises an explicit history-writing run; suppress the
            // unrelated delayed automatic subnet recalculation in the fixture.
            typeof(NetworkToolPane).GetField("applying",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(network,true);
            (typeof(NetworkToolPane).GetField("subnetDelay",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(network) as CancellationTokenSource)?.Cancel();
            var networkWindow=new Window { Width=900,Height=700,Content=network }; networkWindow.Show(); RedesignChecks.Frames(10); RedesignChecks.Wait(network.Shutdown);
            using(var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.Combine(paths.Data,"serverdesk.db"),Pooling=false }.ToString()))
            {
                db.Open(); using var cmd=db.CreateCommand(); cmd.CommandText="CREATE TRIGGER history_fail BEFORE INSERT ON settings WHEN NEW.key='networkTools.history.v1' BEGIN SELECT RAISE(ABORT,'fixture history unavailable'); END"; cmd.ExecuteNonQuery();
            }
            RedesignChecks.Wait(network.StartAsync()); RedesignChecks.Frames(5);
            var results=network.GetVisualDescendants().OfType<ListBox>().Single(c=>c.Name=="NetworkResults");
            Verify(results.ItemCount>0 && network.StatusText==AppText.T("Diagnostyka zakończona. Wyniki zachowano, ale nie udało się zapisać lub odczytać historii i ustawień lokalnych."),"History write failure discarded diagnostic success: "+results.ItemCount+" / "+network.StatusText);
            networkWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"history-warning-"+language+".png"));
            foreach(var format in new[]{"TXT","CSV","JSON","PEM","Response"})
            {
                var content=format is "PEM" or "Response"?"Fixture żółć\n":NetworkExport.Render(format,["Value"],[new("fixture",["żółć"])]);
                var output=Path.Combine(paths.Data,"network-"+format+".txt"); RedesignChecks.Wait(network.SaveExportAsync(output,content)); Verify(File.ReadAllText(output)==content,"Network export bytes differ: "+format);
                if(OperatingSystem.IsWindows())
                {
                    using var locked=new FileStream(output,FileMode.Open,FileAccess.Read,FileShare.None);
                    Verify(RecordError(()=>network.SaveExportAsync(output,"replacement")) is InvalidOperationException,"Locked network export was overwritten");
                }
                Verify(File.ReadAllText(output)==content,"Network export failure changed original");
            }
            network.Dispose(); RedesignChecks.Wait(network.Shutdown); networkWindow.Close();
            RedesignChecks.Wait(repo.SetSettingAsync("networkTools.wolFavorites","[\"{\\\"Name\\\":null,\\\"Mac\\\":null}\"]"));
            var wake=new NetworkToolPane(owner,NetworkToolKind.Wake,networkStore,new NetworkToolsService(),[],""); var wakeWindow=new Window { Width=900,Height=700,Content=wake }; wakeWindow.Show(); RedesignChecks.Frames(10);
            Verify(wake.StatusText==AppText.T("Nie można odczytać zapisanych danych narzędzi sieciowych. Oryginalne dane zachowano."),"Invalid WoL favorite did not show preservation guidance"); wake.Dispose(); RedesignChecks.Wait(wake.Shutdown); wakeWindow.Close();

            var editing=Dialogs.Edit(owner,new ServerProfile { Name="RDP fixture",Host="example.test",Protocol=Protocol.RDP,Port=3389,Rdp=new() { Fullscreen=true } },[]); RedesignChecks.Frames(); var editor=owner.OwnedWindows.Single(); PolishChecks.Page(editor,3);
            var fullscreen=editor.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="RdpExternalFullscreen"); var mode=editor.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="RdpDisplayMode");
            mode.SelectedIndex=1; RedesignChecks.Frames(); Verify(!fullscreen.IsEnabled && fullscreen.IsChecked==true && fullscreen.Content as string==AppText.T("Pełny ekran klienta w osobnym oknie"),"Embedded RDP has a misleading fullscreen setting");
            if(OperatingSystem.IsWindows()) { mode.SelectedIndex=0; RedesignChecks.Frames(); Verify(fullscreen.IsEnabled,"External RDP lost fullscreen option"); }
            fullscreen.BringIntoView(); RedesignChecks.Frames(); editor.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-fullscreen-"+language+".png"));
            editor.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(editing);
            owner.Close(); SshAcceptanceChecks.Until(()=>!owner.IsVisible,"Hardening owner did not close");
            Console.WriteLine("PASS "+language+": protected command/report exports, no host binding, failure preservation/local-path guidance, diagnostic success with failed history, invalid WoL feedback and RDP fullscreen scope; native pickers bypassed");
        }
    }
    private static Exception? RecordError(Func<Task> action)
    { try { RedesignChecks.Wait(action()); return null; } catch(Exception ex) { return ex; } }
}
