using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Automation;
using ServerDesk.App;
using ServerDesk.Infrastructure;
using Iciclecreek.Terminal;

internal static class RedesignChecks
{
    internal static void Frames(int count=12) { for(var i=0;i<count;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } }
    internal static void Wait(Task task) { var until=DateTime.UtcNow.AddSeconds(40); while(!task.IsCompleted && DateTime.UtcNow<until)Frames(1); if(!task.IsCompleted)throw new TimeoutException("UI fixture task did not complete within 40 seconds"); task.GetAwaiter().GetResult(); }
    internal static void Run(MainWindow window,ProfileRepository repository,string root)
    {
        double Luminance(string hex) { var c=Color.Parse(hex); double Channel(byte value) { var s=value/255d; return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4); } return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B); }
        foreach(var theme in DesignSystem.Themes)foreach(var text in new[]{theme.Foreground,theme.Secondary})foreach(var background in new[]{theme.Background,theme.Sidebar,theme.Surface,theme.Hover,theme.Selected})
        { var a=Luminance(text); var b=Luminance(background); var ratio=(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05); if(ratio<4.5)throw new Exception($"Contrast below AA: {theme.Id} {text} / {background}: {ratio}"); }
        Console.WriteLine($"PASS all {DesignSystem.Themes.Length} themes: primary/secondary text contrast >= 4.5:1 on all standard surfaces");
        var tree=window.GetVisualDescendants().OfType<TreeView>().Single();
        void Activity(string name) { var button=window.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)==name); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Frames(); }
        Activity("Ulubione"); if(tree.Items.Count!=2)throw new Exception("Favorites filter failed");
        Activity("Ostatnie"); if(tree.Items.Count!=1)throw new Exception("Recent filter failed"); Activity("Połączenia");
        var grid=(Grid)window.Content!; tree.Focus(); window.KeyPress(Key.B,RawInputModifiers.Control,PhysicalKey.B,"b"); Frames(); if(grid.ColumnDefinitions[1].Width.Value!=0)throw new Exception("Ctrl+B failed to hide sidebar");
        window.KeyPress(Key.B,RawInputModifiers.Control|RawInputModifiers.Shift,PhysicalKey.B,"B"); Frames(); if(grid.ColumnDefinitions[1].Width.Value<=0)throw new Exception("Ctrl+Shift+B failed to show sidebar");
        window.Width=900; window.Height=600; Frames(); window.CaptureRenderedFrame()!.Save(Path.Combine(root,"main-900.png"));
        window.SetRenderScaling(1.5); Frames(); window.CaptureRenderedFrame()!.Save(Path.Combine(root,"main-dpi150.png"));
        window.SetRenderScaling(2); Frames(); window.CaptureRenderedFrame()!.Save(Path.Combine(root,"main-dpi200.png"));
        window.SetRenderScaling(1); window.Width=1320; window.Height=840; Frames();
        var settings=Dialogs.Settings(window,repository,()=>Task.CompletedTask); Frames(); var dialog=window.OwnedWindows.Single(); dialog.GetVisualDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Motyw aplikacji").SelectedIndex=2;
        dialog.GetVisualDescendants().OfType<TabControl>().Single(t=>t.Name=="SettingsCategories").SelectedIndex=1; Frames(); dialog.GetVisualDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Paleta terminala").SelectedItem="Nord"; Frames();
        var save=dialog.GetVisualDescendants().OfType<Button>().Single(b=>(b.Tag as string??b.Content as string)=="Zapisz"); save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(settings);
        if(repository.SettingAsync("theme").GetAwaiter().GetResult()!="graphite" || repository.SettingAsync("terminalPalette").GetAwaiter().GetResult()!="Nord")throw new Exception("Appearance settings did not persist");
        using var terminal=new TerminalControl { Process="" }; var terminalWindow=new Window { Width=600,Height=300,Content=terminal }; terminalWindow.Show(); Frames();
        foreach(var name in TerminalAppearance.Names) { TerminalAppearance.Apply(terminal,name,14); terminal.Terminal.Write("\u001b[31mANSI red\u001b[0m · żółć\r\n"); Frames(3); terminalWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"palette-"+Array.IndexOf(TerminalAppearance.Names,name)+".png")); }
        var previous=terminal.Background; DesignSystem.Apply("light"); Frames(); if(!Equals(terminal.Background,previous))throw new Exception("UI theme altered independent terminal colors");
        terminalWindow.Close(); DesignSystem.Apply("system"); if(Application.Current!.RequestedThemeVariant!=Avalonia.Styling.ThemeVariant.Default)throw new Exception("Follow System did not delegate to platform"); DesignSystem.Apply("dark"); repository.SetSettingAsync("terminalPalette",TerminalAppearance.Names[0]).GetAwaiter().GetResult();
        Console.WriteLine("PASS section filters, sidebar shortcuts, 900x600, 150/200% render scaling, appearance persistence and independent terminal palettes");
    }
}
