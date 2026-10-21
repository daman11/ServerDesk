using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ApplicationUpdateChecks
{
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> send):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(send(request));
    }
    private static T Named<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(4); }
    internal static void Run(string root)
    {
        var repo=new ProfileRepository(new AppPaths()); RedesignChecks.Wait(repo.InitializeAsync());
        var sentinel=new ServerProfile { Name="Update sentinel",Host="fixture.invalid",Username="fixture" }; RedesignChecks.Wait(repo.SaveAsync(sentinel));
        var bytes=new byte[]{1,2,3,4}; var package=new ApplicationUpdate("0.99.0","https://github.com/daman11/ServerDesk/releases/download/v0.99.0/ServerDesk-0.99.0-win-x64-Setup.exe",4,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var installation=Path.Combine(root,"installation żółć"); Directory.CreateDirectory(installation);
        foreach(var name in new[]{"install-state.ini","Uninstall.exe","ServerDesk.App.exe","ServerDesk.Updater.exe"})File.WriteAllText(Path.Combine(installation,name),"fixture");
        File.WriteAllText(Path.Combine(installation,"install-state.ini"),"[Setup]\nNoRegister=1\n");
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); DesignSystem.Apply(language=="pl"?"graphite":"light"); DesignSystem.ApplyPreferences(new() { InterfaceFontSize=16 });
            var owner=new Window { Width=1000,Height=700 }; owner.Show(); RedesignChecks.Frames(3);
            using var client=new GitHubUpdateClient(new Handler(request=>request.RequestUri!.Host=="api.github.com"?new(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(new[]{new { draft=false,prerelease=true,tag_name="v0.99.0",assets=new[]{new { name=package.FileName,state="uploaded",browser_download_url=package.DownloadUrl,size=4,digest="sha256:"+package.Sha256 }} }})) }:new(HttpStatusCode.OK) { Content=new ByteArrayContent(bytes) }));
            var showing=Dialogs.ApplicationUpdates(owner,repo,updateClient:client,installation:Path.GetFullPath(installation)); RedesignChecks.Frames(5); var dialog=owner.OwnedWindows.Single();
            Click(Named<Button>(dialog,"CheckUpdates"));
            if(Named<TextBlock>(dialog,"UpdateStatus").Text!=AppText.F($"Dostępna wersja: {package.Version}"))throw new Exception("New version was not shown in the selected language");
            foreach(var size in new[]{new Size(650,500),new Size(430,350)})
            {
                dialog.Width=size.Width; dialog.Height=size.Height; RedesignChecks.Frames(4);
                foreach(var button in dialog.GetVisualDescendants().OfType<Button>().Where(b=>b.IsEffectivelyVisible))
                {
                    var origin=button.TranslatePoint(default,dialog)!.Value;
                    if(origin.X<0 || origin.Y<0 || origin.X+button.Bounds.Width>dialog.ClientSize.Width+.5 || origin.Y+button.Bounds.Height>dialog.ClientSize.Height+.5)throw new Exception("Update action is clipped in a compact window");
                }
                dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"updates-{language}-{size.Width}.png"));
            }
            if(OperatingSystem.IsWindows())
            {
                Click(Named<Button>(dialog,"DownloadUpdate"));
                var deadline=DateTime.UtcNow.AddSeconds(15); while(!Named<Button>(dialog,"InstallUpdate").IsEffectivelyVisible && DateTime.UtcNow<deadline)RedesignChecks.Frames(2);
                if(!Named<Button>(dialog,"InstallUpdate").IsEffectivelyVisible)throw new Exception("Verified download did not expose installation");
                Click(Named<Button>(dialog,"InstallUpdate")); RedesignChecks.Wait(showing);
                var prepared=showing.Result??throw new Exception("Update was not prepared");
                if(!File.Exists(Path.Combine(prepared.Directory,"update.json")) || !File.Exists(Path.Combine(prepared.Directory,"ServerDesk.Updater.exe")))throw new Exception("Update handoff is incomplete");
                if(!Directory.EnumerateFiles(repo.DatabaseBackupDirectory).Any())throw new Exception("Update did not create a database backup");
            }
            else { if(Named<Button>(dialog,"DownloadUpdate").IsEffectivelyVisible)throw new Exception("Linux must use manual package installation"); dialog.Close(); RedesignChecks.Wait(showing); }
            owner.Close(); RedesignChecks.Frames(3);
            var errorOwner=new Window(); errorOwner.Show(); RedesignChecks.Frames(2);
            using var failed=new GitHubUpdateClient(new Handler(_=>new(HttpStatusCode.NotFound)));
            var errorShowing=Dialogs.ApplicationUpdates(errorOwner,repo,updateClient:failed); RedesignChecks.Frames(4); var error=errorOwner.OwnedWindows.Single();
            Click(Named<Button>(error,"CheckUpdates"));
            if(!Named<TextBlock>(error,"UpdateStatus").Text!.Contains(AppText.T("Repozytorium nie ma jeszcze opublikowanych wydań. Spróbuj ponownie później.")))throw new Exception("First publication has no useful empty-repository feedback");
            error.Close(); RedesignChecks.Wait(errorShowing); errorOwner.Close(); RedesignChecks.Frames(2);
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(new());
        var profiles=repo.ListAsync(); RedesignChecks.Wait(profiles); if(profiles.Result.Single().Id!=sentinel.Id)throw new Exception("Updater changed a saved connection");
        var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(new AppPaths()),new SshConnectionFactory(repo),new ExternalRdpClient());
        main.Show(); RedesignChecks.Frames(12); var started=0;
        RedesignChecks.Wait((Task)typeof(MainWindow).GetField("initialization",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(main)!);
        foreach(var save in new[]{false,true})
        {
            var settings=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(6); var settingsWindow=main.OwnedWindows.Single();
            Named<TabControl>(settingsWindow,"SettingsCategories").SelectedIndex=5; RedesignChecks.Frames(4);
            Named<CheckBox>(settingsWindow,"CheckUpdatesOnStartup").IsChecked=false;
            Click(settingsWindow.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,save?"Zapisz":"Anuluj"))); RedesignChecks.Wait(settings);
            var preferences=repo.PreferencesAsync(); RedesignChecks.Wait(preferences);
            if(preferences.Result.CheckUpdatesOnStartup==save)throw new Exception("Startup update opt-out ignored Save/Cancel");
        }
        var startupRequests=0;
        main.StartupUpdateClientFactory=()=>new GitHubUpdateClient(new Handler(_=> { startupRequests++; return new(HttpStatusCode.OK) { Content=new StringContent("[]") }; }));
        RedesignChecks.Wait(main.CheckUpdatesAtStartupAsync());
        if(startupRequests!=0)throw new Exception("Explicit startup opt-out contacted GitHub");
        main.PreviewPreferences(new() { CheckUpdatesOnStartup=true });
        RedesignChecks.Wait(main.CheckUpdatesAtStartupAsync()); RedesignChecks.Wait(main.CheckUpdatesAtStartupAsync());
        if(startupRequests!=1)throw new Exception("Startup must perform exactly one non-blocking check");
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            if(language=="en")
            {
                main.Close(); RedesignChecks.Frames(5);
                main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(new AppPaths()),new SshConnectionFactory(repo),new ExternalRdpClient());
                main.Show(); RedesignChecks.Frames(10);
                RedesignChecks.Wait((Task)typeof(MainWindow).GetField("initialization",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(main)!);
            }
            main.PreviewPreferences(new() { InterfaceFontSize=16 });
            RedesignChecks.Wait(main.PresentAvailableUpdateAsync(package)); RedesignChecks.Frames(4);
            if(!Named<Border>(main,"UpdateAvailableBanner").IsVisible)throw new Exception("New version did not show a reminder");
            Click(Named<Button>(main,"RemindAvailableUpdate"));
            if(Named<Border>(main,"UpdateAvailableBanner").IsVisible)throw new Exception("Remind action did not hide notification");
            var skipped=repo.SettingAsync("skippedUpdateVersion"); RedesignChecks.Wait(skipped);
            if(skipped.Result==package.Version)throw new Exception("Remind action persisted a skipped version");
            RedesignChecks.Wait(main.PresentAvailableUpdateAsync(package)); RedesignChecks.Frames(3);
            main.Width=900; main.Height=650; RedesignChecks.Frames(3);
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"update-reminder-{language}.png"));
            foreach(var button in new[]{"OpenAvailableUpdate","SkipAvailableUpdate","RemindAvailableUpdate"}.Select(name=>Named<Button>(main,name)))
            {
                var point=button.TranslatePoint(default,main)!.Value;
                if(point.X<0 || point.X+button.Bounds.Width>main.ClientSize.Width+.5)throw new Exception("Update reminder action clipped");
            }
            Click(Named<Button>(main,"SkipAvailableUpdate"));
            RedesignChecks.Wait(main.PresentAvailableUpdateAsync(package)); RedesignChecks.Frames(3);
            if(Named<Border>(main,"UpdateAvailableBanner").IsVisible)throw new Exception("Skipped version returned");
            var reopened=new ProfileRepository(new AppPaths()); var remembered=reopened.SettingAsync("skippedUpdateVersion"); RedesignChecks.Wait(remembered);
            if(remembered.Result!=package.Version)throw new Exception("Skipped version was not persisted");
            var newer=package with { Version="0.99.1",DownloadUrl=package.DownloadUrl.Replace("0.99.0","0.99.1") };
            RedesignChecks.Wait(main.PresentAvailableUpdateAsync(newer)); RedesignChecks.Frames(3);
            if(!Named<Border>(main,"UpdateAvailableBanner").IsVisible)throw new Exception("Skipping an old version hid a newer release");
            RedesignChecks.Wait(repo.SetSettingAsync("skippedUpdateVersion",""));
        }
        AppText.SetLanguage("pl");
        main.StartUpdateHelper=_=>started++;
        var synthetic=new PreparedApplicationUpdate(Path.GetFullPath(root),package);
        main.RequestUpdateInstall(synthetic); RedesignChecks.Frames(5);
        var confirm=main.OwnedWindows.Single();
        Click(confirm.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj"))); RedesignChecks.Frames(4);
        if(!main.IsVisible || started!=0)throw new Exception("Cancelling installation closed the app or started the helper");
        main.RequestUpdateInstall(synthetic); RedesignChecks.Frames(5); confirm=main.OwnedWindows.Single();
        Click(confirm.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Potwierdź")));
        var closeDeadline=DateTime.UtcNow.AddSeconds(15); while(main.IsVisible && DateTime.UtcNow<closeDeadline)RedesignChecks.Frames(2);
        if(main.IsVisible || started!=1)throw new Exception("Confirmed update did not hand off exactly once and close cleanly");
        Console.WriteLine("PASS updates: PL/EN font16 compact reminder, once-per-start check/opt-out, remind without persistence, durable exact-version skip and newer release; dialog metadata/download/backup/handoff, cancellation; Linux manual fallback; synthetic HTTP only, no installer executed or production data");
    }
}
