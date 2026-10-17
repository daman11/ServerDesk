using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class BrowserWorkflowChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static Task Call(object owner,string name,params object?[] args)=>(Task)owner.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,args)!;
    private static IEnumerable<TreeViewItem> Nodes(IEnumerable<object?> items)
    { foreach(var node in items.OfType<TreeViewItem>()) { yield return node; foreach(var child in Nodes(node.Items))yield return child; } }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetLogicalDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Until(Func<bool> done)
    { var clock=Stopwatch.StartNew(); while(!done()) { if(clock.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException("Browser workflow fixture timed out"); RedesignChecks.Frames(1); } }
    private static void Click(Window window,string tag)=>window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private sealed class Vault:ICredentialStore
    {
        internal int Reads,Deletes;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; return Task.FromResult<string?>(null); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default)=>throw new InvalidOperationException("Fixture must not save credentials");
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Deletes++; return Task.CompletedTask; }
    }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"workflow-"+language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var settings=repo.ApplicationSettingsAsync().GetAwaiter().GetResult(); RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { Language=language } }));
            var first=new ServerProfile { Name="app2",Host="fixture.invalid",Username="fixture",Folder="Prod/Linux/Apps",Color="#72B7AC",IconId="database",Favorite=true };
            var second=first with { Id=Guid.NewGuid(),Name="app10",Folder="ProdCopy" };
            RedesignChecks.Wait(repo.SaveAsync(first)); RedesignChecks.Wait(repo.SaveAsync(second));
            foreach(var folder in new[]{"Empty/Sub","Other","Target/Deep"})RedesignChecks.Wait(repo.AddFolderAsync(folder));
            RedesignChecks.Wait(repo.SetFolderIconAsync("Prod/Linux","database"));
            var deep="Deep/"+string.Join('/',Enumerable.Range(1,31).Select(i=>"n"+i)); RedesignChecks.Wait(repo.AddFolderAsync(deep));
            var vm=new MainViewModel(repo); var vault=new Vault();
            var main=new MainWindow(vm,repo,vault,new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show();
            Until(()=>vm.AllServers.Count==2); RedesignChecks.Frames(20);
            var tree=Field<TreeView>(main,"tree"); var collapsed=Field<HashSet<string>>(main,"collapsed");
            TreeViewItem Folder(string path)=>Nodes(tree.Items).Single(n=>n.Tag as string==path);
            TreeViewItem Profile(Guid id)=>Nodes(tree.Items).Single(n=>n.Tag is ServerProfile p && p.Id==id);
            void Press(Key key,KeyModifiers modifiers=KeyModifiers.None)=>tree.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=key,KeyModifiers=modifiers });
            Window Prompt() { Until(()=>main.OwnedWindows.Any()); RedesignChecks.Frames(3); return main.OwnedWindows.Single(); }
            void Done() { Until(()=>!main.OwnedWindows.Any()); RedesignChecks.Frames(); }
            void Error()
            {
                Until(()=>main.OwnedWindows.Any(w=>w.Title==AppText.T("ServerDesk — błąd")));
                Click(main.OwnedWindows.Single(),"Zamknij"); Done();
            }
            void Reload() { RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames(); }
            try
            {
                // New actions inherit the actual selection, never a stale hidden folder.
                tree.SelectedItem=Folder(first.Folder); var editing=Call(main,"EditAsync",(object?)null); var editor=Prompt();
                if(Named<TextBox>(editor,"ProfileFolder").Text!=first.Folder)throw new Exception("New connection ignored selected folder");
                Named<TextBox>(editor,"ProfileName").Text="new in folder"; Named<TextBox>(editor,"ProfileHost").Text="fixture.invalid"; Named<TextBox>(editor,"ProfileUsername").Text="fixture";
                Click(editor,"Zapisz"); RedesignChecks.Wait(editing); Done();
                var added=repo.ListAsync().GetAwaiter().GetResult().Single(p=>p.Name=="new in folder");
                if(added.Folder!=first.Folder || added.Id==first.Id)throw new Exception("New profile saved outside selected folder");
                tree.SelectedItem=Profile(first.Id); editing=Call(main,"EditAsync",(object?)null); editor=Prompt();
                if(Named<TextBox>(editor,"ProfileFolder").Text!=first.Folder)throw new Exception("New profile ignored selected server's folder");
                Click(editor,"Anuluj"); RedesignChecks.Wait(editing); Done();
                tree.SelectedItem=null; editing=Call(main,"EditAsync",(object?)null); editor=Prompt();
                if(Named<TextBox>(editor,"ProfileFolder").Text!="")throw new Exception("Unselected tree reused stale folder");
                Click(editor,"Anuluj"); RedesignChecks.Wait(editing); Done();
                if(repo.ListAsync().GetAwaiter().GetResult().Count!=3)throw new Exception("Cancelled creation persisted a profile");
                tree.SelectedItem=Profile(first.Id); RedesignChecks.Wait(Call(main,"SelectSectionAsync","transfers")); RedesignChecks.Frames();
                editing=Call(main,"EditAsync",(object?)null); editor=Prompt();
                if(Named<TextBox>(editor,"ProfileFolder").Text!="")throw new Exception("Transfer section reused hidden connection selection");
                Click(editor,"Anuluj"); RedesignChecks.Wait(editing); Done();
                RedesignChecks.Wait(Call(main,"SelectSectionAsync","connections")); RedesignChecks.Frames();
                Console.WriteLine("PASS "+language+": selected-folder/profile defaults, actual save, root default and cancel");

                // A modal prompt holds its original target through selection/rebuild changes.
                Folder("Prod/Linux").IsExpanded=false; Folder("ProdCopy").IsExpanded=false; tree.SelectedItem=Folder("Prod"); Press(Key.F2);
                var prompt=Prompt(); if(prompt.GetVisualDescendants().OfType<TextBox>().Single().Text!="Prod")throw new Exception("F2 renamed wrong folder");
                tree.SelectedItem=Folder("Other"); vm.SortDescending=true; RedesignChecks.Frames();
                prompt.GetVisualDescendants().OfType<TextBox>().Single().Text="Archive/Renamed"; Click(prompt,"OK"); Done();
                if(vm.SelectedFolder!="Other" || tree.SelectedItem is not TreeViewItem { Tag:"Other" } || !vm.Folders.Contains("Other") || vm.Folders.Contains("Prod"))throw new Exception("Rename followed changed selection or stole its selection");
                if(!collapsed.Contains("Archive/Renamed/Linux") || collapsed.Contains("Prod/Linux") || !collapsed.Contains("ProdCopy") || Folder("Archive/Renamed/Linux").IsExpanded)throw new Exception("Rename lost subtree collapse state or changed a prefix sibling");
                if(vm.FolderIcons.GetValueOrDefault("Archive/Renamed/Linux")!="database" || vm.AllServers.Single(p=>p.Id==first.Id).Folder!="Archive/Renamed/Linux/Apps")throw new Exception("Rename lost icon/profile mapping");
                tree.SelectedItem=Folder("Archive/Renamed/Linux/Apps"); Press(Key.F2); prompt=Prompt(); prompt.GetVisualDescendants().OfType<TextBox>().Single().Text="Archive/Renamed/Linux/Services"; Click(prompt,"OK"); Done();
                if(tree.SelectedItem is not TreeViewItem { Tag:"Archive/Renamed/Linux/Services" })throw new Exception("Renamed folder selection disappeared");
                Folder(deep).IsExpanded=false; tree.SelectedItem=Folder("Deep"); Press(Key.F2); prompt=Prompt(); prompt.GetVisualDescendants().OfType<TextBox>().Single().Text="DeepRenamed"; Click(prompt,"OK"); Done();
                if(!collapsed.Contains("DeepRenamed"+deep[4..]) || !vm.Folders.Contains("DeepRenamed"+deep[4..]) || collapsed.Contains(deep))throw new Exception("Depth-32 rename lost leaf state");
                var before=collapsed.ToHashSet(); tree.SelectedItem=Folder("Archive/Renamed"); Press(Key.F2); prompt=Prompt(); prompt.GetVisualDescendants().OfType<TextBox>().Single().Text="Other"; Click(prompt,"OK"); Error();
                if(!collapsed.SetEquals(before) || !vm.Folders.Contains("Archive/Renamed"))throw new Exception("Rejected rename changed view state");
                Console.WriteLine("PASS "+language+": captured rename target, preserved current/new selection, icons/profiles, prefix sibling, depth32 collapse mapping and rejected rename");

                Folder("Empty/Sub").IsExpanded=false; tree.SelectedItem=Folder("Empty"); Press(Key.Delete); prompt=Prompt();
                if(!prompt.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Empty"))throw new Exception("Delete confirmation showed wrong folder");
                tree.SelectedItem=Folder("Other"); Click(prompt,"Potwierdź"); Done();
                if(vm.Folders.Contains("Empty") || !vm.Folders.Contains("Other") || vm.SelectedFolder!="Other" || collapsed.Contains("Empty/Sub") || !collapsed.Contains("ProdCopy"))throw new Exception("Delete changed wrong folder, unrelated selection or collapse state");
                RedesignChecks.Wait(repo.AddFolderAsync("Empty/Sub")); Reload(); if(!Folder("Empty/Sub").IsExpanded)throw new Exception("Recreated folder inherited deleted collapse state");
                tree.SelectedItem=Folder("Empty"); Press(Key.Delete); Click(Prompt(),"Anuluj"); Done();
                if(!vm.Folders.Contains("Empty/Sub"))throw new Exception("Cancelled delete removed folder");
                tree.SelectedItem=Folder("Archive/Renamed"); Press(Key.Delete); Click(Prompt(),"Potwierdź"); Error();
                if(!vm.Folders.Contains("Archive/Renamed") || vm.AllServers.Count!=3)throw new Exception("Nonempty-folder delete removed data");
                Console.WriteLine("PASS "+language+": captured delete target, cancellation/nonempty guard, unrelated selection and no stale collapsed paths after recreation");

                var format=(DataFormat<ServerProfile>)typeof(MainWindow).GetField("ServerDrag",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                DragDropEffects Drag(Control target,ServerProfile profile,RoutedEvent<DragEventArgs> kind)
                {
                    var data=new DataTransfer(); data.Add(DataTransferItem.Create(format,profile));
                    var e=new DragEventArgs(kind,data,target,new Point(8,8),KeyModifiers.None); target.RaiseEvent(e); return e.DragEffects;
                }
                var rootDrop=Field<Border>(main,"rootProfileDrop");
                if(AutomationProperties.GetName(rootDrop)!=AppText.T("Upuść serwer tutaj, aby przenieść go poza foldery."))throw new Exception("Root drop missing localized accessible hint");
                Folder("Target").IsExpanded=false; Folder("Target/Deep").IsExpanded=false;
                if(Drag(Folder("Target/Deep"),first,DragDrop.DragOverEvent)!=DragDropEffects.Move || Drag(Folder("Target/Deep"),first,DragDrop.DropEvent)!=DragDropEffects.Move)throw new Exception("Folder rejected profile drag");
                Until(()=>vm.AllServers.Single(p=>p.Id==first.Id).Folder=="Target/Deep"); RedesignChecks.Frames();
                if(!Folder("Target").IsExpanded || !Folder("Target/Deep").IsExpanded || tree.SelectedItem is not TreeViewItem { Tag:ServerProfile p } || p.Id!=first.Id)throw new Exception("Drop did not reveal/select moved profile");
                if(Drag(Folder("Target/Deep"),first,DragDrop.DropEvent)!=DragDropEffects.None)throw new Exception("Same-folder drop was accepted using stale drag metadata");
                if(Drag(rootDrop,first,DragDrop.DragOverEvent)!=DragDropEffects.Move || Drag(rootDrop,first,DragDrop.DropEvent)!=DragDropEffects.Move)throw new Exception("Root rejected profile drop");
                Until(()=>vm.AllServers.Single(p=>p.Id==first.Id).Folder==""); RedesignChecks.Frames();
                var viewport=tree.GetVisualDescendants().OfType<ScrollViewer>().First();
                var movedNode=Profile(first.Id); var position=movedNode.TranslatePoint(default,viewport);
                if(position==null || position.Value.Y<0 || position.Value.Y+24>viewport.Bounds.Height)throw new Exception("Moved root profile remained outside tree viewport");
                var moved=repo.ListAsync().GetAwaiter().GetResult().Single(p=>p.Id==first.Id);
                if(moved!=first with { Folder="" } || vm.AllServers.Single(p=>p.Id==second.Id)!=second)throw new Exception("Drag changed unrelated metadata/profile");
                if(Drag(rootDrop,first,DragDrop.DropEvent)!=DragDropEffects.None || Drag(rootDrop,first with { Id=Guid.NewGuid() },DragDrop.DropEvent)!=DragDropEffects.None)throw new Exception("Same-folder or unknown-profile drop accepted");
                var external=new DataTransfer(); external.Add(DataTransferItem.CreateText("fixture text"));
                var externalDrop=new DragEventArgs(DragDrop.DropEvent,external,rootDrop,new Point(8,8),KeyModifiers.None); rootDrop.RaiseEvent(externalDrop);
                if(externalDrop.DragEffects!=DragDropEffects.None)throw new Exception("Non-profile drop accepted");
                vm.Search="no-matches";
                if(Drag(Folder("Empty"),first,DragDrop.DropEvent)!=DragDropEffects.None)throw new Exception("Filtered profile drop accepted before rebuild");
                RedesignChecks.Frames(); vm.Search=""; RedesignChecks.Frames();
                RedesignChecks.Wait(Call(main,"SelectSectionAsync","favorite")); RedesignChecks.Frames();
                if(rootDrop.IsVisible || Drag(rootDrop,second,DragDrop.DropEvent)!=DragDropEffects.None)throw new Exception("Root drop active outside connection section");
                RedesignChecks.Wait(Call(main,"SelectSectionAsync","connections")); RedesignChecks.Frames();
                Console.WriteLine("PASS "+language+": production routed folder/root drag-over/drop, reveal/selection, metadata isolation and stale/unknown/filtered/inactive rejection");

                tree.SelectedItem=Profile(first.Id); Press(Key.F2); editor=Prompt();
                if(Named<TextBox>(editor,"ProfileName").Text!=first.Name)throw new Exception("F2 opened wrong server"); Click(editor,"Anuluj"); Done();
                Press(Key.D,KeyModifiers.Control); editor=Prompt();
                if(Named<TextBox>(editor,"ProfileName").Text!=first.Name+" — "+AppText.T("kopia") || Named<TextBox>(editor,"ProfileFolder").Text!="")throw new Exception("Ctrl+D did not open localized independent duplicate editor"); Click(editor,"Anuluj"); Done();
                Press(Key.Delete); Click(Prompt(),"Anuluj"); Done();
                if(vm.AllServers.Count!=3 || vault.Deletes!=0)throw new Exception("Cancelled profile delete removed data/credentials");
                Press(Key.F2,KeyModifiers.Shift); RedesignChecks.Frames(); if(main.OwnedWindows.Any())throw new Exception("Modified F2 intercepted");
                tree.SelectedItem=Folder("Empty"); var expanded=Folder("Empty").IsExpanded; Press(Key.Enter);
                if(Folder("Empty").IsExpanded==expanded)throw new Exception("Enter did not toggle selected folder");
                var search=main.GetVisualDescendants().OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)==AppText.T("Szukaj połączeń"));
                search.Focus(); search.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Delete }); RedesignChecks.Frames();
                if(main.OwnedWindows.Any())throw new Exception("Browser Delete intercepted text input");
                tree.SelectedItem=Profile(first.Id); vm.Search="missing"; Press(Key.F2); Press(Key.Delete); Press(Key.D,KeyModifiers.Control); Press(Key.Enter); RedesignChecks.Frames();
                if(main.OwnedWindows.Any() || main.OpenSessions.Any() || vault.Reads!=0 || vault.Deletes!=0)throw new Exception("Hidden selection launched an action or accessed credentials");
                vm.Search=""; RedesignChecks.Frames();
                tree.SelectedItem=Profile(added.Id); Press(Key.Delete); prompt=Prompt(); tree.SelectedItem=Profile(second.Id); Click(prompt,"Potwierdź"); Done();
                if(vm.AllServers.Count!=2 || vm.AllServers.Any(p=>p.Id==added.Id) || vm.SelectedServer?.Id!=second.Id || vault.Deletes!=2)throw new Exception("Profile Delete followed changed selection or removed wrong credentials");
                Folder("DeepRenamed").IsExpanded=false; tree.SelectedItem=Profile(first.Id); Profile(first.Id).BringIntoView(); RedesignChecks.Frames();
                foreach(var theme in new[]{"graphite","light"})
                {
                    DesignSystem.Apply(theme); main.PreviewPreferences(preferences with { InterfaceFontSize=16,SidebarWidth=220 }); main.Width=900; main.Height=600; RedesignChecks.Frames();
                    if(rootDrop.Bounds.Width<=0 || rootDrop.Bounds.Height<28 || rootDrop.Bounds.Bottom>Field<Grid>(main,"connectionBrowser").Bounds.Height+1)throw new Exception("Root drop clipped in small window");
                    main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"browser-workflow-{language}-{theme}.png"));
                }
                Console.WriteLine("PASS "+language+": F2/Ctrl+D/Delete cancel, folder Enter, text-input isolation, modifiers, hidden-selection fast keys; PL/EN 900px font16 Graphite/Light");
                main.Close(); Until(()=>!main.IsVisible);
                var saved=JsonSerializer.Deserialize<string[]>(repo.SettingAsync("collapsedFolders").GetAwaiter().GetResult()??"[]")!;
                if(!saved.Contains("DeepRenamed"+deep[4..]) || saved.Contains(deep) || saved.Contains("Prod/Linux"))throw new Exception("Shutdown persisted pre-rename collapse paths");
            }
            finally { main.Close(); Until(()=>!main.IsVisible); }
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
    }
}
