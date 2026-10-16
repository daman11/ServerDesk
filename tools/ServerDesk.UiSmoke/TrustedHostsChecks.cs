using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class TrustedHostsChecks
{
    private static T Named<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(control=>control.Name==name);
    private static void Click(Window window,string name)=>Named<Button>(window,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Until(Func<bool> done,string message)
    {
        var clock=Stopwatch.StartNew(); while(!done()) { if(clock.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException(message); RedesignChecks.Frames(1); }
    }
    private static void Ready(Window window)=>Until(()=>Named<TextBox>(window,"TrustedHostsSearch").IsEnabled,"Trust dialog operation did not finish");
    private static void Confirm(Window owner,bool accept)
    {
        Until(()=>owner.OwnedWindows.Count==1,"Trust confirmation did not open"); var confirmation=owner.OwnedWindows.Single();
        confirmation.GetVisualDescendants().OfType<Button>().Single(button=>button.Tag as string==(accept?"Potwierdź":"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        RedesignChecks.Frames(2); Ready(owner);
    }
    private static string Fingerprint(byte value)=>"SHA256:"+Convert.ToBase64String(Enumerable.Repeat(value,32).ToArray()).TrimEnd('=');

    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var alpha=new TrustedHostKey("alpha.internal",22,Fingerprint(1)); var otherPort=alpha with { Port=2222,Fingerprint=Fingerprint(2) }; var ipv6=new TrustedHostKey("2001:db8::1",22,Fingerprint(3));
            foreach(var entry in new[]{alpha,otherPort,ipv6})RedesignChecks.Wait(repo.TrustHostAsync(entry.Host,entry.Port,entry.Fingerprint));
            var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            main.PreviewPreferences(main.Preferences with { InterfaceFontSize=16 }); DesignSystem.Apply(language=="pl"?"graphite":"light");
            var original=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
            var settings=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var settingsWindow=main.OwnedWindows.Single();
            settingsWindow.GetVisualDescendants().OfType<NumericUpDown>().Single(control=>AutomationProperties.GetName(control)==AppText.T("Rozmiar tekstu interfejsu")).Value=16; RedesignChecks.Frames(2);
            Named<TextBox>(settingsWindow,"SettingsSearch").Text=language=="pl"?"zaufane":"trusted"; RedesignChecks.Frames();
            if(Named<TabControl>(settingsWindow,"SettingsCategories").SelectedIndex!=4 || !Named<Button>(settingsWindow,"ManageTrustedHosts").IsEffectivelyVisible)throw new Exception("Trust settings are not searchable");
            Click(settingsWindow,"ManageTrustedHosts"); Until(()=>settingsWindow.OwnedWindows.Count==1,"Trust manager did not open from settings"); var dialog=settingsWindow.OwnedWindows.Single(); Ready(dialog);
            if(DesignSystem.Preferences.InterfaceFontSize!=16)throw new Exception("Trust layout was not tested at the requested font size");
            var list=Named<ListBox>(dialog,"TrustedHostsList"); var search=Named<TextBox>(dialog,"TrustedHostsSearch");
            if(list.ItemCount!=3 || Named<Button>(dialog,"TrustedHostsForget").IsEnabled)throw new Exception("Trust list/default action state incorrect");
            list.SelectedItem=list.Items.OfType<TrustedHostKey>().Single(entry=>entry==alpha);
            Click(dialog,"TrustedHostsCopy"); Ready(dialog); RedesignChecks.Frames(2);
            var clipboard=dialog.Clipboard!.TryGetTextAsync(); RedesignChecks.Wait(clipboard); if(clipboard.Result!=alpha.Fingerprint)throw new Exception("Copied data is not the selected fingerprint");
            search.Text="ALPHA.INTERNAL:2222"; RedesignChecks.Frames(2); if(list.ItemCount!=1 || list.Items.OfType<TrustedHostKey>().Single()!=otherPort)throw new Exception("Host/port search incorrect");
            search.Text=ipv6.Fingerprint; RedesignChecks.Frames(2); if(list.ItemCount!=1)throw new Exception("Fingerprint search incorrect");
            search.Text="absent"; RedesignChecks.Frames(2); if(list.ItemCount!=0 || Named<Button>(dialog,"TrustedHostsForget").IsEnabled)throw new Exception("Empty search leaves destructive action enabled");
            search.Text=""; RedesignChecks.Frames(2); list.SelectedItem=list.Items.OfType<TrustedHostKey>().Single(entry=>entry==alpha);
            Click(dialog,"TrustedHostsForget"); Confirm(dialog,false);
            if(repo.HostKeyAsync(alpha.Host,alpha.Port).GetAwaiter().GetResult()!=alpha.Fingerprint)throw new Exception("Cancelling removed trust");
            using(var blocker=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.Combine(paths.Data,"serverdesk.db") }.ToString()))
            {
                blocker.Open(); using var transaction=blocker.BeginTransaction();
                Click(dialog,"TrustedHostsForget"); Confirm(dialog,true);
                if(repo.HostKeyAsync(alpha.Host,alpha.Port).GetAwaiter().GetResult()!=alpha.Fingerprint || string.IsNullOrWhiteSpace(Named<TextBlock>(dialog,"TrustedHostsNotice").Text))throw new Exception("A busy database lost trust or failed silently");
                transaction.Rollback();
            }
            foreach(var width in new[]{520,800})
            {
                dialog.Width=width; dialog.Height=560; RedesignChecks.Frames();
                foreach(var name in new[]{"TrustedHostsCopy","TrustedHostsForget"})
                {
                    var button=Named<Button>(dialog,name); var point=button.TranslatePoint(default,dialog)!.Value;
                    if(point.X<0 || point.Y<0 || point.X+button.Bounds.Width>dialog.ClientSize.Width+1 || point.Y+button.Bounds.Height>dialog.ClientSize.Height+1)throw new Exception("Trust dialog actions escape bounds");
                }
                dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"trusted-hosts-{language}-{width}.png"));
            }
            // Simulate another window replacing a key after this row was loaded.
            RedesignChecks.Wait(repo.ForgetHostKeyAsync(alpha)); RedesignChecks.Wait(repo.TrustHostAsync(alpha.Host,alpha.Port,Fingerprint(4)));
            Click(dialog,"TrustedHostsForget"); Confirm(dialog,true);
            if(repo.HostKeyAsync(alpha.Host,alpha.Port).GetAwaiter().GetResult()!=Fingerprint(4))throw new Exception("Stale confirmation removed a replacement key");
            list.SelectedItem=list.Items.OfType<TrustedHostKey>().Single(entry=>entry==otherPort); Click(dialog,"TrustedHostsForget"); Confirm(dialog,true);
            if(repo.HostKeyAsync(otherPort.Host,otherPort.Port).GetAwaiter().GetResult()!=null || list.ItemCount!=2)throw new Exception("Confirmed removal failed or removed a different port");
            dialog.Close(); RedesignChecks.Frames(); settingsWindow.Close(); RedesignChecks.Wait(settings);
            if(repo.ApplicationSettingsAsync().GetAwaiter().GetResult()!=original || repo.HostKeyAsync(otherPort.Host,otherPort.Port).GetAwaiter().GetResult()!=null)throw new Exception("Settings cancel unexpectedly changed applied trust or preferences");
            foreach(var entry in repo.TrustedHostKeysAsync().GetAwaiter().GetResult())RedesignChecks.Wait(repo.ForgetHostKeyAsync(entry));
            var empty=Dialogs.TrustedHosts(main,repo); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); Ready(dialog);
            if(Named<ListBox>(dialog,"TrustedHostsList").ItemCount!=0 || Named<Button>(dialog,"TrustedHostsForget").IsEnabled || string.IsNullOrEmpty(Named<TextBlock>(dialog,"TrustedHostsCount").Text))throw new Exception("Empty trust store state missing");
            dialog.Width=520; dialog.Height=560; RedesignChecks.Frames(); dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"trusted-hosts-empty-{language}.png"));
            dialog.Close(); RedesignChecks.Wait(empty); main.Close(); RedesignChecks.Frames();
        }
        AppText.SetLanguage("pl"); DesignSystem.Apply("dark");
        Console.WriteLine("PASS trusted SSH keys UI: settings/search, host/port/fingerprint filters, clipboard, cancel, busy database error/recovery, exact-port removal, stale replacement protection, immediate apply independent of settings cancel, empty store and compact/large PL/EN Graphite/Light font16");
    }

    internal static void Network(string root,string host,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"network-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
        var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames();
        var confirms=0; var allow=false; var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=> { confirms++; return Task.FromResult(allow); } };
        var profile=new ServerProfile { Name="Owned trust fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        RedesignChecks.Wait(repo.TrustHostAsync(host,22222,Fingerprint(99)));
        void Rejected(bool sftp)
        {
            Task operation=sftp?factory.SftpAsync(profile,"serverdesk-test-passphrase",default):factory.SshAsync(profile,"serverdesk-test-passphrase",default);
            try { RedesignChecks.Wait(operation); throw new Exception("Connection unexpectedly trusted a rejected key"); }
            catch(Exception) when(operation.IsFaulted) { }
        }
        Rejected(false); Rejected(true); if(confirms!=0)throw new Exception("Changed host key reached the first-use confirmation prompt");
        var wrong=repo.TrustedHostKeysAsync().GetAwaiter().GetResult().Single(); RedesignChecks.Wait(repo.ForgetHostKeyAsync(wrong));
        Rejected(false); if(confirms!=1 || repo.TrustedHostKeysAsync().GetAwaiter().GetResult().Count!=0)throw new Exception("Declined first-use trust was saved");
        allow=true; var connection=factory.SshAsync(profile,"serverdesk-test-passphrase",default); RedesignChecks.Wait(connection); using var client=connection.Result;
        var trusted=repo.TrustedHostKeysAsync().GetAwaiter().GetResult().Single(); if(confirms!=2 || trusted.Fingerprint==wrong.Fingerprint)throw new Exception("Verified replacement key not recorded");
        var showing=Dialogs.TrustedHosts(main,repo); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single(); Ready(dialog);
        Named<ListBox>(dialog,"TrustedHostsList").SelectedItem=trusted; Click(dialog,"TrustedHostsForget"); Confirm(dialog,true);
        dialog.Close(); RedesignChecks.Wait(showing);
        var command=Task.Run(()=> { using var output=client.RunCommand("printf 'trusted-session-still-live'"); return output.Result; }); RedesignChecks.Wait(command);
        if(command.Result!="trusted-session-still-live")throw new Exception("Removing stored trust interrupted an existing connection");
        allow=false; Rejected(true); if(confirms!=3)throw new Exception("Next SFTP connection did not require fresh trust");
        allow=true; var sftp=factory.SftpAsync(profile,"serverdesk-test-passphrase",default); RedesignChecks.Wait(sftp); using var transport=sftp.Result;
        var again=factory.SshAsync(profile,"serverdesk-test-passphrase",default); RedesignChecks.Wait(again); using var final=again.Result;
        if(confirms!=4 || repo.HostKeyAsync(host,22222).GetAwaiter().GetResult()!=trusted.Fingerprint)throw new Exception("SSH/SFTP did not share accepted trust");
        main.Close(); RedesignChecks.Frames();
        Console.WriteLine("PASS real SSH/SFTP trust: changed key blocked without override, declined first-use not saved, reviewed reset/accept, live session preserved, fresh verification after UI removal and shared stored fingerprint");
    }
}
