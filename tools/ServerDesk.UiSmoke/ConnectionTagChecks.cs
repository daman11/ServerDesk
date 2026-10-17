using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ConnectionTagChecks
{
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static IEnumerable<TreeViewItem> Nodes(IEnumerable<object?> items)
    { foreach(var node in items.OfType<TreeViewItem>()) { yield return node; foreach(var child in Nodes(node.Items))yield return child; } }
    private static void Until(Func<bool> done)
    { var clock=Stopwatch.StartNew(); while(!done()) { if(clock.Elapsed>TimeSpan.FromSeconds(20))throw new TimeoutException("Connection tag fixture timed out"); RedesignChecks.Frames(1); } }
    private static void Verify(bool value,string message) { if(!value)throw new Exception(message); }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"tags-"+language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var settings=repo.ApplicationSettingsAsync().GetAwaiter().GetResult(); RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { Language=language } }));
            var first=new ServerProfile { Name="Database production",Host="fixture.invalid",Username="fixture",Tags="prod, linux, database",Folder="Production/Linux",Favorite=true };
            var profiles=new[]{first,first with { Id=Guid.NewGuid(),Name="Windows production",Tags="prod;windows",Protocol=Protocol.RDP,Port=3389,Folder="Production/Windows",Favorite=false },first with { Id=Guid.NewGuid(),Name="Linux lab",Tags="lab, linux",Folder="Lab",Favorite=false },first with { Id=Guid.NewGuid(),Name="Untagged",Tags="",Folder="",Favorite=false }};
            foreach(var profile in profiles)RedesignChecks.Wait(repo.SaveAsync(profile));
            var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            var tree=Field<TreeView>(main,"tree");
            int Count()=>Nodes(tree.Items).Count(n=>n.Tag is ServerProfile);
            Window Open()
            { Click(Named<Button>(main,"ConnectionTagFilter")); RedesignChecks.Frames(); return main.OwnedWindows.Single(w=>w.Name=="ConnectionTagDialog"); }
            CheckBox Choice(Window dialog,string name)=>dialog.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="ConnectionTagChoice" && c.Tag as string==name);
            try
            {
                Verify(Count()==4,"Initial tag browser lost profiles");
                var protocolChoice=Named<ComboBox>(main,"ConnectionProtocolFilter"); protocolChoice.SelectedIndex=2; RedesignChecks.Frames(); Verify(Count()==1 && vm.ProtocolFilter==Protocol.RDP,"Compact protocol selector did not filter the tree");
                Click(Named<Button>(main,"ClearConnectionFilters")); RedesignChecks.Frames(); Verify(Count()==4 && protocolChoice.SelectedIndex==0,"Clearing filters did not reset the compact protocol selector");
                tree.SelectedItem=Nodes(tree.Items).Single(n=>n.Tag is ServerProfile p && p.Id==first.Id);
                vm.SelectedTags=["windows"]; tree.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter }); RedesignChecks.Frames();
                Verify(Count()==1 && vm.SelectedServer==null && !main.OpenSessions.Any() && !main.OwnedWindows.Any(),"Tag filter retained or opened a hidden selected profile");
                vm.SelectedTags=["prod","linux"]; vm.TagMode=TagFilterMode.Any; RedesignChecks.Frames(); Verify(Count()==3,"Any tag filter mismatch");
                vm.TagMode=TagFilterMode.All; RedesignChecks.Frames(); Verify(Count()==1,"All tag filter mismatch");
                vm.ProtocolFilter=Protocol.RDP; RedesignChecks.Frames(); Verify(Count()==0 && Field<StackPanel>(main,"browserEmpty").IsVisible,"Protocol and tag filters did not intersect");
                Click(Named<Button>(main,"ClearConnectionFilters")); RedesignChecks.Frames(); Verify(Count()==4 && vm.SelectedTags.Length==0 && vm.ProtocolFilter==null,"Clear filters retained a tag condition");
                var dialog=Open(); Choice(dialog,"prod").IsChecked=true;
                Verify(Named<TextBlock>(dialog,"ConnectionTagPreview").Text==AppText.F($"Pasujące połączenia: {2}"),"Draft preview did not count matching connections");
                Named<CheckBox>(dialog,"SelectedConnectionTagsOnly").IsChecked=true; RedesignChecks.Frames(); Verify(Named<ListBox>(dialog,"ConnectionTagList").Items.Count==1,"Selected-only view lost the draft or shows unselected tags");
                Named<CheckBox>(dialog,"SelectedConnectionTagsOnly").IsChecked=false; RedesignChecks.Frames(); Choice(dialog,"linux").IsChecked=true;
                Named<ComboBox>(dialog,"ConnectionTagMode").SelectedIndex=1; RedesignChecks.Frames(); Verify(Named<TextBlock>(dialog,"ConnectionTagPreview").Text==AppText.F($"Pasujące połączenia: {1}"),"All-tags draft preview mismatch");
                var color=dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ConnectionTagColor" && b.Tag as string=="prod");
                color.ContextMenu!.Items.OfType<MenuItem>().Single(i=>i.Tag as string=="#EB8181").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Click(dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj")); RedesignChecks.Frames();
                Verify(vm.SelectedTags.Length==0 && repo.TagColorsAsync().GetAwaiter().GetResult().Count==0,"Cancel saved filter/color drafts");
                dialog=Open(); Named<TextBox>(dialog,"ConnectionTagSearch").Text="PROD"; RedesignChecks.Frames();
                Verify(Named<ListBox>(dialog,"ConnectionTagList").Items.Count==1,"Tag search is not case insensitive"); Click(Named<Button>(dialog,"SelectVisibleConnectionTags")); RedesignChecks.Frames(); Verify(Choice(dialog,"prod").IsChecked==true,"Select visible did not select the matching tag");
                Named<TextBox>(dialog,"ConnectionTagSearch").Text="missing"; RedesignChecks.Frames(); dialog.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,""); RedesignChecks.Frames(); Verify(dialog.IsVisible && Named<TextBox>(dialog,"ConnectionTagSearch").Text=="","Escape did not clear tag search before closing");
                Named<TextBox>(dialog,"ConnectionTagSearch").Text="prod"; RedesignChecks.Frames();
                color=dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ConnectionTagColor");
                color.ContextMenu!.Items.OfType<MenuItem>().Single(i=>i.Tag as string=="#EB8181").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"tags-dialog-{language}.png"));
                Click(Named<Button>(dialog,"ApplyConnectionTags")); Until(()=>!dialog.IsVisible); RedesignChecks.Frames();
                Verify(Count()==2 && vm.SelectedTags.SequenceEqual(new[]{"prod"}) && vm.TagColors["PROD"]=="#EB8181","Applying tag/color changes failed");
                vm.Search="Windows"; RedesignChecks.Frames(); Verify(Count()==1,"Text search did not intersect tags");
                Click(Named<Button>(main,"ClearConnectionFilters")); RedesignChecks.Frames();
                dialog=Open(); Named<ComboBox>(dialog,"ConnectionTagMode").SelectedIndex=2; RedesignChecks.Frames();
                Verify(dialog.GetVisualDescendants().OfType<CheckBox>().Where(c=>c.Name=="ConnectionTagChoice").All(c=>!c.IsEnabled),"Untagged mode kept tag checkboxes enabled");
                Click(Named<Button>(dialog,"ApplyConnectionTags")); Until(()=>!dialog.IsVisible); RedesignChecks.Frames(); Verify(Count()==1 && vm.Servers.Single().Name=="Untagged","Untagged mode mismatch");
                Click(Named<Button>(main,"ClearConnectionFilters")); RedesignChecks.Frames();
                var edit=Dialogs.Edit(main,first,vm.Folders,new[]{"prod","linux","database","backup"}.Concat(Enumerable.Range(0,20).Select(n=>"team-"+n.ToString("D2"))),vm.TagColors); RedesignChecks.Frames(); var editor=main.OwnedWindows.Single(); PolishChecks.Page(editor,2);
                var input=editor.GetVisualDescendants().OfType<ConnectionTagsEditor>().Single().Input;
                var suggestion=Named<WrapPanel>(editor,"ProfileTagSuggestions").Children.OfType<Button>().Single(b=>b.Tag as string=="backup"); Click(suggestion); RedesignChecks.Frames(); Verify(ProfileTags.Parse(input.Text).Contains("backup"),"Suggestion did not add a tag");
                var remove=Named<WrapPanel>(editor,"ProfileTagChips").Children.OfType<Button>().Single(b=>b.Tag as string=="database"); Click(remove); RedesignChecks.Frames(); Verify(!ProfileTags.Parse(input.Text).Contains("database"),"Chip removal failed");
                var prodChip=Named<WrapPanel>(editor,"ProfileTagChips").Children.OfType<Button>().Single(b=>b.Tag as string=="prod"); var linuxChip=Named<WrapPanel>(editor,"ProfileTagChips").Children.OfType<Button>().Single(b=>b.Tag as string=="linux");
                Click(prodChip); Click(linuxChip); Verify(ProfileTags.Parse(input.Text).SequenceEqual(new[]{"backup"}),"Rapid chip removal resurrected a removed tag"); input.Text="prod, linux, backup"; RedesignChecks.Frames();
                var entry=Named<TextBox>(editor,"ProfileTagEntry"); entry.Text="team-19"; RedesignChecks.Frames();
                suggestion=Named<WrapPanel>(editor,"ProfileTagSuggestions").Children.OfType<Button>().Single(b=>b.Tag as string=="team-19"); Click(suggestion); RedesignChecks.Frames(); Verify(ProfileTags.Parse(input.Text).Contains("team-19") && entry.Text=="","Search did not reach a tag beyond the first suggestion page");
                entry.Text="new-one; new-two"; entry.Focus(); editor.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Frames(); Verify(editor.IsVisible && ProfileTags.Parse(input.Text).Contains("new-two"),"Enter in tag entry saved/closed the profile or lost a multi-tag paste");
                entry.Text="PROD"; editor.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Frames(); Verify(ProfileTags.Parse(input.Text).Count(t=>t.Equals("prod",StringComparison.OrdinalIgnoreCase))==1,"Entry created a duplicate assignment");
                editor.CaptureRenderedFrame()!.Save(Path.Combine(root,$"tags-editor-{language}.png"));
                Click(editor.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj")); RedesignChecks.Wait(edit); Verify(repo.ListAsync().GetAwaiter().GetResult().Single(p=>p.Id==first.Id).Tags==first.Tags,"Editor cancel persisted tags");
                edit=Dialogs.Edit(main,first,vm.Folders,[],vm.TagColors); RedesignChecks.Frames(); editor=main.OwnedWindows.Single(); PolishChecks.Page(editor,2); editor.GetVisualDescendants().OfType<ConnectionTagsEditor>().Single().Input.Text="prod; LINUX, prod, backup"; Named<TextBox>(editor,"ProfileTagEntry").Text="pending";
                Click(editor.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz")); RedesignChecks.Wait(edit); var edited=edit.GetAwaiter().GetResult()!;
                Verify(edited.Tags=="prod, LINUX, backup, pending","Editor did not normalize tags or commit the unfinished entry on save"); RedesignChecks.Wait(main.SaveEditedProfileAsync(first,edited)); RedesignChecks.Frames();
                var production=Nodes(tree.Items).Single(n=>n.Tag as string=="Production"); production.IsExpanded=false;
                vm.SelectedTags=["prod"]; RedesignChecks.Frames(); production=Nodes(tree.Items).Single(n=>n.Tag as string=="Production"); Verify(production.IsExpanded,"Tag filter did not reveal matching folders"); production.IsExpanded=false; production.IsExpanded=true;
                Click(Named<Button>(main,"ClearConnectionFilters")); RedesignChecks.Frames(); production=Nodes(tree.Items).Single(n=>n.Tag as string=="Production"); Verify(!production.IsExpanded,"Temporary tag expansion overwrote folder collapse state"); production.IsExpanded=true; RedesignChecks.Frames();
                RedesignChecks.Wait(repo.SaveAsync(first with { Id=Guid.NewGuid(),Name="Long label fixture",Folder="",Tags="produkcja zapasowa bardzo długa etykieta, oprogramowanie serwera" })); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames();
                Nodes(tree.Items).Single(n=>n.Tag is ServerProfile p && p.Name=="Long label fixture").BringIntoView(); RedesignChecks.Frames();
                var longRow=main.GetVisualDescendants().OfType<Grid>().First(s=>s.Name=="ConnectionRowTags" && s.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="produkcja zapasowa bardzo długa etykieta"));
                var longChip=(Border)longRow.Children[0]; var longText=longChip.GetVisualDescendants().OfType<TextBlock>().Single(); var textPoint=longText.TranslatePoint(default,longChip)!.Value;
                Verify(textPoint.X+longText.Bounds.Width<=longChip.Bounds.Width,"Long tag text overlaps adjacent chips");
                Verify(longChip.Bounds.Height<=30,"Long tags wrapped and expanded the connection row");
                foreach(var theme in new[]{"graphite","light"})
                {
                    DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 }); main.Width=900; main.Height=680; RedesignChecks.Frames(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"tags-browser-{language}-{theme}.png"));
                    dialog=Open(); dialog.Width=500; dialog.Height=530; RedesignChecks.Frames();
                    var apply=Named<Button>(dialog,"ApplyConnectionTags"); var point=apply.TranslatePoint(default,dialog)!.Value; Verify(point.Y>=0 && point.Y+apply.Bounds.Height<=dialog.ClientSize.Height,"Tag footer is clipped at compact height/font16");
                    dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"tags-compact-{language}-{theme}.png")); dialog.Close(); RedesignChecks.Frames();
                }
                Console.WriteLine("PASS "+language+": exact any/all/untagged filters intersect search/protocol; fast Enter cannot open hidden profile; clear resets filters; color drafts cancel/apply; suggestions/remove/normalized save; Graphite/Light font16 compact views");
            }
            finally { foreach(var owned in main.OwnedWindows.ToArray())owned.Close(); main.Close(); Until(()=>!main.IsVisible); }
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
    }
}
