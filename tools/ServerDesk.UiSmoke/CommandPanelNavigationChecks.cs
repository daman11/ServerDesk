using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static partial class CommandPanelChecks
{
    internal static void Navigation(string root)
    {
        Directory.CreateDirectory(root);
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync()); var vault=new Vault(); var store=new CommandStore(repo,vault);
            var first=new CommandDefinition { Id="user.navigation.a",Origin=CommandOrigin.User,Name="A navigation fixture",Category="Navigation",Shell=CommandShell.Bash,Template="printf '%s' {{text}} {{code}}",Parameters=[new() { Key="text",Default="initial" },new() { Key="code",Kind=CommandParameterKind.Code }] };
            var second=first with { Id="user.navigation.b",Name="B navigation fixture",Template="printf {{text}}",Parameters=[new() { Key="text",Default="second" }] };
            var third=second with { Id="user.navigation.c",Name="C navigation fixture" };
            Wait(store.SaveLibraryAsync(new() { Commands=[first,second,third],Categories=["Navigation"] }));
            Wait(repo.SaveApplicationSettingsAsync(Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language } }));
            Wait(store.SaveStateAsync(new() { Preferences=new() { Automatic=false,System="linux",Shell=CommandShell.Bash } }));
            var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(12); Wait(main.OpenCommandsPanelAsync()); RedesignChecks.Frames(5);
            var panel=Named<CommandPanel>(main,"CommandsPanel"); var list=Named<ListBox>(panel,"CommandList");
            Named<ComboBox>(panel,"CommandSection").SelectedIndex=1; Named<ComboBox>(panel,"CommandSort").SelectedIndex=1;
            var query=Named<TextBox>(panel,"CommandSearch"); query.Text="navigation"; panel.RefreshList(); Select(panel,first.Id);
            var previous=Named<Button>(panel,"CommandPrevious"); var next=Named<Button>(panel,"CommandNext"); var position=Named<TextBlock>(panel,"CommandDetailPosition");
            if(previous.IsEnabled || !next.IsEnabled || position.Text!="1 / 3" || AutomationProperties.GetName(position)!=AppText.F($"Komenda {1} z {3}"))throw new Exception("Initial command navigation position/state is wrong");
            var text=Named<TextBox>(panel,"CommandParameter_text"); text.Text="draft żółć";
            var code=Named<TextBox>(panel,"CommandParameter_code"); code.Text="harmless_argument"; RedesignChecks.Frames(2);
            var approval=Named<CheckBox>(panel,"CommandCodeApproval"); approval.IsChecked=true; RedesignChecks.Frames(2); var preview=Named<TextBox>(panel,"CommandPreview").Text;
            Wait(main.Clipboard!.SetTextAsync("command-navigation-clipboard-sentinel"));
            Click(panel,"Ulubione"); Wait(panel.Shutdown); RedesignChecks.Frames(3);
            if(!ReferenceEquals(text,Named<TextBox>(panel,"CommandParameter_text")) || text.Text!="draft żółć" || code.Text!="harmless_argument" || approval.IsChecked!=true || preview!=Named<TextBox>(panel,"CommandPreview").Text || !Named<Border>(panel,"CommandDetailCard").IsVisible)throw new Exception("Favorite toggle lost the selected command draft or Code approval");
            if(!Result(store.ReadStateAsync()).Favorites.Contains(first.Id))throw new Exception("Favorite toggle did not persist");
            panel.FocusSearch(); var profileOptions=Named<Expander>(panel,"CommandContextOptions"); profileOptions.IsExpanded=true; RedesignChecks.Frames(3);
            Named<TextBox>(panel,"CommandVersion").Text="changed-context"; RedesignChecks.Frames(5); profileOptions.IsExpanded=false; Select(panel,first.Id);
            text=Named<TextBox>(panel,"CommandParameter_text");
            if(text.Text!="initial" || Named<TextBox>(panel,"CommandParameter_code").Text!="" || Named<CheckBox>(panel,"CommandCodeApproval").IsChecked==true)throw new Exception("Context change retained a parameter draft or Code approval");
            previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(!ReferenceEquals(text,Named<TextBox>(panel,"CommandParameter_text")))throw new Exception("Out-of-range command navigation reset the form");
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(2);
            if((list.SelectedItem as CommandMatch)?.Command.Id!=second.Id || position.Text!="2 / 3" || Named<TextBox>(panel,"CommandParameter_text").Text!="second")throw new Exception("Next command did not follow the filtered/sorted list");
            main.KeyPress(Key.Right,RawInputModifiers.Alt,PhysicalKey.ArrowRight,""); RedesignChecks.Frames(2);
            if((list.SelectedItem as CommandMatch)?.Command.Id!=third.Id || next.IsEnabled || position.Text!="3 / 3")throw new Exception("Alt+Right did not reach the last command");
            main.KeyPress(Key.Left,RawInputModifiers.Alt,PhysicalKey.ArrowLeft,""); RedesignChecks.Frames(2);
            if((list.SelectedItem as CommandMatch)?.Command.Id!=second.Id || query.Text!="navigation")throw new Exception("Alt+Left lost navigation or search context");
            main.KeyPress(Key.Right,RawInputModifiers.Control|RawInputModifiers.Alt,PhysicalKey.ArrowRight,""); RedesignChecks.Frames(2);
            if((list.SelectedItem as CommandMatch)?.Command.Id!=second.Id)throw new Exception("Ctrl+Alt was mistaken for command navigation");
            panel.FocusSearch(); Named<ComboBox>(panel,"CommandSort").SelectedIndex=2; Select(panel,third.Id);
            if(position.Text!="1 / 3")throw new Exception("Descending result position is wrong");
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(2);
            if((list.SelectedItem as CommandMatch)?.Command.Id!=second.Id)throw new Exception("Navigation ignored descending result order");
            if(Result(main.Clipboard!.TryGetTextAsync())!="command-navigation-clipboard-sentinel" || Result(store.ReadStateAsync()).Recent.Length!=0 || main.OwnedWindows.Count!=0)throw new Exception("Command browsing copied/executed or recorded a command");
            foreach(var font in new[]{13,16})
            {
                main.Width=920; main.Height=620; main.PreviewPreferences(main.CurrentPreferences with { InterfaceFontSize=font }); RedesignChecks.Frames(6);
                var bar=Named<Grid>(panel,"CommandDetailNavigation");
                foreach(var control in new Control[]{previous,next,position,Named<Button>(panel,"CommandBackToList")})
                {
                    var point=control.TranslatePoint(default,bar)!.Value;
                    if(point.X<-.5 || point.Y<-.5 || point.X+control.Bounds.Width>bar.Bounds.Width+.5 || point.Y+control.Bounds.Height>bar.Bounds.Height+.5)throw new Exception("Command navigation clips in a compact panel");
                }
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"command-navigation-{language}-font{font}.png"));
            }
            panel.FocusSearch(); Named<ComboBox>(panel,"CommandSection").SelectedIndex=2; panel.RefreshList(); Select(panel,first.Id);
            Click(panel,"Ulubione"); Wait(panel.Shutdown); RedesignChecks.Frames(3);
            if(list.ItemCount!=0 || Named<Border>(panel,"CommandDetailCard").IsVisible || previous.IsEnabled || next.IsEnabled)throw new Exception("Removing the last favorite retained stale command details/navigation");
            Close(main);
        }
        AppText.SetLanguage("pl"); Console.WriteLine("PASS command details Previous/Next and Alt+arrows follow filtered order, preserve search, boundaries and favorite drafts/Code approval, clear removed favorites, fit compact PL/EN font13/16 and never dispatch/copy/record while browsing");
    }
}
