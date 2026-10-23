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
    internal static void ConnectionStatusLayout(string root)
    {
        var preferences=DesignSystem.Preferences;
        var factory=typeof(MainWindow).GetMethod("Row",BindingFlags.Static|BindingFlags.NonPublic)!;
        try
        {
            foreach(var theme in new[]{"dark","light"})foreach(var width in new[]{180,300})foreach(var font in new[]{13,16})
            {
                DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=font });
                var stack=new StackPanel { Spacing=12,Width=width,Margin=new Thickness(16) };
                var window=new Window { Width=width+32,Height=600,Content=stack };
                var rows=new List<(Control Row,Protocol Protocol,bool Connected)>();
                foreach(var protocol in new[]{Protocol.SSH,Protocol.RDP})foreach(var favorite in new[]{false,true})foreach(var connected in new[]{false,true})
                {
                    var profile=new ServerProfile { Name="server-production",Host="fixture.invalid",Protocol=protocol,Port=protocol==Protocol.RDP?3389:22,Favorite=favorite,Color="#4C9CE5" };
                    var row=(Control)factory.Invoke(null,[ProfileIcons.Resolve(profile),profile.Name,favorite,connected,protocol.ToString(),profile,font==16?profile.Endpoint:null])!;
                    stack.Children.Add(row); rows.Add((row,protocol,connected));
                }
                window.Show(); RedesignChecks.Frames(3);
                try
                {
                    foreach(var group in rows.GroupBy(r=>r.Protocol))
                    {
                        double? protocolX=null,nameX=null;
                        foreach(var (row,protocol,connected) in group)
                        {
                            var texts=row.GetVisualDescendants().OfType<TextBlock>().ToArray();
                            var badge=texts.Single(t=>t.Text==protocol.ToString()); var name=texts.Single(t=>t.Text=="server-production");
                            var x=badge.TranslatePoint(default,row)!.Value.X; var nx=name.TranslatePoint(default,row)!.Value.X;
                            if(protocolX.HasValue && (Math.Abs(protocolX.Value-x)>.1 || Math.Abs(nameX!.Value-nx)>.1))throw new Exception("Connection status/favorite moved protocol or server name");
                            protocolX=x; nameX=nx;
                            if(badge.Bounds.Width<=0 || x+badge.Bounds.Width>row.Bounds.Width+.1 || nx+name.Bounds.Width>x+.1)throw new Exception("Protocol clipped or overlapped server name");
                            var marker=row.GetVisualDescendants().OfType<DeskIcon>().SingleOrDefault(i=>i.Glyph=="circle-check");
                            if((marker!=null)!=connected)throw new Exception("Connected marker does not match row state");
                            if(marker!=null)
                            {
                                var icon=row.GetVisualDescendants().OfType<ProfileVisual>().Single();
                                var markerX=marker.TranslatePoint(default,row)!.Value.X; var iconX=icon.TranslatePoint(default,row)!.Value.X;
                                if(markerX<iconX+icon.Bounds.Width || markerX+marker.Bounds.Width>nx)throw new Exception("Status marker overlaps icon or server name");
                            }
                        }
                    }
                    window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"connection-status-{theme}-{width}-font{font}.png"));
                }
                finally { window.Close(); }
            }
        }
        finally { DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark"); }
        Console.WriteLine("PASS connection row layout: fixed protocol/name positions across connected/disconnected and favorite states; separate icon/status; SSH/RDP, narrow/wide rows, fonts 13/16, address details, Dark/Light");
    }
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        ConnectionStatusLayout(root);
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
