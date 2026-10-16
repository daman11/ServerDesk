using System.Reflection;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
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

internal static class SortingChecks
{
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(3); }
    private static string[] Names(ListBox list)=>list.Items.Cast<FileEntry>().Where(entry=>!entry.IsDirectory).Select(entry=>entry.Name).ToArray();
    private static void Equal(IEnumerable<string> expected,IEnumerable<string> actual,string message) { if(!expected.SequenceEqual(actual))throw new Exception(message+": "+string.Join(", ",actual)); }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var profiles=new[]{new ServerProfile { Name="server10",Host="10.0.0.100",Username="fixture",LastConnected=DateTimeOffset.UtcNow.AddDays(-1),Favorite=true },new ServerProfile { Name="server2",Host="10.0.0.20",Username="fixture",LastConnected=DateTimeOffset.UtcNow.AddDays(-2),Favorite=true },new ServerProfile { Name="server1",Host="10.0.0.2",Username="fixture",LastConnected=DateTimeOffset.UtcNow,Favorite=true }};
            foreach(var profile in profiles)RedesignChecks.Wait(repo.SaveAsync(profile)); foreach(var folder in new[]{"rack10/sub10","rack2","rack1/sub10","rack1/sub2"})RedesignChecks.Wait(repo.AddFolderAsync(folder));
            var vm=new MainViewModel(repo); MainWindow Open() { var window=new MainWindow(vm,repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); window.Show(); RedesignChecks.Frames(20); return window; }
            var main=Open(); var tree=Field<TreeView>(main,"tree"); var selector=Field<ComboBox>(main,"sortSelector"); var direction=Field<Button>(main,"sortDirection");
            IEnumerable<string> TreeNames()=>tree.Items.OfType<TreeViewItem>().Where(node=>node.Tag is ServerProfile).Select(node=>((ServerProfile)node.Tag!).Name);
            Equal(new[]{"rack1","rack2","rack10"},tree.Items.OfType<TreeViewItem>().Where(node=>node.Tag is string).Select(node=>(string)node.Tag!),"Folder natural order");
            var rack1=tree.Items.OfType<TreeViewItem>().Single(node=>node.Tag as string=="rack1"); Equal(new[]{"rack1/sub2","rack1/sub10"},rack1.Items.OfType<TreeViewItem>().Select(node=>(string)node.Tag!),"Nested folder order");
            Equal(new[]{"server1","server2","server10"},TreeNames(),"Initial natural names");
            tree.SelectedItem=tree.Items.OfType<TreeViewItem>().Single(node=>node.Tag is ServerProfile p && p.Id==profiles[1].Id);
            selector.SelectedIndex=1; RedesignChecks.Frames(); Equal(new[]{"server1","server2","server10"},TreeNames(),"Numeric host order"); Click(direction); Equal(new[]{"server10","server2","server1"},TreeNames(),"Descending host order");
            if(!tree.GetVisualDescendants().OfType<TextBlock>().Any(text=>text.Name=="ConnectionSortValue" && text.Text=="10.0.0.100:22"))throw new Exception("Address sort hides the value used to sort");
            if(vm.SelectedServer?.Id!=profiles[1].Id)throw new Exception("Sort lost selected server");
            Call(main,"SelectSectionAsync","recent"); RedesignChecks.Frames(); selector.SelectedIndex=0; RedesignChecks.Frames(); Equal(new[]{"server1","server2","server10"},TreeNames(),"Recent view ignores selected name sort");
            selector.SelectedIndex=3; RedesignChecks.Frames(); Equal(new[]{"server1","server10","server2"},TreeNames(),"Newest connection order"); Click(direction); Equal(new[]{"server2","server10","server1"},TreeNames(),"Oldest connection order");
            Call(main,"SelectSectionAsync","favorite"); RedesignChecks.Frames(); selector.SelectedIndex=1; Click(direction); Equal(new[]{"server10","server2","server1"},TreeNames(),"Favorite view direction");
            Call(main,"SelectSectionAsync","connections"); RedesignChecks.Frames();
            foreach(var theme in new[]{"graphite","light"})
            { DesignSystem.Apply(theme); main.PreviewPreferences(preferences with { InterfaceFontSize=16,SidebarWidth=220 }); main.Width=900; RedesignChecks.Frames(); var point=direction.TranslatePoint(default,main)!.Value; if(point.X<0 || point.X+direction.Bounds.Width>268+1)throw new Exception("Sort direction escapes compact sidebar"); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"connections-{language}-{theme}.png")); }
            main.PreviewPreferences(preferences); main.Width=1320;
            RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames(); if(vm.SortMode!=ProfileSort.Host || !vm.SortDescending)throw new Exception("Profile reload reset sorting");
            if(language=="pl")
            {
                RedesignChecks.Wait(Field<Task>(main,"profileSortSave"));
                using var database=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.Combine(paths.Data,"serverdesk.db") }.ToString()); database.Open(); using var transaction=database.BeginTransaction();
                using(var command=database.CreateCommand()) { command.Transaction=transaction; command.CommandText="UPDATE settings SET value=value WHERE key='serverSort'"; command.ExecuteNonQuery(); }
                var unlock=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var released=Task.Run(async()=> { await Task.WhenAny(unlock.Task,Task.Delay(4000)); transaction.Rollback(); });
                try { var watch=Stopwatch.StartNew(); selector.SelectedIndex=2; RedesignChecks.Frames(3); if(watch.Elapsed>TimeSpan.FromSeconds(2))throw new Exception("A busy profile database blocked sorting on the UI thread"); }
                finally { unlock.TrySetResult(); RedesignChecks.Wait(released); }
                RedesignChecks.Wait(Field<Task>(main,"profileSortSave"));
            }
            for(var i=0;i<8;i++) { selector.SelectedIndex=i%4; vm.SortDescending=i%2==0; } selector.SelectedIndex=1; vm.SortDescending=true;
            main.Close(); RedesignChecks.Frames(25); if(main.IsVisible)throw new Exception("Closing did not flush sort settings");
            var saved=repo.ProfileOrderingAsync().GetAwaiter().GetResult(); if(saved!=(ProfileSort.Host,true))throw new Exception("Rapid sort changes persisted stale state");
            vm=new MainViewModel(repo); main=Open(); if(vm.SortMode!=ProfileSort.Host || !vm.SortDescending || Field<ComboBox>(main,"sortSelector").SelectedIndex!=1)throw new Exception("Reopening lost sort mode/direction");
            Sftp(main,repo,root,language,preferences); main.Close(); RedesignChecks.Frames();
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
        Console.WriteLine("PASS sorting UI: natural names/folders/IP, both directions, recent/favorite views, selected profile retention, refresh, UI responsive under database write lock, rapid changes and restart persistence; independent local/remote SFTP headers/combos/arrows, keyboard, selection, type/size/date, PL/EN Graphite/Light font16 and compact layouts");
    }
    private static void Sftp(MainWindow main,ProfileRepository repo,string root,string language,ApplicationPreferences preferences)
    {
        using var pane=new SessionPane(main,new() { Name="Sorting SFTP",Host="fixture.invalid",Username="fixture" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
        pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().IsVisible=false; Field<Grid>(pane,"files").IsVisible=true;
        FileEntry Entry(string name,long size,int day,bool directory=false)=>new(name,"/fixture/"+name,directory,false,size,new(2026,1,day),"644");
        var entries=new[]{Entry("file10.txt",2,3),Entry("..",0,1,true),Entry("folder10",0,1,true),Entry("file1.txt",3,2),Entry("file2.log",1,1),Entry("folder2",0,1,true)};
        Field<List<FileEntry>>(pane,"localEntries").AddRange(entries); Field<List<FileEntry>>(pane,"remoteEntries").AddRange(entries); Call(pane,"FilterFiles");
        var window=new Window { Width=1400,Height=850,Content=pane }; window.Show(); RedesignChecks.Frames();
        var local=Field<ListBox>(pane,"localList"); var remote=Field<ListBox>(pane,"remoteList"); local.SelectedItems!.Add(entries[0]); local.SelectedItems.Add(entries[3]);
        Button Header(bool remoteSide,FileSort sort)=>window.GetVisualDescendants().OfType<Button>().Single(b=>b.Name==(remoteSide?"Remote":"Local")+"Sort_"+sort);
        Equal(new[]{"file1.txt","file2.log","file10.txt"},Names(local),"Natural local names"); Click(Header(false,FileSort.Size)); Equal(new[]{"file2.log","file10.txt","file1.txt"},Names(local),"Header size ascending"); Click(Header(false,FileSort.Size)); Equal(new[]{"file1.txt","file10.txt","file2.log"},Names(local),"Header size descending");
        if(local.SelectedItems!.Count!=2)throw new Exception("Header sorting lost selected files"); Equal(new[]{"file1.txt","file2.log","file10.txt"},Names(remote),"Local sort affected remote panel");
        Click(Header(true,FileSort.Type)); Equal(new[]{"file2.log","file1.txt","file10.txt"},Names(remote),"Remote extension sorting");
        Field<ComboBox>(pane,"localSort").SelectedIndex=2; Equal(new[]{"file2.log","file1.txt","file10.txt"},Names(local),"Combo and ascending reset");
        Header(false,FileSort.Modified).Focus(); window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Frames(); Equal(new[]{"file10.txt","file1.txt","file2.log"},Names(local),"Header keyboard activation");
        if(Field<ComboBox>(pane,"fileTarget").SelectedIndex!=0 || !Field<bool>(pane,"localDescending"))throw new Exception("Header focus/direction not synchronized");
        Call(pane,"FilterFiles"); if(local.SelectedItems!.Count!=2 || !Field<bool>(pane,"localDescending"))throw new Exception("Refilter lost selection or direction");
        foreach(var theme in new[]{"graphite","light"})foreach(var width in new[]{650,1400})
        { DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 }); window.Width=width; RedesignChecks.Frames(); foreach(var header in window.GetVisualDescendants().OfType<Button>().Where(b=>b.Name?.Contains("Sort_")==true && b.IsEffectivelyVisible)) { var point=header.TranslatePoint(default,window)!.Value; if(point.X<0 || point.X+header.Bounds.Width>window.ClientSize.Width+1)throw new Exception("SFTP header escapes its panel"); } window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"sftp-{language}-{theme}-{width}.png")); }
        window.Close(); DesignSystem.ApplyPreferences(preferences);
    }
}
