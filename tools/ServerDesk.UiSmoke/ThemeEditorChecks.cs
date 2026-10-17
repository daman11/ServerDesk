using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System.Reflection;
using System.Text.Json;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ThemeEditorChecks
{
    private static T Named<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Window window,string name)=>window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal static void Run(string root)
    {
        double Luminance(string hex) { var c=Color.Parse(hex); double Linear(byte b) { var s=b/255d; return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4); } return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B); }
        if(!DesignSystem.Themes.Select(t=>t.Id).Append("system").SequenceEqual(ApplicationThemes.Ids))throw new Exception("UI and persisted theme catalog differ");
        foreach(var theme in DesignSystem.Themes)foreach(var text in new[]{theme.Foreground,theme.Secondary})foreach(var surface in new[]{theme.Background,theme.Sidebar,theme.Surface,theme.Hover,theme.Selected})
        {
            var a=Luminance(text); var b=Luminance(surface); if((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)<4.5)throw new Exception($"Theme contrast below AA: {theme.Id} {text}/{surface}");
        }
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"data-"+language))); RedesignChecks.Wait(repo.InitializeAsync());
            var original=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
            var main=new MainWindow(new(repo),repo,new CredentialStore(new AppPaths(Path.Combine(root,"data-"+language))),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames();
            var editing=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single(); var picker=Named<ComboBox>(dialog,"ApplicationTheme");
            if(picker.ItemCount!=DesignSystem.Themes.Length+1)throw new Exception("Theme selector is incomplete");
            foreach(var theme in DesignSystem.Themes)
            {
                picker.SelectedIndex=Array.IndexOf(DesignSystem.Themes,theme); RedesignChecks.Frames(3);
                if(DesignSystem.CurrentId!=theme.Id || Application.Current!.RequestedThemeVariant!=(theme.IsLight?ThemeVariant.Light:ThemeVariant.Dark))throw new Exception("Theme preview/variant failed: "+theme.Id);
                if(Array.IndexOf(DesignSystem.Themes,theme)>=5)dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"theme-{language}-{theme.Id}.png"));
            }
            picker.SelectedIndex=DesignSystem.Themes.Length; RedesignChecks.Frames();
            if(DesignSystem.CurrentId!="system" || Application.Current!.RequestedThemeVariant!=ThemeVariant.Default)throw new Exception("System theme maps to a preset after extending the list");
            Click(dialog,"Anuluj"); RedesignChecks.Wait(editing);
            if(DesignSystem.CurrentId!=original.Theme || repo.ApplicationSettingsAsync().GetAwaiter().GetResult()!=original)throw new Exception("Theme cancel did not roll back");
            editing=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single();
            var search=Named<TextBox>(dialog,"SettingsSearch"); search.Text=AppText.T("Domyślny edytor tekstu"); RedesignChecks.Frames();
            if(Named<TabControl>(dialog,"SettingsCategories").SelectedIndex!=2 || !Named<ComboBox>(dialog,"SftpTextEditor").IsEffectivelyVisible)throw new Exception("Editor setting is not searchable");
            search.Text=""; var editor=Named<ComboBox>(dialog,"SftpTextEditor"); var path=Named<TextBox>(dialog,"SftpTextEditorPath");
            editor.SelectedIndex=(int)TextEditorKind.Custom; path.Text="relative/editor"; search.Text="cpu"; Click(dialog,"Zapisz"); RedesignChecks.Frames();
            if(editing.IsCompleted || Named<TabControl>(dialog,"SettingsCategories").SelectedIndex!=2 || !path.Classes.Contains("error") || !path.IsEffectivelyVisible)throw new Exception("Invalid custom editor did not reveal its field");
            path.Text=Path.Combine(root,"Custom editor","editor.exe");
            foreach(var theme in new[]{"ocean","solar-paper"})
            {
                Named<TabControl>(dialog,"SettingsCategories").SelectedIndex=0; RedesignChecks.Frames(3);
                picker=Named<ComboBox>(dialog,"ApplicationTheme"); picker.SelectedIndex=Array.FindIndex(DesignSystem.Themes,t=>t.Id==theme);
                dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c=>AutomationProperties.GetName(c)==AppText.T("Rozmiar tekstu interfejsu")).Value=16;
                Named<TabControl>(dialog,"SettingsCategories").SelectedIndex=2; dialog.Width=680; dialog.Height=500; RedesignChecks.Frames(); path.BringIntoView(); RedesignChecks.Frames();
                var viewport=path.GetVisualAncestors().OfType<ScrollViewer>().First(); var point=path.TranslatePoint(default,viewport)!.Value;
                if(point.X<-.5 || point.X+path.Bounds.Width>viewport.Bounds.Width+.5)throw new Exception("Editor path escapes narrow settings");
                dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"editor-{language}-{theme}-680.png"));
            }
            picker.SelectedIndex=DesignSystem.Themes.Length; Click(dialog,"Zapisz"); RedesignChecks.Wait(editing);
            var saved=repo.ApplicationSettingsAsync().GetAwaiter().GetResult(); if(saved.Theme!="system" || saved.Preferences.SftpTextEditor!=TextEditorKind.Custom || saved.Preferences.SftpTextEditorPath!=path.Text)throw new Exception("Theme/editor save failed");
            editing=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single();
            if(Named<ComboBox>(dialog,"ApplicationTheme").SelectedIndex!=DesignSystem.Themes.Length)throw new Exception("Reopening lost system theme");
            Named<TabControl>(dialog,"SettingsCategories").SelectedIndex=2; RedesignChecks.Frames(3);
            if(Named<ComboBox>(dialog,"SftpTextEditor").SelectedIndex!=(int)TextEditorKind.Custom || Named<TextBox>(dialog,"SftpTextEditorPath").Text!=saved.Preferences.SftpTextEditorPath)throw new Exception("Reopening lost saved editor");
            Click(dialog,"Domyślne ustawienia"); if(Named<ComboBox>(dialog,"SftpTextEditor").SelectedIndex!=0 || Named<TextBox>(dialog,"SftpTextEditorPath").Text!="" || Named<TextBox>(dialog,"SftpTextEditorPath").IsEnabled)throw new Exception("Editor defaults failed");
            Click(dialog,"Anuluj"); RedesignChecks.Wait(editing); if(repo.ApplicationSettingsAsync().GetAwaiter().GetResult()!=saved)throw new Exception("Cancel persisted defaults");
            CheckFileMenu(main,repo,root,language);
            main.Close(); RedesignChecks.Frames();
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(new()); DesignSystem.Apply("dark");
        Console.WriteLine($"PASS {DesignSystem.Themes.Length} themes: AA text contrast, PL/EN preview/variants/system/save/cancel; SFTP editor search/validation/save/reopen/defaults and 680x500 layouts; local file menu launches an owned editor process with the exact path and saved preferences; directory/link/multi-selection guards");
    }
    private static void CheckFileMenu(MainWindow main,ProfileRepository repo,string root,string language)
    {
        var fixture=Directory.CreateDirectory(Path.Combine(root,"editor-fixture-"+language)).FullName;
        var file=Path.Combine(fixture,"--config żółć & 'text'.txt"); File.WriteAllText(file,"fixture text");
        var executable=Path.Combine(AppContext.BaseDirectory,OperatingSystem.IsWindows()?"ServerDesk.UiSmoke.exe":"ServerDesk.UiSmoke");
        var settings=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { SftpTextEditor=TextEditorKind.Custom,SftpTextEditorPath=executable } }));
        main.PreviewPreferences(settings.Preferences with { SftpTextEditor=TextEditorKind.Automatic,SftpTextEditorPath="" });
        var profile=new ServerProfile { Name="Editor fixture",Host="fixture.invalid",Username="fixture" };
        using var pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); var window=new Window { Width=1000,Height=650,Content=pane }; window.Show(); RedesignChecks.Frames();
        object Field(string name)=>typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
        void Update()=>typeof(SessionPane).GetMethod("UpdateFileSelection",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,null);
        var list=(ListBox)Field("localList"); var regular=LocalTransferSource.Read(file);
        ((TextBox)Field("localPath")).Text=fixture; typeof(SessionPane).GetField("loadedLocalPath",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(pane,fixture);
        var directory=regular with { Name="folder",FullPath=Path.Combine(fixture,"folder"),IsDirectory=true }; var link=regular with { Name="link",FullPath=Path.Combine(fixture,"link"),IsSymbolicLink=true };
        list.ItemsSource=new[]{regular,directory,link};
        var menu=list.ContextMenu!.Items.OfType<MenuItem>().Single(m=>m.Header as string==AppText.T("Edytuj plik"));
        foreach(var blocked in new[]{directory,link}) { list.SelectedItem=blocked; Update(); if(menu.IsEnabled)throw new Exception("Text editing is enabled for a directory/link"); }
        list.SelectedItems!.Clear(); list.SelectedItems.Add(regular); list.SelectedItems.Add(directory); Update(); if(menu.IsEnabled)throw new Exception("Text editing is enabled for multiple entries");
        list.SelectedItems.Clear(); list.SelectedItem=regular; Update(); if(!menu.IsEnabled || menu.Icon is not DeskIcon)throw new Exception("Regular file has no enabled/iconed edit action");
        var previous=Environment.GetEnvironmentVariable("SERVERDESK_EDITOR_TEST_ROOT"); Environment.SetEnvironmentVariable("SERVERDESK_EDITOR_TEST_ROOT",fixture);
        try
        {
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var until=DateTime.UtcNow.AddSeconds(10); while(!File.Exists(file+".opened.json") && DateTime.UtcNow<until)RedesignChecks.Frames(1);
            if(!File.Exists(file+".opened.json") || !JsonSerializer.Deserialize<string[]>(File.ReadAllText(file+".opened.json"))!.SequenceEqual(new[]{file}))throw new Exception("Editor process did not receive the exact selected path/saved editor choice");
        }
        finally { Environment.SetEnvironmentVariable("SERVERDESK_EDITOR_TEST_ROOT",previous); RedesignChecks.Wait(pane.CloseAsync()); window.Close(); }
    }
}
