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

internal static class ApplicationLogsChecks
{
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); DesignSystem.Apply(language=="pl"?"graphite":"light");
            var paths=new AppPaths(Path.Combine(root,language+" dane ąę "+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync()); var original=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
            Directory.CreateDirectory(paths.Logs); var older=Path.Combine(paths.Logs,"serverdesk-20261017.log"); File.WriteAllText(older,"Older fixture żółć\n");
            var current=Path.Combine(paths.Logs,"serverdesk-20261018.log"); var content=string.Concat(Enumerable.Repeat("Fixture informational event\n",12000))+"[ERR] DirectoryNotFoundException fixture\n  at Fixture.DeleteAsync()\n"; File.WriteAllText(current,content);
            File.SetLastWriteTimeUtc(older,new DateTime(2026,10,17,0,0,0,DateTimeKind.Utc)); File.SetLastWriteTimeUtc(current,new DateTime(2026,10,18,0,0,0,DateTimeKind.Utc));
            var owner=new Window { Width=1100,Height=800 }; owner.Show(); var editing=Dialogs.Settings(owner,repo,()=>Task.CompletedTask); RedesignChecks.Frames(8); var settings=owner.OwnedWindows.Single();
            settings.GetVisualDescendants().OfType<NumericUpDown>().Single(c=>AutomationProperties.GetName(c)==AppText.T("Rozmiar tekstu interfejsu")).Value=16;
            Named<TextBox>(settings,"SettingsSearch").Text="logs"; RedesignChecks.Frames(4);
            if(Named<TabControl>(settings,"SettingsCategories").SelectedIndex!=5 || !Named<Button>(settings,"OpenApplicationLogs").IsEffectivelyVisible)throw new Exception("Log viewer cannot be found in settings search");
            settings.CaptureRenderedFrame()!.Save(Path.Combine(root,$"log-settings-{language}.png"));
            Click(settings,"OpenApplicationLogs"); RedesignChecks.Frames(6); var dialog=settings.OwnedWindows.Single(); var view=Named<ApplicationLogsView>(dialog,"ApplicationLogsView"); Wait(view.Shutdown); RedesignChecks.Frames(4);
            var files=Named<ComboBox>(view,"ApplicationLogFiles"); var preview=Named<TextBox>(view,"ApplicationLogPreview"); var export=Named<Button>(view,"ApplicationLogsExport");
            if(files.ItemCount!=2 || files.SelectedItem is not ApplicationLogEntry selected || selected.Name!=Path.GetFileName(current) || !preview.IsReadOnly || preview.Text?.EndsWith("  at Fixture.DeleteAsync()\n")!=true || preview.Text.Length>=content.Length || !export.IsEnabled)throw new Exception("Latest bounded log preview did not load");
            var query=Named<TextBox>(view,"ApplicationLogSearch"); query.Text="directorynotfoundexception"; Click(view,"ApplicationLogsFindNext"); RedesignChecks.Frames(3);
            if(!string.Equals(preview.SelectedText,query.Text,StringComparison.OrdinalIgnoreCase))throw new Exception("Log preview search did not select the error");
            var destination=Path.Combine(paths.Data,"saved-copy.log"); view.ChooseExportDestination=_=>Task.FromResult<string?>(destination); Click(view,"ApplicationLogsExport"); Wait(view.Shutdown);
            if(File.ReadAllText(destination)!=content || File.ReadAllText(current)!=content)throw new Exception("Full export was truncated or changed the active log");
            view.ChooseExportDestination=_=>Task.FromResult<string?>(null); Click(view,"ApplicationLogsExport"); Wait(view.Shutdown); if(File.ReadAllText(destination)!=content)throw new Exception("Cancelled picker changed the saved copy");
            var opened=""; view.LaunchFolder=directory=> { opened=directory.FullName; return Task.FromResult(false); }; Click(view,"ApplicationLogsOpenFolder"); Wait(view.Shutdown); if(opened!=Path.GetFullPath(paths.Logs))throw new Exception("Log folder action points to another data root");
            view.ChooseExportDestination=_=>Task.FromResult<string?>(Path.Combine(paths.Logs,"copy.log")); Click(view,"ApplicationLogsExport"); Wait(view.Shutdown);
            if(File.Exists(Path.Combine(paths.Logs,"copy.log")) || !Named<TextBlock>(view,"ApplicationLogsStatus").Text!.Contains(AppText.T("Nie udało się zapisać kopii logu. Wybierz plik poza folderem logów i sprawdź uprawnienia oraz wolne miejsce. Istniejący plik docelowy nie został zastąpiony.")))throw new Exception("Unsafe destination was not explained");
            foreach(var size in new[]{new Size(920,680),new Size(600,480)})
            {
                dialog.Width=size.Width; dialog.Height=size.Height; RedesignChecks.Frames(5);
                foreach(var name in new[]{"ApplicationLogsExport","ApplicationLogsOpenFolder","ApplicationLogsClose"})
                {
                    var button=Named<Button>(view,name); var point=button.TranslatePoint(default,dialog)!.Value;
                    if(point.X<0 || point.Y<0 || point.X+button.Bounds.Width>dialog.ClientSize.Width+.5 || point.Y+button.Bounds.Height>dialog.ClientSize.Height+.5)throw new Exception("Log dialog action is outside the compact window");
                    if(button.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.TextLayout.WidthIncludingTrailingWhitespace>t.Bounds.Width+1))throw new Exception("Log dialog action label is clipped");
                }
                if(preview.Bounds.Height<30 || preview.TranslatePoint(default,dialog)!.Value.Y+preview.Bounds.Height>dialog.ClientSize.Height)throw new Exception("Compact dialog lost the log viewport");
                var presenter=preview.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().Single(); var caret=presenter.TextLayout.HitTestTextPosition(preview.CaretIndex); var hit=presenter.TranslatePoint(caret.Position,preview)!.Value;
                if(hit.Y<0 || hit.Y+caret.Height>preview.Bounds.Height+.5)throw new Exception("Found log error is not visible after resizing");
                dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"application-logs-{language}-{size.Width}.png"));
            }
            files.SelectedItem=files.Items.OfType<ApplicationLogEntry>().Single(e=>e.Name==Path.GetFileName(older)); Wait(view.Shutdown); if(preview.Text!="Older fixture żółć\n")throw new Exception("Selecting an older log kept stale preview text");
            File.Delete(older); File.Delete(current); Directory.Delete(paths.Logs); Wait(view.RefreshAsync());
            if(files.ItemCount!=0 || export.IsEnabled || preview.Text!="" || Directory.Exists(paths.Logs))throw new Exception("Removed log directory was not handled as empty");
            dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"application-logs-empty-{language}.png"));
            _=view.RefreshAsync(); Click(view,"ApplicationLogsClose"); RedesignChecks.Frames(4); Wait(view.Shutdown);
            settings.Close(); Wait(editing); owner.Close(); RedesignChecks.Frames(3);
            if(repo.ApplicationSettingsAsync().GetAwaiter().GetResult()!=original)throw new Exception("Using logs persisted the unsaved settings draft");
        }
        AppText.SetLanguage("pl"); Console.WriteLine("PASS application logs: settings search/open, PL/EN Graphite/Light font16, newest/older bounded read-only preview, find error, full export/cancel/safe destination, folder target/failure, 920x680/600x480 footer bounds, missing directory and close cancellation; settings draft unchanged, isolated log fixtures only");
    }
}
