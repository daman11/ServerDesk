using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ConnectionBrowserChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static Task Call(object owner,string name,params object[] args)=>(Task)owner.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,args)!;
    private static IEnumerable<TreeViewItem> Nodes(IEnumerable<object?> items)
    { foreach(var node in items.OfType<TreeViewItem>()) { yield return node; foreach(var child in Nodes(node.Items))yield return child; } }
    private static void Until(Func<bool> done)
    { var clock=Stopwatch.StartNew(); while(!done()) { if(clock.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException("Connection browser fixture timed out"); RedesignChecks.Frames(1); } }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"browser-"+language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var settings=repo.ApplicationSettingsAsync().GetAwaiter().GetResult(); RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { Language=language } }));
            var first=new ServerProfile { Name="node2",Host="fixture.invalid",Username="fixture",Folder="Rack2/Lab",Favorite=false };
            var second=first with { Id=Guid.NewGuid(),Name="node10",Folder="Rack10/Lab",Favorite=true,Protocol=Protocol.RDP,Port=3389 };
            RedesignChecks.Wait(repo.AddFolderAsync(first.Folder)); RedesignChecks.Wait(repo.AddFolderAsync(second.Folder));
            var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            var tree=Field<TreeView>(main,"tree");
            TreeViewItem Folder(string path)=>Nodes(tree.Items).Single(n=>n.Tag as string==path);
            TreeViewItem Profile(Guid id)=>Nodes(tree.Items).Single(n=>n.Tag is ServerProfile p && p.Id==id);
            void FolderSelected() { if(tree.SelectedItem is not TreeViewItem { Tag:string path } || path!="Rack2/Lab" || vm.SelectedFolder!=path || vm.SelectedServer!=null)throw new Exception("Folder selection was lost or replaced during rebuild"); }
            void NoHiddenSelection()
            { if(tree.SelectedItem!=null || vm.SelectedServer!=null || main.OpenSessions.Any() || main.OwnedWindows.Any())throw new Exception("A filtered profile remained selected or launched a connection"); }
            try
            {
                var empty=Field<StackPanel>(main,"browserEmpty");
                if(empty.IsVisible || Nodes(tree.Items).Count(n=>n.Tag is string)!=4)throw new Exception("Empty-folder tree is covered by the empty-server prompt");
                vm.Search="missing"; RedesignChecks.Frames(); if(!empty.IsVisible || tree.Items.Count!=0)throw new Exception("Empty search results have no prompt");
                vm.Search=""; RedesignChecks.Frames(); if(empty.IsVisible)throw new Exception("Clearing search covered the empty folders");
                var folder=Folder(first.Folder); var context=folder.ContextMenu!;
                foreach(var attempt in new[]{1,2})
                {
                    context.Open(folder); RedesignChecks.Frames(3);
                    var entries=context.Items.OfType<MenuItem>().ToArray();
                    var labels=new[]{"Nowe połączenie w folderze","Zmień ikonę folderu","Zmień nazwę","Usuń pusty folder"}.Select(label=>AppText.T(label));
                    if(entries.Length!=4 || !entries.Select(item=>item.Header as string).SequenceEqual(labels) || entries.Any(item=>item.Icon is not DeskIcon))throw new Exception("Deferred folder menu lost or duplicated localized actions/icons");
                    context.Close(); RedesignChecks.Frames(2);
                }
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"empty-folders-{language}.png"));
                RedesignChecks.Wait(repo.SaveAsync(first)); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames();
                var originalSize=main.ClientSize; var originalPreferences=main.CurrentPreferences;
                foreach(var target in new[]{"favorite","recent"})
                {
                    RedesignChecks.Wait(Call(main,"SelectSectionAsync",target)); RedesignChecks.Frames();
                    var recovery=Field<Button>(main,"emptyBrowserAction");
                    if(!empty.IsVisible || !recovery.IsEffectivelyVisible || Avalonia.Automation.AutomationProperties.GetName(recovery)!=AppText.T("Pokaż wszystkie połączenia") || Field<DeskIcon>(main,"browserEmptyIcon").Glyph!=DeskIcons.Resolve(target=="favorite"?"star":"history"))throw new Exception("Empty section has no contextual icon and path back to connections");
                    foreach(var theme in new[]{"graphite","light"})
                    {
                        DesignSystem.Apply(theme); main.PreviewPreferences(originalPreferences with { SidebarWidth=220,InterfaceFontSize=16 }); main.Width=900; main.Height=680; RedesignChecks.Frames();
                        var bounds=recovery.TranslatePoint(default,main)!.Value;
                        if(bounds.X<48 || bounds.X+recovery.Bounds.Width>269)throw new Exception("Empty-section recovery escapes the narrow sidebar");
                        main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"empty-{target}-{language}-{theme}.png"));
                    }
                    recovery.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Until(()=>Field<string>(main,"section")=="connections" && !empty.IsVisible);
                    if(main.OpenSessions.Any() || main.OwnedWindows.Any())throw new Exception("Empty section navigation opened a connection or editor");
                }
                main.PreviewPreferences(originalPreferences); main.Width=originalSize.Width; main.Height=originalSize.Height; DesignSystem.Apply("dark");
                RedesignChecks.Wait(repo.SaveAsync(second)); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames();
                main.PreviewPreferences(originalPreferences with { SidebarWidth=220,InterfaceFontSize=16 }); main.Width=900; main.Height=680;
                vm.Search="missing"; vm.ProtocolFilter=Protocol.RDP; RedesignChecks.Frames(); var clear=Field<Button>(main,"clearBrowserFilters");
                var clearPoint=clear.TranslatePoint(default,main)!.Value;
                if(clearPoint.X<48 || clearPoint.X+clear.Bounds.Width>269 || clear.GetVisualDescendants().OfType<TextBlock>().Any(label=>label.TextLayout.WidthIncludingTrailingWhitespace>label.Bounds.Width+1))throw new Exception("Labelled filter reset clips in the narrow sidebar");
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"filters-{language}-220-font16.png"));
                clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames();
                if(vm.Search!="" || vm.ProtocolFilter!=null || !Field<TextBox>(main,"connectionSearch").IsKeyboardFocusWithin)throw new Exception("Clearing filters did not return focus to connection search");
                main.PreviewPreferences(originalPreferences); main.Width=originalSize.Width; main.Height=originalSize.Height;
                tree.SelectedItem=Folder("Rack2/Lab"); vm.SortMode=ProfileSort.Host; vm.SortDescending=true; RedesignChecks.Frames(); FolderSelected();
                RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames(); FolderSelected();
                tree.SelectedItem=Profile(first.Id);
                vm.Search="no-such-profile";
                // Dispatch before the Background tree rebuild, reproducing fast Enter.
                tree.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter });
                RedesignChecks.Frames(); NoHiddenSelection();
                vm.Search=""; RedesignChecks.Frames(); if(tree.SelectedItem is TreeViewItem { Tag:ServerProfile } || vm.SelectedServer!=null)throw new Exception("Clearing search restored a hidden server selection");
                tree.SelectedItem=Profile(first.Id); vm.ProtocolFilter=Protocol.RDP;
                tree.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter }); RedesignChecks.Frames(); NoHiddenSelection();
                vm.ProtocolFilter=null; RedesignChecks.Frames(); tree.SelectedItem=Profile(first.Id);
                RedesignChecks.Wait(Call(main,"SelectSectionAsync","favorite")); RedesignChecks.Frames(); NoHiddenSelection();
                RedesignChecks.Wait(Call(main,"SelectSectionAsync","connections")); RedesignChecks.Frames();
                Folder("Rack2").IsExpanded=false; Folder("Rack10").IsExpanded=true;
                vm.Search="fixture"; RedesignChecks.Frames();
                if(!Folder("Rack2").IsExpanded || !Folder("Rack10").IsExpanded)throw new Exception("Search did not reveal result ancestors");
                Folder("Rack2").IsExpanded=false; Folder("Rack2").IsExpanded=true; Folder("Rack10").IsExpanded=false;
                vm.SortMode=ProfileSort.Name; vm.SortDescending=true; RedesignChecks.Frames(); vm.Search=""; RedesignChecks.Frames();
                if(Folder("Rack2").IsExpanded || !Folder("Rack10").IsExpanded)throw new Exception("Temporary search expansion overwrote saved folder states");
                foreach(var theme in new[]{"graphite","light"})
                {
                    DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 }); main.Width=900; main.Height=680; RedesignChecks.Frames();
                    main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"browser-{language}-{theme}.png"));
                }
                main.Close(); Until(()=>!main.IsVisible);
                var saved=JsonSerializer.Deserialize<string[]>(repo.SettingAsync("collapsedFolders").GetAwaiter().GetResult()??"[]")!;
                if(!saved.Contains("Rack2") || saved.Contains("Rack10"))throw new Exception("Closing persisted temporary search expansion");
                vm=new MainViewModel(repo); main=new MainWindow(vm,repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); tree=Field<TreeView>(main,"tree");
                if(Folder("Rack2").IsExpanded || !Folder("Rack10").IsExpanded || vm.SortMode!=ProfileSort.Name || !vm.SortDescending)throw new Exception("Restart lost collapse states or sort order");
                tree.SelectedItem=Folder("Rack2/Lab"); RedesignChecks.Frames();
                var renaming=main.Safe(()=>Call(main,"RenameFolderAsync")); RedesignChecks.Frames(); var prompt=main.OwnedWindows.Single();
                if(prompt.GetVisualDescendants().OfType<TextBox>().Single().Text!="Rack2/Lab")throw new Exception("Rename prompt did not retain current folder");
                prompt.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="OK").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(renaming); RedesignChecks.Frames();
                if(main.OwnedWindows.Any() || !repo.FoldersAsync().GetAwaiter().GetResult().Contains("Rack2/Lab"))throw new Exception("Unchanged folder name produced an error or removed the folder");
                tree.SelectedItem=Profile(first.Id); RedesignChecks.Frames(); RedesignChecks.Wait(repo.DeleteAsync(first.Id));
                var moving=main.Safe(()=>Call(main,"MoveAsync")); RedesignChecks.Frames(); prompt=main.OwnedWindows.Single();
                prompt.GetVisualDescendants().OfType<TextBox>().Single().Text="Ghost/Deep";
                prompt.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="OK").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(()=>main.OwnedWindows.Any(w=>w.Title==AppText.T("ServerDesk — błąd"))); var error=main.OwnedWindows.Single();
                var expected=AppText.T("Serwer już nie istnieje. Odśwież listę połączeń i spróbuj ponownie.");
                // The error dialog now appends a localized correlation ID;
                // retain the exact actionable message and validate that ID.
                var errorIdPattern=Regex.Escape(AppText.T("Identyfikator błędu: {0}")).Replace(Regex.Escape("{0}"),"[0-9a-f]{32}");
                var errorPattern="\\A"+Regex.Escape(expected+"\n\n")+errorIdPattern+"\\z";
                if(vm.Status!=expected || !error.GetVisualDescendants().OfType<TextBlock>().Any(t=>Regex.IsMatch(t.Text??"",errorPattern)))throw new Exception("Stale move feedback or its correlation ID is not localized");
                error.CaptureRenderedFrame()!.Save(Path.Combine(root,$"stale-move-{language}.png"));
                error.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zamknij").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(moving);
                if(repo.FoldersAsync().GetAwaiter().GetResult().Any(f=>f=="Ghost" || f.StartsWith("Ghost/",StringComparison.Ordinal)))throw new Exception("Stale UI move created phantom folders");
                RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames();
                if(Nodes(tree.Items).Any(n=>n.Tag is ServerProfile p && p.Id==first.Id))throw new Exception("Refresh did not remove deleted profile");
                Console.WriteLine("PASS "+language+": unchanged-name rename through production prompt; stale profile move through production prompt/error/reload preserves folders");
                Console.WriteLine("PASS "+language+": empty folders stay accessible; repeated deferred folder menus retain four localized actions/icons; folder selection survives sort/reload; search/protocol/favorites clear hidden profiles; immediate Enter cannot open hidden connection; search expansion stays temporary; restart preserves collapse states and sort; 900px font16 Graphite/Light");
            }
            finally { main.Close(); Until(()=>!main.IsVisible); }
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
    }
}
