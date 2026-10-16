using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Media;
using ServerDesk.App;
using ServerDesk.Infrastructure;

internal static class DesignPolishChecks
{
    internal static void Run(MainWindow main,ProfileRepository repository,string root)
    {
        var search=main.GetLogicalDescendants().OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)=="Szukaj połączeń");
        search.Text="web-prod"; RedesignChecks.Frames(); var clear=main.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Wyczyść wyszukiwanie");
        Click(main,clear); RedesignChecks.Frames(); if(search.Text!="" || clear.IsVisible)throw new Exception("Clear-search button failed");
        main.Width=900; main.Height=600; RedesignChecks.Frames();
        foreach(var button in main.GetLogicalDescendants().OfType<Button>().Where(b=>b.Classes.Contains("welcome-action")))
        { var point=button.TranslatePoint(default,main)!.Value; if(point.X<0 || point.Y<0 || point.X+button.Bounds.Width>main.Bounds.Width+.5 || point.Y+button.Bounds.Height>main.Bounds.Height+.5)throw new Exception("Welcome action clipped at 900x600"); }
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"design-welcome-900.png"));
        main.Width=1320; main.Height=840; RedesignChecks.Frames();
        var hits=0; var flat=new Button { Content=DeskIcons.Labelled("refresh","Odśwież"),Classes={"chrome"} }; flat.Click+=(_,_)=>hits++;
        var disabled=new Button { Content=DeskIcons.Labelled("folder","SFTP"),IsEnabled=false,Classes={"chrome","segment"} }; disabled.Click+=(_,_)=>hits++;
        var selected=new Button { Content=DeskIcons.Labelled("ssh","Terminal"),Classes={"chrome","segment","active"} };
        var live=new Button { Content=DeskIcons.Labelled("success","Połączono",16,"Success"),IsEnabled=false,Classes={"chrome","live"} };
        var primary=new Button { Content=DeskIcons.Labelled("connect","Połącz"),Classes={"chrome","primary"} };
        var sheet=new Window { Width=560,Height=190,Content=new StackPanel { Margin=new Thickness(20),Spacing=16,Children={DesignSystem.Label("Pasek narzędzi · stany interakcji",false,20),new StackPanel { Orientation=Orientation.Horizontal,Spacing=8,Children={flat,selected,disabled,live,primary} }} } }; sheet.Show(); RedesignChecks.Frames();
        Click(sheet,flat); if(hits!=1)throw new Exception("Flat button pointer activation failed");
        flat.Focus(); sheet.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Frames(); if(hits!=2)throw new Exception("Flat button keyboard activation failed");
        Click(sheet,disabled); if(hits!=2)throw new Exception("Disabled flat button activated");
        foreach(var theme in new[]{"dark","light"})
        {
            DesignSystem.Apply(theme); RedesignChecks.Frames();
            var label=live.GetVisualDescendants().OfType<TextBlock>().Single();
            var foreground=((ISolidColorBrush)label.Foreground!).Color; var background=Color.Parse(DesignSystem.Themes.Single(t=>t.Id==theme).Background);
            double Luminance(Color c) { double Channel(byte b) { var v=b/255d; return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4); } return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B); }
            var a=Luminance(foreground); var b=Luminance(background); if((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)<4.5 || live.Opacity<.99)throw new Exception("Connected status is dimmed or has insufficient contrast");
            sheet.CaptureRenderedFrame()!.Save(Path.Combine(root,"design-controls-"+theme+".png"));
            sheet.MouseMove(new Point(2,2),RawInputModifiers.None); primary.Focus(); sheet.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," "); RedesignChecks.Frames();
            if(!primary.IsPressed)throw new Exception("Primary button did not enter keyboard pressed state");
            a=Luminance(((ISolidColorBrush)primary.GetVisualDescendants().OfType<TextBlock>().Single().Foreground!).Color); b=Luminance(((ISolidColorBrush)primary.Background!).Color);
            if((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)<4.5)throw new Exception("Keyboard-pressed primary action lost text contrast");
            sheet.CaptureRenderedFrame()!.Save(Path.Combine(root,"design-primary-pressed-"+theme+".png")); sheet.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," "); RedesignChecks.Frames();
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,"design-welcome-"+theme+".png"));
        }
        sheet.Close(); DesignSystem.Apply("dark");
        Console.WriteLine("PASS real pointer/keyboard activation of flat buttons, disabled input protection, clear-search action, 900x600 welcome layout and connected/keyboard-pressed primary text contrast >=4.5:1 in dark/light themes");
    }
    private static void Click(Window window,Control control)
    {
        var point=control.TranslatePoint(new Point(control.Bounds.Width/2,control.Bounds.Height/2),window)!.Value;
        window.MouseMove(point,RawInputModifiers.None); window.MouseDown(point,MouseButton.Left,RawInputModifiers.None); window.MouseUp(point,MouseButton.Left,RawInputModifiers.None); RedesignChecks.Frames();
    }
}
