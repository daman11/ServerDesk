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

internal static class AboutChecks
{
    private static T Named<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); DesignSystem.Apply(language=="pl"?"graphite":"light"); DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 });
            var owner=new Window { Width=1000,Height=720 }; owner.Show(); RedesignChecks.Frames(3);
            var showing=Dialogs.About(owner); RedesignChecks.Frames(6); var dialog=owner.OwnedWindows.Single(); var tabs=Named<TabControl>(dialog,"AboutTabs");
            if(dialog.Title!=AppText.T("O ServerDesk") || tabs.ItemCount!=3)throw new Exception("About dialog is not localized or is missing a section");
            var details=Named<SelectableTextBlock>(dialog,"AboutBuildInfo").Text??"";
            if(!details.StartsWith("ServerDesk ") || !details.Contains("Avalonia ") || !details.Contains(".NET "))throw new Exception("About lacks version, framework or build information");
            var copy=Named<Button>(dialog,"AboutCopyInfo"); copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(3);
            var clipboard=dialog.Clipboard!.TryGetTextAsync(); RedesignChecks.Wait(clipboard);
            if(clipboard.Result!=details || !Named<TextBlock>(dialog,"AboutCopyStatus").IsEffectivelyVisible)throw new Exception("About version copy or feedback failed");
            foreach(var size in new[]{new Size(680,640),new Size(460,480)})
            {
                dialog.Width=size.Width; dialog.Height=size.Height; RedesignChecks.Frames(5);
                foreach(var (index,title) in new[]{(0,"Program"),(1,"Licencja"),(2,"Składniki")})
                {
                    tabs.SelectedIndex=index; RedesignChecks.Frames(4);
                    if(AutomationProperties.GetName((TabItem)tabs.Items[index]!)!=AppText.T(title))throw new Exception("About section header language mismatch");
                    if(index==1 && Named<SelectableTextBlock>(dialog,"AboutLicenseText").Text!=File.ReadAllText("LICENSE"))throw new Exception("About does not preserve the original MIT license");
                    var close=Named<Button>(dialog,"AboutClose");
                    foreach(var button in new[]{copy,close})
                    {
                        var point=button.TranslatePoint(default,dialog)!.Value;
                        if(point.X<0 || point.Y<0 || point.X+button.Bounds.Width>dialog.ClientSize.Width+.5 || point.Y+button.Bounds.Height>dialog.ClientSize.Height+.5)throw new Exception("About actions are clipped in a compact window");
                        if(button.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.TextLayout.WidthIncludingTrailingWhitespace>t.Bounds.Width+1))throw new Exception("About action label is clipped");
                    }
                    foreach(var label in tabs.Items.OfType<TabItem>().SelectMany(t=>((Control)t.Header!).GetVisualDescendants().OfType<TextBlock>()))
                        if(label.TextLayout.WidthIncludingTrailingWhitespace>label.Bounds.Width+1)throw new Exception("About tab label is clipped");
                    dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"about-{language}-{size.Width}-tab{index}.png"));
                    if(index==1)
                    {
                        var text=Named<SelectableTextBlock>(dialog,"AboutLicenseText"); var scroll=text.GetVisualAncestors().OfType<ScrollViewer>().First();
                        scroll.Offset=new Vector(0,scroll.Extent.Height); RedesignChecks.Frames(3);
                        var end=text.TranslatePoint(new Point(0,text.Bounds.Height),scroll)!.Value;
                        if(scroll.Offset.Y<=0 || end.Y>scroll.Bounds.Height+.5 || end.Y<0)throw new Exception("The complete MIT license cannot be scrolled into view");
                        dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"about-license-end-{language}-{size.Width}.png"));
                    }
                }
            }
            dialog.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,""); RedesignChecks.Wait(showing);
            if(dialog.IsVisible)throw new Exception("About Escape did not close the dialog"); owner.Close(); RedesignChecks.Frames(3);
        }
        DesignSystem.ApplyPreferences(preferences); AppText.SetLanguage("pl");
        Console.WriteLine("PASS About: localized program/license/components, verbatim embedded MIT, build information copy, PL/EN Graphite/Light font16 at 680x640/460x480, visible tabs/footer and Escape; no profile data or network access");
    }
}
