using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class HelpChecks
{
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(control=>control.Name==name);
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static T Result<T>(Task<T> task) { RedesignChecks.Wait(task); return task.GetAwaiter().GetResult(); }
    internal static void Run(string root)
    {
        var pl=HelpContent.Read("pl"); var en=HelpContent.Read("en");
        if(pl.Length!=15 || !pl.Select(topic=>topic.Id).SequenceEqual(en.Select(topic=>topic.Id)))throw new Exception("PL/EN help coverage differs");
        foreach(var topic in pl.Concat(en))
        {
            if(DeskIcons.Resolve(topic.Icon)=="file" || topic.PlainText.Length<300)throw new Exception("Help topic is incomplete or has a missing icon: "+topic.Id);
            if(topic.Matches("no-such-help-1f81e8"))throw new Exception("Search matches unrelated text");
        }
        if(!pl.Single(topic=>topic.Id=="shortcuts").Matches("skroty klawiatura") || !en.Single(topic=>topic.Id=="security").Matches("secret-tool unlocked"))throw new Exception("Help search does not cover contents/keywords or diacritics");
        try { HelpContent.Read("../en"); throw new Exception("Unknown help language accepted"); } catch(ArgumentException) { }
        var original=DesignSystem.Preferences; var originalTheme=DesignSystem.CurrentId;
        try { foreach(var language in new[]{"pl","en"})CheckWindow(root,language); }
        finally { AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(original); DesignSystem.Apply(originalTheme); }
        Console.WriteLine("PASS help: 15 complete embedded topics in PL/EN, content/diacritic/multi-term search, menu/rail/palette/F1 singleton, copy/empty/keyboard, all topics/font16/Graphite/Light/wide/narrow, dock/float/redock/close/reopen, correct switcher identity, no credentials/connections or saved help session, terminal F1 retained");
    }
    private static void CheckWindow(string root,string language)
    {
        AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language)); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
        var settings=ApplicationSettings.Defaults with { Preferences=new() { Language=language,InterfaceFontSize=16,DisabledPlugins=new(new[]{PluginCatalog.Commands,PluginCatalog.LinuxMonitoring}) } };
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings)); var credentials=new UnusedCredentials();
        var main=new MainWindow(new(repo),repo,credentials,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); main.SetWorkspaceMode(WorkspaceMode.Single); main.Width=1320;
        try
        {
            var menu=main.GetVisualDescendants().OfType<Button>().Single(button=>button.Tag as string=="Menu"); var helpMenu=menu.ContextMenu!.Items.OfType<MenuItem>().Single(item=>item.Tag as string=="Pomoc");
            if(helpMenu.Icon is not DeskIcon || helpMenu.InputGesture?.Key!=Key.F1)throw new Exception("Help menu icon/shortcut missing");
            helpMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); RedesignChecks.Frames();
            var help=main.GetVisualDescendants().OfType<HelpPane>().Single(); if(main.OwnedWindows.Count!=0 || !help.IsActive)throw new Exception("Help is not a workspace tab");
            var helpButton=main.GetVisualDescendants().OfType<Button>().Single(button=>button.Classes.Contains("activity") && AutomationProperties.GetName(button)==AppText.T("Pomoc")); Click(helpButton); RedesignChecks.Frames();
            Named<TextBox>(help,"HelpSearch").Focus(); main.KeyPress(Key.F1,RawInputModifiers.None,PhysicalKey.F1,""); RedesignChecks.Frames();
            if(!ReferenceEquals(help,main.OpenHelp()) || main.GetVisualDescendants().OfType<HelpPane>().Count()!=1 || !helpButton.Classes.Contains("active"))throw new Exception("Opening Help created duplicate tabs or lost active state");
            var search=Named<TextBox>(help,"HelpSearch"); var list=Named<ListBox>(help,"HelpTopics"); var picker=Named<ComboBox>(help,"HelpCompactTopics"); var copy=Named<Button>(help,"HelpCopyTopic");
            var clipboard=main.Clipboard!; RedesignChecks.Wait(clipboard.SetTextAsync("clipboard sentinel"));
            search.Text="XWayland"; RedesignChecks.Frames(); if(list.ItemCount!=2)throw new Exception("Search does not match article contents");
            var rdp=((HelpTopic[])list.ItemsSource!).Single(topic=>topic.Id=="rdp"); list.SelectedItem=rdp; RedesignChecks.Frames();
            if(Named<TextBlock>(help,"HelpArticleTitle").Text!=rdp.Title || Result(clipboard.TryGetTextAsync())!="clipboard sentinel")throw new Exception("Topic selection modified the clipboard or displayed another article");
            Click(copy); RedesignChecks.Frames(); if(Result(clipboard.TryGetTextAsync())!=rdp.PlainText)throw new Exception("Copy omitted help paragraphs");
            search.Text="no-such-help-1f81e8"; RedesignChecks.Frames(); if(list.ItemCount!=0 || copy.IsEnabled || !Named<Button>(help,"HelpClearEmpty").IsEffectivelyVisible)throw new Exception("Empty help has no usable reset");
            Click(Named<Button>(help,"HelpClearEmpty")); RedesignChecks.Frames(); if(list.ItemCount!=15 || !search.IsFocused)throw new Exception("Clearing help did not restore topics/focus");
            foreach(var theme in new[]{"graphite","light"})foreach(var width in new[]{1320,900})
            {
                main.PreviewPreferences(settings.Preferences with { AccentColor="#C85CA7" }); DesignSystem.Apply(theme); main.Width=width; main.Height=720; RedesignChecks.Frames();
                if(picker.IsVisible!=(width==900) || Named<Border>(help,"HelpNavigation").IsVisible!=(width==1320))throw new Exception("Help topic navigation did not adapt");
                foreach(var topic in HelpContent.Read(language))
                {
                    list.SelectedItem=((HelpTopic[])list.ItemsSource!).Single(item=>item.Id==topic.Id); RedesignChecks.Frames(2);
                    var scroll=Named<ScrollViewer>(help,"HelpArticleScroll");
                    if(scroll.Extent.Width>scroll.Viewport.Width+1)throw new Exception("Help article scrolls horizontally: "+topic.Id);
                    var body=Named<StackPanel>(help,"HelpArticle");
                    var cards=body.Children.OfType<Border>().Where(card=>card.Name=="HelpSection").ToArray();
                    if(cards.Length!=topic.Sections.Length)throw new Exception("Help section grouping is incomplete");
                    foreach(var card in cards)
                    {
                        var heading=Named<TextBlock>(card,"HelpSectionHeading"); var paragraphs=Named<StackPanel>(card,"HelpSectionParagraphs");
                        var headerBottom=Named<Border>(card,"HelpSectionHeader").Bounds.Bottom; var paragraphTop=paragraphs.TranslatePoint(default,card)!.Value.Y;
                        if(headerBottom>paragraphTop || heading.FontSize<=main.CurrentPreferences.InterfaceFontSize)throw new Exception("Help section heading blends into or overlaps its content");
                        if(card.Bounds.Width>scroll.Viewport.Width+1)throw new Exception("Help section escapes viewport");
                        foreach(var text in paragraphs.Children.OfType<SelectableTextBlock>())if(text.Bounds.Width>paragraphs.Bounds.Width+1)throw new Exception("Help paragraph escapes section");
                    }
                    for(var index=1;index<cards.Length;index++)if(cards[index].Bounds.Top-cards[index-1].Bounds.Bottom<20)throw new Exception("Help sections have no visible separation");
                    if(topic.Id=="shortcuts" || (topic.Id=="sftp" && theme=="graphite" && width==1320))main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"help-{topic.Id}-{language}-{theme}-{width}.png"));
                }
                list.SelectedIndex=0; RedesignChecks.Frames(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"help-{language}-{theme}-{width}.png"));
            }
            picker.SelectedIndex=3; RedesignChecks.Frames(); if(list.SelectedIndex!=3)throw new Exception("Compact topic selection is not synchronized");
            search.Text="password"; search.Focus(); main.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,""); RedesignChecks.Frames(); if(search.Text!="")throw new Exception("Help Escape did not clear search");
            list.Focus(); main.KeyPress(Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); if(!search.IsFocused)throw new Exception("Help Ctrl+F did not focus search");
            var switcher=main.SwitchSessionAsync(); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single(); var choices=Named<ListBox>(dialog,"SessionSwitcherResults"); var choice=(SessionChoice)choices.Items[0]!;
            if(choice.Entry.Protocol!=AppText.T("Pomoc") || choice.Entry.State!=AppText.T("Dokumentacja offline"))throw new Exception("Help masquerades as a protocol session");
            dialog.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,""); RedesignChecks.Wait(switcher);
            var palette=typeof(MainWindow).GetMethod("CommandsAsync",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
            var choosing=(Task)palette.Invoke(main,null)!; RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); Named<TextBox>(dialog,"CommandSearch").Text=AppText.T("Pomoc"); RedesignChecks.Frames(); dialog.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Wait(choosing);
            if(!ReferenceEquals(help,main.OpenHelp()))throw new Exception("Palette did not reuse Help");
            main.SetWorkspaceMode(WorkspaceMode.Columns); main.DockSession(help,SessionDock.Secondary); RedesignChecks.Frames();
            main.SetWorkspaceMode(WorkspaceMode.Grid); main.DockSession(help,SessionDock.Quaternary); main.Width=900; main.Height=600; RedesignChecks.Frames();
            var copyPosition=copy.TranslatePoint(default,help)!.Value;
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,"help-grid-"+language+".png"));
            if(copyPosition.Y<0 || copyPosition.Y+copy.Bounds.Height>help.Bounds.Height+1 || Named<ScrollViewer>(help,"HelpArticleScroll").Viewport.Height<50)throw new Exception("Four-panel help hides actions or article: pane="+help.Bounds+" copy="+copyPosition+"/"+copy.Bounds+" article="+Named<ScrollViewer>(help,"HelpArticleScroll").Viewport+" header="+Named<Control>(help,"HelpHeading").IsVisible);
            main.DockSession(help,SessionDock.Floating); RedesignChecks.Frames(); var floating=main.OwnedWindows.Single(); floating.Width=560; floating.Height=500; RedesignChecks.Frames();
            floating.CaptureRenderedFrame()!.Save(Path.Combine(root,"help-floating-"+language+".png")); floating.Close(); RedesignChecks.Frames(); if(main.OwnedWindows.Count!=0 || !help.IsVisible)throw new Exception("Closing floating help did not redock it");
            RedesignChecks.Wait(main.SaveSessionSnapshotAsync()); if(WorkspaceSnapshot.Parse(Result(repo.SettingAsync("lastWorkspace"))??throw new Exception("Missing snapshot")).Sessions.Count!=0)throw new Exception("Help persisted as a server session");
            var header=main.GetVisualDescendants().OfType<TabItem>().Single(tab=>ReferenceEquals(tab.Tag,help)); Click(Named<Button>(header,"WorkspaceTabClose")); RedesignChecks.Frames();
            if(main.GetVisualDescendants().OfType<HelpPane>().Any())throw new Exception("Help close retained its pane");
            using(var terminal=new TerminalControl { Process="" })
            {
                var fixture=new Window { Content=terminal }; fixture.Show(); terminal.Focus();
                var handle=typeof(MainWindow).GetMethod("HandleKeys",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
                var key=new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.F1,Source=terminal }; handle.Invoke(main,new object?[]{main,key}); if(key.Handled || main.GetVisualDescendants().OfType<HelpPane>().Any())throw new Exception("Help steals terminal F1"); fixture.Close();
            }
            main.Activate(); Named<TextBox>(main,"ConnectionSearch").Focus(); main.KeyPress(Key.F1,RawInputModifiers.None,PhysicalKey.F1,""); RedesignChecks.Frames();
            if(main.GetVisualDescendants().OfType<HelpPane>().Count()!=1 || ReferenceEquals(help,main.OpenHelp()) || credentials.Reads!=0 || main.OpenSessions.Count!=0 || Result(repo.ListAsync()).Count!=0)throw new Exception("F1 reopening failed or touched connection state/credentials");
        }
        finally { foreach(var child in main.OwnedWindows.ToArray())child.Close(); main.Close(); RedesignChecks.Frames(); }
    }
    private sealed class UnusedCredentials : ICredentialStore
    {
        internal int Reads;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; return Task.FromResult<string?>(null); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new Exception("Help attempted to save credentials");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Help attempted to delete credentials");
    }
}
