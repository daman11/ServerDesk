using System.Text.Json.Serialization;

namespace ServerDesk.Core;

public enum Protocol { SSH=0,RDP=1,VNC=2,Serial=3 }
public enum Authentication { Password, PrivateKey }
public sealed record ServerProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    // Local vault binding. Rotated when the destination/authentication changes;
    // never carried into a configuration export or a duplicated/imported profile.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public Guid? CredentialId { get; init; }
    public string Name { get; init; } = "";
    public string Host { get; init; } = "";
    public int Port { get; init; } = 22;
    public Protocol Protocol { get; init; }
    public string Username { get; init; } = "";
    public string Domain { get; init; } = "";
    public Authentication Authentication { get; init; }
    public string PrivateKeyPath { get; init; } = "";
    public string Folder { get; init; } = "";
    public string Tags { get; init; } = "";
    public string Description { get; init; } = "";
    public string Color { get; init; } = "#7C8CFF";
    public string IconId { get; init; }="auto";
    public string CustomIconPng { get; init; }="";
    public bool Favorite { get; init; }
    public DateTimeOffset? LastConnected { get; init; }
    public RdpOptions Rdp { get; init; }=new();
    public VncOptions Vnc { get; init; }=new();
    public SerialOptions Serial { get; init; }=new();
    [JsonIgnore] public string Endpoint=>Protocol==Protocol.Serial?Serial.PortName:$"{Host}:{Port}";
    [JsonIgnore] public string Display => $"{(Favorite ? "★ " : "")}{Name}  ·  {Protocol}\n{Endpoint}  {Folder}";
    public void Validate()
    {
        if(CredentialId==Guid.Empty)throw new ArgumentException(L.T("Niepoprawne dane profilu."));
        if(Name==null || Host==null || Username==null || Domain==null || PrivateKeyPath==null || Tags==null || Description==null)throw new ArgumentException(L.T("Niepoprawne dane profilu."));
        ProfileIcons.Validate(IconId);
        ProfileAppearance.Validate(Color,CustomIconPng);
        if (string.IsNullOrWhiteSpace(Name) || Protocol!=Protocol.Serial && string.IsNullOrWhiteSpace(Host)) throw new ArgumentException(L.T("Nazwa i adres są wymagane."));
        if (Host.Any(char.IsWhiteSpace) || Host.Any(c => c is '/' or '\\' or '\r' or '\n' or '\0') || Host.StartsWith('-')) throw new ArgumentException(L.T("Niepoprawny adres hosta."));
        if (Port is < 1 or > 65535) throw new ArgumentException(L.T("Port musi mieścić się w zakresie 1–65535."));
        if (!Enum.IsDefined(Protocol) || !Enum.IsDefined(Authentication)) throw new ArgumentException(L.T("Nieznany protokół lub metoda logowania."));
        if (Username.Any(char.IsControl) || Domain.Any(char.IsControl)) throw new ArgumentException(L.T("Niepoprawny login."));
        ProfileFolder.Validate(Folder,true);
        if (Protocol == Protocol.SSH && string.IsNullOrWhiteSpace(Username)) throw new ArgumentException(L.T("SSH wymaga loginu."));
        if(Rdp==null)throw new ArgumentException(L.T("Niepoprawne ustawienia RDP."));
        Rdp.Validate(); if(Protocol==Protocol.RDP)RdpIdentity.Parse(Username,Domain);
        if(Vnc==null||Serial==null)throw new ArgumentException(L.T("Niepoprawne ustawienia VNC lub Serial."));
        Vnc.Validate(); if(Protocol==Protocol.Serial)Serial.Validate();
    }
}

public static class QuickConnect
{
    public static ServerProfile Parse(string text)
    {
        if(Uri.TryCreate(text,UriKind.Absolute,out var serial) && serial.Scheme=="serial")return ParseSerial(serial);
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("ssh" or "rdp" or "vnc") || uri.Host.Length == 0 || uri.UserInfo.Contains(':') || uri.Scheme=="vnc" && uri.UserInfo.Length>0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath is not ("" or "/"))
            throw new ArgumentException(L.T("Użyj ssh://login@host:22, rdp://host:3389, vnc://host:5900 lub serial://COM3?baud=115200. Nie wpisuj hasła w URL."));
        var protocol = uri.Scheme switch { "rdp"=>Protocol.RDP,"vnc"=>Protocol.VNC,_=>Protocol.SSH };
        var profile = new ServerProfile { Name = uri.Host, Host = uri.Host.Trim('[', ']'), Protocol = protocol, Port = uri.Port > 0 ? uri.Port : DefaultPort(protocol), Username = Uri.UnescapeDataString(uri.UserInfo) };
        profile.Validate();
        return profile;
    }
    public static int DefaultPort(Protocol protocol)=>protocol switch { Protocol.RDP=>3389,Protocol.VNC=>5900,_=>22 };
    private static ServerProfile ParseSerial(Uri uri)
    {
        if(uri.UserInfo.Length>0 || uri.Fragment.Length>0 || uri.Port!=-1 || uri.Host.Length>0 && uri.AbsolutePath is not ("" or "/"))throw new ArgumentException(L.T("Użyj serial://COM3 albo serial:///dev/ttyUSB0, opcjonalnie ?baud=115200."));
        var port=uri.Host.Length>0?uri.Host.ToUpperInvariant():Uri.UnescapeDataString(uri.AbsolutePath);
        var baud=115200;
        if(uri.Query.Length>0)
        {
            var query=uri.Query[1..].Split('=');
            if(query.Length!=2 || query[0]!="baud" || !int.TryParse(query[1],out baud))throw new ArgumentException(L.T("Jedyny parametr szybkiego Serial to baud, np. ?baud=9600. Pozostałe ustawienia zmienisz w profilu."));
        }
        var result=new ServerProfile { Name=port,Protocol=Protocol.Serial,Serial=new() { PortName=port,BaudRate=baud } }; result.Validate(); return result;
    }
}

public static class RemotePath
{
    public static string Join(string directory, string name)
    {
        if (name is "" or "." or ".." || name.Contains('/') || name.Contains('\0')) throw new ArgumentException(L.T("Niepoprawna nazwa pliku."));
        return directory.TrimEnd('/') + "/" + name;
    }
    public static string Parent(string path) { var i = path.TrimEnd('/').LastIndexOf('/'); return i <= 0 ? "/" : path[..i]; }
}

public interface ICredentialStore
{
    Task<string?> GetAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Guid id, string secret, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
public sealed record HostKeyChallenge(string Host, int Port, string Algorithm, string Fingerprint, string? PreviousFingerprint);
public sealed record FileEntry(string Name, string FullPath, bool IsDirectory, bool IsSymbolicLink, long Size, DateTime Modified, string Permissions)
{
    public string Display => $"{(IsDirectory ? "▸" : IsSymbolicLink ? "↗" : "·")} {Name}    {(IsDirectory ? "katalog" : Size.ToString("N0")+" B")}    {Permissions}    {(Modified==DateTime.MinValue?"":Modified.ToString("yyyy-MM-dd HH:mm"))}";
}
public sealed record TransferProgress(long Bytes, long Total, double BytesPerSecond);
