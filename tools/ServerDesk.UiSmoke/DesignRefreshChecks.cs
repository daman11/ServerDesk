using System.Reflection;
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

internal static class DesignRefreshChecks
{
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static T Named<T>(Control parent,string name) where T:Control=>parent.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static T Setting<T>(Window window,string label) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>AutomationProperties.GetName(c)==AppText.T(label));
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(); }
    private static void InViewport(Control control,Window window)
    {
        var point=control.TranslatePoint(default,window)!.Value;
        if(point.X<-.5 || point.Y<-.5 || point.X+control.Bounds.Width>window.ClientSize.Width+.5 || point.Y+control.Bounds.Height>window.ClientSize.Height+.5)throw new Exception("Control outside viewport: "+control.Name);
    }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"refresh-"+language+"-"+Guid.NewGuid().ToString("N")))); RedesignChecks.Wait(repo.InitializeAsync());
            var credentials=new CountingStore(); var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,credentials,new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            if(Named<Control>(main,"WelcomeRecent").IsVisible || Named<TextBlock>(main,"WelcomeSummary").Text!=AppText.F($"Zapisane: {0} · ulubione: {0} · protokoły: {0}"))throw new Exception("Empty welcome contains fabricated history or counts");
            var now=DateTimeOffset.UtcNow;
            var profiles=Enumerable.Range(0,5).Select(i=>new ServerProfile { Name=i==0?"Anuluj":"server"+i,Host="192.0.2."+(10+i),Username="fixture",Favorite=i<2,LastConnected=i==4?null:now.AddDays(-i) }).ToArray();
            foreach(var profile in profiles)RedesignChecks.Wait(repo.SaveAsync(profile)); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames();
            Button[] Recent()=>main.GetVisualDescendants().OfType<Button>().Where(b=>b.Name=="WelcomeRecentConnection").ToArray();
            void RecentNames(params string[] expected) { if(!Recent().Select(b=>b.Tag as string).SequenceEqual(expected))throw new Exception("Welcome recent order/limit: "+string.Join(",",Recent().Select(b=>b.Tag))); }
            RecentNames("Anuluj","server1","server2");
            if(Named<TextBlock>(main,"WelcomeSummary").Text!=AppText.F($"Zapisane: {5} · ulubione: {2} · protokoły: {1}") || Recent().Any(b=>b.IsDefault || b.IsCancel) || credentials.Reads!=0 || main.OpenSessions.Count>0)throw new Exception("Welcome counts, credential isolation or user-data button semantics failed");
            var first=Recent()[0]; RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames(); if(!ReferenceEquals(first,Recent()[0]))throw new Exception("Unchanged profile refresh replaces focused recent controls");
            RedesignChecks.Wait(repo.SaveAsync(profiles[0] with { Name="Production renamed" })); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames(); RecentNames("Production renamed","server1","server2");
            RedesignChecks.Wait(repo.DeleteAsync(profiles[1].Id)); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames(); RecentNames("Production renamed","server2","server3");
            foreach(var theme in new[]{"graphite","light"})foreach(var width in new[]{900,1320})
            {
                main.PreviewPreferences(preferences with { InterfaceFontSize=16 }); DesignSystem.Apply(theme); main.Width=width; main.Height=width==900?600:840; RedesignChecks.Frames();
                var actions=Named<Grid>(main,"WelcomeActions"); if(actions.Children.Count!=4 || actions.ColumnDefinitions.Count!=2)throw new Exception("Welcome actions did not adapt to main window");
                if(Math.Abs(actions.Children[0].Bounds.Height-actions.Children[1].Bounds.Height)>.5 || Math.Abs(actions.Children[2].Bounds.Height-actions.Children[3].Bounds.Height)>.5)throw new Exception("Welcome card heights differ within a row");
                foreach(var card in actions.Children) { card.BringIntoView(); RedesignChecks.Frames(2); InViewport(card,main); }
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"welcome-{language}-{theme}-{width}.png"));
            }
            main.PreviewPreferences(preferences with { SidebarWidth=500,InterfaceFontSize=16 }); main.Width=900; RedesignChecks.Frames();
            var narrowActions=Named<Grid>(main,"WelcomeActions"); if(narrowActions.ColumnDefinitions.Count!=1)throw new Exception("Narrow welcome did not switch to one column");
            foreach(var card in narrowActions.Children) { card.BringIntoView(); RedesignChecks.Frames(2); InViewport(card,main); }
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"welcome-{language}-narrow.png"));
            main.PreviewPreferences(preferences); main.Width=1320; main.Height=840;
            Click(Recent()[0]); if(credentials.Reads!=1 || main.OwnedWindows.Count!=1)throw new Exception("Recent action bypassed normal credential prompt"); main.OwnedWindows.Single().Close(); RedesignChecks.Frames();
            if(main.OpenSessions.Count!=0)throw new Exception("Cancelling welcome credential prompt started a session");
            Settings(main,repo,root,language,preferences); Files(main,repo,root,language,preferences);
            main.Close(); RedesignChecks.Frames(20); if(main.IsVisible)throw new Exception("Design fixture did not shut down");
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
        Console.WriteLine("PASS design refresh PL/EN: real welcome counts and recent order/limit/rename/delete, no automatic credentials, normal prompt/cancel, equal action cards and one/two-column scrolling; settings cross-category search, diacritics, empty/clear/keyboard, draft retention and rollback; SFTP panel focus/rename/selection, row/header bounds, font16, densities, Graphite/Light and narrow windows");
    }
    private static void Settings(MainWindow main,ProfileRepository repo,string root,string language,ApplicationPreferences preferences)
    {
        var original=repo.ApplicationSettingsAsync().GetAwaiter().GetResult(); var runtime=main.CurrentPreferences;
        var task=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var window=main.OwnedWindows.Single();
        var search=Named<TextBox>(window,"SettingsSearch"); var categories=Named<TabControl>(window,"SettingsCategories"); var tabs=categories.Items.OfType<TabItem>().ToArray();
        foreach(var theme in new[]{"graphite","light"})
        {
            DesignSystem.Apply(theme); RedesignChecks.Frames(); InViewport(search,window);
            if(search.TranslatePoint(default,window)!.Value.X<500)throw new Exception("Wide settings header did not place search beside title");
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"settings-appearance-{language}-{theme}.png"));
        }
        categories.SelectedIndex=1; RedesignChecks.Frames(); var font=Setting<NumericUpDown>(window,"Rozmiar czcionki terminala"); font.Value=19;
        categories.SelectedIndex=0; RedesignChecks.Frames(); Setting<NumericUpDown>(window,"Rozmiar tekstu interfejsu").Value=16;
        search.Text="cpu"; RedesignChecks.Frames(); if(categories.SelectedIndex!=4 || tabs.Count(t=>t.IsVisible)!=1 || !Setting<CheckBox>(window,"CPU").IsEffectivelyVisible)throw new Exception("Search did not expose matching monitoring category");
        search.Text="polaczen"; RedesignChecks.Frames(); if(categories.SelectedIndex!=3 || !Setting<NumericUpDown>(window,"Szerokość panelu połączeń").IsEffectivelyVisible)throw new Exception("Settings search does not ignore Polish diacritics");
        search.Text="terminal font"; RedesignChecks.Frames(); if(language=="en" && (!font.IsEffectivelyVisible || font.Value!=19))throw new Exception("English multi-term search or draft retention failed");
        search.Text=AppText.T("Wiersze historii terminala"); RedesignChecks.Frames();
        var historyHint=AppText.T("Kursor ma podgląd na żywo. Programy zdalne mogą zmieniać jego wygląd. Limit historii obowiązuje przy następnym połączeniu SSH lub Serial; 0 wyłącza historię. Bieżący bufor pozostaje zachowany.");
        if(!window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==historyHint && t.IsEffectivelyVisible))throw new Exception("Searching a setting hid its next-connection/contextual help");
        search.Text=AppText.T("Język aplikacji / Language"); RedesignChecks.Frames();
        if(!Named<TextBlock>(window,"LanguageRestartNotice").IsEffectivelyVisible)throw new Exception("Searching language hid the restart notice");
        search.Text=AppText.T("Krój czcionki interfejsu"); RedesignChecks.Frames();
        if(Named<StackPanel>(window,"AccentColorChoices").IsEffectivelyVisible)throw new Exception("Font-only search exposed unrelated accent preset actions");
        search.Text="qwerty-no-match"; RedesignChecks.Frames(); if(categories.IsVisible || !Named<TextBlock>(window,"SettingsSearchEmpty").IsVisible)throw new Exception("Settings empty search state missing");
        search.Focus(); window.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,"\u001b"); window.KeyRelease(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,"\u001b"); RedesignChecks.Frames();
        if(search.Text!="" || !window.IsVisible || !categories.IsVisible || tabs.Any(t=>!t.IsVisible))throw new Exception("Escape did not clear search without closing settings");
        categories.Focus(); window.KeyPress(Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); window.KeyRelease(Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); RedesignChecks.Frames(); if(!search.IsKeyboardFocusWithin)throw new Exception("Ctrl+F did not focus settings search");
        foreach(var theme in new[]{"graphite","light"})
        {
            DesignSystem.Apply(theme); window.Width=960; window.Height=720; search.Text="terminal"; RedesignChecks.Frames();
            if(font.Value!=19)throw new Exception("Filtering discarded terminal font draft"); window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"settings-search-{language}-{theme}.png"));
            window.Width=680; window.Height=500; search.Text="qwerty-no-match"; RedesignChecks.Frames();
            foreach(var button in window.GetVisualDescendants().OfType<Button>().Where(b=>b.Tag as string is "Zapisz" or "Anuluj" or "Domyślne ustawienia"))InViewport(button,window);
            InViewport(search,window); InViewport(Named<TextBlock>(window,"SettingsSearchEmpty"),window); InViewport(Named<Button>(window,"SettingsShowAll"),window); window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"settings-empty-{language}-{theme}.png"));
            var emptyViewport=Named<ScrollViewer>(window,"SettingsSearchViewport"); var recovery=Named<Button>(window,"SettingsShowAll"); var recoveryPoint=recovery.TranslatePoint(default,emptyViewport)!.Value;
            if(recoveryPoint.Y<0 || recoveryPoint.Y+recovery.Bounds.Height>emptyViewport.Bounds.Height+.5)throw new Exception("Settings recovery action is obscured by the footer/restart reminder");
            Click(Named<Button>(window,"SettingsShowAll"));
            if(search.Text!="" || !search.IsKeyboardFocusWithin || !categories.IsVisible || font.Value!=19 || !window.IsVisible)throw new Exception("Empty-settings recovery lost draft, focus or the open dialog");
        }
        Click(window.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)==AppText.T("Wyczyść wyszukiwanie ustawień")));
        categories.SelectedIndex=0; RedesignChecks.Frames();
        if(search.Text!="" || Named<TextBlock>(window,"SettingsSearchCount").IsVisible || !Named<TextBlock>(window,"LanguageRestartNotice").IsVisible)throw new Exception("Clear search did not restore notes/count");
        Click(window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj")); RedesignChecks.Wait(task);
        if(repo.ApplicationSettingsAsync().GetAwaiter().GetResult()!=original || main.CurrentPreferences!=runtime)throw new Exception("Search/cancel changed persisted settings or failed preview rollback");
        task=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); window=main.OwnedWindows.Single();
        var accent=Setting<TextBox>(window,"Kolor akcentu"); accent.Text="invalid accent"; search=Named<TextBox>(window,"SettingsSearch"); search.Text="cpu"; RedesignChecks.Frames();
        Click(window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz"));
        if(task.IsCompleted || search.Text!="" || Named<TabControl>(window,"SettingsCategories").SelectedIndex!=0 || !accent.IsEffectivelyVisible || repo.ApplicationSettingsAsync().GetAwaiter().GetResult()!=original)throw new Exception("Save validation did not expose a filtered invalid field");
        Click(window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj")); RedesignChecks.Wait(task);
    }
    private static void Files(MainWindow main,ProfileRepository repo,string root,string language,ApplicationPreferences preferences)
    {
        using var pane=new SessionPane(main,new() { Name="Production · long remote server name",Host="fixture.invalid",Username="fixture" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
        var window=new Window { Width=1400,Height=760,Content=pane }; window.Show(); RedesignChecks.Frames();
        pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().IsVisible=false; var files=Field<Grid>(pane,"files"); files.IsVisible=true;
        var entries=new[]{new FileEntry("file2.txt","/fixture/file2.txt",false,false,12345,DateTime.Now,"644"),new FileEntry("file10.txt","/fixture/file10.txt",false,false,4567890,DateTime.Now,"640")};
        Field<List<FileEntry>>(pane,"localEntries").AddRange(entries); Field<List<FileEntry>>(pane,"remoteEntries").AddRange(entries); typeof(SessionPane).GetMethod("FilterFiles",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,null); RedesignChecks.Frames();
        var local=Field<ListBox>(pane,"localList"); var remote=Field<ListBox>(pane,"remoteList"); var target=Field<ComboBox>(pane,"fileTarget");
        remote.SelectedItem=remote.Items.OfType<FileEntry>().First(); var selected=remote.SelectedItem;
        Click(Named<Button>(window,"SftpLocalPanel")); if(target.SelectedIndex!=0 || !local.IsKeyboardFocusWithin)throw new Exception("Local panel heading did not select/focus list");
        Click(Named<Button>(window,"SftpRemotePanel")); if(target.SelectedIndex!=1 || !remote.IsKeyboardFocusWithin || !ReferenceEquals(selected,remote.SelectedItem))throw new Exception("Remote heading lost selection/focus");
        pane.UpdatePresentation(pane.Profile with { Name="Renamed live server" }); RedesignChecks.Frames(); if(!Named<Button>(window,"SftpRemotePanel").GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==AppText.T("Zdalny")+" · Renamed live server"))throw new Exception("SFTP heading did not follow profile rename");
        foreach(var theme in new[]{"graphite","light"})foreach(var density in new[]{InterfaceDensity.Compact,InterfaceDensity.Comfortable})foreach(var width in new[]{650,1400})
        {
            DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16,Density=density }); DesignSystem.Apply(theme); window.Width=width; RedesignChecks.Frames();
            foreach(var button in window.GetVisualDescendants().OfType<Button>().Where(b=>b.IsEffectivelyVisible && (b.Name?.Contains("Sort_")==true || b.Name is "SftpLocalPanel" or "SftpRemotePanel")))InViewport(button,window);
            foreach(var list in new[]{local,remote}.Where(c=>c.IsEffectivelyVisible))foreach(var item in list.GetVisualDescendants().OfType<ListBoxItem>())
                foreach(var label in item.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.IsEffectivelyVisible)) { var p=label.TranslatePoint(default,item)!.Value; if(p.Y<-.5 || p.Y+label.Bounds.Height>item.Bounds.Height+.5)throw new Exception("Font16 file row clips text"); }
            if(target.SelectedIndex!=1 || !ReferenceEquals(selected,remote.SelectedItem) || width==650 && local.IsEffectivelyVisible)throw new Exception("Responsive SFTP lost active panel/selection");
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"sftp-panels-{language}-{theme}-{density}-{width}.png"));
        }
        window.Close(); DesignSystem.ApplyPreferences(preferences);
    }
    private sealed class CountingStore:ICredentialStore
    {
        internal int Reads;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; return Task.FromResult<string?>(null); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
