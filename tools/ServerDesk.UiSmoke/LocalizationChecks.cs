using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class LocalizationChecks
{
    private static T Field<T>(Window w,string name) where T:Control=>w.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Window w,string source)=>w.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==source).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static bool Text(Window w,string value)=>w.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==value);
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var w=main.OwnedWindows.Single(); Field<ComboBox>(w,"ApplicationLanguage").SelectedIndex=1; Click(w,"Anuluj"); RedesignChecks.Wait(edit);
        if(repo.PreferencesAsync().Result.Language!="pl" || AppText.Language!="pl")throw new Exception("Cancelling language choice changed runtime or saved language");
        edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=main.OwnedWindows.Single(); Field<ComboBox>(w,"ApplicationLanguage").SelectedIndex=1; Click(w,"Zapisz"); RedesignChecks.Wait(edit);
        if(repo.PreferencesAsync().Result.Language!="en" || AppText.Language!="pl")throw new Exception("Language not persisted or applied before declared restart");
        main.Close(); RedesignChecks.Frames(20);
        AppText.SetLanguage(repo.PreferencesAsync().Result.Language);
        var paths=new AppPaths(); var english=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); english.Show(); RedesignChecks.Frames(25);
        if(!Text(english,"CONNECTIONS") || !Text(english,"Ready to work") || !Text(english,"Layout"))throw new Exception("English main window is incomplete");
        var sentinel=new ServerProfile { Name="Połącz",Host="localhost",Username="test",Folder="Ustawienia" }; RedesignChecks.Wait(repo.SaveAsync(sentinel)); RedesignChecks.Wait(((MainViewModel)english.DataContext!).LoadAsync()); RedesignChecks.Frames(20);
        if(!Text(english,"Połącz") || !Text(english,"Ustawienia"))throw new Exception("User profile or folder was translated");
        english.CaptureRenderedFrame()!.Save(Path.Combine(root,"main-en.png"));
        edit=Dialogs.Settings(english,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=english.OwnedWindows.Single();
        if(w.Title!="ServerDesk settings" || Field<ComboBox>(w,"ApplicationLanguage").SelectedIndex!=1 || !Text(w,"The language change takes effect after restarting ServerDesk."))throw new Exception("English language settings or restart notice missing");
        var categories=Field<TabControl>(w,"SettingsCategories");
        foreach(var index in Enumerable.Range(0,4))
        {
            categories.SelectedIndex=index; RedesignChecks.Frames();
            w.CaptureRenderedFrame()!.Save(Path.Combine(root,"settings-en-"+index+".png"));
        }
        categories.SelectedIndex=0; Field<ComboBox>(w,"ApplicationLanguage").SelectedIndex=0; Click(w,"Anuluj"); RedesignChecks.Wait(edit); if(repo.PreferencesAsync().Result.Language!="en")throw new Exception("Cancel in English changed language");
        edit=Dialogs.Edit(english,null,[]); RedesignChecks.Frames(); w=english.OwnedWindows.Single();
        foreach(var theme in new[]{"dark","light"})foreach(var width in new[]{600,780})
        {
            DesignSystem.Apply(theme); w.Width=width; w.Height=520;
            foreach(var index in Enumerable.Range(0,3)) { PolishChecks.Page(w,index); RedesignChecks.Frames(); PolishChecks.AssertNavigation(w); w.CaptureRenderedFrame()!.Save(Path.Combine(root,$"editor-en-{theme}-{width}-{index}.png")); }
        }
        PolishChecks.Page(w,0); Click(w,"Zapisz"); RedesignChecks.Frames();
        if(edit.IsCompleted || !Text(w,"Enter a server name."))throw new Exception("English field validation missing");
        Field<TextBox>(w,"ProfileName").Text="Połącz"; Field<TextBox>(w,"ProfileHost").Text="localhost"; Field<TextBox>(w,"ProfileUsername").Text="test"; Click(w,"Zapisz"); RedesignChecks.Wait(edit);
        if(((Task<ServerProfile?>)edit).Result?.Name!="Połącz")throw new Exception("English profile editor translated user input");
        foreach(var protocol in new[]{Protocol.VNC,Protocol.Serial,Protocol.RDP})
        {
            edit=Dialogs.Edit(english,sentinel with { Protocol=protocol,Serial=new() { PortName="COM999" } },[]); RedesignChecks.Frames(); w=english.OwnedWindows.Single(); if(protocol==Protocol.RDP)PolishChecks.Page(w,3); w.CaptureRenderedFrame()!.Save(Path.Combine(root,"editor-en-"+protocol+".png")); Click(w,"Anuluj"); RedesignChecks.Wait(edit);
        }
        foreach(var protocol in new[]{Protocol.SSH,Protocol.VNC,Protocol.Serial,Protocol.RDP})
        {
            using var pane=new SessionPane(english,sentinel with { Protocol=protocol,Serial=new() { PortName="COM999" } },new SshConnectionFactory(repo),new ExternalRdpClient(),repo); var window=new Window { Width=960,Height=650,Content=pane }; window.Show(); RedesignChecks.Frames();
            if(!window.GetVisualDescendants().OfType<Button>().Any(b=>AutomationProperties.GetName(b)=="Connect"))throw new Exception("English connect action missing: "+protocol);
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,"pane-en-"+protocol+".png")); pane.Dispose(); window.Close();
        }
        using(var server=new VncTestServer())
        {
            var profile=sentinel with { Id=Guid.NewGuid(),Protocol=Protocol.VNC,Port=server.Port,Host="127.0.0.1",Vnc=new() { Authentication=VncAuthentication.None } };
            using var pane=new SessionPane(english,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); var window=new Window { Width=960,Height=650,Content=pane }; window.Show(); RedesignChecks.Frames();
            RedesignChecks.Wait(pane.ConnectAsync("",CancellationToken.None));
            if(!pane.IsConnected || !pane.ConnectionStatus.Contains("VNC") || !window.GetVisualDescendants().OfType<Button>().Any(b=>AutomationProperties.GetName(b)=="Connected"))throw new Exception("English live VNC status/actions failed");
            if(repo.HistoryAsync(profileId:profile.Id).Result.Single().Name!="Połącz")throw new Exception("English live history changed the profile name");
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,"vnc-live-en.png")); RedesignChecks.Wait(pane.CloseAsync()); window.Close();
        }
        RedesignChecks.Wait(repo.ConnectedAsync(sentinel)); var history=Dialogs.History(english,repo); RedesignChecks.Frames(20); w=english.OwnedWindows.Single(); if(w.Title!="Connection history" || !Text(w,"Połącz"))throw new Exception("English history changed profile data or title"); w.CaptureRenderedFrame()!.Save(Path.Combine(root,"history-en.png")); w.Close(); RedesignChecks.Wait(history);
        var commands=Dialogs.Commands(english,new[]{"New connection","Settings"}); RedesignChecks.Frames(); w=english.OwnedWindows.Single(); if(w.Title!="Command palette")throw new Exception("English command palette missing"); w.Close(); RedesignChecks.Wait(commands);
        edit=Dialogs.Settings(english,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=english.OwnedWindows.Single(); Field<ComboBox>(w,"ApplicationLanguage").SelectedIndex=0; Click(w,"Zapisz"); RedesignChecks.Wait(edit); if(repo.PreferencesAsync().Result.Language!="pl" || AppText.Language!="en")throw new Exception("Switch back did not persist for next launch");
        english.Close(); RedesignChecks.Frames(20); AppText.SetLanguage("pl"); DesignSystem.Apply("dark");
        Console.WriteLine("PASS Polish/English settings save/cancel/reopen/restart semantics; main/editor/settings/history/commands/protocol panes, validation, dark/light small layouts; profile, folder and input values remain unchanged");
    }
}
