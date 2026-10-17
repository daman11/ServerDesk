using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
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

internal static class AppPolishChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Click(Window window,string tag)=>window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            var paths=new AppPaths(Path.Combine(root,"isolated-"+language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            RedesignChecks.Wait(repo.SaveAsync(new() { Name="Saved IPv4",Host="192.0.2.10",Username="fixture" }));
            var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 });
            foreach(var theme in new[]{"graphite","light"})
            {
                DesignSystem.Apply(theme);
                var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single();
                dialog.Width=680; dialog.Height=500; RedesignChecks.Frames();
                var categories=dialog.GetVisualDescendants().OfType<TabControl>().Single(t=>t.Name=="SettingsCategories");
                if(categories.Items.OfType<TabItem>().Any(t=>t.Width!=48 || ToolTip.GetTip(t)==null))throw new Exception("Compact settings navigation did not adapt");
                for(var i=0;i<categories.ItemCount;i++)
                {
                    categories.SelectedIndex=i; RedesignChecks.Frames();
                    var save=dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz");
                    var point=save.TranslatePoint(default,dialog)!.Value;
                    if(point.X<0 || point.Y<0 || point.X+save.Bounds.Width>dialog.ClientSize.Width+1 || point.Y+save.Bounds.Height>dialog.ClientSize.Height+1)throw new Exception("Settings footer is outside the small window");
                    dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"settings-{language}-{theme}-{i}.png"));
                }
                dialog.Width=960; RedesignChecks.Frames();
                if(categories.Items.OfType<TabItem>().Any(t=>t.Width!=184))throw new Exception("Settings labels did not return at full width");
                Click(dialog,"Anuluj"); RedesignChecks.Wait(edit);
            }
            var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var applying=false;
            var saving=Dialogs.Settings(main,repo,()=> { applying=true; return gate.Task; }); RedesignChecks.Frames(); var settings=main.OwnedWindows.Single();
            Click(settings,"Zapisz"); var until=DateTime.UtcNow.AddSeconds(10); while(!applying && DateTime.UtcNow<until)RedesignChecks.Frames(1);
            var navigation=settings.GetVisualDescendants().OfType<TabControl>().Single(t=>t.Name=="SettingsCategories");
            if(!applying || navigation.IsEnabled || settings.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz").IsEffectivelyEnabled)throw new Exception("Settings can be edited while being saved");
            settings.Close(); RedesignChecks.Frames(); if(saving.IsCompleted)throw new Exception("Settings closed during application");
            gate.SetResult(); RedesignChecks.Wait(saving);

            var commands=Dialogs.Commands(main,new[]{"Network Tools · Ping","Network Tools · DNS Lookup","Settings"}); RedesignChecks.Frames(); var palette=main.OwnedWindows.Single();
            var search=palette.GetVisualDescendants().OfType<TextBox>().Single(t=>t.Name=="CommandSearch"); var choices=palette.GetVisualDescendants().OfType<ListBox>().Single(t=>t.Name=="CommandResults");
            search.Text="no-such-command"; RedesignChecks.Frames(); palette.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r");
            if(commands.IsCompleted || choices.ItemCount!=0 || !palette.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="CommandsEmpty").IsEffectivelyVisible)throw new Exception("Empty command search closed the palette or hid its feedback");
            search.Text="network tools"; RedesignChecks.Frames(); palette.KeyPress(Key.Down,RawInputModifiers.None,PhysicalKey.ArrowDown,""); palette.KeyPress(Key.Up,RawInputModifiers.None,PhysicalKey.ArrowUp,"");
            if(choices.SelectedIndex!=0 || !search.IsFocused)throw new Exception("Command arrow keys lost selection or search focus");
            palette.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Wait(commands); if(commands.Result!="Network Tools · Ping")throw new Exception("Command selection did not run");

            var message=Dialogs.Message(main,"Long diagnostic",string.Join("\n",Enumerable.Repeat("Long diagnostic value to select and copy: 192.0.2.1",80))); RedesignChecks.Frames(); var messageWindow=main.OwnedWindows.Single(); messageWindow.SizeToContent=SizeToContent.Manual; messageWindow.Height=400; RedesignChecks.Frames();
            var actions=messageWindow.GetVisualDescendants().OfType<StackPanel>().Single(s=>s.Name=="DialogActions"); var scroll=messageWindow.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="DialogScroll");
            if(scroll.TranslatePoint(default,messageWindow)!.Value.Y+scroll.Bounds.Height>actions.TranslatePoint(default,messageWindow)!.Value.Y+1 || actions.TranslatePoint(default,messageWindow)!.Value.Y+actions.Bounds.Height>messageWindow.ClientSize.Height+1)throw new Exception("Long dialog hides or overlaps its actions");
            if(!messageWindow.GetVisualDescendants().OfType<SelectableTextBlock>().Any())throw new Exception("Diagnostic message cannot be selected");
            messageWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,$"dialog-long-{language}.png")); messageWindow.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,""); RedesignChecks.Wait(message);

            var wake=main.OpenNetworkTool(NetworkToolKind.Wake); RedesignChecks.Frames(); Field<TextBox>(wake,"target").Text="AA:BB:CC:DD:EE:FF";
            var savedHost=wake.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="NetworkSavedHost"); savedHost.SelectedItem=((IEnumerable)savedHost.ItemsSource!).Cast<object>().Single();
            if(wake.ReadRequest().Target!="AA:BB:CC:DD:EE:FF" || wake.ReadRequest().Broadcast!="192.0.2.10")throw new Exception("Saved host overwrote WoL MAC instead of destination IPv4");
            var subnet=main.OpenNetworkTool(NetworkToolKind.Subnet); subnet.UseHost("2001:db8::1"); if(subnet.ReadRequest().Target!="2001:db8::1/128")throw new Exception("IPv6 host did not fill a valid subnet");
            var http=main.OpenNetworkTool(NetworkToolKind.Http); http.UseHost("2001:db8::1"); if(http.ReadRequest().Target!="https://[2001:db8::1]")throw new Exception("IPv6 HTTP target is invalid");
            Field<TextBox>(http,"target").Text="https://user:secret@example.com/private?token=fixture"; if(http.Caption.Contains("secret") || http.Caption.Contains("private") || http.Caption.Contains("token"))throw new Exception("HTTP caption exposes private URL data");
            var ping=main.OpenNetworkTool(NetworkToolKind.Ping); ping.ApplyRequest(new() { Tool=NetworkToolKind.Ping,Target="127.0.0.1",Continuous=true,IntervalMs=100 }); var work=ping.StartAsync(); RedesignChecks.Frames(12);
            if(Field<TextBox>(ping,"target").IsEffectivelyEnabled)throw new Exception("An active tool allows mixing the target with an older result");
            main.Close(); RedesignChecks.Frames(); var confirm=main.OwnedWindows.Single(); Click(confirm,"Anuluj"); RedesignChecks.Frames(); if(!main.IsVisible || !ping.IsRunning)throw new Exception("Canceling close did not preserve running tool");
            Field<Button>(ping,"stop").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(work); RedesignChecks.Frames();
            if(!Field<TextBox>(ping,"target").IsEffectivelyEnabled)throw new Exception("Stopped tool did not unlock its inputs");
            var results=Field<ObservableCollection<NetworkRow>>(ping,"rows"); if(results.Count==0)throw new Exception("Actual ICMP returned no samples");
            var list=Field<ListBox>(ping,"resultList"); list.SelectedIndex=0; var details=ping.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="NetworkDetails"); details.IsExpanded=true; RedesignChecks.Frames();
            if(!details.IsVisible || !Field<TextBox>(ping,"resultDetails").Text!.Contains("127.0.0.1"))throw new Exception("Selected result details are incomplete");
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"ping-details-{language}.png"));
            foreach(var pane in Field<List<NetworkToolPane>>(main,"networkPanes").ToArray()) { main.RemovePane(pane); RedesignChecks.Wait(pane.Shutdown); }
            main.Close(); RedesignChecks.Frames();
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
        Console.WriteLine("PASS app polish: PL/EN compact/full settings in Graphite/Light; async save lock and close guard; command empty states, arrows and Enter; long scrollable/selectable dialogs with fixed actions and Escape; saved-host WoL/subnet/IPv6 HTTP; safe captions; active-tool input lock, close cancellation and complete selected result details; real ICMP");
    }
}
