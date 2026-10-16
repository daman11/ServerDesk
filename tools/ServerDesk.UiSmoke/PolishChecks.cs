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

internal static class PolishChecks
{
    private static T Named<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static Button Action(Window window,string name)=>window.GetVisualDescendants().OfType<Button>().Single(c=>AutomationProperties.GetName(c)==name);
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(); }
    internal static void Page(Window editor,int index) { Named<TabControl>(editor,"ProfileEditorNavigation").SelectedIndex=index; RedesignChecks.Frames(); }
    internal static void AssertNavigation(Window editor)
    {
        foreach(var tab in Named<TabControl>(editor,"ProfileEditorNavigation").Items.OfType<TabItem>().Where(t=>t.IsEffectivelyVisible))
        foreach(var label in ((Control)tab.Header!).GetVisualDescendants().OfType<TextBlock>())
        {
            if(label.Bounds.Width+1<label.TextLayout.WidthIncludingTrailingWhitespace)throw new Exception("Profile navigation label clipped: "+label.Text);
            foreach(var ancestor in label.GetVisualAncestors().OfType<Control>().Where(c=>c.ClipToBounds))
            { var p=label.TranslatePoint(default,ancestor)!.Value; if(p.X< -1 || p.X+label.Bounds.Width>ancestor.Bounds.Width+1)throw new Exception("Profile navigation escapes clipping ancestor: "+label.Text); }
        }
    }
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        var edit=Dialogs.Edit(main,null,["Produkcja/Linux","Produkcja/Windows"]); RedesignChecks.Frames(); var editor=main.OwnedWindows.Single();
        Click(Action(editor,"Zapisz"));
        if(edit.IsCompleted || !Named<TextBox>(editor,"ProfileName").Classes.Contains("error") || !Named<TextBox>(editor,"ProfileHost").Classes.Contains("error") || !Named<TextBox>(editor,"ProfileUsername").Classes.Contains("error"))throw new Exception("Empty editor did not highlight all required fields");
        editor.CaptureRenderedFrame()!.Save(Path.Combine(root,"profile-validation.png"));
        Named<TextBox>(editor,"ProfileName").Text="Database production"; Named<TextBox>(editor,"ProfileHost").Text="db.example"; Named<TextBox>(editor,"ProfileUsername").Text="deploy"; RedesignChecks.Frames();
        if(Named<TextBox>(editor,"ProfileName").Classes.Contains("error"))throw new Exception("Corrected editor field retained an error");
        Named<ComboBox>(editor,"ProfileAuthentication").SelectedItem=Authentication.PrivateKey; Click(Action(editor,"Zapisz"));
        if(!Named<TextBox>(editor,"ProfilePrivateKey").Classes.Contains("error") || edit.IsCompleted)throw new Exception("Private-key profile accepted an empty path");
        Named<TextBox>(editor,"ProfilePrivateKey").Text="C:/keys/production key";
        Page(editor,2); Named<TextBox>(editor,"ProfileFolder").Text="Production//Linux"; Click(Action(editor,"Zapisz"));
        if(Named<TabControl>(editor,"ProfileEditorNavigation").SelectedIndex!=2 || !Named<TextBox>(editor,"ProfileFolder").Classes.Contains("error"))throw new Exception("Invalid folder not located on its page");
        Named<TextBox>(editor,"ProfileFolder").Text="Production/Linux";
        Page(editor,1); var color=editor.GetVisualDescendants().OfType<TextBox>().Single(c=>AutomationProperties.GetName(c)=="Kolor serwera"); color.Text="blue"; Click(Action(editor,"Zapisz"));
        if(edit.IsCompleted || !color.Classes.Contains("error"))throw new Exception("Invalid color accepted"); color.Text="#69CFB6"; RedesignChecks.Frames();
        if(!editor.GetVisualDescendants().OfType<ProfileVisual>().Any(v=>v.IsEffectivelyVisible && v.Profile.Color=="#69CFB6"))throw new Exception("Live editor preview did not follow color");
        var protocol=Named<ComboBox>(editor,"ProfileProtocol"); protocol.SelectedItem=Protocol.RDP; RedesignChecks.Frames();
        if(Named<TextBox>(editor,"ProfilePort").Text!="3389")throw new Exception("Default port did not follow RDP protocol");
        Page(editor,3); Named<TextBox>(editor,"RdpWidth").Text="invalid hidden width";
        Page(editor,0); Named<TextBox>(editor,"ProfilePort").Text="3391"; protocol.SelectedItem=Protocol.SSH; RedesignChecks.Frames(); if(Named<TextBox>(editor,"ProfilePort").Text!="3391")throw new Exception("Protocol change overwrote a custom port");
        // Hidden RDP draft values must not block an SSH profile.
        Click(Action(editor,"Zapisz")); RedesignChecks.Wait(edit);
        var saved=edit.GetAwaiter().GetResult()??throw new Exception("Valid editor did not save");
        if(saved.Protocol!=Protocol.SSH || saved.Folder!="Production/Linux" || saved.Color!="#69CFB6" || saved.PrivateKeyPath!="C:/keys/production key" || saved.Rdp.Width!=1600)throw new Exception("Editor lost profile data or retained inactive RDP draft");

        var source=new ServerProfile { Name="Windows administration",Host="rdp.example",Protocol=Protocol.RDP,Port=3389,Username="LAB\\Administrator" };
        edit=Dialogs.Edit(main,source,[]); RedesignChecks.Frames(); editor=main.OwnedWindows.Single(); Page(editor,3);
        Named<TextBox>(editor,"RdpWidth").Text="0"; Click(Action(editor,"Zapisz"));
        if(edit.IsCompleted || Named<TabControl>(editor,"ProfileEditorNavigation").SelectedIndex!=3 || !Named<TextBox>(editor,"RdpWidth").Classes.Contains("error"))throw new Exception("RDP dimension error not highlighted");
        Named<TextBox>(editor,"RdpWidth").Text="1920";
        var prefs=DesignSystem.Preferences;
        foreach(var theme in new[]{"dark","light"})foreach(var size in new[]{(780d,720d,13),(600d,520d,16)})
        {
            DesignSystem.ApplyPreferences(prefs with { InterfaceFontSize=size.Item3 }); DesignSystem.Apply(theme); editor.Width=size.Item1; editor.Height=size.Item2;
            for(var page=0;page<4;page++)
            {
                Page(editor,page);
                AssertNavigation(editor);
                var save=Action(editor,"Zapisz"); var point=save.TranslatePoint(default,editor)!.Value;
                if(point.X<0 || point.Y<0 || point.X+save.Bounds.Width>editor.ClientSize.Width+1 || point.Y+save.Bounds.Height>editor.ClientSize.Height+1)throw new Exception("Profile editor footer escapes viewport");
                editor.CaptureRenderedFrame()!.Save(Path.Combine(root,$"profile-{theme}-{size.Item1}-page{page}.png"));
            }
        }
        Click(Action(editor,"Anuluj")); RedesignChecks.Wait(edit); if(edit.Result!=null || source.Rdp.Width!=1600)throw new Exception("Cancel modified the original profile");
        DesignSystem.ApplyPreferences(prefs); DesignSystem.Apply("dark");
        Console.WriteLine("PASS profile editor: field validation/correction, private-key path, folder/color/RDP errors, default/custom ports, live preview, inactive RDP draft, cancel, four pages at 600/780px in dark/light and font13/16");

        var search=main.GetVisualDescendants().OfType<TextBox>().Single(c=>AutomationProperties.GetName(c)=="Szukaj połączeń");
        Click(Action(main,"Filtr protokołu: RDP"));
        var tree=main.GetVisualDescendants().OfType<TreeView>().Single();
        if(tree.GetVisualDescendants().OfType<TreeViewItem>().Where(i=>i.Tag is ServerProfile).Any(i=>((ServerProfile)i.Tag!).Protocol!=Protocol.RDP))throw new Exception("Protocol filter retained SSH servers");
        search.Text="no-such-server"; RedesignChecks.Frames(); if(tree.Items.Count!=0 || !Action(main,"Wyczyść filtry pustej listy").IsEffectivelyVisible)throw new Exception("Empty query did not show recovery action");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"browser-empty.png")); Click(Action(main,"Wyczyść filtry pustej listy"));
        if(search.Text!="" || Named<TextBlock>(main,"ConnectionResultCount").Text!="Widoczne: 4 / 4")throw new Exception("Clear filters did not recover all servers");
        search.Text="deploy linux"; RedesignChecks.Frames();
        var results=tree.GetVisualDescendants().OfType<TreeViewItem>().Where(i=>i.Tag is ServerProfile).Select(i=>(ServerProfile)i.Tag!).ToArray();
        if(results.Length!=1 || results[0].Name!="web-prod-01")throw new Exception("Metadata token search inconsistent with tree");
        search.Focus(); main.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,"\u001b"); RedesignChecks.Frames(); if(search.Text!="")throw new Exception("Escape did not clear connection query");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"browser-refined.png"));
        var runtime=main.CurrentPreferences;
        main.PreviewPreferences(runtime with { SidebarWidth=220,InterfaceFontSize=16 }); main.Width=900; main.Height=600;
        search.Text="no results"; RedesignChecks.Frames();
        foreach(var button in main.GetVisualDescendants().OfType<Button>().Where(b=>b.IsEffectivelyVisible && (AutomationProperties.GetName(b)?.StartsWith("Filtr protokołu:")==true || b==Action(main,"Wyczyść filtry pustej listy"))))
        {
            var point=button.TranslatePoint(default,main)!.Value;
            if(point.X<48 || point.X+button.Bounds.Width>268+1)throw new Exception("Browser controls escape a 220px sidebar at font16");
            foreach(var label in button.GetVisualDescendants().OfType<TextBlock>())if(label.Bounds.Width+1<label.TextLayout.WidthIncludingTrailingWhitespace)throw new Exception("Browser filter text clipped at font16: "+label.Text);
        }
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"browser-220-font16.png"));
        Click(Action(main,"Wyczyść filtry pustej listy")); main.PreviewPreferences(runtime); main.Width=1320; main.Height=840; RedesignChecks.Frames();
        Console.WriteLine("PASS browser: RDP filter, token search across login/folder, result counts, empty recovery, Escape clear");
        var viewModel=new MainViewModel(repo); RedesignChecks.Wait(viewModel.LoadAsync());
        viewModel.SortMode=ProfileSort.Host;
        if(!viewModel.Servers.Select(p=>p.Host).SequenceEqual(new[]{"10.24.8.11","10.24.8.20","10.24.9.10","192.168.20.5"}))throw new Exception("View model ignored host sorting");
        var original=viewModel.Servers.First(); viewModel.SelectedServer=original;
        RedesignChecks.Wait(repo.SaveAsync(original with { Name="Refreshed server metadata",Color="#E5B65C" })); RedesignChecks.Wait(viewModel.LoadAsync());
        if(viewModel.SelectedServer?.Name!="Refreshed server metadata" || viewModel.SelectedServer.Color!="#E5B65C")throw new Exception("Reload retained stale selected profile");
        RedesignChecks.Wait(repo.SaveAsync(original)); RedesignChecks.Wait(viewModel.LoadAsync());
        viewModel.ProtocolFilter=Protocol.RDP; viewModel.Search="windows";
        if(viewModel.Servers.Count!=1 || viewModel.Servers[0].Protocol!=Protocol.RDP)throw new Exception("View model protocol/token filter differs from browser");
        Console.WriteLine("PASS view model: host sort, latest selection after profile update, combined protocol/query filter");
    }
}
