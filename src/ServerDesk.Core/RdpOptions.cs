namespace ServerDesk.Core;
public enum RdpDisplayMode { External, Embedded }
public enum RdpKeyboardMode { Local, Remote, Fullscreen }
public sealed record RdpOptions
{
    public RdpDisplayMode DisplayMode { get; init; }=RdpDisplayMode.Embedded;
    public int Width { get; init; }=1600;
    public int Height { get; init; }=900;
    public bool Fullscreen { get; init; }
    public bool Clipboard { get; init; }=true;
    public bool DynamicResolution { get; init; }=true;
    public RdpKeyboardMode KeyboardMode { get; init; }=RdpKeyboardMode.Fullscreen;
    public bool AutoReconnect { get; init; }
    public bool RedirectLocalFolder { get; init; }
    public string LocalFolder { get; init; }="";
    public void Validate()
    {
        if(!Enum.IsDefined(DisplayMode))throw new ArgumentException(L.T("Niepoprawny tryb wyświetlania RDP."));
        if(!Enum.IsDefined(KeyboardMode))throw new ArgumentException(L.T("Niepoprawny tryb skrótów klawiatury RDP."));
        if(Width is <640 or >7680 || Height is <480 or >4320)throw new ArgumentException(L.T("Rozdzielczość RDP: szerokość 640–7680, wysokość 480–4320."));
        if(LocalFolder==null || LocalFolder.Any(char.IsControl))throw new ArgumentException(L.T("Niepoprawna ścieżka przekazywanego katalogu."));
        if(RedirectLocalFolder && string.IsNullOrWhiteSpace(LocalFolder))throw new ArgumentException(L.T("Wybierz katalog lokalny do przekazania."));
    }
}
public sealed record RdpIdentity(string User,string Domain)
{
    public static RdpIdentity Parse(string user,string domain)
    {
        if(user.Any(char.IsControl)||domain.Any(char.IsControl))throw new ArgumentException(L.T("Niepoprawny login RDP."));
        var parts=user.Split('\\');
        if(parts.Length>2 || parts.Length==2 && (parts[0].Length==0||parts[1].Length==0))throw new ArgumentException(L.T("Użyj loginu użytkownik, DOMENA\\użytkownik lub użytkownik@domena."));
        if(parts.Length==2)
        { if(domain.Length>0 && !domain.Equals(parts[0],StringComparison.OrdinalIgnoreCase))throw new ArgumentException(L.T("Domena w loginie i polu Domena musi być zgodna.")); return new(parts[1],parts[0]); }
        return new(user,domain);
    }
    public string WindowsUsername=>Domain.Length==0?User:Domain+"\\"+User;
}
