using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ExtendedSettingsChecks
{
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static T Setting<T>(Window root,string label) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>AutomationProperties.GetName(c)==AppText.T(label));
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static void Click(Window root,string tag)=>root.GetVisualDescendants().OfType<Button>().Single(c=>c.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Page(Window w,int index) { Named<TabControl>(w,"SettingsCategories").SelectedIndex=index; RedesignChecks.Frames(3); }
    private static TerminalControl Terminal(SessionPane pane)=>pane.Children.OfType<TerminalControl>().Single();
    private static void Cursor(TerminalControl terminal,XTerm.Common.CursorStyle style,bool blink)
    {
        RedesignChecks.Frames(3); var view=terminal.GetVisualDescendants().OfType<TerminalView>().Single();
        if(view.CursorStyle!=style || view.CursorBlink!=blink || terminal.Terminal.Options.CursorStyle!=style || terminal.Terminal.Options.CursorBlink!=blink)
            throw new Exception($"Cursor not applied to renderer and emulator: {view.CursorStyle}/{view.CursorBlink} vs {style}/{blink}");
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language)); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
            var original=Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language } }; Wait(repo.SaveApplicationSettingsAsync(original));
            var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            using var ssh=new SessionPane(main,new() { Name="Settings SSH",Host="fixture.invalid",Username="fixture" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
            using var serial=new SessionPane(main,new() { Name="Settings Serial",Protocol=Protocol.Serial,Serial=new() { PortName="fixture" } },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
            main.RegisterPane(ssh); main.RegisterPane(serial); RedesignChecks.Frames(20);
            // Both panels are realized to exercise cursor changes without opening a physical serial port.
            main.FocusSession(ssh); RedesignChecks.Frames(10);
            var terminals=new[]{Terminal(ssh),Terminal(serial)};
            foreach(var t in terminals) { t.ApplyTemplate(); t.Terminal.Reset(); t.Terminal.Write("PRESERVE_HISTORY_żółć\r\n"+string.Concat(Enumerable.Range(0,300).Select(i=>$"history-{i}\r\n"))); }
            RedesignChecks.Frames(5);
            var before=terminals.Select(t=>SshAcceptanceChecks.Text(t).TrimEnd('\n')).ToArray(); var buffers=terminals.Select(t=>t.BufferSize).ToArray();
            var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var w=main.OwnedWindows.Single();
            if(Named<TabControl>(w,"SettingsCategories").ItemCount!=6)throw new Exception("Missing SFTP settings category");
            Page(w,1); Named<ComboBox>(w,"TerminalCursor").SelectedIndex=1; Setting<CheckBox>(w,"Migający kursor").IsChecked=false; Named<NumericUpDown>(w,"TerminalScrollbackLines").Value=0;
            foreach(var t in terminals)Cursor(t,XTerm.Common.CursorStyle.Block,false);
            for(var i=0;i<terminals.Length;i++)if(terminals[i].BufferSize!=buffers[i] || SshAcceptanceChecks.Text(terminals[i]).TrimEnd('\n')!=before[i])throw new Exception($"Preview discarded terminal history: terminal {i}, buffer {terminals[i].BufferSize}/{buffers[i]}");
            var active=Terminal(ssh); active.Terminal.Write("\u001b[3 q"); RedesignChecks.Frames(3); Cursor(active,XTerm.Common.CursorStyle.Underline,true);
            Page(w,0); Setting<NumericUpDown>(w,"Rozmiar tekstu interfejsu").Value=16; Cursor(active,XTerm.Common.CursorStyle.Underline,true);
            Page(w,2); Named<TextBox>(w,"SftpLocalDirectory").Text=root; Named<ComboBox>(w,"SftpLocalSort").SelectedIndex=2; Named<ComboBox>(w,"SftpRemoteSort").SelectedIndex=1;
            Setting<CheckBox>(w,"Pokaż ukryte pliki po otwarciu SFTP").IsChecked=true; Setting<CheckBox>(w,"Sortuj lokalne pliki malejąco").IsChecked=true; Setting<CheckBox>(w,"Sortuj zdalne pliki malejąco").IsChecked=true; Named<ComboBox>(w,"SftpStartSide").SelectedIndex=0;
            foreach(var theme in new[]{"graphite","light"})foreach(var width in new[]{680,960})
            {
                DesignSystem.Apply(theme); w.Width=width; w.Height=width==680?500:720;
                foreach(var page in new[]{1,2})
                {
                    Page(w,page); var tabs=Named<TabControl>(w,"SettingsCategories"); var scroll=(ScrollViewer)((TabItem)tabs.SelectedItem!).Content!;
                    var navigation=Named<ScrollViewer>(w,"SettingsNavigationScroll");
                    var tabPositions=tabs.Items.OfType<TabItem>().Select(tab=>tab.TranslatePoint(default,navigation)!.Value).ToArray();
                    if(tabPositions.Select(p=>p.X).Distinct().Count()!=1 || navigation.Bounds.Width>(width==680?85:220))throw new Exception("Settings categories wrap into extra columns");
                    var save=w.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz");
                    var footer=save.TranslatePoint(default,w)!.Value;
                    if(footer.Y+save.Bounds.Height>w.Bounds.Height+1 || scroll.TranslatePoint(default,w)!.Value.Y+scroll.Bounds.Height>footer.Y+1)throw new Exception("Settings page overlaps footer");
                    var input=page==2?(Control)Named<TextBox>(w,"SftpLocalDirectory"):Named<ComboBox>(w,"TerminalCursor"); input.BringIntoView(); RedesignChecks.Frames(3);
                    var point=input.TranslatePoint(default,scroll)!.Value;
                    if(point.X<0 || point.X+input.Bounds.Width>scroll.Bounds.Width+1)throw new Exception("New setting escapes page width");
                    w.CaptureRenderedFrame()!.Save(Path.Combine(root,$"extended-settings-{language}-{theme}-{width}-{page}.png"));
                }
            }
            Click(w,"Anuluj"); Wait(edit); RedesignChecks.Frames();
            if(Result(repo.ApplicationSettingsAsync())!=original || main.CurrentPreferences!=original.Preferences)throw new Exception("Cancel persisted new preferences");
            Cursor(active,XTerm.Common.CursorStyle.Bar,true);
            edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=main.OwnedWindows.Single(); Page(w,2);
            var local=Named<TextBox>(w,"SftpLocalDirectory"); local.Text="relative/path"; Named<TextBox>(w,"SettingsSearch").Text="CPU"; Click(w,"Zapisz"); RedesignChecks.Frames();
            if(edit.IsCompleted || Named<TabControl>(w,"SettingsCategories").SelectedIndex!=2 || !local.IsEffectivelyVisible || !local.Classes.Contains("error") || Result(repo.ApplicationSettingsAsync())!=original)throw new Exception("Invalid path saved or validation did not reveal its category");
            local.Text=root; Named<ComboBox>(w,"SftpLocalSort").SelectedIndex=3; Named<ComboBox>(w,"SftpRemoteSort").SelectedIndex=2;
            Named<ComboBox>(w,"SftpStartSide").SelectedIndex=0; Setting<CheckBox>(w,"Pokaż ukryte pliki po otwarciu SFTP").IsChecked=true; Setting<CheckBox>(w,"Sortuj lokalne pliki malejąco").IsChecked=true;
            Page(w,1); Named<ComboBox>(w,"TerminalCursor").SelectedIndex=2; Setting<CheckBox>(w,"Migający kursor").IsChecked=false; Named<NumericUpDown>(w,"TerminalScrollbackLines").Value=100000;
            Click(w,"Zapisz"); Wait(edit); var saved=Result(repo.PreferencesAsync());
            if(saved.TerminalScrollbackLines!=100000 || saved.TerminalCursor!=TerminalCursorShape.Underline || saved.TerminalCursorBlink || saved.SftpLocalDirectory!=root || saved.SftpLocalSort!=FileSort.Type || saved.SftpRemoteSort!=FileSort.Modified || !saved.SftpShowHidden || !saved.SftpLocalDescending || saved.SftpStartRemote)throw new Exception("New settings failed to persist together");
            if(active.BufferSize!=buffers[0] || SshAcceptanceChecks.Text(active).TrimEnd('\n')!=before[0])throw new Exception("Save trimmed the existing terminal buffer");
            edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=main.OwnedWindows.Single(); Page(w,2);
            if(Named<TextBox>(w,"SftpLocalDirectory").Text!=root || Named<ComboBox>(w,"SftpRemoteSort").SelectedIndex!=2)throw new Exception("Reopening lost SFTP preferences");
            var search=Named<TextBox>(w,"SettingsSearch"); search.Text=AppText.T("Wiersze historii terminala"); RedesignChecks.Frames();
            if(Named<TabControl>(w,"SettingsCategories").SelectedIndex!=1 || !Named<NumericUpDown>(w,"TerminalScrollbackLines").IsEffectivelyVisible)throw new Exception("New terminal preferences are not searchable");
            search.Text=""; Click(w,"Domyślne ustawienia"); Click(w,"Zapisz"); Wait(edit);
            if(Result(repo.PreferencesAsync())!=new ApplicationPreferences())throw new Exception("Defaults failed to restore mixed true/false settings");
            Wait(ssh.CloseAsync()); Wait(serial.CloseAsync()); main.Close(); RedesignChecks.Frames();
        }
        AppText.SetLanguage("pl"); DesignSystem.Apply("dark");
        Console.WriteLine("PASS extended settings PL/EN: six responsive categories, search, invalid-path recovery, cursor live preview and remote overrides, retained history, cancel/save/reopen/defaults; Graphite/Light font16 at 680x500 and 960x720");
    }
    internal static void Network(string root,string host,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"data")); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        var local=Directory.CreateDirectory(Path.Combine(root,"local żółć")).FullName; Directory.CreateDirectory(Path.Combine(local,"child")); File.WriteAllText(Path.Combine(local,".hidden"),"fixture");
        var defaults=new ApplicationPreferences { TerminalScrollbackLines=200,TerminalCursor=TerminalCursorShape.Block,TerminalCursorBlink=false,SftpLocalDirectory=local,SftpShowHidden=true,SftpLocalSort=FileSort.Size,SftpRemoteSort=FileSort.Modified,SftpLocalDescending=true,SftpRemoteDescending=true,SftpStartRemote=false,ShowMetrics=false };
        var settings=Result(repo.ApplicationSettingsAsync()) with { Preferences=defaults }; Wait(repo.SaveApplicationSettingsAsync(settings));
        var factory=new SshConnectionFactory(repo); var main=new MainWindow(new(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        factory.ConfirmHostKey=(_,_)=>Task.FromResult(true); // Disposable, explicitly launched loopback fixture only.
        var profile=new ServerProfile { Name="Settings fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); var window=new Window { Width=1100,Height=800,Content=pane }; window.Show(); RedesignChecks.Frames(20);
        try
        {
            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(pane.ShowAsync(1));
            if(Terminal(pane).BufferSize!=200 || Field<TextBox>(pane,"localPath").Text!=local || !Field<CheckBox>(pane,"hidden").IsChecked.GetValueOrDefault() || Field<ComboBox>(pane,"localSort").SelectedIndex!=1 || Field<ComboBox>(pane,"remoteSort").SelectedIndex!=2 || !Field<bool>(pane,"localDescending") || !Field<bool>(pane,"remoteDescending") || Field<ComboBox>(pane,"fileTarget").SelectedIndex!=0)throw new Exception("Saved first-open defaults ignored");
            if(!Field<ListBox>(pane,"localList").Items.OfType<FileEntry>().Any(f=>f.Name==".hidden"))throw new Exception("Initial hidden-file preference ignored by file view");
            Wait(pane.ShowAsync(0)); RedesignChecks.Frames(10); Cursor(Terminal(pane),XTerm.Common.CursorStyle.Block,false); var terminal=Terminal(pane); var sessionId=terminal.SessionId; var emulator=terminal.Terminal;
            terminal.Terminal.Write(string.Concat(Enumerable.Range(0,500).Select(i=>$"bounded-history-{i}\r\n"))); RedesignChecks.Frames(3);
            if(terminal.Terminal.Options.Scrollback!=200 || terminal.Terminal.Buffer.Lines.Length>terminal.Terminal.Rows+200 || SshAcceptanceChecks.Text(terminal).Contains("bounded-history-0\n") || !SshAcceptanceChecks.Text(terminal).Contains("bounded-history-499"))throw new Exception("Configured nonzero history cap ignored by emulator");
            Wait(terminal.SendInputAsync("printf 'SETTINGS_BUFFER_PRESERVED\\n'\r",default)); RedesignChecks.Frames(25); var before=SshAcceptanceChecks.Text(terminal).TrimEnd('\n');
            Field<ComboBox>(pane,"localSort").SelectedIndex=3; Field<CheckBox>(pane,"hidden").IsChecked=false; Field<ComboBox>(pane,"fileTarget").SelectedIndex=1;
            var changed=defaults with { TerminalScrollbackLines=0,TerminalCursor=TerminalCursorShape.Underline,TerminalCursorBlink=true,SftpLocalDirectory=Path.Combine(local,"missing"),SftpShowHidden=true,SftpLocalSort=FileSort.Name,SftpRemoteSort=FileSort.Size,SftpStartRemote=false };
            Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=changed })); pane.ApplySessionPreferences(changed); Wait(pane.ApplyAppearanceAsync());
            Cursor(terminal,XTerm.Common.CursorStyle.Underline,true);
            if(!pane.IsConnected || terminal.SessionId!=sessionId || !ReferenceEquals(emulator,terminal.Terminal) || terminal.BufferSize!=200 || !SshAcceptanceChecks.Text(terminal).Contains(before))throw new Exception("Saving preferences disrupted live SSH/history");
            Wait(pane.ShowAsync(1));
            if(Field<TextBox>(pane,"localPath").Text!=local || Field<ComboBox>(pane,"localSort").SelectedIndex!=3 || Field<CheckBox>(pane,"hidden").IsChecked==true || Field<ComboBox>(pane,"fileTarget").SelectedIndex!=1)throw new Exception("Saved defaults reset an open SFTP browser");
            Wait(pane.ReconnectAsync()); Wait(pane.ShowAsync(1));
            if(terminal.BufferSize!=0 || Field<ComboBox>(pane,"localSort").SelectedIndex!=3 || Field<CheckBox>(pane,"hidden").IsChecked==true)throw new Exception("Reconnect ignored new history or reset per-pane SFTP choices");
            using var next=new SessionPane(main,profile with { Id=Guid.NewGuid() },factory,new ExternalRdpClient(),repo); var nextWindow=new Window { Width=1100,Height=800,Content=next }; nextWindow.Show(); RedesignChecks.Frames(20);
            try
            {
                Wait(next.ConnectAsync("serverdesk-test-passphrase",default)); Wait(next.ShowAsync(1));
                var home=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
                if(Field<TextBox>(next,"localPath").Text!=home || Field<ComboBox>(next,"remoteSort").SelectedIndex!=1 || !Field<CheckBox>(next,"hidden").IsChecked.GetValueOrDefault() || Field<ComboBox>(next,"fileTarget").SelectedIndex!=0 || !Field<string>(next,"appearanceWarning").Contains(AppText.T("Katalog startowy SFTP jest niedostępny. Otwarto lokalny katalog domowy.")))throw new Exception("New pane ignored defaults or silently lost unavailable start directory");
                nextWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"sftp-start-fallback.png"));
                Wait(next.ShowAsync(0)); RedesignChecks.Frames(10); var t=Terminal(next); t.Terminal.Write(string.Concat(Enumerable.Repeat("no-history\r\n",500))); RedesignChecks.Frames(5);
                if(t.Terminal.Buffer.Lines.Length>t.Terminal.Rows)throw new Exception($"Zero scrollback did not disable history: control={t.BufferSize}, options={t.Terminal.Options.Scrollback}, rows={t.Terminal.Rows}, lines={t.Terminal.Buffer.Lines.Length}");
            }
            finally { Wait(next.CloseAsync()); nextWindow.Close(); }
            Console.WriteLine("PASS real SSH/SFTP defaults: first open, hidden files, independent sort/direction/active side; live cursor retains connection/buffer; next-connection zero history; same-pane choices survive reconnect; new pane uses latest preferences and notified home fallback");
        }
        finally { Wait(pane.CloseAsync()); window.Close(); main.Close(); RedesignChecks.Frames(); }
    }
}
