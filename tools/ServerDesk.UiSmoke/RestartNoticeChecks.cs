using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class RestartNoticeChecks
{
    private static T Named<T>(Window w,string name) where T:Control=>w.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static Button Action(Window w,string tag)=>w.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag);
    private static void Click(Window w,string tag)=>Action(w,tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static Rect Bounds(Control control,Window w)=>new(control.TranslatePoint(default,w)!.Value,control.Bounds.Size);
    private static void Inside(Control control,Window w)
    {
        var r=Bounds(control,w);
        if(r.Width<=0 || r.Height<=0 || r.Left<0 || r.Top<0 || r.Right>w.Bounds.Width+1 || r.Bottom>w.Bounds.Height+1)throw new Exception($"Control outside window: {control.Name} {r}, window={w.Bounds}");
    }
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        Check(main,repo,root,"pl"); main.Close(); RedesignChecks.Frames(20);
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(repo.ApplicationSettingsAsync().Result with { Preferences=new() { Language="en" } })); AppText.SetLanguage("en");
        var paths=new AppPaths();
        var english=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient());
        english.Show(); RedesignChecks.Frames(25);
        Check(english,repo,root,"en"); english.Close(); RedesignChecks.Frames(20); AppText.SetLanguage("pl"); DesignSystem.Apply("dark");
        Console.WriteLine("PASS destination-language reminders/actions PL→EN/EN→PL: unsaved/cancel/invalid/save/reopen/current-language/dismiss, cancelled restart and launch failure preserve live VNC; save-and-restart launches once and drains root shutdown; settings at 680x500/760x700/960x720, font16, Dark/Light/Graphite; 900x600 banner stays below captions and above workspace");
    }
    private static void Check(MainWindow main,ProfileRepository repo,string root,string running)
    {
        var target=running=="pl"?"en":"pl"; var targetIndex=target=="en"?1:0;
        var banner=Named<Border>(main,"LanguageRestartBanner");
        if(banner.IsVisible)throw new Exception("Restart reminder visible on matching startup");
        var original=repo.ApplicationSettingsAsync().Result;
        var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var w=main.OwnedWindows.Single();
        var picker=Named<ComboBox>(w,"ApplicationLanguage"); var pending=Named<Border>(w,"SettingsRestartReminder");
        if(pending.IsVisible)throw new Exception("Settings warn about restart without a language change");
        picker.SelectedIndex=targetIndex;
        if(!pending.IsVisible || banner.IsVisible)throw new Exception("Unsaved language reminder missing or announced as saved");
        if(Named<TextBlock>(w,"LanguageRestartNotice").Text!=AppText.T("Zmiana języka zostanie zastosowana po ponownym uruchomieniu ServerDesk.",target) ||
            AutomationProperties.GetName(Named<Button>(w,"SettingsSaveRestart"))!=AppText.T("Zapisz i uruchom ponownie",target) ||
            !pending.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==AppText.T("Zmiana języka wymaga restartu",target)))throw new Exception("Settings restart notice is not in destination language");
        var tabs=Named<TabControl>(w,"SettingsCategories");
        foreach(var theme in new[]{"dark","light","graphite"})foreach(var size in new[]{new Size(680,500),new Size(760,700),new Size(960,720)})
        {
            DesignSystem.ApplyPreferences(main.CurrentPreferences with { InterfaceFontSize=16,Density=InterfaceDensity.Comfortable }); DesignSystem.Apply(theme);
            w.Width=size.Width; w.Height=size.Height;
            foreach(var index in Enumerable.Range(0,6))
            {
                tabs.SelectedIndex=index; RedesignChecks.Frames(3); Inside(pending,w); Inside(Action(w,"Zapisz"),w); Inside(Action(w,"Anuluj"),w); Inside(tabs,w);
                if(Bounds(tabs,w).Bottom>Bounds(pending,w).Top+1)throw new Exception("Settings content overlaps restart notice");
                foreach(var tab in tabs.Items.OfType<TabItem>())foreach(var label in ((Control)tab.Header!).GetVisualDescendants().OfType<TextBlock>())
                {
                    var text=Bounds(label,w);
                    if(label.Bounds.Width+1<label.TextLayout.WidthIncludingTrailingWhitespace)throw new Exception("Settings navigation label clipped: "+label.Text);
                    foreach(var ancestor in label.GetVisualAncestors().OfType<Control>().Where(c=>c.ClipToBounds))
                    { var clip=Bounds(ancestor,w); if(text.Left<clip.Left-1 || text.Right>clip.Right+1)throw new Exception("Settings header text escapes clipping ancestor: "+label.Text+" / "+ancestor.GetType().Name); }
                }
                var page=(ScrollViewer)((TabItem)tabs.SelectedItem!).Content!; Inside(page,w);
                var navigation=Named<ScrollViewer>(w,"SettingsNavigationScroll"); var selectedBounds=Bounds((TabItem)tabs.SelectedItem!,w); var navigationBounds=Bounds(navigation,w);
                if(selectedBounds.Top<navigationBounds.Top-1 || selectedBounds.Bottom>navigationBounds.Bottom+1 || navigation.Bounds.Width>(size.Width<820?85:220))throw new Exception("Settings category is hidden or wrapped instead of scrolling");
                if(Bounds((TabItem)tabs.SelectedItem!,w).Right>Bounds(page,w).Left+1)throw new Exception("Settings sidebar overlaps content page");
                w.CaptureRenderedFrame()!.Save(Path.Combine(root,$"settings-{running}-{theme}-{(int)size.Width}-{index}.png"));
            }
        }
        var first=tabs.Items.OfType<TabItem>().First(); first.BringIntoView(); RedesignChecks.Frames(3); var position=first.TranslatePoint(new Point(first.Bounds.Width/2,first.Bounds.Height/2),w)!.Value;
        w.MouseMove(position,RawInputModifiers.None); w.MouseDown(position,MouseButton.Left,RawInputModifiers.None); w.MouseUp(position,MouseButton.Left,RawInputModifiers.None); RedesignChecks.Frames();
        if(tabs.SelectedIndex!=0)throw new Exception("Settings sidebar pointer selection failed");
        first.Focus(); w.KeyPress(Key.Down,RawInputModifiers.None,PhysicalKey.ArrowDown," "); w.KeyRelease(Key.Down,RawInputModifiers.None,PhysicalKey.ArrowDown," "); RedesignChecks.Frames();
        if(tabs.SelectedIndex!=1)throw new Exception("Settings sidebar keyboard selection failed");
        Click(w,"Anuluj"); RedesignChecks.Wait(edit); RedesignChecks.Frames();
        if(repo.ApplicationSettingsAsync().Result!=original || banner.IsVisible)throw new Exception("Cancelled preview persisted language or reminder");
        edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=main.OwnedWindows.Single(); Named<ComboBox>(w,"ApplicationLanguage").SelectedIndex=targetIndex;
        w.GetVisualDescendants().OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)==AppText.T("Kolor akcentu")).Text="invalid";
        Click(w,"Zapisz i uruchom ponownie"); RedesignChecks.Frames();
        if(edit.IsCompleted || repo.ApplicationSettingsAsync().Result!=original || banner.IsVisible)throw new Exception("Invalid save changed language or announced success");
        Click(w,"Anuluj"); RedesignChecks.Wait(edit);

        using var server=new VncTestServer();
        var profile=new ServerProfile { Name="Restart reminder live fixture",Host="127.0.0.1",Port=server.Port,Protocol=Protocol.VNC,Vnc=new() { Authentication=VncAuthentication.None } };
        RedesignChecks.Wait(repo.SaveAsync(profile));
        using var pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane);
        RedesignChecks.Wait(pane.ConnectAsync("",CancellationToken.None));
        void Save(string language)
        {
            edit=Dialogs.Settings(main,repo,pane.ApplyAppearanceAsync); RedesignChecks.Frames(); w=main.OwnedWindows.Single();
            Named<ComboBox>(w,"ApplicationLanguage").SelectedIndex=language=="en"?1:0; Click(w,"Zapisz"); RedesignChecks.Wait(edit); RedesignChecks.Frames();
            if(repo.PreferencesAsync().Result.Language!=language || AppText.Language!=running || !pane.IsConnected || !main.OpenSessions.Contains(pane))throw new Exception("Saving language interrupted runtime language or live session");
        }
        Save(target);
        if(!banner.IsVisible || !banner.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains(target=="en"?"English":"Polski")==true))throw new Exception("Saved language reminder does not identify next language");
        if(!banner.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==AppText.T("Zapisano zmianę języka — wymagany restart",target)) ||
            AutomationProperties.GetName(Named<Button>(main,"LanguageRestartButton"))!=AppText.T("Uruchom ponownie",target))throw new Exception("Saved notice/action is not in destination language");
        var launches=0; main.StartReplacement=()=>launches++;
        Click(main,"Uruchom ponownie"); RedesignChecks.Frames();
        var confirmation=main.OwnedWindows.Single();
        if(confirmation.Title!=AppText.T("Uruchomić ponownie ServerDesk?",target) || AutomationProperties.GetName(Action(confirmation,"Anuluj"))!=AppText.T("Anuluj",target))throw new Exception("Restart confirmation language mismatch");
        if(new[]{"Anuluj","Potwierdź"}.Any(tag=>!Action(confirmation,tag).GetVisualDescendants().OfType<DeskIcon>().Any()) || !Action(confirmation,"Potwierdź").Classes.Contains("primary") || Action(confirmation,"Anuluj").Classes.Contains("primary"))throw new Exception("Translated confirmation lost its icons or primary/secondary distinction");
        confirmation.CaptureRenderedFrame()!.Save(Path.Combine(root,$"restart-confirmation-{running}-{target}.png"));
        Click(confirmation,"Anuluj"); RedesignChecks.Frames();
        if(launches!=0 || !pane.IsConnected || !main.IsVisible)throw new Exception("Cancelled restart launched a child or disconnected the session");
        main.StartReplacement=()=>throw new System.ComponentModel.Win32Exception("Fixture launch failure");
        Click(main,"Uruchom ponownie"); RedesignChecks.Frames(); Click(main.OwnedWindows.Single(),"Potwierdź"); RedesignChecks.Frames();
        var failure=main.OwnedWindows.Single(); failure.Close(); RedesignChecks.Frames();
        if(!pane.IsConnected || !main.IsVisible)throw new Exception("Failed restart disconnected the session");
        main.StartReplacement=()=>launches++;
        main.Width=900; main.Height=600; main.PreviewPreferences(main.CurrentPreferences with { InterfaceFontSize=16 }); RedesignChecks.Frames();
        Inside(banner,main); var titlebar=Named<Grid>(main,"WorkspaceTitleBar"); var captions=Named<StackPanel>(main,"WindowCaptionButtons"); Inside(captions,main);
        var host=((Grid)main.Content!).Children.OfType<Grid>().Single(g=>Grid.GetRow(g)==1 && Grid.GetColumn(g)==3);
        if(Bounds(titlebar,main).Bottom>Bounds(banner,main).Top || Bounds(banner,main).Bottom>Bounds(host,main).Top || host.Bounds.Height<250)throw new Exception("Restart reminder overlaps titlebar or live session");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"restart-live-{running}.png"));
        var dismiss=Named<Button>(main,"LanguageRestartDismiss");
        var point=dismiss.TranslatePoint(new Point(dismiss.Bounds.Width/2,dismiss.Bounds.Height/2),main)!.Value;
        main.MouseMove(point,RawInputModifiers.None); main.MouseDown(point,MouseButton.Left,RawInputModifiers.None); main.MouseUp(point,MouseButton.Left,RawInputModifiers.None); RedesignChecks.Frames();
        if(banner.IsVisible || !pane.IsConnected)throw new Exception("Dismiss action failed or disconnected VNC");
        edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=main.OwnedWindows.Single();
        if(!Named<Border>(w,"SettingsRestartReminder").IsVisible)throw new Exception("Reopened settings hide a pending restart");
        Click(w,"Anuluj"); RedesignChecks.Wait(edit);
        Save(target); if(!banner.IsVisible)throw new Exception("Saving pending language did not restore reminder");
        Save(running); if(banner.IsVisible)throw new Exception("Saving current language did not clear reminder");
        edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=main.OwnedWindows.Single(); Named<ComboBox>(w,"ApplicationLanguage").SelectedIndex=targetIndex;
        Click(w,"Zapisz i uruchom ponownie"); RedesignChecks.Wait(edit); RedesignChecks.Frames();
        if(repo.PreferencesAsync().Result.Language!=target)throw new Exception("Restart button failed to save selected language");
        Click(main.OwnedWindows.Single(),"Potwierdź");
        RedesignChecks.Wait(pane.CloseAsync());
        // Closing the transport precedes the remaining workspace/placement writes.
        // A fixed number of frames raced those writes on slower native VM disks.
        SshAcceptanceChecks.Until(()=>launches>0 && !main.IsVisible,"Restart did not finish root shutdown");
        if(launches!=1 || main.IsVisible || pane.IsConnected)throw new Exception("Restart did not use complete root shutdown or launched more than once");
        var snapshot=WorkspaceSnapshot.Parse(repo.SettingAsync("lastWorkspace").Result!);
        if(!snapshot.Sessions.Any(s=>s.ProfileId==profile.Id))throw new Exception("Restart shutdown lost the saved workspace tab");
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(original)); main.PreviewPreferences(original.Preferences); DesignSystem.Apply(original.Theme);
    }
}
