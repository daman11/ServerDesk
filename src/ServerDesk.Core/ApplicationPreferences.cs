namespace ServerDesk.Core;

public enum InterfaceDensity { Compact,Standard,Comfortable }
public enum SidebarStartup { Remember,Show,Hide }
public sealed record ApplicationPreferences
{
    public string Language { get; init; }="pl";
    public int InterfaceFontSize { get; init; }=13;
    public InterfaceDensity Density { get; init; }=InterfaceDensity.Standard;
    public string AccentColor { get; init; }="";
    public string TerminalFontFamily { get; init; }="Domyślna";
    public int SidebarWidth { get; init; }=272;
    public SidebarStartup SidebarOnStartup { get; init; }
    public int TabLabelWidth { get; init; }=220;
    public bool ShowProtocolBadges { get; init; }=true;
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
    public void Validate()
    {
        if(!AppText.IsSupported(Language))throw new ArgumentException(AppText.T("Nieobsługiwany język aplikacji."));
        if(InterfaceFontSize is <11 or >16 || SidebarWidth is <220 or >500 || TabLabelWidth is <140 or >360 || !Enum.IsDefined(Density) || !Enum.IsDefined(SidebarOnStartup) || !TerminalFonts.Contains(TerminalFontFamily))throw new ArgumentException(L.T("Niepoprawne ustawienia interfejsu."));
        if(AccentColor==null || AccentColor.Length>0 && (AccentColor.Length!=7 || AccentColor[0]!='#' || AccentColor[1..].Any(c=>!Uri.IsHexDigit(c))))throw new ArgumentException(L.T("Kolor akcentu powinien mieć postać #RRGGBB lub być pusty."));
    }
}
