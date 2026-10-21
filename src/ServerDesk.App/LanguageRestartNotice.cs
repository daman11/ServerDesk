using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private readonly Border languageRestartBanner=new() { Name="LanguageRestartBanner",IsVisible=false };
    private readonly TextBlock languageRestartDetail=DesignSystem.Label("",true,12);
    private readonly TextBlock languageRestartHeading=DesignSystem.Label("");
    private Button languageRestartButton=null!,languageRestartDismiss=null!;
    private string savedRestartLanguage="pl";
    private string? restartLanguage;
    internal Action StartReplacement { get; set; }=()=> { using var child=ApplicationRestart.Start(typeof(Program).Assembly.Location); };

    internal void RequestRestart(string language)
    {
        if(closing || closePending)return;
        restartLanguage=language;
        Close();
    }

    private Control BuildWorkspaceHeader(Grid titlebar)
    {
        titlebar.Height=DesignTokens.HeaderHeight;
        languageRestartHeading.FontWeight=FontWeight.SemiBold; languageRestartHeading.TextWrapping=TextWrapping.Wrap;
        languageRestartDetail.TextWrapping=TextWrapping.Wrap;
        languageRestartButton=Dialogs.Button("Uruchom ponownie",()=>RequestRestart(savedRestartLanguage));
        languageRestartButton.Name="LanguageRestartButton"; languageRestartButton.Classes.Add("primary"); languageRestartButton.HorizontalAlignment=HorizontalAlignment.Left;
        var text=new StackPanel { Spacing=6,Children={languageRestartHeading,languageRestartDetail,languageRestartButton} };
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions("24,*,Auto"),ColumnSpacing=12 };
        row.Children.Add(DeskIcons.Create("info",20,"AccentPrimary")); Grid.SetColumn(text,1); row.Children.Add(text);
        languageRestartDismiss=DeskIcons.Button("close","Ukryj przypomnienie o restarcie",()=>languageRestartBanner.IsVisible=false,28);
        languageRestartDismiss.Name="LanguageRestartDismiss"; languageRestartDismiss.VerticalAlignment=VerticalAlignment.Top; Grid.SetColumn(languageRestartDismiss,2); row.Children.Add(languageRestartDismiss);
        languageRestartBanner.Child=row; languageRestartBanner.Padding=new Thickness(12,10);
        languageRestartBanner.Margin=new Thickness(12,0,12,10); languageRestartBanner.CornerRadius=new CornerRadius(6);
        languageRestartBanner.BorderThickness=new Thickness(1);
        DesignSystem.Resource(languageRestartBanner,Border.BackgroundProperty,"SurfaceSelected");
        DesignSystem.Resource(languageRestartBanner,Border.BorderBrushProperty,"BorderDefault");
        WindowDecorationProperties.SetElementRole(languageRestartBanner,WindowDecorationsElementRole.User);
        AutomationProperties.SetName(languageRestartBanner,L.T("Zmiana języka wymaga restartu"));
        var stack=new StackPanel { Children={titlebar,languageRestartBanner,BuildUpdateBanner()} };
        var header=new Border { Name="WorkspaceHeader",Child=stack,BorderThickness=new Thickness(0,0,0,1) };
        DesignSystem.Resource(header,Border.BorderBrushProperty,"BorderDefault");
        return header;
    }

    // Call only for committed settings: appearance previews must not announce a saved language.
    internal void UpdateLanguageRestartNotice(string savedLanguage)
    {
        savedRestartLanguage=savedLanguage;
        languageRestartHeading.Text=L.T("Zapisano zmianę języka — wymagany restart",savedLanguage);
        languageRestartDetail.Text=L.F($"Język po restarcie: {(savedLanguage=="en"?"English":"Polski")}. Możesz uruchomić aplikację ponownie teraz lub później.",savedLanguage);
        var action=L.T("Uruchom ponownie",savedLanguage); languageRestartButton.Content=DeskIcons.Labelled("refresh-cw",action);
        AutomationProperties.SetName(languageRestartButton,action);
        var dismiss=L.T("Ukryj przypomnienie o restarcie",savedLanguage);
        AutomationProperties.SetName(languageRestartDismiss,dismiss); ToolTip.SetTip(languageRestartDismiss,dismiss);
        AutomationProperties.SetName(languageRestartBanner,L.T("Zmiana języka wymaga restartu",savedLanguage));
        languageRestartBanner.IsVisible=savedLanguage!=L.Language;
    }
}
