using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class PresentationConsistencyChecks
{
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static bool HasIcon(Control content)=>content is DeskIcon || content.GetLogicalDescendants().OfType<DeskIcon>().Any();
    private static int menuCount,buttonCount;
    private sealed class Vault:ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<string?>(null);
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default)=>Task.CompletedTask;
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>Task.CompletedTask;
    }
    private static void Menus(IEnumerable<MenuItem> menus)
    {
        foreach(var menu in menus)
        {
            if(menu.Icon is not DeskIcon icon || DeskIcons.Geometry(icon.Glyph).Bounds.Width<=0)throw new Exception("Missing menu icon: "+menu.Header);
            if(menu.Tag is string source)
            {
                if(menu.Header as string!=AppText.T(source) || string.IsNullOrWhiteSpace(AutomationProperties.GetName(menu)))throw new Exception("Menu label/accessibility mismatch: "+source);
                if((menu.Header as string)?.Contains("Ctrl+")==true)throw new Exception("Shortcut embedded in menu title: "+source);
            }
            menuCount++; Menus(menu.Items.OfType<MenuItem>());
        }
    }
    private static void Buttons(Control root)
    {
        foreach(var button in root.GetLogicalDescendants().OfType<Button>())
        {
            if(button.Tag is string source && DeskIcons.ForAction(source)!=null)
            {
                if(button.Content is not Control content || !HasIcon(content))throw new Exception("Missing action icon: "+source);
                if(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))throw new Exception("Unnamed action: "+source);
                buttonCount++;
            }
            if(button.ContextMenu is {} menu)Menus(menu.Items.OfType<MenuItem>());
        }
    }
    private static void CaptureMenu(MainWindow main,Button button,string file)
    {
        var menu=button.ContextMenu!; menu.Open(button); RedesignChecks.Frames(4); Menus(menu.Items.OfType<MenuItem>());
        if(TopLevel.GetTopLevel(menu) is {} popup)popup.CaptureRenderedFrame()!.Save(file);
        else main.CaptureRenderedFrame()!.Save(file);
        menu.Close(); RedesignChecks.Frames(2);
    }
    internal static void Run(string root)
    {
        // Audit actual production factory calls as well as dynamic menus below.
        var labels=new HashSet<string>(StringComparer.Ordinal);
        foreach(var file in Directory.EnumerateFiles("src/ServerDesk.App","*.cs"))
        {
            if(Path.GetFileName(file)=="RdpInteractionCheck.cs")continue;
            foreach(Match match in Regex.Matches(File.ReadAllText(file),"(?<!DeskIcons\\.)(?:Button|AddAction|Action|OperationMenu|Menu)\\(\"([^\"\\r\\n]+)\""))labels.Add(match.Groups[1].Value);
        }
        var missing=labels.Where(label=>DeskIcons.ForAction(label)==null).Order().ToArray();
        if(missing.Length>0)throw new Exception("Production actions without icon metadata: "+string.Join("; ",missing));
        foreach(var glyph in labels.Select(DeskIcons.ForAction).OfType<string>().Concat(DeskIcons.ApplicationPaths.Keys))
        {
            if(DeskIcons.Resolve(glyph)=="file" && glyph!="file")throw new Exception("Unknown icon silently falls back: "+glyph);
            var bounds=DeskIcons.Geometry(glyph).Bounds; if(bounds.Width<=0||bounds.Height<=0||bounds.X<0||bounds.Y<0||bounds.Right>24||bounds.Bottom>24)throw new Exception("Invalid action geometry: "+glyph);
        }
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(repo.ApplicationSettingsAsync().Result with { Preferences=preferences with { Language=language } }));
            var profile=new ServerProfile { Name="Network Tools · Paleta poleceń",Folder="Komendy",Host="fixture.invalid",Username="fixture" }; RedesignChecks.Wait(repo.SaveAsync(profile));
            var main=new MainWindow(new(repo),repo,new Vault(),new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            var title=Named<StackPanel>(main,"WorkspaceTitleActions"); var layout=title.Children.OfType<Button>().Single(b=>b.Tag as string=="Układ"); var more=title.Children.OfType<Button>().Single(b=>b.Tag as string=="Menu");
            var modes=layout.ContextMenu!.Items.OfType<MenuItem>().ToArray();
            var expected=new[]{"layout-single","layout-columns","layout-rows","layout-three","layout-four","layout-equalize","panel-left"};
            if(!modes.Select(m=>((DeskIcon)m.Icon!).Glyph).SequenceEqual(expected))throw new Exception("Layout icons do not describe their layouts");
            var actionMenu=more.ContextMenu!.Items.OfType<MenuItem>().ToArray();
            foreach(var (source,shortcut) in new[]{("Paleta akcji","Ctrl+Shift+P"),("Komendy","Ctrl+Shift+K")})
            {
                var item=actionMenu.Single(m=>m.Tag as string==source); if(item.InputGesture?.ToString()!=shortcut)throw new Exception("Shortcut metadata mismatch: "+source);
            }
            foreach(var theme in new[]{"graphite","light"})foreach(var width in new[]{900,1320})
            {
                main.PreviewPreferences(preferences with { Language=language,InterfaceFontSize=width==900?16:13 }); DesignSystem.Apply(theme); main.Width=width; main.Height=width==900?600:840; RedesignChecks.Frames();
                CaptureMenu(main,layout,Path.Combine(root,$"layout-{language}-{theme}-{width}.png")); CaptureMenu(main,more,Path.Combine(root,$"menu-{language}-{theme}-{width}.png"));
            }
            modes[3].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); RedesignChecks.Frames(); if(main.Workspace.Mode!=WorkspaceMode.Three)throw new Exception("Three-panel menu action failed");
            modes[4].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); RedesignChecks.Frames(); if(main.Workspace.Mode!=WorkspaceMode.Grid)throw new Exception("Four-panel menu action failed"); main.SetWorkspaceMode(WorkspaceMode.Single);
            var paletteTask=(Task)typeof(MainWindow).GetMethod("CommandsAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null)!; RedesignChecks.Frames(5); var palette=main.OwnedWindows.Single();
            if(palette.Title!=AppText.T("Paleta akcji") || !Named<ListBox>(palette,"CommandResults").GetVisualDescendants().OfType<DeskIcon>().Any())throw new Exception("Action palette title or icons missing");
            palette.CaptureRenderedFrame()!.Save(Path.Combine(root,$"action-palette-{language}.png")); palette.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(paletteTask);
            RedesignChecks.Frames(5);
            var folder=Field<TreeView>(main,"tree").Items.OfType<TreeViewItem>().Single();
            foreach(var node in new[]{folder}.Concat(folder.Items.OfType<TreeViewItem>()).ToArray())
            {
                var menu=node.ContextMenu!; menu.Open(node); RedesignChecks.Frames(3); Menus(menu.Items.OfType<MenuItem>()); menu.Close();
            }
            foreach(var protocol in Enum.GetValues<Protocol>())
            {
                var pane=new SessionPane(main,profile with { Id=Guid.NewGuid(),Protocol=protocol,Serial=new() { PortName="COM999" } },new(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane); RedesignChecks.Frames(4); Buttons(pane);
                if(protocol==Protocol.SSH)
                {
                    var monitoring=Named<WrapPanel>(pane,"SessionToolbar").Children.OfType<Button>().Single(b=>b.Tag as string=="Monitoring");
                    if(AutomationProperties.GetName(monitoring)!=AppText.T("Monitoring"))throw new Exception("Monitoring label differs from its accessible name");
                }
                if(protocol==Protocol.RDP)
                {
                    var show=Field<Button>(pane,"rdpShow"); if(AutomationProperties.GetName(show)!=AppText.T(pane.UseEmbeddedRdp?"Pulpit":"Pokaż okno"))throw new Exception("RDP desktop label differs from its accessible name");
                }
                Menus(Field<TabControl>(main,"tabs").Items.OfType<TabItem>().Last().ContextMenu!.Items.OfType<MenuItem>());
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"protocol-{protocol}-{language}.png"));
            }
            foreach(var tool in Enum.GetValues<NetworkToolKind>())
            {
                var pane=main.OpenNetworkTool(tool); RedesignChecks.Frames(3); Buttons(pane);
                if(!pane.Caption.StartsWith(NetworkToolCatalog.Name(tool),StringComparison.Ordinal))throw new Exception("Network tool caption differs from its name");
                if(tool==NetworkToolKind.Http && pane.ReadRequest().Authentication!="None")throw new Exception("Translated HTTP choice changed protocol value");
                if(tool==NetworkToolKind.Ports)
                {
                    var group=(StackPanel)Field<Dictionary<string,Control>>(pane,"fieldGroups")["portProfile"]; ((ComboBox)group.Children.Last()).SelectedItem="Web Services";
                    if(pane.ReadRequest().Ports!=NetworkToolsService.PortProfiles["Web Services"])throw new Exception("Translated port profile changed backend lookup");
                }
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"network-{tool}-{language}.png"));
            }
            RedesignChecks.Wait(main.OpenCommandsPanelAsync()); RedesignChecks.Frames(5); Buttons(Named<CommandPanel>(main,"CommandsPanel"));
            var section=Named<ComboBox>(main,"CommandSection"); if(section.Items[0] as string!=AppText.T("Ściągawka poleceń"))throw new Exception("Command reference name is not localized");
            if(repo.ListAsync().Result.Single()!=profile)throw new Exception("Presentation audit changed user profile/folder data");
            main.Close(); RedesignChecks.Frames(5); var confirmation=main.OwnedWindows.Single(); confirmation.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            SshAcceptanceChecks.Until(()=>!main.IsVisible,"Presentation audit did not shut down");
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
        File.WriteAllText(Path.Combine(root,"coverage.json"),JsonSerializer.Serialize(new { sourceActions=labels.Order().ToArray(),menuCount,buttonCount },new JsonSerializerOptions { WriteIndented=true }));
        Console.WriteLine($"PASS presentation consistency: {labels.Count} production action labels, {menuCount} menu items, {buttonCount} action buttons; PL/EN, distinct layout icons, separate shortcuts, server/folder/tab/terminal/SFTP/commands menus, nine network tools and all protocols, Graphite/Light, compact/font16; user data preserved");
    }
}
