using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static partial class CommandPanelChecks
{
    internal static void BackupRecovery(string root)
    {
        try
        {
            foreach(var language in new[]{"pl","en"})
            {
                AppText.SetLanguage(language);
                var source=new ProfileRepository(new AppPaths(Path.Combine(root,"source-"+language)));
                Wait(source.InitializeAsync()); var vault=new Vault(); var store=new CommandStore(source,vault);
                Wait(source.SaveApplicationSettingsAsync(Result(source.ApplicationSettingsAsync()) with { Preferences=new() { Language=language } }));
                Wait(store.SaveLibraryAsync(new() { Commands=[new() { Id="user.restore-fixture",Name="Disposable restore fixture",Origin=CommandOrigin.User,Category="Fixture",Template="echo fixture",Shell=CommandShell.Bash }] }));
                var backup=Result(source.CreateDatabaseBackupAsync());
                // A normal edit retires the old protected chunks. The old SQLite
                // copy is still valid but its library can no longer be read.
                Wait(store.SaveLibraryAsync(new()));
                var paths=new AppPaths(Path.Combine(root,"restored-"+language)); File.Copy(backup.Path,Path.Combine(paths.Data,"serverdesk.db"));
                var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
                var pointer=Result(repo.SettingAsync("commands.vault.v1"));
                var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show();
                try
                {
                    RedesignChecks.Frames(12); Wait(main.OpenCommandsPanelAsync()); RedesignChecks.Frames(5);
                    var panel=Named<CommandPanel>(main,"CommandsPanel");
                    var notice=panel.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text?.Contains("SQLite")==true);
                    if(!notice.IsEffectivelyVisible || language=="en" && !notice.Text!.Contains("did not provide your custom command content"))throw new Exception("Unavailable protected-content guidance is hidden or untranslated");
                    var list=Named<ListBox>(panel,"CommandList");
                    if(list.ItemCount<40 || !list.IsEffectivelyVisible || list.Bounds.Height<70)throw new Exception("Missing custom content hides the built-in library");
                    Select(panel,"general.hostname");
                    if(panel.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Duplikuj").IsEnabled)throw new Exception("Missing custom content can be overwritten by duplication");
                    main.CaptureRenderedFrame()!.Save(Path.Combine(root,"missing-commands-"+language+".png"));
                    if(pointer!=Result(repo.SettingAsync("commands.vault.v1")))throw new Exception("Opening the restored library replaced its protected-data reference");
                }
                finally { Close(main); }
            }
        }
        finally { AppText.SetLanguage("pl"); }
        Console.WriteLine("PASS backup recovery PL/EN: missing protected content explained, built-ins available, library writes blocked, reference retained; disposable data and no remote sessions");
    }
}
