using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Iciclecreek.Terminal;

internal static partial class SftpBatchChecks
{
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(pane)!;
    private static object? Call(SessionPane pane,string name,params object[] args)=>typeof(SessionPane).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(pane,args);
    private static void Wait(Task task)
    { var until=DateTime.UtcNow.AddSeconds(60); while(!task.IsCompleted && DateTime.UtcNow<until)RedesignChecks.Frames(1); if(!task.IsCompleted)throw new TimeoutException("SFTP batch timed out"); task.GetAwaiter().GetResult(); }
    private static void Click(Window w,Control control,RawInputModifiers keys=RawInputModifiers.None,MouseButton mouse=MouseButton.Left)
    { var p=control.TranslatePoint(new Point(control.Bounds.Width/2,control.Bounds.Height/2),w)!.Value; w.MouseMove(p,keys); w.MouseDown(p,mouse,keys); w.MouseUp(p,mouse,keys); RedesignChecks.Frames(3); }
    private static Control Row(ListBox list,string name)=>list.GetVisualDescendants().OfType<ListBoxItem>().Single(i=>i.DataContext is FileEntry f && f.Name==name);
    private static string[] Names(ListBox list)=>(list.SelectedItems?.OfType<FileEntry>()??[]).Select(f=>f.Name).Order().ToArray();
    private static void Select(ListBox list,params string[] names)
    { list.SelectedItems!.Clear(); foreach(var entry in list.Items.OfType<FileEntry>().Where(e=>names.Contains(e.Name)))list.SelectedItems.Add(entry); }
    private static void Press(Window w,Key key,RawInputModifiers modifiers,PhysicalKey physical)
    { w.KeyPress(key,modifiers,physical," "); w.KeyRelease(key,modifiers,physical," "); RedesignChecks.Frames(3); }
    internal static void Layout(MainWindow main,ProfileRepository repo,string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            using var pane=new SessionPane(main,new() { Name="Selection fixture",Host="fixture.invalid",Username="test" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
            var files=Field<Grid>(pane,"files"); files.IsVisible=true; pane.Children.OfType<TerminalControl>().Single().IsVisible=false;
            var local=Field<ListBox>(pane,"localList"); var remote=Field<ListBox>(pane,"remoteList");
            var entries=new[]{new FileEntry("..","/fixture/..",true,false,0,DateTime.Now,""),new("folder","/fixture/folder",true,false,0,DateTime.Now,""),new("a.txt","/fixture/a.txt",false,false,12,DateTime.Now,""),new("b.txt","/fixture/b.txt",false,false,24,DateTime.Now,""),new("c.log","/fixture/c.log",false,false,36,DateTime.Now,"")};
            Field<List<FileEntry>>(pane,"localEntries").AddRange(entries); Field<List<FileEntry>>(pane,"remoteEntries").AddRange(entries); Call(pane,"FilterFiles");
            var w=new Window { Width=1200,Height=740,Content=pane }; w.Show(); RedesignChecks.Frames();
            Click(w,Row(local,"a.txt")); Click(w,Row(local,"b.txt"),RawInputModifiers.Control);
            if(!Names(local).SequenceEqual(new[]{"a.txt","b.txt"}))throw new Exception("Ctrl+click did not retain a group");
            var drag=Call(pane,"CaptureFileDrag",false)!;
            if(((IEnumerable)drag.GetType().GetProperty("Entries")!.GetValue(drag)!).Cast<object>().Count()!=2)throw new Exception("Drag payload lost selected entries");
            var point=Row(local,"a.txt").TranslatePoint(new Point(80,12),w)!.Value;
            w.MouseMove(point,RawInputModifiers.None); w.MouseDown(point,MouseButton.Left,RawInputModifiers.None); RedesignChecks.Frames(2);
            if(Names(local).Length!=2)throw new Exception("Starting a drag collapses the selected group");
            w.MouseUp(point,MouseButton.Left,RawInputModifiers.None); RedesignChecks.Frames(2); if(Names(local).Length!=1)throw new Exception("Ordinary click no longer collapses selection");
            Click(w,Row(local,"b.txt"),RawInputModifiers.Control); Click(w,Row(local,"a.txt"),mouse:MouseButton.Right);
            if(Names(local).Length!=2)throw new Exception("Right-click collapsed selection"); local.ContextMenu!.Close();
            if(local.ContextMenu.Items.OfType<MenuItem>().Single(m=>m.Header as string==AppText.T("Zmień nazwę")).IsEnabled)throw new Exception("Rename enabled for a group");
            Field<ComboBox>(pane,"localSort").SelectedIndex=1; Call(pane,"FilterFiles");
            if(!Names(local).SequenceEqual(new[]{"a.txt","b.txt"}) || Names(remote).Length!=0)throw new Exception("Sort lost selection or changed opposite panel");
            Field<TextBox>(pane,"localFilter").Text="a.txt"; RedesignChecks.Frames(2); if(!Names(local).SequenceEqual(new[]{"a.txt"}))throw new Exception("Filter retained an invisible selected file");
            Field<TextBox>(pane,"localFilter").Text=""; Call(pane,"FilterFiles"); local.Focus(); Press(w,Key.A,RawInputModifiers.Control,PhysicalKey.A);
            if(Names(local).Length!=4 || Names(local).Contains(".."))throw new Exception("Ctrl+A must select visible entries and skip parent");
            Press(w,Key.Escape,RawInputModifiers.None,PhysicalKey.Escape); if(Names(local).Length!=0)throw new Exception("Escape did not clear selection");
            Click(w,Row(local,"a.txt")); Click(w,Row(local,"c.log"),RawInputModifiers.Shift); if(Names(local).Length<2 || !Names(local).Contains("c.log"))throw new Exception("Shift selection failed");
            Select(local,"a.txt","b.txt"); Select(remote,"folder","c.log");
            foreach(var theme in new[]{"dark","light"})foreach(var width in new[]{650,1200})
            {
                DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 }); DesignSystem.Apply(theme); w.Width=width; RedesignChecks.Frames();
                var selectedRow=Row(local,"a.txt").GetVisualDescendants().OfType<ContentPresenter>().Single(p=>p.Name=="PART_ContentPresenter");
                if(selectedRow.Background is not ISolidColorBrush selectedBrush || selectedBrush.Color!=Color.Parse(DesignSystem.Themes.Single(t=>t.Id==theme).Selected))throw new Exception("SFTP selected rows do not use the high-contrast theme surface");
                foreach(var summary in w.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.Name is "LocalSelectionSummary" or "RemoteSelectionSummary" && t.IsEffectivelyVisible))
                { var p=summary.TranslatePoint(default,w)!.Value; if(p.X<0 || p.X+summary.Bounds.Width>w.Bounds.Width+1 || p.Y+summary.Bounds.Height>w.Bounds.Height+1 || !summary.Text!.Contains('2'))throw new Exception("Selection summary missing or outside viewport"); }
                w.CaptureRenderedFrame()!.Save(Path.Combine(root,$"selection-{language}-{theme}-{width}.png"));
            }
            w.Width=1200; Field<List<FileEntry>>(pane,"localEntries").Clear(); Field<List<FileEntry>>(pane,"localEntries").AddRange(Enumerable.Range(0,1000).Select(i=>new FileEntry("file-"+i,"/fixture/file-"+i,false,false,i,DateTime.Now,""))); Call(pane,"FilterFiles"); RedesignChecks.Frames(); local.Focus(); Press(w,Key.A,RawInputModifiers.Control,PhysicalKey.A);
            if(Names(local).Length!=1000)throw new Exception("Large list selection lost entries"); Field<ComboBox>(pane,"localSort").SelectedIndex=2; RedesignChecks.Frames();
            if(Names(local).Length!=1000 || local.GetVisualDescendants().OfType<ListBoxItem>().Count()>200)throw new Exception("Large list selection or virtualization regressed");
            w.Close();
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
        Console.WriteLine("PASS SFTP group selection: real Ctrl/Shift clicks, group retained on drag start, ordinary/right click, immutable drag payload, Ctrl+A/Escape, independent panels, filter/sort preservation, single-file guards, virtualized 1000-entry list; PL/EN 650/1200px Dark/Light font16");
    }
}
