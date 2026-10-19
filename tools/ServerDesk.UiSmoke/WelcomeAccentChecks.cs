using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Automation;
using Avalonia.Interactivity;
using Shape=Avalonia.Controls.Shapes.Shape;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class WelcomeAccentChecks
{
    private static Color ColorOf(object? value)=>value switch { ISolidColorBrush brush=>brush.Color,Color color=>color,_=>throw new Exception("Expected a solid theme color") };
    private static Color Rgb(Color color)=>Color.FromRgb(color.R,color.G,color.B);
    internal static void Run(string root)
    {
        var original=DesignSystem.Preferences; var theme=DesignSystem.CurrentId;
        try { Controls(root,original); Welcome(root,original); Settings(root); }
        finally { AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(original); DesignSystem.Apply(theme); }
        Console.WriteLine("PASS welcome icon/text geometry PL/EN, one/two columns, font13/16; all 11 theme accents and custom/reset colors, native checkbox/toggle/radio/slider/text selection, primary normal/hover/pressed and contrast; settings preview/theme switch/cancel/save/reopen/system palettes");
    }
    private static void Welcome(string root,ApplicationPreferences preferences)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"welcome-"+language)); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=preferences with { Language=language } }));
            var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames();
            try
            {
                foreach(var preset in new[]{"graphite","light"})foreach(var font in new[]{13,16})foreach(var scenario in new[]{(Width:1320,Sidebar:272),(Width:900,Sidebar:272),(Width:900,Sidebar:420)})
                {
                    var width=scenario.Width; var narrow=scenario.Sidebar==420;
                    main.PreviewPreferences(preferences with { Language=language,InterfaceFontSize=font,SidebarWidth=scenario.Sidebar,AccentColor="#C85CA7" }); DesignSystem.Apply(preset); main.Width=width; main.Height=820; RedesignChecks.Frames();
                    var actions=main.GetVisualDescendants().OfType<Grid>().Single(view=>view.Name=="WelcomeActions");
                    var positions=new List<(Point Icon,Point Text)>();
                    foreach(var button in actions.Children.OfType<Button>())
                    {
                        var row=(Grid)button.Content!; var icon=row.Children.OfType<DeskIcon>().Single(); var heading=((StackPanel)row.Children[1]).Children.OfType<TextBlock>().First();
                        var ip=icon.TranslatePoint(default,actions)!.Value; var tp=heading.TranslatePoint(default,actions)!.Value;
                        if(Math.Abs(ip.Y-tp.Y-2)>1 || row.Bounds.Width<button.Bounds.Width-button.Padding.Left-button.Padding.Right-3)throw new Exception("Welcome icon is centered against variable content instead of its heading: "+language+" "+width);
                        if(ColorOf(icon.Foreground)!=Color.Parse("#C85CA7"))throw new Exception("Welcome icon ignored custom accent: "+ColorOf(icon.Foreground)+" preference="+DesignSystem.Preferences.AccentColor); positions.Add((ip,tp));
                    }
                    var columns=actions.ColumnDefinitions.Count;
                    if(columns!=(narrow?1:2))throw new Exception("Expected welcome column mode was not exercised");
                    for(var i=columns;i<positions.Count;i++)if(Math.Abs(positions[i].Icon.X-positions[i-columns].Icon.X)>1 || Math.Abs(positions[i].Text.X-positions[i-columns].Text.X)>1)throw new Exception("Welcome columns have uneven icon/text indents");
                    if(columns==2 && (Math.Abs(positions[0].Icon.Y-positions[1].Icon.Y)>1 || Math.Abs(positions[2].Icon.Y-positions[3].Icon.Y)>1))throw new Exception("Welcome icons have different row baselines");
                    if(font==16 && (width==1320 || narrow))main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"welcome-{language}-{preset}-{width}-{scenario.Sidebar}.png"));
                }
            }
            finally { main.Close(); RedesignChecks.Frames(); }
        }
    }
    private static void Controls(string root,ApplicationPreferences preferences)
    {
        var primary=new Button { Content="Save",Classes={"primary"} }; var chrome=new Button { Content=DeskIcons.Labelled("connect","Connect"),Classes={"chrome","primary"} };
        var check=new CheckBox { Content="Checkbox",IsChecked=true }; var toggle=new ToggleSwitch { Content="Toggle switch",IsChecked=true }; var radio=new RadioButton { Content="Radio button",IsChecked=true };
        var slider=new Slider { Minimum=0,Maximum=100,Value=60,Width=320 }; var input=new TextBox { Text="Selected text",SelectionStart=0,SelectionEnd=8 };
        var progress=new ProgressBar { Maximum=100,Value=60,Height=5 };
        var active=new Button { Content="Selected module",Classes={"chrome","segment","active"} };
        var window=new Window { Width=660,Height=610,Content=new StackPanel { Margin=new Thickness(24),Spacing=12,Children={primary,chrome,check,toggle,radio,slider,input,progress,active} } }; window.Show();
        try
        {
            foreach(var preset in DesignSystem.Themes)foreach(var custom in new[]{"","#C85CA7","#1C923B","#FFFFFF","#000000"})
            {
                DesignSystem.ApplyPreferences(preferences with { AccentColor=custom }); DesignSystem.Apply(preset.Id); RedesignChecks.Frames(3);
                var expected=Color.Parse(custom.Length>0?custom:preset.Accent); var variant=preset.IsLight?ThemeVariant.Light:ThemeVariant.Dark;
                var fluent=Application.Current!.Styles.OfType<FluentTheme>().Single(); window.TryFindResource("AccentPrimary",window.ActualThemeVariant,out var accentResource);
                if(fluent.Palettes[variant].Accent!=expected || ColorOf(accentResource)!=expected)throw new Exception("Native/application palettes disagree: "+preset.Id+custom);
                var shades=new HashSet<Color> { Rgb(expected) };
                foreach(var suffix in new[]{"Dark1","Dark2","Dark3","Light1","Light2","Light3"})if(fluent.Palettes[variant].TryGetResource("SystemAccentColor"+suffix,variant,out var value))shades.Add(Rgb(ColorOf(value)));
                bool UsesAccent(Control control)=>control.GetVisualDescendants().SelectMany(visual=>visual switch { Border border=>new[]{border.Background,border.BorderBrush},Shape shape=>new[]{shape.Fill,shape.Stroke},_=>Array.Empty<IBrush?>() }).OfType<ISolidColorBrush>().Any(brush=>brush.Color.A>0 && shades.Contains(Rgb(brush.Color)));
                foreach(var control in new Control[]{check,toggle,radio,slider})if(!UsesAccent(control))throw new Exception("Native control retained another accent: "+control.GetType().Name+" "+preset.Id+custom);
                if(input.SelectionBrush is null)throw new Exception("TextBox.SelectionBrush is null: "+preset.Id+custom);
                if(progress.Foreground is null)throw new Exception("ProgressBar.Foreground is null: "+preset.Id+custom);
                if(!shades.Contains(Rgb(ColorOf(input.SelectionBrush))) || ColorOf(progress.Foreground)!=expected)throw new Exception("Selection or progress ignored accent: "+preset.Id+custom);
                Contrast(ColorOf(input.SelectionBrush),ColorOf(input.SelectionForegroundBrush));
                input.Focus(); RedesignChecks.Frames(1);
                if(ColorOf(input.GetVisualDescendants().OfType<Border>().Single(border=>border.Name=="PART_BorderElement").BorderBrush)!=expected)throw new Exception("Focused input retained preset accent: "+preset.Id+custom);
                foreach(var control in new Control[]{toggle,radio})
                    if(!control.GetVisualDescendants().OfType<Shape>().Any(shape=>shape.Fill is ISolidColorBrush brush && ContrastRatio(expected,brush.Color)>=4.5))throw new Exception("Checked glyph blends into accent: "+control.GetType().Name+preset.Id+custom);
                if(custom.Length==0 && ColorOf(active.Background)!=Color.Parse(preset.Selected))throw new Exception("Clearing custom accent did not restore preset selection");
                foreach(var button in new[]{primary,chrome})
                {
                    window.MouseMove(new Point(2,2),RawInputModifiers.None); RedesignChecks.Frames(1); CheckPrimary(button,expected);
                    window.MouseMove(button.TranslatePoint(new Point(8,8),window)!.Value,RawInputModifiers.None); RedesignChecks.Frames(1); CheckPrimary(button,expected);
                    button.Focus(); window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," "); RedesignChecks.Frames(1); CheckPrimary(button,expected); window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");
                }
                if(preset.Id is "graphite" or "light" && custom=="#C85CA7")window.CaptureRenderedFrame()!.Save(Path.Combine(root,"accent-controls-"+preset.Id+".png"));
            }
        }
        finally { window.Close(); RedesignChecks.Frames(); }
    }
    private static void Settings(string root)
    {
        AppText.SetLanguage("pl"); var paths=new AppPaths(Path.Combine(root,"settings")); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults));
        var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames();
        static void Click(Window dialog,string label)=>dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        static TextBox Accent(Window dialog)=>dialog.GetVisualDescendants().OfType<TextBox>().Single(box=>AutomationProperties.GetName(box)=="Kolor akcentu");
        static void Check(Window window,string hex)
        {
            var expected=Color.Parse(hex); var fluent=Application.Current!.Styles.OfType<FluentTheme>().Single();
            if(fluent.Palettes[window.ActualThemeVariant].Accent!=expected)throw new Exception("Settings preview/rollback did not update Fluent accent");
        }
        try
        {
            var editing=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single();
            Accent(dialog).Text="#C85CA7"; RedesignChecks.Frames(); Check(main,"#C85CA7");
            dialog.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="ApplicationTheme").SelectedIndex=Array.FindIndex(DesignSystem.Themes,t=>t.Id=="light"); RedesignChecks.Frames(); Check(main,"#C85CA7");
            Click(dialog,"Anuluj"); RedesignChecks.Wait(editing); Check(main,DesignSystem.Themes.Single(t=>t.Id==ApplicationSettings.Defaults.Theme).Accent);
            if(repo.ApplicationSettingsAsync().GetAwaiter().GetResult()!=ApplicationSettings.Defaults)throw new Exception("Cancel persisted accent draft");
            editing=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single();
            Accent(dialog).Text="#1C923B"; RedesignChecks.Frames(); Click(dialog,"Zapisz"); RedesignChecks.Wait(editing);
            if(repo.ApplicationSettingsAsync().GetAwaiter().GetResult().Preferences.AccentColor!="#1C923B")throw new Exception("Accent save failed");
            editing=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single();
            if(Accent(dialog).Text!="#1C923B")throw new Exception("Reopening lost accent");
            Accent(dialog).Text=""; RedesignChecks.Frames(); Check(main,DesignSystem.Themes.Single(t=>t.Id==DesignSystem.CurrentId).Accent); Click(dialog,"Anuluj"); RedesignChecks.Wait(editing); Check(main,"#1C923B");
            DesignSystem.Apply("system"); var fluent=Application.Current!.Styles.OfType<FluentTheme>().Single();
            foreach(var variant in new[]{ThemeVariant.Light,ThemeVariant.Dark})if(fluent.Palettes[variant].Accent!=Color.Parse("#1C923B"))throw new Exception("System variants disagree on custom accent");
        }
        finally { foreach(var child in main.OwnedWindows.ToArray())child.Close(); main.Close(); RedesignChecks.Frames(); }
    }
    private static void CheckPrimary(Button button,Color accent)
    {
        var presenter=button.GetVisualDescendants().OfType<ContentPresenter>().First(view=>view.Name=="PART_ContentPresenter");
        var background=presenter.Background is ISolidColorBrush { Color.A:>0 }?ColorOf(presenter.Background):ColorOf(button.Background);
        if(background!=accent)throw new Exception("Primary action lost accent in native interaction state: "+button.Classes);
        var foreground=ColorOf(button.GetVisualDescendants().OfType<TextBlock>().Single().Foreground);
        Contrast(background,foreground);
    }
    private static double ContrastRatio(Color background,Color foreground)
    {
        static double L(Color color) { double C(byte channel) { var v=channel/255d; return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4); } return .2126*C(color.R)+.7152*C(color.G)+.0722*C(color.B); }
        var a=L(background); var b=L(foreground); return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
    }
    private static void Contrast(Color background,Color foreground) { if(ContrastRatio(background,foreground)<4.5)throw new Exception("Accent foreground has insufficient contrast"); }
}
