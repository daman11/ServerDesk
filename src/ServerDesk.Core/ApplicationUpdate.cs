using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ServerDesk.Core;

public sealed record ApplicationUpdate(string Version,string DownloadUrl,long Size,string Sha256)
{
    public const string Repository="daman11/ServerDesk";
    public const long MaximumSize=350L*1024*1024;
    public static string ReleasesUrl=>$"https://github.com/{Repository}/releases";
    public string FileName=>$"ServerDesk-{Version}-win-x64-Setup.exe";
    public string ReleaseUrl=>$"{ReleasesUrl}/tag/v{Version}";
    public static Version ParseVersion(string value)
    {
        if(value is null || value.Length>32 || !Regex.IsMatch(value,@"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z",RegexOptions.CultureInvariant) ||
            !System.Version.TryParse(value,out var version))throw new InvalidDataException("Invalid update version.");
        return version;
    }
    public void Validate()
    {
        ParseVersion(Version);
        if(Size is <=0 or >MaximumSize || Sha256 is null || !Regex.IsMatch(Sha256,@"\A[a-f0-9]{64}\z",RegexOptions.CultureInvariant) ||
            DownloadUrl!=$"https://github.com/{Repository}/releases/download/v{Version}/{FileName}")throw new InvalidDataException("Invalid update asset metadata.");
    }
    public async Task VerifyAsync(Stream file,CancellationToken ct=default)
    {
        Validate();
        if(!file.CanSeek || file.Length!=Size)throw new InvalidDataException("Update file size mismatch.");
        file.Position=0;
        var actual=await SHA256.HashDataAsync(file,ct);
        if(!CryptographicOperations.FixedTimeEquals(actual,Convert.FromHexString(Sha256)))throw new InvalidDataException("Update SHA-256 mismatch.");
        file.Position=0;
    }
}
