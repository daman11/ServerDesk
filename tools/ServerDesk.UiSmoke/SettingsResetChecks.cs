using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SettingsResetChecks
{
    private static T Named<T>(Control owner,string name) where T:Control=>owner.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control owner,string name)=>Named<Button>(owner,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Tag(Window owner,string tag)=>owner.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Read<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Verify(bool value,string message) { if(!value)throw new Exception(message); }
    private static void Until(Func<bool> value)=>SshAcceptanceChecks.Until(value,"Settings reset operation did not settle");
    private static Window Settings(MainWindow main,ProfileRepository repo,out Task editing)
    {
        editing=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(8); return main.OwnedWindows.Single();
    }
    private static FactorySettingsResetView Factory(Window settings)
    {
        Named<TextBox>(settings,"SettingsSearch").Text="factory"; RedesignChecks.Frames(5);
        Verify(Named<TabControl>(settings,"SettingsCategories").SelectedIndex==5,"Reset settings search did not select Security");
        var view=Named<FactorySettingsResetView>(settings,"FactorySettingsReset"); view.BringIntoView(); RedesignChecks.Frames(4); return view;
    }
    private static void Schedule(Window settings,FactorySettingsResetView view,bool confirm)
    {
        Click(view,"ScheduleFactorySettingsReset"); RedesignChecks.Frames(4); Verify(view.IsBusy,"Reset request did not guard concurrent settings actions");
        Verify(!Named<Button>(settings,"RestoreDefaultSettings").IsEffectivelyEnabled,"Defaults remained active during reset operation");
        Verify(settings.OwnedWindows.Single().GetVisualDescendants().OfType<TextBlock>().Any(t=>(t.Text??"").Contains(AppText.Language=="en"?"ALL saved connections":"WSZYSTKIE zapisane połączenia")),"Confirmation did not explain the destructive full reset scope");
        Tag(settings.OwnedWindows.Single(),confirm?"Potwierdź":"Anuluj"); Until(()=>!view.IsBusy); RedesignChecks.Frames(3);
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"dane żółć "+language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
            var original=ApplicationSettings.Defaults with { Theme=language=="pl"?"graphite":"light",TerminalFont=18,Preferences=new() { Language=language,InterfaceFontSize=16,SftpShowHidden=true,ShowTabIcons=false } };
            Wait(repo.SaveApplicationSettingsAsync(original));
            var profile=new ServerProfile { Name="Preserved profile żółć",Host="fixture.invalid",Username="fixture",Folder="LAB",Tags="production",Description="preserve note" }; Wait(repo.SaveAsync(profile));
            Wait(repo.SetSettingAsync("networkTools.user-document","private fixture")); Wait(repo.SetSettingAsync("connectionTagColors","{\"production\":\"#123456\"}"));
            var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            var replacements=0; main.StartReplacement=()=>replacements++;
            var settings=Settings(main,repo,out var editing);
            Click(settings,"RestoreDefaultSettings"); RedesignChecks.Frames(5);
            Verify(Named<ComboBox>(settings,"ApplicationLanguage").SelectedIndex==0 && Named<ComboBox>(settings,"ApplicationTheme").SelectedIndex==0,"Default form did not reset its appearance controls");
            Named<TabControl>(settings,"SettingsCategories").SelectedIndex=2; RedesignChecks.Frames(4); Verify(Named<ComboBox>(settings,"SftpTextEditor").SelectedIndex==0,"Default form did not reset SFTP preferences");
            Verify(Read(repo.ApplicationSettingsAsync())==original,"Defaults changed persisted values before Save");
            Tag(settings,"Anuluj"); Wait(editing); Verify(Read(repo.ApplicationSettingsAsync())==original,"Cancel persisted default preferences");
            settings=Settings(main,repo,out editing); Click(settings,"RestoreDefaultSettings"); Tag(settings,"Zapisz"); Wait(editing);
            Verify(Read(repo.ApplicationSettingsAsync())==ApplicationSettings.Defaults,"Saving defaults did not commit all preferences");
            Wait(repo.SaveApplicationSettingsAsync(original)); main.PreviewPreferences(original.Preferences); DesignSystem.Apply(original.Theme);
            Wait(repo.SetSettingAsync("pendingFactorySettingsReset","1"));
            settings=Settings(main,repo,out editing); var legacy=Factory(settings);
            Verify(!Named<Button>(legacy,"ScheduleFactorySettingsReset").IsEnabled && Named<TextBlock>(legacy,"FactorySettingsResetStatus").Text!.Contains(language=="en"?"preferences-only":"samych preferencji"),"An earlier preferences-only request was displayed as a destructive full reset");
            Click(legacy,"CancelFactorySettingsReset"); Until(()=>!legacy.IsBusy); Tag(settings,"Anuluj"); Wait(editing);
            settings=Settings(main,repo,out editing); var view=Factory(settings);
            Schedule(settings,view,false); Verify(!Read(repo.IsFactorySettingsResetPendingAsync()) && !Directory.Exists(repo.DatabaseBackupDirectory),"Cancelled confirmation queued a reset or created a backup");
            Schedule(settings,view,true); Verify(Read(repo.IsFactorySettingsResetPendingAsync()),"Confirmed reset was not queued"); Verify(Read(repo.ApplicationSettingsAsync())==original,"Scheduling reset changed live settings");
            var copies=Read(DatabaseBackupCatalog.ReadAsync(repo.DatabaseBackupDirectory)).Entries; Verify(copies.Count==1,"Reset did not create exactly one backup");
            foreach(var width in new[]{960,680})
            {
                settings.Width=width; settings.Height=width==680?500:720; RedesignChecks.Frames(5); var restart=Named<Button>(view,"RestartFactorySettingsReset"); restart.BringIntoView(); RedesignChecks.Frames(3);
                Verify(Named<Button>(view,"CancelFactorySettingsReset").IsEffectivelyVisible && Named<Button>(view,"RestartFactorySettingsReset").IsEffectivelyVisible,"Pending reset actions disappeared");
                var point=restart.TranslatePoint(default,settings)!.Value; var save=settings.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz"); var footer=save.TranslatePoint(default,settings)!.Value;
                Verify(point.Y>=0 && point.Y+restart.Bounds.Height<=footer.Y,"Restart action is hidden behind the fixed settings footer");
                settings.CaptureRenderedFrame()!.Save(Path.Combine(root,$"settings-reset-{language}-{width}.png"));
            }
            Tag(settings,"Anuluj"); Wait(editing); Verify(Read(repo.IsFactorySettingsResetPendingAsync()),"Settings Cancel silently cancelled an independent reset request");
            settings=Settings(main,repo,out editing); view=Factory(settings); Click(view,"CancelFactorySettingsReset"); Until(()=>!view.IsBusy);
            Verify(!Read(repo.IsFactorySettingsResetPendingAsync()) && File.Exists(copies[0].Path),"Reset cancellation lost the backup or left the request pending");
            Schedule(settings,view,true); Click(view,"RestartFactorySettingsReset"); Wait(editing); Until(()=>!main.IsVisible);
            Verify(replacements==1 && Read(repo.IsFactorySettingsResetPendingAsync()),"Restart did not use the existing guarded handoff");
            Verify(Read(repo.SettingAsync("mainWindowPlacement"))!=null,"Fixture did not exercise a late shutdown placement write");
            Verify(Read(repo.ApplyPendingFactorySettingsResetAsync()),"Startup did not consume the scheduled reset");
            Verify(Read(repo.ApplicationSettingsAsync())==ApplicationSettings.Defaults && Read(repo.SettingAsync("lastWorkspace"))==null && Read(repo.MainWindowPlacementAsync())==null,"Shutdown values resurrected after reset");
            Verify(Read(repo.ListAsync()).Count==0 && Read(repo.SettingAsync("networkTools.user-document"))==null && Read(repo.SettingAsync("connectionTagColors"))==null,"Full factory reset did not remove connections/plugin data/tag colors");
            var finalCopies=Read(DatabaseBackupCatalog.ReadAsync(repo.DatabaseBackupDirectory)); Verify(finalCopies.Entries.Count==3 && finalCopies.Entries.Count(c=>c.BeforeReset)==1,"Full reset did not create a final startup backup");
            AppText.SetLanguage("pl"); var reopened=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); reopened.Show(); RedesignChecks.Frames(20);
            Verify(reopened.OpenSessions.Count==0 && reopened.CurrentPreferences==new ApplicationPreferences(),"Fresh main window did not load factory preferences");
            reopened.Close(); Until(()=>!reopened.IsVisible);
        }
        Console.WriteLine("PASS settings resets PL/EN: draft defaults save/cancel, full factory scope, confirmation/cancel/reopen, scheduling and final startup backups, guarded restart, connections/plugin data/tag colors cleared only on startup, Graphite/Light 960x720 and 680x500");
    }
}
