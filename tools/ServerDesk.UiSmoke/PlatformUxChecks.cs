using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class PlatformUxChecks
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ClosedDialog(Window owner)
    {
        var dialog=new Window { Width=500,Height=300,Content=new TextBox { Text="Disposable focus/placement fixture" } };
        DialogScreenPlacement.Track(dialog,owner); dialog.Show(owner); RedesignChecks.Frames(1); dialog.Close(); RedesignChecks.Frames(1);
        return new WeakReference(dialog);
    }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static Button Action(Window dialog,string tag)=>dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag);
    private static void Press(Window window,Key key,RawInputModifiers modifiers=RawInputModifiers.None)
    { var physical=key switch { Key.Down=>PhysicalKey.ArrowDown,Key.Home=>PhysicalKey.Home,Key.End=>PhysicalKey.End,Key.F=>PhysicalKey.F,Key.Escape=>PhysicalKey.Escape,_=>PhysicalKey.Tab }; window.KeyPress(key,modifiers,physical,null); if(window.IsVisible)window.KeyRelease(key,modifiers,physical,null); RedesignChecks.Frames(2); }
    private static void InWindow(Control control,Window window)
    {
        var point=control.TranslatePoint(default,window)??throw new Exception("Detached acceptance control");
        if(!control.IsEffectivelyVisible || control.Bounds.Width<=0 || control.Bounds.Height<=0 || point.X<-.5 || point.Y<-.5 || point.X+control.Bounds.Width>window.ClientSize.Width+.5 || point.Y+control.Bounds.Height>window.ClientSize.Height+.5)
            throw new Exception("Unreachable control: "+control.Name+" / "+AutomationProperties.GetName(control)+" at "+point+" in "+window.ClientSize);
    }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var settings=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
            RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { Language=language,InterfaceFontSize=16 } }));
            var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            var task=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single();
            ScreenArea area=new(0,0,1920,1040,1); var placement=DialogScreenPlacement.Track(dialog,main,()=>area);
            var categories=Named<TabControl>(dialog,"SettingsCategories"); var search=Named<TextBox>(dialog,"SettingsSearch");
            var save=Action(dialog,"Zapisz"); var cancel=Action(dialog,"Anuluj");
            foreach(var scale in new[]{1d,1.5,2,1})
            {
                area=new(-1366,40,1366,728,scale); dialog.SetRenderScaling(scale); dialog.Position=new PixelPoint(9000,9000);
                placement.Apply(area,true); RedesignChecks.Frames(12);
                if(dialog.ClientSize.Width>area.Width/scale || dialog.ClientSize.Height>area.Height/scale || dialog.Position.X<area.X || dialog.Position.Y<area.Y)throw new Exception("Dialog did not fit simulated DPI/monitor transition");
                foreach(var theme in DesignSystem.Themes)
                {
                    DesignSystem.Apply(theme.Id); RedesignChecks.Frames(2);
                    InWindow(save,dialog); InWindow(cancel,dialog); InWindow(search,dialog);
                    foreach(var tab in categories.Items.OfType<TabItem>())
                    {
                        categories.SelectedItem=tab; RedesignChecks.Frames(2);
                        if(string.IsNullOrWhiteSpace(AutomationProperties.GetName(tab)))throw new Exception("Settings category lacks an accessible name");
                        var viewport=Named<ScrollViewer>(dialog,"SettingsNavigationScroll");
                        if(tab.TranslatePoint(default,viewport) is not {} point || point.Y<-.5 || point.Y+tab.Bounds.Height>viewport.Bounds.Height+.5)throw new Exception("Selected category cannot be revealed in compact navigation");
                    }
                }
                if(scale==2)dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"settings-{language}-font16-dpi200.png"));
            }
            area=new(0,0,1920,1040,1); dialog.SetRenderScaling(1); placement.Apply(area,true); RedesignChecks.Frames();
            if(dialog.MinWidth!=680 || dialog.MinHeight!=500 || dialog.MaxWidth<960 || dialog.MaxHeight<720)throw new Exception("Small monitor constraints remained stuck after returning to a larger monitor");
            dialog.Width=960; dialog.Height=720; categories.SelectedIndex=0; RedesignChecks.Frames();
            categories.Items.OfType<TabItem>().First().Focus(); Press(dialog,Key.Down);
            if(categories.SelectedIndex!=1)throw new Exception("Settings category Down navigation failed");
            Press(dialog,Key.End); if(categories.SelectedIndex!=categories.ItemCount-1)throw new Exception("Settings category End navigation failed");
            Press(dialog,Key.Home); if(categories.SelectedIndex!=0)throw new Exception("Settings category Home navigation failed");
            Press(dialog,Key.F,RawInputModifiers.Control); if(!search.IsKeyboardFocusWithin)throw new Exception("Ctrl+F did not focus settings search");
            search.Text="CPU"; RedesignChecks.Frames(); Press(dialog,Key.Escape);
            if(search.Text!="" || task.IsCompleted)throw new Exception("Escape should first clear the search and keep the dialog open");
            var saveReached=false; var cancelReached=false;
            for(var i=0;i<70 && (!saveReached || !cancelReached);i++)
            {
                Press(dialog,Key.Tab);
                if(dialog.FocusManager?.GetFocusedElement() is Control focused && TopLevel.GetTopLevel(focused)!=dialog)throw new Exception("Tab focus escaped the modal dialog");
                saveReached|=save.IsKeyboardFocusWithin; cancelReached|=cancel.IsKeyboardFocusWithin;
            }
            if(!saveReached || !cancelReached)throw new Exception("Settings actions cannot be reached with Tab");
            Press(dialog,Key.Escape); RedesignChecks.Wait(task);
            var confirm=Dialogs.Confirm(main,"DPI acceptance",string.Join("\n",Enumerable.Repeat("Long disposable confirmation text for scroll and keyboard checks.",80)));
            RedesignChecks.Frames(); var message=main.OwnedWindows.Single();
            area=new(0,0,1366,728,2); message.SetRenderScaling(2); var messagePlacement=DialogScreenPlacement.Track(message,main,()=>area);
            messagePlacement.Apply(area,true); RedesignChecks.Frames(12);
            InWindow(Action(message,"Potwierdź"),message); InWindow(Action(message,"Anuluj"),message);
            var messageScroll=Named<ScrollViewer>(message,"DialogScroll");
            if(messageScroll.Viewport.Height<=0 || messageScroll.Extent.Height<=messageScroll.Viewport.Height)throw new Exception("Long confirmation content is not scrollable above fixed actions");
            Press(message,Key.Escape); RedesignChecks.Wait(confirm); if(confirm.Result)throw new Exception("Escape accepted the confirmation");
            var closed=Enumerable.Range(0,20).Select(_=>ClosedDialog(main)).ToArray(); main.Focus(); RedesignChecks.Frames(5);
            for(var pass=0;pass<3;pass++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); RedesignChecks.Frames(2); }
            var retained=closed.Count(reference=>reference.IsAlive); if(retained>1)throw new Exception("Closed dialog screen subscriptions retain windows: "+retained);
            Console.WriteLine("INFO "+language+": closed placement dialogs retained after GC = "+retained+"/20");
            main.Close(); RedesignChecks.Frames(20);
            Console.WriteLine("PASS "+language+$": {DesignSystem.Themes.Length} themes, font16, simulated 100/150/200/100% DPI and monitor removal; footer/search/category visibility, restored resize limits, accessible category names, arrows/Home/End, Ctrl+F, Escape and modal Tab access");
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
    }
}
