using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class BrowserContextMenuChecks
{
    private static string gesture="";
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Concat(root.GetLogicalDescendants().OfType<T>()).Distinct().Single(control=>control.Name==name);
    private static MenuItem Item(ContextMenu menu,string source)=>menu.Items.OfType<MenuItem>().Single(item=>item.Tag as string==source);
    private static void Click(Window window,string source)=>window.GetVisualDescendants().OfType<Button>().Single(button=>button.Tag as string==source).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void RightClick(Window window,Control control,Point? local=null)
    {
        // Separate gestures beyond the platform double-click interval.
        for(var i=0;i<30;i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); RedesignChecks.Frames(1); }
        var point=control.TranslatePoint(local??new Point(Math.Min(12,control.Bounds.Width/2),Math.Min(12,control.Bounds.Height/2)),window)!.Value;
        gesture="hit="+window.InputHitTest(point)?.GetType().Name;
        window.MouseMove(point); window.MouseDown(point,MouseButton.Right); gesture+=" afterDown="+window.InputHitTest(point)?.GetType().Name; window.MouseUp(point,MouseButton.Right); RedesignChecks.Frames();
    }
    private static void Invoke(ContextMenu menu,string source)
    {
        var action=Item(menu,source); var root=TopLevel.GetTopLevel(action)!; var point=action.TranslatePoint(new Point(action.Bounds.Width/2,action.Bounds.Height/2),root)!.Value;
        root.MouseMove(point); root.MouseDown(point,MouseButton.Left); root.MouseUp(point,MouseButton.Left); RedesignChecks.Frames();
        if(menu.IsOpen)throw new Exception("Menu action did not dismiss its popup");
    }
    private static void Until(Func<bool> ready) { var deadline=DateTime.UtcNow.AddSeconds(15); while(!ready() && DateTime.UtcNow<deadline)RedesignChecks.Frames(1); if(!ready())throw new Exception("Context-menu action timed out"); }
    private static void Save(Window editor,string name)
    { Named<TextBox>(editor,"ProfileName").Text=name; Named<TextBox>(editor,"ProfileHost").Text="fixture.invalid"; Named<TextBox>(editor,"ProfileUsername").Text="fixture"; Click(editor,"Zapisz"); }
    internal static void Run(string root)
    {
        var original=DesignSystem.Preferences; var theme=DesignSystem.CurrentId;
        try { foreach(var language in new[]{"pl","en"})Check(root,language); }
        finally { AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(original); DesignSystem.Apply(theme); }
        Console.WriteLine("PASS connection context menus PL/EN: physical right-click on blank/empty/header, icon/localization and live filter states, server/folder targeting, native text editing, Transfers/Network exclusion, root/folder/quick-profile save persistence without touching existing profiles or credentials");
    }
    private static void Check(string root,string language)
    {
        AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { Language=language } }));
        var vm=new MainViewModel(repo); var vault=new NoSecrets(); var main=new MainWindow(vm,repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        var sidebar=Named<DockPanel>(main,"ConnectionSidebar"); var area=sidebar.ContextMenu!;
        main.AddHandler(InputElement.PointerPressedEvent,(_,e)=>gesture+=" down="+e.Source?.GetType().Name+"/"+e.Pointer.Captured?.GetType().Name+"/"+e.ClickCount,RoutingStrategies.Bubble,true);
        main.AddHandler(InputElement.PointerReleasedEvent,(_,e)=>gesture+=" up="+e.Source?.GetType().Name+"/"+e.Pointer.Captured?.GetType().Name,RoutingStrategies.Bubble,true);
        var expected=new[]{"Nowe połączenie","Szybkie połączenie","Nowy folder","Import konfiguracji","Eksport konfiguracji","Rozwiń wszystkie foldery","Zwiń wszystkie foldery","Wyczyść filtry"};
        void OpenArea()
        {
            RightClick(main,sidebar,new Point(30,sidebar.Bounds.Height-18));
            if(!area.IsOpen || !area.Items.OfType<MenuItem>().Select(item=>item.Tag as string).SequenceEqual(expected))
            {
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,"area-menu-failure.png"));
                throw new Exception("Blank-area context menu is missing: open="+area.IsOpen+" search="+vm.Search+" "+gesture);
            }
            foreach(var item in area.Items.OfType<MenuItem>())if(item.Icon is not DeskIcon || item.Header as string!=AppText.T((string)item.Tag!))throw new Exception("Context action icon/translation missing");
        }
        try
        {
            foreach(var preset in new[]{"graphite","light"})
            {
                DesignSystem.Apply(preset); OpenArea(); if(Item(area,"Wyczyść filtry").IsEnabled)throw new Exception("Clear filters enabled with no filters");
                TopLevel.GetTopLevel(area)!.CaptureRenderedFrame()!.Save(Path.Combine(root,$"context-{language}-{preset}.png")); area.Close();
            }
            RightClick(main,Named<TextBlock>(main,"SidebarModuleTitle")); if(!area.IsOpen)throw new Exception("Header has no area menu"); area.Close();
            var search=Named<TextBox>(main,"ConnectionSearch"); RightClick(main,search); if(area.IsOpen || search.ContextFlyout?.IsOpen!=true)throw new Exception("Application menu replaced native textbox context actions"); search.ContextFlyout!.Hide();
            OpenArea(); Invoke(area,"Nowy folder"); var prompt=main.OwnedWindows.Single(); prompt.GetVisualDescendants().OfType<TextBox>().Single().Text="Lab"; Click(prompt,"OK"); Until(()=>vm.Folders.Contains("Lab")); RedesignChecks.Frames();
            var folder=main.GetVisualDescendants().OfType<TreeViewItem>().Single(node=>node.Tag as string=="Lab");
            var folderHeader=Named<ContentPresenter>(folder,"PART_HeaderPresenter");
            RightClick(main,folderHeader); if(area.IsOpen || folder.ContextMenu?.IsOpen!=true)throw new Exception("Folder menu was replaced by area menu");
            Invoke(folder.ContextMenu!,"Nowe połączenie w folderze"); var editor=main.OwnedWindows.Single();
            if(Named<TextBox>(editor,"ProfileFolder").Text!="Lab")throw new Exception("Folder creation targeted another folder"); Save(editor,"Folder fixture"); Until(()=>vm.AllServers.Count==1); RedesignChecks.Frames();
            var existing=vm.AllServers.Single(); var tree=sidebar.GetVisualDescendants().OfType<TreeView>().Single(); var profile=main.GetVisualDescendants().OfType<TreeViewItem>().Single(node=>node.Tag is ServerProfile p && p.Id==existing.Id); tree.SelectedItem=profile;
            RightClick(main,Named<ContentPresenter>(profile,"PART_HeaderPresenter")); if(area.IsOpen || profile.ContextMenu?.IsOpen!=true)throw new Exception("Server menu was replaced by area menu: "+gesture);
            Invoke(profile.ContextMenu!,"Edytuj"); editor=main.OwnedWindows.Single(); if(Named<TextBox>(editor,"ProfileName").Text!=existing.Name)throw new Exception("Right-click edited another server"); Click(editor,"Anuluj"); RedesignChecks.Frames();
            OpenArea(); Invoke(area,"Nowe połączenie"); editor=main.OwnedWindows.Single(); if(Named<TextBox>(editor,"ProfileFolder").Text!="")throw new Exception("Background creation inherited stale folder selection"); Save(editor,"Root fixture"); Until(()=>vm.AllServers.Count==2); RedesignChecks.Frames();
            search.Text="no-result"; RedesignChecks.Frames(); OpenArea(); if(!Item(area,"Wyczyść filtry").IsEnabled)throw new Exception("Clear filters did not update on reopen"); Invoke(area,"Wyczyść filtry"); if(vm.Search!="")throw new Exception("Clear filters did not run"); RedesignChecks.Frames();
            OpenArea(); Invoke(area,"Szybkie połączenie"); var quick=main.OwnedWindows.Single(); Named<TextBox>(quick,"QuickConnectionAddress").Text="ssh://fixture@quick.invalid:22"; RedesignChecks.Frames(); Click(quick,"Edytuj jako profil"); RedesignChecks.Frames(); editor=main.OwnedWindows.Single();
            if(Named<TextBox>(editor,"ProfileHost").Text!="quick.invalid")throw new Exception("Quick-connection draft lost destination"); Click(editor,"Zapisz"); Until(()=>vm.AllServers.Count==3); RedesignChecks.Frames();
            var stored=repo.ListAsync().GetAwaiter().GetResult(); if(stored.Single(p=>p.Id==existing.Id)!=existing || stored.Count!=3 || vault.Calls!=0 || main.OpenSessions.Count!=0)throw new Exception("Menu creation changed existing data or accessed credentials");
            foreach(var section in new[]{"Transfery","Narzędzia sieciowe"})
            {
                var button=main.GetVisualDescendants().OfType<Button>().Single(control=>control.Classes.Contains("activity") && AutomationProperties.GetName(control)==AppText.T(section)); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames();
                RightClick(main,Named<TextBlock>(main,"SidebarModuleTitle")); if(area.IsOpen)throw new Exception("Connection menu leaked into another module");
            }
            if(new ProfileRepository(paths).ListAsync().GetAwaiter().GetResult().Count!=3)throw new Exception("New profiles did not persist");
        }
        finally { area.Close(); foreach(var dialog in main.OwnedWindows.ToArray())dialog.Close(); main.Close(); RedesignChecks.Frames(); }
    }
    private sealed class NoSecrets:ICredentialStore
    {
        internal int Calls;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Menu must not read credentials"); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Calls++; throw new Exception("Menu must not save credentials"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Menu must not delete credentials"); }
    }
}
