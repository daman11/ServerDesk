namespace ServerDesk.Core;

public enum InterfaceDensity { Compact,Standard,Comfortable }
public enum SidebarStartup { Remember,Show,Hide }
public enum TerminalCursorShape { Bar,Block,Underline }
public enum WorkspaceStartup { Remember,Single,Columns,Rows,Three,Grid }
public enum NewTabPlacement { FocusedPanel,FirstPanel,LeastPopulated }
public enum TextEditorKind { Automatic,VisualStudioCode,NotepadPlusPlus,Kate,GnomeTextEditor,Custom }
public sealed record ApplicationPreferences
{
    public PluginSelection DisabledPlugins { get; init; }=PluginSelection.Empty;
    public string Language { get; init; }="pl";
    public bool CheckUpdatesOnStartup { get; init; }
    public int InterfaceFontSize { get; init; }=13;
    public string InterfaceFontFamily { get; init; }="Domyślna";
    public int ControlCornerRadius { get; init; }=6;
    public int ConnectionRowPadding { get; init; }=1;
    public int MenuItemPadding { get; init; }=7;
    public InterfaceDensity Density { get; init; }=InterfaceDensity.Standard;
    public string AccentColor { get; init; }="";
    public string TerminalFontFamily { get; init; }="Domyślna";
    public int TerminalScrollbackLines { get; init; }=10000;
    public TerminalCursorShape TerminalCursor { get; init; }=TerminalCursorShape.Bar;
    public bool TerminalCursorBlink { get; init; }=true;
    public string SftpLocalDirectory { get; init; }="";
    public TextEditorKind SftpTextEditor { get; init; }
    public string SftpTextEditorPath { get; init; }="";
    public bool SftpShowHidden { get; init; }
    public FileSort SftpLocalSort { get; init; }=FileSort.Name;
    public FileSort SftpRemoteSort { get; init; }=FileSort.Name;
    public bool SftpLocalDescending { get; init; }
    public bool SftpRemoteDescending { get; init; }
    public bool SftpStartRemote { get; init; }=true;
    public int SidebarWidth { get; init; }=272;
    public SidebarStartup SidebarOnStartup { get; init; }
    public int TabLabelWidth { get; init; }=220;
    public WorkspaceStartup WorkspaceOnStartup { get; init; }
    public NewTabPlacement NewTabTarget { get; init; }
    public int TabStripHeight { get; init; }=35;
    public int PaneSeparatorSize { get; init; }=4;
    public bool ShowPanelNumbers { get; init; }=true;
    public bool ShowProtocolBadges { get; init; }=true;
    public bool ShowConnectionAddresses { get; init; }
    public bool ShowTabIcons { get; init; }=true;
    public bool ShowTabCloseButtons { get; init; }=true;
    public bool ShowSessionStatus { get; init; }=true;
    public bool ShowStatusBar { get; init; }=true;
    public bool ShowMetrics { get; init; }=true;
    public bool ShowTransferCount { get; init; }=true;
    public bool MetricCpu { get; init; }=true;
    public bool MetricMemory { get; init; }=true;
    public bool MetricNetwork { get; init; }=true;
    public bool MetricUptime { get; init; }=true;
    public bool MetricDisk { get; init; }=true;
    public bool MetricSystem { get; init; }=true;
    public bool MetricClock { get; init; }=true;
    public static IReadOnlyList<string> TerminalFonts { get; }=Array.AsReadOnly<string>(["Domyślna","Cascadia Mono","Consolas","JetBrains Mono","DejaVu Sans Mono"]);
    public static IReadOnlyList<string> InterfaceFonts { get; }=Array.AsReadOnly<string>(["Domyślna","Inter","Segoe UI","Noto Sans","DejaVu Sans","Arial"]);
    public void Validate()
    {
        PluginCatalog.Validate(DisabledPlugins);
        if(!AppText.IsSupported(Language))throw new ArgumentException(AppText.T("Nieobsługiwany język aplikacji."));
        if(!InterfaceFonts.Contains(InterfaceFontFamily))throw new ArgumentException(L.T("Nieobsługiwany krój czcionki interfejsu."),nameof(InterfaceFontFamily));
        if(ControlCornerRadius is <0 or >12)throw new ArgumentException(L.T("Zaokrąglenie kontrolek musi mieć od 0 do 12 px."),nameof(ControlCornerRadius));
        if(ConnectionRowPadding is <0 or >10)throw new ArgumentException(L.T("Odstęp w wierszu połączenia musi mieć od 0 do 10 px."),nameof(ConnectionRowPadding));
        if(MenuItemPadding is <2 or >12)throw new ArgumentException(L.T("Odstęp w pozycji menu musi mieć od 2 do 12 px."),nameof(MenuItemPadding));
        if(!Enum.IsDefined(WorkspaceOnStartup) || !Enum.IsDefined(NewTabTarget) || TabStripHeight is <30 or >48 || PaneSeparatorSize is <4 or >12)throw new ArgumentException(L.T("Niepoprawne ustawienia paneli obszaru pracy."),nameof(WorkspaceOnStartup));
        if(TerminalScrollbackLines is <0 or >100000)throw new ArgumentException(L.T("Historia terminala musi mieć od 0 do 100000 wierszy."),nameof(TerminalScrollbackLines));
        if(!Enum.IsDefined(TerminalCursor))throw new ArgumentException(L.T("Nieobsługiwany kształt kursora terminala."),nameof(TerminalCursor));
        if(SftpLocalDirectory==null || SftpLocalDirectory.Length>4096 || SftpLocalDirectory.Contains('\0') || SftpLocalDirectory.Length>0 && !Path.IsPathFullyQualified(SftpLocalDirectory))throw new ArgumentException(L.T("Katalog startowy SFTP powinien być pełną ścieżką lokalną lub pusty, aby użyć katalogu domowego."),nameof(SftpLocalDirectory));
        if(!Enum.IsDefined(SftpTextEditor))throw new ArgumentException(L.T("Nieobsługiwany edytor tekstu."),nameof(SftpTextEditor));
        if(SftpTextEditorPath==null || SftpTextEditorPath.Length>4096 || SftpTextEditorPath.Any(char.IsControl) || SftpTextEditorPath.Length>0 && !Path.IsPathFullyQualified(SftpTextEditorPath) || SftpTextEditor==TextEditorKind.Custom && string.IsNullOrWhiteSpace(SftpTextEditorPath))throw new ArgumentException(L.T("Podaj pełną ścieżkę do programu edytora, bez argumentów."),nameof(SftpTextEditorPath));
        if(!Enum.IsDefined(SftpLocalSort) || !Enum.IsDefined(SftpRemoteSort))throw new ArgumentException(L.T("Niepoprawny domyślny porządek plików SFTP."),nameof(SftpLocalSort));
        if(InterfaceFontSize is <11 or >16 || SidebarWidth is <220 or >500 || TabLabelWidth is <140 or >360 || !Enum.IsDefined(Density) || !Enum.IsDefined(SidebarOnStartup) || !TerminalFonts.Contains(TerminalFontFamily))throw new ArgumentException(L.T("Niepoprawne ustawienia interfejsu."));
        if(AccentColor==null || AccentColor.Length>0 && (AccentColor.Length!=7 || AccentColor[0]!='#' || AccentColor[1..].Any(c=>!Uri.IsHexDigit(c))))throw new ArgumentException(L.T("Kolor akcentu powinien mieć postać #RRGGBB lub być pusty."),nameof(AccentColor));
    }
}
