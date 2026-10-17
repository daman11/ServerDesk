using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed record RdpLaunch(string Executable, string[] Arguments, bool InteractiveConsole, string? ConfigurationFile=null);
public interface IRdpClient
{
    bool SupportsEmbeddedLinux=>false;
    RdpLaunch Prepare(ServerProfile profile,RdpOptions options);
    RdpLaunch PrepareEmbeddedLinux(ServerProfile profile,RdpOptions options,ulong parentWindow,string fingerprint)=>throw new NotSupportedException();
    Task<RdpEndpointInfo?> CheckAsync(ServerProfile profile,CancellationToken ct)=>Task.FromResult<RdpEndpointInfo?>(null);
    Task<RdpEndpointInfo?> InspectCertificateAsync(ServerProfile profile,CancellationToken ct)=>CheckAsync(profile,ct);
}
public static class RdpConfiguration
{
    public static string Address(ServerProfile profile)=>(profile.Host.Contains(':')?$"[{profile.Host}]":profile.Host)+":"+profile.Port;
    public static string[] WindowsLines(ServerProfile profile,RdpOptions options)
    {
        profile.Validate(); options.Validate(); var identity=RdpIdentity.Parse(profile.Username,profile.Domain);
        if(options.RedirectLocalFolder)throw new NotSupportedException(L.T("mstsc nie przekazuje pojedynczego folderu. Wyłącz tę opcję; przekazywanie folderu jest dostępne przez FreeRDP na Linux."));
        return [$"full address:s:{Address(profile)}",$"username:s:{identity.WindowsUsername}",$"screen mode id:i:{(options.Fullscreen?2:1)}",$"desktopwidth:i:{options.Width}",$"desktopheight:i:{options.Height}",$"smart sizing:i:{(options.DynamicResolution?1:0)}",$"keyboardhook:i:{(int)options.KeyboardMode}",$"autoreconnection enabled:i:{(options.AutoReconnect?1:0)}","authentication level:i:2","enablecredsspsupport:i:1",$"redirectclipboard:i:{(options.Clipboard?1:0)}","drivestoredirect:s:","redirectprinters:i:0","prompt for credentials:i:1"];
    }
    public static string[] FreeRdpArguments(ServerProfile profile,RdpOptions options)
    {
        profile.Validate(); options.Validate(); var identity=RdpIdentity.Parse(profile.Username,profile.Domain);
        // The client runs in an interactive PTY. /from-stdin also disables its
        // certificate trust prompt, so let the client request credentials normally.
        var args=new List<string> { $"/v:{Address(profile)}",$"/size:{options.Width}x{options.Height}","/sec:nla",options.Clipboard?"+clipboard":"-clipboard" };
        if(identity.User.Length>0)args.Add("/u:"+identity.User);
        if(identity.Domain.Length>0)args.Add("/d:"+identity.Domain);
        if(options.DynamicResolution)args.Add("/dynamic-resolution"); else args.Add("/smart-sizing");
        if(options.Fullscreen)args.Add("/f");
        if(options.RedirectLocalFolder)
        {
            if(!Path.IsPathFullyQualified(options.LocalFolder) || !Directory.Exists(options.LocalFolder))throw new ArgumentException(L.T("Przekazywany katalog musi istnieć i mieć ścieżkę absolutną na tym komputerze."));
            if(options.LocalFolder.Contains(','))throw new ArgumentException(L.T("FreeRDP wymaga ścieżki folderu bez przecinka."));
            args.Add("/drive:ServerDesk,"+options.LocalFolder);
        }
        return args.ToArray();
    }
    public static string[] EmbeddedFreeRdpArguments(ServerProfile profile,RdpOptions options,ulong parentWindow,string certificateFingerprint)
    {
        if(parentWindow==0)throw new ArgumentException(L.T("Osadzony pulpit RDP nie ma gotowego okna X11."));
        if(string.IsNullOrWhiteSpace(profile.Username))throw new ArgumentException(L.T("Wpisz login w profilu serwera, aby połączyć RDP w zakładce."));
        var fingerprint=RdpCertificateFingerprint.Display(certificateFingerprint);
        var args=FreeRdpArguments(profile,options with { Fullscreen=false }).ToList();
        // GUI owns the trust decision and password input. Only the approved exception
        // is allowed; another untrusted certificate is denied without a CLI prompt.
        args.Add("/cert:deny,fingerprint:sha256:"+fingerprint);
        args.Add("/from-stdin:force");
        args.Add("/parent-window:"+parentWindow.ToString(System.Globalization.CultureInfo.InvariantCulture));
        args.Add("-decorations"); args.Add("-grab-keyboard"); args.Add("-toggle-fullscreen"); args.Add("-auto-reconnect");
        args.Add("/log-level:ERROR");
        return args.ToArray();
    }
}
public sealed class ExternalRdpClient(AppPaths? paths=null) : IRdpClient
{
    public bool SupportsEmbeddedLinux=>OperatingSystem.IsLinux();
    public async Task<RdpEndpointInfo?> InspectCertificateAsync(ServerProfile profile,CancellationToken ct)=>await new RdpEndpointProbe().CheckAsync(profile,ct,inspectCertificate:true);
    public RdpLaunch PrepareEmbeddedLinux(ServerProfile profile,RdpOptions options,ulong parentWindow,string fingerprint)=>new(FindEmbeddedLinuxClient(),RdpConfiguration.EmbeddedFreeRdpArguments(profile,options,parentWindow,fingerprint),false);
    public static string FindEmbeddedLinuxClient()=>new[]{"xfreerdp3","xfreerdp"}.Select(FindExecutable).FirstOrDefault(x=>x!=null)??throw new FileNotFoundException(L.T("Brak klienta X11 FreeRDP. Zainstaluj freerdp3-x11; osadzony pulpit wymaga X11/XWayland."));
    public async Task<RdpEndpointInfo?> CheckAsync(ServerProfile profile,CancellationToken ct)=>await new RdpEndpointProbe().CheckAsync(profile,ct);
    public RdpLaunch Prepare(ServerProfile profile,RdpOptions options)
    {
        if (OperatingSystem.IsWindows())
        {
            var lines=RdpConfiguration.WindowsLines(profile,options);
            var directory=Path.Combine((paths??new AppPaths()).Data,"rdp"); Directory.CreateDirectory(directory); var file=Path.Combine(directory,Guid.NewGuid().ToString("N")+".rdp");
            File.WriteAllLines(file,lines,System.Text.Encoding.Unicode);
            // Never search the current directory or PATH for a credential-bearing client.
            return new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"mstsc.exe"),[file],false,file);
        }
        var candidates = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") != null ? new[] { "sdl-freerdp3", "wlfreerdp", "xfreerdp3", "xfreerdp" } : new[] { "xfreerdp3", "xfreerdp", "sdl-freerdp3" };
        var executable = candidates.Select(FindExecutable).FirstOrDefault(x => x != null) ?? throw new FileNotFoundException(L.T("Brak klienta FreeRDP. Zainstaluj freerdp3-x11 lub klienta SDL/Wayland."));
        return new(executable,RdpConfiguration.FreeRdpArguments(profile,options),true);
    }
    private static string? FindExecutable(string name) => (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(Path.IsPathFullyQualified).Select(x=>Path.Combine(x,name)).FirstOrDefault(File.Exists);
}
