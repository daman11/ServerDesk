using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SftpClipboardChecks
{
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static void Set(SessionPane pane,string name,object value)=>typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(pane,value);
    private static void Call(SessionPane pane,string name)=>typeof(SessionPane).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,null);
    private static MenuItem Item(MenuItem group,string source)=>group.Items.OfType<MenuItem>().Single(item=>item.Tag as string==source);
    private static MenuItem Group(ContextMenu menu)=>menu.Items.OfType<MenuItem>().Single(item=>item.Tag as string=="Kopiuj do schowka");
    private static void Click(MenuItem item) { item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); RedesignChecks.Frames(); }
    private static T Result<T>(Task<T> task) { RedesignChecks.Wait(task); return task.Result; }
    internal static void Run(string root)
    {
        var language=AppText.Language; var preferences=DesignSystem.Preferences; var theme=DesignSystem.CurrentId;
        try { foreach(var chosen in new[]{"pl","en"})Check(root,chosen); }
        finally { AppText.SetLanguage(language); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply(theme); }
        Console.WriteLine("PASS SFTP clipboard PL/EN Graphite/Light: local/remote and active-pane menus, icons/gestures, cached offline copy, names/paths/directory, parent/empty no-op, Ctrl+Shift+C/Ctrl+F/Escape, native text copy, unconfirmed/loading/busy/closed guards and unchanged clipboard on read; no network, vault access, transfers or persisted profiles");
    }
    private static void Check(string root,string language)
    {
        AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { Language=language,InterfaceFontSize=16 } }));
        var vault=new NoSecrets(); var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(); DesignSystem.Apply(language=="pl"?"graphite":"light");
        using var pane=new SessionPane(main,new() { Name="Clipboard fixture",Host="fixture.invalid",Username="fixture" },new(repo),new ExternalRdpClient(),repo);
        pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().IsVisible=false; Field<Grid>(pane,"files").IsVisible=true;
        var window=new Window { Width=900,Height=640,Content=pane }; window.Show(); RedesignChecks.Frames();
        var clipboard=window.Clipboard!; var local=Field<ListBox>(pane,"localList"); var remote=Field<ListBox>(pane,"remoteList"); var target=Field<ComboBox>(pane,"fileTarget");
        var localPath=Path.Combine(paths.Data,"local folder"); const string remotePath="/srv/app space";
        var localEntry=new FileEntry("żółć.txt",Path.Combine(localPath,"żółć.txt"),false,false,10,DateTime.MinValue,"");
        var remoteEntries=new[]{new FileEntry("..","/srv",true,false,0,DateTime.MinValue,""),new FileEntry("A.txt",remotePath+"/A.txt",false,false,1,DateTime.MinValue,""),new FileEntry("a.txt",remotePath+"/a.txt",false,false,1,DateTime.MinValue,""),new FileEntry("link",remotePath+"/link",false,true,0,DateTime.MinValue,"")};
        var localGroup=Group(local.ContextMenu!); var remoteGroup=Group(remote.ContextMenu!);
        void Press(Key key,RawInputModifiers modifiers=RawInputModifiers.None)=>SshAcceptanceChecks.Press(window,key,modifiers);
        string? Text()=>Result(clipboard.TryGetTextAsync());
        void Seed()=>RedesignChecks.Wait(clipboard.SetTextAsync("clipboard sentinel"));
        try
        {
            Seed();
            Set(pane,"loadedLocalPath",localPath); Set(pane,"loadedRemotePath",remotePath); Field<TextBox>(pane,"localPath").Text=localPath; Field<TextBox>(pane,"remotePath").Text=remotePath;
            Field<List<FileEntry>>(pane,"localEntries").Add(localEntry); Field<List<FileEntry>>(pane,"remoteEntries").AddRange(remoteEntries); Call(pane,"FilterFilesAsync"); RedesignChecks.Frames();
            local.SelectedItem=localEntry; remote.SelectAll(); if(Text()!="clipboard sentinel")throw new Exception("Opening cached files/selection changed clipboard");
            foreach(var menu in new[]{local.ContextMenu!,remote.ContextMenu!})if(menu.Items.OfType<MenuItem>().Any(item=>(item.Tag as string is "Wyślij" or "Pobierz" or "Nowy katalog") && item.IsEnabled))throw new Exception("Disconnected menu enabled a file operation");
            foreach(var group in new[]{localGroup,remoteGroup})foreach(var item in group.Items.OfType<MenuItem>())if(item.Icon is not DeskIcon || item.Header as string!=AppText.T((string)item.Tag!))throw new Exception("Missing clipboard icon/translation");
            if(Item(remoteGroup,"Kopiuj pełne ścieżki").InputGesture?.ToString()!=KeyGesture.Parse("Ctrl+Shift+C").ToString())throw new Exception("Clipboard shortcut missing from menu");
            local.Focus(); Click(Item(remoteGroup,"Kopiuj pełne ścieżki")); if(Text()!=string.Join(Environment.NewLine,remoteEntries.Skip(1).Select(e=>e.FullPath)))throw new Exception("Remote context menu followed another pane, lost case variants or included parent");
            Click(Item(remoteGroup,"Kopiuj nazwy")); if(Text()!=string.Join(Environment.NewLine,remoteEntries.Skip(1).Select(e=>e.Name)))throw new Exception("Name copy differs");
            Click(Item(localGroup,"Kopiuj pełne ścieżki")); if(Text()!=localEntry.FullPath)throw new Exception("Local path copy changed path syntax");
            remote.Focus(); Press(Key.C,RawInputModifiers.Control|RawInputModifiers.Shift); if(Text()!=string.Join(Environment.NewLine,remoteEntries.Skip(1).Select(e=>e.FullPath)))throw new Exception("List shortcut copied another pane");
            Press(Key.F,RawInputModifiers.Control); var filter=Field<TextBox>(pane,"remoteFilter"); if(!filter.IsKeyboardFocusWithin)throw new Exception("Ctrl+F did not focus active filter"); filter.Text="A.txt"; RedesignChecks.Frames(); Press(Key.Escape); RedesignChecks.Frames(); if(filter.Text!="" || !remote.IsKeyboardFocusWithin || remote.ItemCount!=4)throw new Exception("Escape did not clear filter and restore list focus");
            var input=Field<TextBox>(pane,"localPath"); input.Focus(); input.SelectAll(); Press(Key.C,RawInputModifiers.Control); if(Text()!=localPath)throw new Exception("File shortcut replaced native text copy");
            var operations=pane.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Operacje"); target.SelectedIndex=0; Click(Item(Group(operations.ContextMenu!),"Kopiuj ścieżkę katalogu")); if(Text()!=localPath)throw new Exception("Operations menu ignored active side");
            remote.SelectedItem=remoteEntries[0]; Call(pane,"UpdateFileSelection"); Seed(); if(Item(remoteGroup,"Kopiuj nazwy").IsEnabled)throw new Exception("Parent-only selection can copy a name"); Click(Item(remoteGroup,"Kopiuj pełne ścieżki")); if(Text()!="clipboard sentinel")throw new Exception("Parent selection overwrote clipboard");
            remote.SelectedItems!.Clear(); Click(Item(remoteGroup,"Kopiuj ścieżkę katalogu")); if(Text()!=remotePath)throw new Exception("Directory copy required a selection");
            remote.SelectedItem=remoteEntries[1]; Seed(); Field<TextBox>(pane,"remotePath").Text="/unconfirmed"; if(remoteGroup.IsEnabled)throw new Exception("Unconfirmed directory enabled copy"); Click(Item(remoteGroup,"Kopiuj pełne ścieżki")); if(Text()!="clipboard sentinel")throw new Exception("Unconfirmed path copied stale row"); Field<TextBox>(pane,"remotePath").Text=remotePath;
            Set(pane,"fileRefreshes",1); Call(pane,"UpdateFileSelection"); Click(Item(remoteGroup,"Kopiuj ścieżkę katalogu")); if(remoteGroup.IsEnabled || Text()!="clipboard sentinel")throw new Exception("Directory loading copied stale data"); Set(pane,"fileRefreshes",0);
            var pending=new TaskCompletionSource(); var tasks=Field<HashSet<Task>>(pane,"fileViewTasks"); tasks.Add(pending.Task); Call(pane,"UpdateFileSelection"); Click(Item(remoteGroup,"Kopiuj pełne ścieżki")); if(remoteGroup.IsEnabled || Text()!="clipboard sentinel")throw new Exception("Pending filter copied stale selection"); pending.SetResult(); tasks.Remove(pending.Task); Call(pane,"UpdateFileSelection");
            Field<List<FileEntry>>(pane,"localEntries").AddRange(Enumerable.Range(0,2500).Select(i=>localEntry with { Name="file"+i,FullPath=Path.Combine(localPath,"file"+i) }));
            Call(pane,"FilterFilesAsync"); if(localGroup.IsEnabled)throw new Exception("Real background filtering did not disable clipboard actions"); RedesignChecks.Wait(Field<Task>(pane,"fileViewUpdate"));
            if(!Item(localGroup,"Kopiuj pełne ścieżki").IsEnabled || !remoteGroup.IsEnabled || Text()!="clipboard sentinel")throw new Exception("Background completion left clipboard actions disabled or changed clipboard");
            remote.Focus(); remote.ContextMenu!.Open(remote); remoteGroup.IsSubMenuOpen=true; RedesignChecks.Frames(); window.CaptureRenderedFrame()!.Save(Path.Combine(root,"sftp-copy-"+language+".png")); remote.ContextMenu.Close();
            RedesignChecks.Wait(pane.CloseAsync()); Seed(); Click(Item(remoteGroup,"Kopiuj pełne ścieżki")); if(Text()!="clipboard sentinel" || vault.Calls!=0 || main.OpenSessions.Count!=0 || repo.ListAsync().GetAwaiter().GetResult().Count!=0)throw new Exception("Copy after close touched clipboard, credentials or saved data");
        }
        finally { window.Close(); main.Close(); RedesignChecks.Frames(); }
    }
    private sealed class NoSecrets:ICredentialStore
    {
        internal int Calls;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("No vault access expected"); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Calls++; throw new Exception("No vault access expected"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("No vault access expected"); }
    }
}
