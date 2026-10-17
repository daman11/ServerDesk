using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls.Shapes;
using Avalonia.Automation;

namespace ServerDesk.App;

internal sealed record DeskTheme(string Id,string Name,string Background,string Sidebar,string Surface,string Terminal,string Accent,string Foreground,string Secondary,string Border,string Hover,string Selected,bool IsLight=false);
internal static partial class DesignSystem
{
    internal static readonly DeskTheme[] Themes = [
        new("dark","ServerDesk Dark","#11161E","#181F29","#212B38","#10151C","#68AEF5","#EDF2F7","#B6C3D3","#303D4E","#273547","#263F59"),
        new("midnight","Midnight","#0D1117","#151B23","#1C2430","#090D12","#58A6FF","#E6EDF3","#ADB8C7","#30363D","#252F3E","#233B56"),
        new("graphite","Graphite","#202124","#25272B","#2D3035","#191B1E","#71B8AE","#E8EAED","#B6BABF","#3D4148","#35393F","#334B48"),
        new("nord","Nord","#2E3440","#323A48","#3B4252","#242A35","#88C0D0","#ECEFF4","#C0C9D7","#4C566A","#434C5E","#42566B"),
        new("light","ServerDesk Light","#F3F6FA","#FFFFFF","#F8FAFD","#10151C","#216DB2","#1D2A3A","#526176","#D8E0EB","#E8EEF6","#DCE9F8",true),
        new("ocean","Ocean","#101D29","#142635","#1A3042","#0D1822","#65CFDE","#EAF4FA","#B0C7D7","#355064","#233F53","#25495D"),
        new("forest","Forest","#141E1B","#1A2822","#22332B","#0E1713","#8ED6A4","#EDF5EF","#B9CDBF","#3D5546","#2B4034","#2D4939"),
        new("aubergine","Aubergine","#201A29","#292134","#332A40","#17121E","#C6A3EB","#F4EDF9","#CDBEDB","#51435F","#40334E","#4A385B"),
        new("dracula","Dracula","#282A36","#252632","#303341","#21222C","#BD93F9","#F8F8F2","#C6C7D8","#55596F","#3A3E50","#444459"),
        new("solar-paper","Solar Paper","#F7F1DF","#FFFAED","#F1EAD5","#10151C","#87600E","#3E3525","#655944","#CFC3A4","#EAE0C5","#E5D7B7",true),
        new("sandstone","Sandstone","#F5EFEB","#FFFAF7","#F0E7E0","#10151C","#945234","#3C302B","#68574F","#D8C6BC","#E9DBD2","#E5D2C6",true) ];
    internal static string CurrentId { get; private set; }="dark";
    internal static event Action? Changed;
    internal static void Initialize(Application app)
    {
        app.Resources["RadiusSmall"]=DesignTokens.Corner; app.Resources["ControlHeight"]=DesignTokens.ControlHeight;
        app.Resources["SpacingSmall"]=DesignTokens.Small; app.Resources["SpacingMedium"]=DesignTokens.Medium; app.Resources["SpacingLarge"]=DesignTokens.Large;
        app.Resources["FontBody"]=DesignTokens.Body; app.Resources["FontCaption"]=DesignTokens.Caption; app.Resources["FontMicro"]=DesignTokens.Micro; app.Resources["FontHeading"]=DesignTokens.Heading; app.Resources["FontTitle"]=DesignTokens.Title; app.Resources["FontSection"]=18d;
        app.Resources["BorderWidth"]=DesignTokens.Border; app.Resources["ButtonPadding"]=DesignTokens.ButtonPadding; app.Resources["InputPadding"]=DesignTokens.InputPadding;
        app.Resources["ChromePadding"]=new Thickness(DesignTokens.Medium,DesignTokens.Small); app.Resources["WelcomeActionPadding"]=new Thickness(DesignTokens.Section,10);
        app.Resources["TabHeight"]=DesignTokens.TabHeight; app.Resources["RowPadding"]=new Thickness(DesignTokens.IconGap,DesignTokens.Small);
        app.Resources["SessionTabMaxWidth"]=220d;
        app.Resources["FileRowHeight"]=26d;
        app.Resources["ConnectionRowPadding"]=new Thickness(4,1); app.Resources["MenuItemPadding"]=new Thickness(10,7);
        app.Resources["ShowTabIcons"]=true; app.Resources["ShowTabCloseButtons"]=true;
        app.Resources["InterfaceFont"]=new FontFamily(OperatingSystem.IsWindows()?"Segoe UI,Inter":"Inter,Noto Sans,DejaVu Sans");
        using(var icon=typeof(DesignSystem).Assembly.GetManifestResourceStream("ServerDesk.App.Assets.Brand.serverdesk.png")) { if(icon!=null)app.Resources["ApplicationIcon"]=new WindowIcon(icon); }
        SetResources(app,Themes.Single(t=>t.Id=="dark"),ThemeVariant.Dark); SetResources(app,Themes.Single(t=>t.Id=="light"),ThemeVariant.Light);
        app.ActualThemeVariantChanged+=(_,_)=> { if(CurrentId=="system")Changed?.Invoke(); };
        void Style<T>(AvaloniaProperty property, object value) where T:StyledElement => app.Styles.Add(new Style(s=>s.OfType<T>()) { Setters={new Setter(property,value)} });
        Style<Window>(Window.IconProperty,new DynamicResourceExtension("ApplicationIcon"));
        Style<Window>(Window.BackgroundProperty,new DynamicResourceExtension("BackgroundPrimary"));
        Style<Window>(Window.ForegroundProperty,new DynamicResourceExtension("ForegroundPrimary"));
        Style<Window>(Window.FontFamilyProperty,new DynamicResourceExtension("InterfaceFont")); Style<Window>(Window.FontSizeProperty,new DynamicResourceExtension("FontBody"));
        Style<Button>(Button.CornerRadiusProperty,new DynamicResourceExtension("RadiusSmall")); Style<Button>(Button.PaddingProperty,new DynamicResourceExtension("ButtonPadding")); Style<Button>(Button.MinHeightProperty,new DynamicResourceExtension("ControlHeight"));
        Style<Button>(Button.BackgroundProperty,new DynamicResourceExtension("SurfaceDefault")); Style<Button>(Button.BorderBrushProperty,new DynamicResourceExtension("BorderDefault"));
        Style<ContextMenu>(ContextMenu.BackgroundProperty,new DynamicResourceExtension("BackgroundSecondary")); Style<MenuItem>(MenuItem.PaddingProperty,new Thickness(8,5)); Style<ToolTip>(ToolTip.BackgroundProperty,new DynamicResourceExtension("BackgroundTertiary")); Style<ToolTip>(ToolTip.ForegroundProperty,new DynamicResourceExtension("ForegroundPrimary")); Style<ToolTip>(ToolTip.PaddingProperty,new Thickness(8,5));
        Style<TextBox>(TextBox.MinHeightProperty,new DynamicResourceExtension("ControlHeight")); Style<TextBox>(TextBox.PaddingProperty,new DynamicResourceExtension("InputPadding")); Style<TextBox>(TextBox.BackgroundProperty,new DynamicResourceExtension("BackgroundTertiary"));
        Style<ComboBox>(ComboBox.MinHeightProperty,new DynamicResourceExtension("ControlHeight")); Style<ComboBox>(ComboBox.PaddingProperty,new Thickness(8,4));
        Style<ListBox>(ListBox.BackgroundProperty,new DynamicResourceExtension("BackgroundSecondary")); Style<ListBox>(ListBox.BorderThicknessProperty,new Thickness(0));
        Style<TreeView>(TreeView.BackgroundProperty,new DynamicResourceExtension("BackgroundSecondary"));
        Style<ProgressBar>(ProgressBar.ForegroundProperty,new DynamicResourceExtension("AccentPrimary")); Style<ProgressBar>(ProgressBar.BackgroundProperty,new DynamicResourceExtension("SurfaceHover"));
        Style<TabItem>(TabItem.MinHeightProperty,new DynamicResourceExtension("TabHeight")); Style<TabItem>(TabItem.PaddingProperty,new Thickness(10,4));
        Style<ListBoxItem>(ListBoxItem.PaddingProperty,new DynamicResourceExtension("RowPadding")); Style<TreeViewItem>(TreeViewItem.PaddingProperty,new DynamicResourceExtension("ConnectionRowPadding"));
        foreach(var type in new[]{typeof(Button),typeof(ListBoxItem),typeof(TreeViewItem),typeof(TabItem)})
        {
            app.Styles.Add(new Style(s=>s.OfType(type).Class(":pointerover")) { Setters={new Setter(TemplatedControl.BackgroundProperty,new DynamicResourceExtension("SurfaceHover"))} });
            app.Styles.Add(new Style(s=>s.OfType(type).Class(":disabled")) { Setters={new Setter(Visual.OpacityProperty,.45d)} });
            app.Styles.Add(new Style(s=>s.OfType(type).Class(":focus-visible")) { Setters={new Setter(TemplatedControl.BorderBrushProperty,new DynamicResourceExtension("AccentPrimary")),new Setter(TemplatedControl.BorderThicknessProperty,new DynamicResourceExtension("BorderWidth"))} });
        }
        foreach(var type in new[]{typeof(ListBoxItem),typeof(TreeViewItem),typeof(TabItem)})
            app.Styles.Add(new Style(s=>s.OfType(type).Class(":selected")) { Setters={new Setter(TemplatedControl.BackgroundProperty,new DynamicResourceExtension("SurfaceSelected"))} });
        app.Styles.Add(new Style(s=>s.OfType<Button>().Class("icon")) { Setters={new Setter(Button.BackgroundProperty,Brushes.Transparent),new Setter(Button.BorderThicknessProperty,new Thickness(0)),new Setter(Button.PaddingProperty,new Thickness(5))} });
        app.Styles.Add(new Style(s=>s.OfType<Button>().Class("primary")) { Setters={new Setter(Button.BackgroundProperty,new DynamicResourceExtension("AccentPrimary")),new Setter(Button.ForegroundProperty,new DynamicResourceExtension("AccentForeground"))} });
        app.Styles.Add(new Style(s=>s.OfType<TextBox>().Class("error")) { Setters={new Setter(TextBox.BorderBrushProperty,new DynamicResourceExtension("Danger")),new Setter(TextBox.BorderThicknessProperty,new DynamicResourceExtension("BorderWidth"))} });
        AddControlStates(app);
        AddChromeStyles(app);
        AddComponentStyles(app);
        Apply("dark");
    }
    private static void SetResources(Application app,DeskTheme t,ThemeVariant variant)
    {
        var light=t.IsLight;
        var values=new Dictionary<string,string> { ["BackgroundPrimary"]=t.Background,["BackgroundSecondary"]=t.Sidebar,["BackgroundTertiary"]=t.Surface,["SurfaceDefault"]=t.Surface,["SurfaceHover"]=t.Hover,["SurfaceSelected"]=t.Selected,["ForegroundPrimary"]=t.Foreground,["ForegroundSecondary"]=t.Secondary,["ForegroundMuted"]=t.Secondary,["BorderDefault"]=t.Border,["BorderStrong"]=t.Secondary,["AccentPrimary"]=t.Accent,["AccentHover"]=t.Accent,["AccentForeground"]=light?"#FFFFFF":"#101418",["Success"]=light?"#19715C":"#69CFB6",["Warning"]=light?"#805C14":"#D7BA7D",["Danger"]=light?"#B7382B":"#F48771",["DeskBackground"]=t.Background,["DeskPanel"]=t.Sidebar };
        var resources=new ResourceDictionary(); foreach(var pair in values) resources[pair.Key]=new SolidColorBrush(Color.Parse(pair.Value));
        resources["TabActiveSurface"]=resources["SurfaceDefault"]; resources["WelcomeSurface"]=resources["BackgroundSecondary"];
        foreach(var key in new[]{"TextControlBackground","TextControlBackgroundFocused"})resources[key]=resources["BackgroundTertiary"];
        resources["TextControlBackgroundPointerOver"]=resources["SurfaceHover"]; resources["TextControlBackgroundDisabled"]=resources["BackgroundSecondary"];
        foreach(var key in new[]{"TextControlForeground","TextControlForegroundFocused","TextControlForegroundPointerOver"})resources[key]=resources["ForegroundPrimary"];
        foreach(var key in new[]{"TextControlPlaceholderForeground","TextControlPlaceholderForegroundFocused","TextControlPlaceholderForegroundPointerOver"})resources[key]=resources["ForegroundSecondary"];
        resources["TextControlBorderBrush"]=resources["BorderDefault"]; resources["TextControlBorderBrushFocused"]=resources["AccentPrimary"]; resources["TextControlBorderBrushPointerOver"]=resources["BorderStrong"];
        // Fluent templates share the same surface and text tokens, including popups and disabled controls.
        foreach(var key in new[]{"SystemControlBackgroundAltHighBrush","SystemControlBackgroundChromeMediumLowBrush","SystemControlBackgroundChromeLowBrush"})resources[key]=resources["BackgroundSecondary"];
        resources["SystemControlForegroundBaseHighBrush"]=resources["ForegroundPrimary"];
        resources["SystemControlForegroundBaseMediumBrush"]=resources["ForegroundSecondary"];
        resources["SystemAccentColor"]=Color.Parse(t.Accent);
        SetAccent(resources,t.Accent,light);
        app.Resources.ThemeDictionaries[variant]=resources;
    }
    internal static void Apply(string id)
    {
        var app=Application.Current!; CurrentId=id=="system"?id:Themes.Any(t=>t.Id==id)?id:"dark";
        if(CurrentId=="system") { SetResources(app,Themes.Single(t=>t.Id=="dark"),ThemeVariant.Dark); SetResources(app,Themes.Single(t=>t.Id=="light"),ThemeVariant.Light); app.RequestedThemeVariant=ThemeVariant.Default; }
        else { var theme=Themes.Single(t=>t.Id==CurrentId); var variant=theme.IsLight?ThemeVariant.Light:ThemeVariant.Dark; SetResources(app,theme,variant); app.RequestedThemeVariant=variant; }
        Changed?.Invoke();
    }
    internal static void Resource(StyledElement element,AvaloniaProperty property,string key)=>element.Bind(property,new DynamicResourceExtension(key));
    internal static IBrush Brush(string key)=> (IBrush)Application.Current!.FindResource(key)!;
    internal static TextBlock Label(string text,bool secondary=false,double size=DesignTokens.Body)
    { var label=new TextBlock { Text=text,FontSize=size,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center }; Resource(label,TextBlock.FontSizeProperty,size switch { <=DesignTokens.Micro=>"FontMicro",<=DesignTokens.Caption=>"FontCaption",<=14=>"FontBody",<=18=>"FontSection",<=22=>"FontHeading",_=>"FontTitle" }); Resource(label,TextBlock.ForegroundProperty,secondary?"ForegroundSecondary":"ForegroundPrimary"); return label; }
}
