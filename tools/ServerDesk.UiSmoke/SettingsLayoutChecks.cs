using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Iciclecreek.Terminal;

internal static class SettingsLayoutChecks
{
    private static T Find<T>(Window dialog,string name) where T:Control=>dialog.GetVisualDescendants().OfType<T>().Single(c=>AutomationProperties.GetName(c)==name);
    private static void Click(Window dialog,string label)=>dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        var original=repo.ApplicationSettingsAsync().GetAwaiter().GetResult(); var originalRuntime=main.CurrentPreferences;
        var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single();
        Find<ComboBox>(dialog,"Motyw aplikacji").SelectedIndex=4; Find<NumericUpDown>(dialog,"Rozmiar tekstu interfejsu").Value=16;
        Find<ComboBox>(dialog,"Odstępy i wysokość kontrolek").SelectedIndex=2; Find<TextBox>(dialog,"Kolor akcentu").Text="#E5B567"; RedesignChecks.Frames();
        if(main.Preferences.InterfaceFontSize!=16 || DesignSystem.CurrentId!="light")throw new Exception("Live interface preview did not apply");
        var categories=dialog.GetVisualDescendants().OfType<TabControl>().Single(t=>t.Name=="SettingsCategories");
        foreach(var tab in categories.Items.OfType<TabItem>())foreach(var label in ((Control)tab.Header!).GetVisualDescendants().OfType<TextBlock>())if(label.Bounds.Width<label.TextLayout.WidthIncludingTrailingWhitespace)throw new Exception("Settings category label clipped after changing UI font: "+label.Text);
        dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"settings-appearance.png"));
        categories.SelectedIndex=1; RedesignChecks.Frames(); Find<ComboBox>(dialog,"Paleta terminala").SelectedItem="Nord"; Find<ComboBox>(dialog,"Krój czcionki terminala").SelectedItem="Consolas"; Find<NumericUpDown>(dialog,"Rozmiar czcionki terminala").Value=17;
        dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"settings-terminal.png"));
        categories.SelectedIndex=2; RedesignChecks.Frames(); Find<NumericUpDown>(dialog,"Szerokość panelu połączeń").Value=320; Find<CheckBox>(dialog,"Pokaż oznaczenia protokołów przy serwerach").IsChecked=false;
        dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"settings-workspace.png"));
        categories.SelectedIndex=3; RedesignChecks.Frames(); Find<CheckBox>(dialog,"Nazwa systemu operacyjnego").IsChecked=false;
        var rectangle=Find<CheckBox>(dialog,"CPU").GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="NormalRectangle");
        if(rectangle.Background is not ISolidColorBrush accentBrush || accentBrush.Color!=Color.Parse("#E5B567"))throw new Exception("Custom accent did not reach checked controls");
        Find<CheckBox>(dialog,"Pokaż dolny pasek statusu").IsChecked=false; RedesignChecks.Frames();
        if(((Grid)main.Content!).RowDefinitions[2].ActualHeight>1)throw new Exception("Status bar cannot be hidden");
        Find<CheckBox>(dialog,"Pokaż dolny pasek statusu").IsChecked=true; dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"settings-monitoring.png"));
        Click(dialog,"Anuluj"); RedesignChecks.Wait(edit); RedesignChecks.Frames();
        if(main.CurrentPreferences!=originalRuntime || (repo.ApplicationSettingsAsync().GetAwaiter().GetResult())!=original)throw new Exception("Cancel persisted preferences or failed to roll back live preview");
        edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single();
        Find<TextBox>(dialog,"Kolor akcentu").Text="bad color"; Click(dialog,"Zapisz"); RedesignChecks.Frames();
        if(edit.IsCompleted || repo.ApplicationSettingsAsync().GetAwaiter().GetResult()!=original)throw new Exception("Invalid accent was saved or closed editor");
        Find<TextBox>(dialog,"Kolor akcentu").Text="#69CFB6"; Find<NumericUpDown>(dialog,"Rozmiar tekstu interfejsu").Value=15; Click(dialog,"Zapisz"); RedesignChecks.Wait(edit); RedesignChecks.Frames();
        if(repo.PreferencesAsync().GetAwaiter().GetResult().AccentColor!="#69CFB6")throw new Exception("Settings save did not persist");
        DesignSystem.Apply("system");
        foreach(var variant in new[]{Avalonia.Styling.ThemeVariant.Dark,Avalonia.Styling.ThemeVariant.Light})
        { var resources=(ResourceDictionary)Application.Current!.Resources.ThemeDictionaries[variant]; if(resources["AccentPrimary"] is not ISolidColorBrush brush || brush.Color!=Color.Parse("#69CFB6"))throw new Exception("Follow System lost custom accent for "+variant); }
        edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single();
        if(Find<TextBox>(dialog,"Kolor akcentu").Text!="#69CFB6")throw new Exception("Reopening settings lost saved accent");
        Click(dialog,"Domyślne ustawienia"); Click(dialog,"Zapisz"); RedesignChecks.Wait(edit);
        if(repo.PreferencesAsync().GetAwaiter().GetResult()!=new ApplicationPreferences())throw new Exception("Default settings do not reset preferences");
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(original)); main.PreviewPreferences(originalRuntime); DesignSystem.Apply(original.Theme);
        VerifyMetricBounds(main,repo,root);
        Console.WriteLine("PASS settings live preview/cancel/invalid input/save/reopen/defaults; hidden status; long Debian OS/large values in separate clipped cells, 900/1320/1920px and fonts 11/13/16 with 200% rendering");
    }
    private static void VerifyMetricBounds(MainWindow main,ProfileRepository repo,string root)
    {
        using var pane=new SessionPane(main,new() { Name="Fixture",Host="fixture.invalid",Username="user" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
        var bar=new ServerMetricsBar(); var window=new Window { Width=1920,Height=70,Content=bar }; window.Show();
        var snapshot=new ServerSnapshot("",new(100,20),98,new(128L<<30,16L<<30,0,0),"","","") { SystemDetails=new("fixture","Debian GNU/Linux 13 (trixie)","","","",TimeSpan.FromDays(1234),""),Traffic=new(987654321,123456789),RemoteTime="12:31",DiskDetails=[new("/dev/sda","ext4","/",100,55,45,55)] };
        var saved=DesignSystem.Preferences;
        foreach(var font in new[]{11,13,16})foreach(var width in new[]{900,1320,1920})
        {
            DesignSystem.ApplyPreferences(saved with { InterfaceFontSize=font }); window.Width=width; bar.Update(pane,width,new()); bar.Render(snapshot,DateTimeOffset.UtcNow,"",true,width); RedesignChecks.Frames();
            var cells=bar.GetVisualDescendants().OfType<Border>().Where(b=>b.Name?.StartsWith("MetricCell_")==true && b.IsEffectivelyVisible).ToArray();
            var systemCell=bar.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="MetricCell_os");
            if(ToolTip.GetTip(systemCell)?.ToString()?.Contains(snapshot.SystemDetails.Distribution)!=true)throw new Exception("Truncated OS name is absent from tooltip");
            foreach(var cell in cells)foreach(var label in cell.GetVisualDescendants().OfType<TextBlock>())
            {
                var point=label.TranslatePoint(default,cell)!.Value;
                if(!cell.ClipToBounds || point.X<0 || point.X+label.Bounds.Width>cell.Bounds.Width+1)throw new Exception("Metric text escapes its own cell: "+cell.Name);
            }
            var visible=cells.Select(c=>new Rect(c.TranslatePoint(default,window)!.Value,c.Bounds.Size)).OrderBy(r=>r.X).ToArray();
            if(visible.Any(r=>r.X<0 || r.Right>width+1) || visible.Zip(visible.Skip(1)).Any(pair=>pair.First.Right>pair.Second.Left+1))throw new Exception("Metric cells overlap");
            if(font==16 && width==1920)window.CaptureRenderedFrame()!.Save(Path.Combine(root,"metrics-debian-font16.png"));
        }
        window.SetRenderScaling(2); RedesignChecks.Frames(); window.CaptureRenderedFrame()!.Save(Path.Combine(root,"metrics-debian-dpi200.png"));
        var options=new ApplicationPreferences { MetricSystem=false,MetricClock=false }; bar.Update(pane,1920,options); bar.Render(snapshot,DateTimeOffset.UtcNow,"",true,1920); RedesignChecks.Frames();
        if(bar.GetVisualDescendants().OfType<Border>().Any(b=>b.Name is "MetricCell_os" or "MetricCell_clock" && b.IsEffectivelyVisible))throw new Exception("Metric field options are ignored");
        window.Close(); DesignSystem.ApplyPreferences(saved);
    }
    internal static void Network(MainWindow main,ProfileRepository repo,string root)
    {
        var pane=main.OpenSessions.First(p=>p.IsConnected); main.FocusSession(pane);
        var terminal=pane.Children.OfType<TerminalControl>().Single(); var id=terminal.SessionId; var emulator=terminal.Terminal;
        var original=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
        var value=original with { TerminalFont=17,TerminalPalette="Nord",Preferences=original.Preferences with { TerminalFontFamily="Consolas",ShowSessionStatus=false,ShowMetrics=false,ShowTransferCount=false } };
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(value)); main.PreviewPreferences(value.Preferences); RedesignChecks.Wait(pane.ApplyAppearanceAsync()); RedesignChecks.Frames();
        if(!pane.IsConnected || id!=terminal.SessionId || !ReferenceEquals(emulator,terminal.Terminal) || terminal.FontSize!=17 || terminal.FontFamily.Name!="Consolas")throw new Exception($"Applying terminal preferences: connected={pane.IsConnected}; same session={id==terminal.SessionId}; same emulator={ReferenceEquals(emulator,terminal.Terminal)}; size={terminal.FontSize}; family={terminal.FontFamily.Name}");
        if(main.GetVisualDescendants().OfType<ServerMetricsBar>().Single().IsVisible)throw new Exception("Metrics cannot be hidden in live session");
        RedesignChecks.Wait(terminal.SendInputAsync("printf 'SETTINGS_LIVE_OK\\n'\r",CancellationToken.None)); RedesignChecks.Frames();
        RedesignChecks.Wait(pane.ShowAsync(2)); RedesignChecks.Frames(260); if(pane.LatestSnapshot==null)throw new Exception("Disabling bottom metrics also disabled explicit Monitoring panel");
        RedesignChecks.Wait(pane.ShowAsync(0)); RedesignChecks.Frames(80); var last=pane.LastSampleAt; RedesignChecks.Frames(280); if(pane.LastSampleAt!=last)throw new Exception("Monitoring kept polling with metrics disabled and Monitoring hidden");
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(original)); main.PreviewPreferences(original.Preferences); RedesignChecks.Wait(pane.ApplyAppearanceAsync());
        var deadline=DateTime.UtcNow.AddSeconds(12); while(pane.LastSampleAt==last && DateTime.UtcNow<deadline)RedesignChecks.Frames(1);
        if(pane.LastSampleAt==last)throw new Exception("Restoring metrics did not resume polling");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"workspace-personalization-live.png"));
        Console.WriteLine("PASS live preference application retains SSH session/emulator/input; terminal font/palette, hidden metrics/session status; explicit Monitoring works and hidden background polling pauses/resumes");
    }
}
