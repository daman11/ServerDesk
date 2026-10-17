using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class TreeAppearanceChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static IEnumerable<TreeViewItem> Nodes(IEnumerable<object?> items)
    { foreach(var node in items.OfType<TreeViewItem>()) { yield return node; foreach(var child in Nodes(node.Items))yield return child; } }
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        var vm=Field<MainViewModel>(main,"vm"); var tree=Field<TreeView>(main,"tree"); var savedPreferences=main.Preferences;
        foreach(var folder in new[]{"01 Empty","02 Empty brand","03 Services/Empty","04 Only folders/Child"})RedesignChecks.Wait(repo.AddFolderAsync(folder));
        RedesignChecks.Wait(repo.SetFolderIconAsync("02 Empty brand","brand-docker"));
        var profile=new ServerProfile { Name="nginx01",Host="fixture.invalid",Username="fixture",Folder="03 Services",IconId="brand-nginx" };
        RedesignChecks.Wait(repo.SaveAsync(profile)); RedesignChecks.Wait(repo.SaveAsync(profile with { Id=Guid.NewGuid(),Name="postgres01",Folder="",IconId="brand-postgresql" }));
        RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames();
        TreeViewItem Folder(string path)=>Nodes(tree.Items).Single(n=>n.Tag as string==path);
        TextBlock Count(string path)=>((Control)Folder(path).Header!).GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="ConnectionFolderCount");
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames();
            if(Count("01 Empty").Text!=AppText.T("Pusty") || Count("03 Services").Text!="1" || Count("04 Only folders").Text!="0")throw new Exception("Empty leaf and zero-server parent were conflated");
            var branded=Folder("02 Empty brand"); var glyphs=((Control)branded.Header!).GetVisualDescendants().OfType<DeskIcon>().ToArray();
            if(!glyphs.Any(i=>i.Glyph=="folder") || !glyphs.Any(i=>i.Glyph=="brand-docker") || !branded.Classes.Contains("empty-folder") || AutomationProperties.GetName(branded)!=AppText.F($"Folder: {"02 Empty brand"}"))throw new Exception("Branded empty folder lost folder identity/accessibility");
            var populated=Folder("03 Services");
            DeskIcon FolderGlyph()=>((Control)populated.Header!).GetVisualDescendants().OfType<DeskIcon>().Single(i=>i.Name=="ConnectionFolderGlyph");
            populated.IsExpanded=false; RedesignChecks.Frames(2); if(FolderGlyph().Glyph!="folder")throw new Exception("Collapsed folder shows open icon");
            populated.IsExpanded=true; RedesignChecks.Frames(2); if(FolderGlyph().Glyph!="folder-open")throw new Exception("Expanded folder shows closed icon");
            var emptySlot=branded.GetVisualDescendants().OfType<Panel>().First(p=>p.Name=="PART_ExpandCollapseChevronContainer");
            var fullSlot=populated.GetVisualDescendants().OfType<Panel>().First(p=>p.Name=="PART_ExpandCollapseChevronContainer");
            if(Math.Abs(emptySlot.Bounds.Width-fullSlot.Bounds.Width)>.1 || emptySlot.Bounds.Width<12)throw new Exception("Empty folder lost reserved expander width");
            foreach(var theme in new[]{"dark","light"})
            {
                DesignSystem.Apply(theme); main.PreviewPreferences(savedPreferences with { InterfaceFontSize=16,SidebarWidth=280 }); main.Width=1100; main.Height=900; RedesignChecks.Frames();
                branded=Folder("02 Empty brand"); tree.SelectedItem=branded;
                ((Control)branded.Header!).BringIntoView(); RedesignChecks.Frames();
                var header=(Control)branded.Header!;
                var labels=header.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.IsEffectivelyVisible).ToArray();
                if(header.Bounds.Height<30 || labels.Any(t=>t.Bounds.Width<=0))throw new Exception("Folder label/badge clipped");
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"tree-folders-{language}-{theme}.png"));
                main.PreviewPreferences(savedPreferences with { InterfaceFontSize=16,SidebarWidth=220 }); main.Width=900; main.Height=650; RedesignChecks.Frames();
                header=(Control)Folder("01 Empty").Header!;
                if(header.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="ConnectionFolderCount").Bounds.Width<=0)throw new Exception("Empty badge lost in narrow sidebar");
            }
            var pick=main.EditFolderIconAsync("01 Empty"); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single();
            var combo=dialog.GetVisualDescendants().OfType<ComboBox>().Single(); combo.SelectedItem=FolderIcons.Choices.Single(c=>c.Id=="brand-postgresql");
            if(dialog.GetVisualDescendants().OfType<DeskIcon>().All(i=>i.Glyph!="brand-postgresql"))throw new Exception("Brand folder picker preview missing");
            dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(pick); RedesignChecks.Frames();
            if(repo.FolderIconsAsync().GetAwaiter().GetResult().GetValueOrDefault("01 Empty")!="brand-postgresql")throw new Exception("Brand folder icon not persisted");
            pick=main.EditFolderIconAsync("01 Empty"); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); combo=dialog.GetVisualDescendants().OfType<ComboBox>().Single(); combo.SelectedItem=FolderIcons.Choices.Single(c=>c.Id=="brand-grafana");
            dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(pick);
            if(repo.FolderIconsAsync().GetAwaiter().GetResult().GetValueOrDefault("01 Empty")!="brand-postgresql")throw new Exception("Cancel changed folder icon");
            Console.WriteLine("PASS "+language+": distinct empty/branded/populated folder headers, empty versus zero-server parent, constant expander width, open/closed glyph, 220/280px font16 Dark/Light, brand folder picker save/cancel");
        }
        AppText.SetLanguage("pl"); main.PreviewPreferences(savedPreferences); DesignSystem.Apply("dark");
    }
}
