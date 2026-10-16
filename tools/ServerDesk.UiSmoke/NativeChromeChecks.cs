using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Runtime.InteropServices;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class NativeChromeChecks
{
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowStyle(nint window,int index);
    internal static void Run(string root)
    {
        var paths=new AppPaths(Path.Combine(root,"native-data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths);
        var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient());
        using var done=new CancellationTokenSource(); Exception? failure=null; var step=0;
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };
        timer.Tick+=(_,_)=>
        {
            try
            {
                var header=main.GetVisualDescendants().OfType<Grid>().Single(g=>g.Name=="WorkspaceTitleBar");
                Console.WriteLine($"Native chrome step={step}; extended={main.IsExtendedIntoWindowDecorations}; decorations={main.WindowDecorationMargin}; state={main.WindowState}; client={main.ClientSize}; requested={main.Width}x{main.Height}; header={header.Bounds}; offscreen={main.OffScreenMargin}");
                if(main.WindowDecorations!=WindowDecorations.BorderOnly || !main.GetVisualDescendants().OfType<StackPanel>().Any(p=>p.Name=="WindowCaptionButtons" && p.Children.Count==3))throw new Exception("Single titlebar caption buttons are missing");
                if(OperatingSystem.IsWindows()) { var style=(long)GetWindowStyle(main.TryGetPlatformHandle()!.Handle,-16); Console.WriteLine($"Win32 style=0x{style:X}"); if((style&0x00C00000)==0x00C00000)throw new Exception("System caption can draw over custom titlebar"); }
                var actions=main.GetVisualDescendants().OfType<StackPanel>().Single(p=>p.Name=="WorkspaceTitleActions"); var captions=main.GetVisualDescendants().OfType<StackPanel>().Single(p=>p.Name=="WindowCaptionButtons");
                if(actions.Bounds.Right>captions.Bounds.Left || captions.Bounds.Right>header.Bounds.Width+1)throw new Exception("Titlebar actions overlap caption buttons");
                if(header.Bounds.Height>40 || header.Bounds.Y>2 || header.Bounds.Width<=400)throw new Exception("Titlebar is duplicated or lost its controls");
                using(var bitmap=new RenderTargetBitmap(new PixelSize((int)main.Bounds.Width,(int)main.Bounds.Height))) { bitmap.Render(main); bitmap.Save(Path.Combine(root,$"native-titlebar-{step}.png")); }
                void ToggleMaximize()
                {
                    main.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Maksymalizuj / przywróć").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                if(step==0)ToggleMaximize();
                else if(step==1) { if(main.WindowState!=WindowState.Maximized)throw new Exception("Maximize did not change window state"); main.PreviewPreferences(main.Preferences with { InterfaceFontSize=16,Density=InterfaceDensity.Comfortable }); ToggleMaximize(); main.Width=900; main.Height=600; }
                else if(step==2)
                {
                    // Windows can add its resize-frame allowance at the native
                    // minimum track height with a custom titlebar.
                    var minimumAllowance=Math.Max(1,(main.FrameSize?.Height??main.ClientSize.Height)-main.ClientSize.Height);
                    if(Math.Abs(main.ClientSize.Width-900)>1 || main.ClientSize.Height<600 || main.ClientSize.Height>600+minimumAllowance)throw new Exception($"Restoring normal state overwrote an explicit resize: {main.ClientSize}");
                    main.UpdateLanguageRestartNotice("en");
                }
                else
                {
                    var banner=main.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="LanguageRestartBanner");
                    var host=((Grid)main.Content!).Children.OfType<Grid>().Single(g=>Grid.GetRow(g)==1 && Grid.GetColumn(g)==3);
                    var position=banner.TranslatePoint(default,main)!.Value;
                    if(!banner.IsVisible || position.Y<header.Bounds.Bottom || position.Y+banner.Bounds.Height>host.Bounds.Y || banner.Bounds.Width>main.ClientSize.Width)throw new Exception("Native restart reminder overlaps captions or session host");
                    if(step==3)ToggleMaximize();
                    else { main.UpdateLanguageRestartNotice("pl"); timer.Stop(); main.Close(); done.Cancel(); }
                }
                step++;
            }
            catch(Exception ex) { failure=ex; timer.Stop(); main.Close(); done.Cancel(); }
        };
        main.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(done.Token);
        if(failure!=null)throw failure;
        Console.WriteLine("PASS native single titlebar: normal/maximized/900px layout; caption buttons/actions in separate columns; Windows native caption style removed; restart banner below captions and above workspace at 900px/maximized, font16");
    }
}
