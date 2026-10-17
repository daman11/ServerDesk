using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class DatabaseBackupChecks
{
    private static T Named<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Until(Func<bool> done)
    { var watch=Stopwatch.StartNew(); while(!done()) { if(watch.Elapsed>TimeSpan.FromSeconds(20))throw new TimeoutException("Database backup UI did not finish"); RedesignChecks.Frames(1); } }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); DesignSystem.Apply(language=="pl"?"graphite":"light");
            var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var preferences=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
            RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(preferences with { Preferences=preferences.Preferences with { Language=language } }));
            var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient());
            main.Show(); RedesignChecks.Frames(20);
            var editing=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var settings=main.OwnedWindows.Single();
            Named<TextBox>(settings,"SettingsSearch").Text=language=="pl"?"kopie":"backups"; RedesignChecks.Frames();
            var create=Named<Button>(settings,"CreateDatabaseBackup");
            if(!create.IsEffectivelyVisible || Named<TabControl>(settings,"SettingsCategories").SelectedIndex!=5)throw new Exception("Database backup settings cannot be found");
            var browser=Named<DatabaseBackupBrowser>(settings,"DatabaseBackupBrowser"); RedesignChecks.Wait(browser.RefreshAsync());
            if(Named<ListBox>(settings,"DatabaseBackupList").ItemCount!=0 || Named<Button>(settings,"DatabaseBackupOpenFolder").IsEnabled)throw new Exception("Missing backup directory looks available");
            create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Until(()=>create.IsEnabled);
            var file=Directory.GetFiles(repo.DatabaseBackupDirectory,"*.db").Single();
            if(!Named<TextBlock>(settings,"DatabaseBackupStatus").IsEffectivelyVisible || Named<TextBlock>(settings,"DatabaseBackupStatus").Text!=AppText.T("Kopia została utworzona i sprawdzona.") || Named<SelectableTextBlock>(settings,"DatabaseBackupPath").Text!=file)throw new Exception("Backup path/status is missing or hidden by search");
            var copies=Named<ListBox>(settings,"DatabaseBackupList");
            if(copies.SelectedItem is not DatabaseBackupEntry entry || entry.Path!=file)throw new Exception("Created backup was not selected in the catalog");
            Named<Button>(settings,"DatabaseBackupCopyPath").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(()=>Named<TextBlock>(settings,"DatabaseBackupListStatus").Text==AppText.T("Skopiowano ścieżkę."));
            if(settings.Clipboard!.TryGetTextAsync().GetAwaiter().GetResult()!=file)throw new Exception("Wrong snapshot path copied");
            string? opened=null; browser.LaunchFolder=folder=> { opened=folder.FullName; return Task.FromResult(true); };
            Named<Button>(settings,"DatabaseBackupOpenFolder").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(opened!=Path.GetFullPath(repo.DatabaseBackupDirectory))throw new Exception("Folder launcher target differs from backup directory");
            settings.Width=680; settings.Height=560; RedesignChecks.Frames(); settings.CaptureRenderedFrame()!.Save(Path.Combine(root,"database-backups-"+language+".png"));
            Named<Button>(settings,"DatabaseBackupCopyPath").BringIntoView(); RedesignChecks.Frames(); settings.CaptureRenderedFrame()!.Save(Path.Combine(root,"database-backup-list-"+language+".png"));
            RedesignChecks.Wait(browser.RefreshAsync());
            if(copies.SelectedItem is not DatabaseBackupEntry refreshed || refreshed.Path!=file)throw new Exception("Refresh lost the selected backup");
            File.Delete(file); Directory.Delete(repo.DatabaseBackupDirectory); File.WriteAllText(repo.DatabaseBackupDirectory,"occupied");
            create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Until(()=>create.IsEnabled);
            if(!Named<TextBlock>(settings,"DatabaseBackupStatus").Text!.Contains(AppText.T("Nie udało się utworzyć kopii. Sprawdź wolne miejsce i uprawnienia katalogu danych. Istniejące kopie pozostają zachowane."),StringComparison.Ordinal))throw new Exception("Backup failure feedback is missing");
            RedesignChecks.Wait(browser.RefreshAsync());
            if(copies.ItemCount!=0 || Named<Button>(settings,"DatabaseBackupOpenFolder").IsEnabled || Named<TextBlock>(settings,"DatabaseBackupListStatus").Text!=AppText.T("Nie udało się odczytać listy kopii. Sprawdź folder i uprawnienia, a następnie odśwież listę."))throw new Exception("Catalog failure exposes stale backups");
            File.Delete(repo.DatabaseBackupDirectory); settings.Close(); RedesignChecks.Wait(editing); main.Close(); Until(()=>!main.IsVisible);

            var corruptPaths=new AppPaths(Path.Combine(root,"corrupt-"+language+"-"+Guid.NewGuid().ToString("N"))); var corrupt=new ProfileRepository(corruptPaths);
            File.WriteAllText(Path.Combine(corruptPaths.Data,"serverdesk.db"),"Invalid SQLite data; preserve this original");
            var failed=new MainWindow(new(corrupt),corrupt,new CredentialStore(corruptPaths),new SshConnectionFactory(corrupt),new ExternalRdpClient());
            failed.Show(); Until(()=>failed.OwnedWindows.Count==1); var message=failed.OwnedWindows.Single();
            if(message.Title!=AppText.T("Nie można uruchomić ServerDesk"))throw new Exception("Startup failure is not localized");
            message.CaptureRenderedFrame()!.Save(Path.Combine(root,"database-startup-error-"+language+".png"));
            message.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zamknij").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(()=>!failed.IsVisible);
        }
        Console.WriteLine("PASS database backup settings/search/create/list/refresh/clipboard/launcher target/failure in PL/EN and safe close after corrupt database startup; isolated data only");
    }
}
