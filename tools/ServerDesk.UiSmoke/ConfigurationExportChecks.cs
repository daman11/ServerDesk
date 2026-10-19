using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ConfigurationExportChecks
{
    private static void Until(Func<bool> done)
    {
        var watch=Stopwatch.StartNew();
        while(!done()) { if(watch.Elapsed>TimeSpan.FromSeconds(20))throw new TimeoutException("Configuration export fixture timed out"); RedesignChecks.Frames(1); }
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var preferences=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
            RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(preferences with { Preferences=preferences.Preferences with { Language=language } }));
            var profile=new ServerProfile { Name="Żółty serwer",Host="example.test",Username="fixture",Folder="Lab/Linux" }; RedesignChecks.Wait(repo.SaveAsync(profile));
            var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            var output=Path.Combine(paths.Data,"exports"); Directory.CreateDirectory(output); var file=Path.Combine(output,"serwery żółć.json"); File.WriteAllText(file,"previous export");
            void ValidExport()
            {
                using var json=JsonDocument.Parse(File.ReadAllText(file));
                if(vm.Status!=AppText.T("Wyeksportowano konfigurację bez haseł") || json.RootElement.GetProperty("Servers")[0].GetProperty("Name").GetString()!=profile.Name)throw new Exception("Configuration export content/success feedback differs");
            }
            void CloseMessage(Task action,string expected,string screenshot)
            {
                Until(()=>main.OwnedWindows.Count==1); var message=main.OwnedWindows.Single();
                if(vm.Status!=expected || !message.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.StartsWith(expected+"\n\n",StringComparison.Ordinal)==true))throw new Exception("Configuration export failure is not localized");
                message.CaptureRenderedFrame()!.Save(Path.Combine(root,screenshot+"-"+language+".png"));
                message.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zamknij").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(action);
            }
            try
            {
                RedesignChecks.Wait(main.ExportConfigurationAsync(file)); ValidExport(); var previous=File.ReadAllBytes(file);
                var blocked=Path.Combine(output,"blocked.json"); Directory.CreateDirectory(blocked); var sentinel=Path.Combine(blocked,"keep"); File.WriteAllText(sentinel,"keep");
                var failure=main.Safe(()=>main.ExportConfigurationAsync(blocked));
                CloseMessage(failure,AppText.T("Nie udało się zapisać eksportu. Poprzedni plik nie został zastąpiony. Sprawdź wolne miejsce, uprawnienia i czy plik nie jest otwarty w innym programie, a następnie spróbuj ponownie."),"export-error");
                if(!previous.SequenceEqual(File.ReadAllBytes(file)) || File.ReadAllText(sentinel)!="keep" || Directory.GetFiles(output,"*.part").Length!=0)throw new Exception("Export error damaged fixture data or left staging files");
                var unsupported=main.Safe(()=>main.ExportConfigurationAsync(null));
                CloseMessage(unsupported,AppText.T("Eksport konfiguracji wymaga lokalnego pliku. Wybierz folder na dysku komputera."),"export-local-file");
                RedesignChecks.Wait(main.ExportConfigurationAsync(file)); ValidExport();
                Console.WriteLine("PASS "+language+": configuration export content/success, localized publication failure and local-file guidance, preserved fixture files, retry; native picker bypassed");
            }
            finally { main.Close(); Until(()=>!main.IsVisible); }
        }
    }
}
