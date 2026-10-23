namespace ServerDesk.Core;

public enum BitwardenItemKind { Login=1,SshKey=5 }
/// <summary>Non-secret, account-scoped reference; never cached vault credentials.</summary>
public sealed record BitwardenReference(Guid ItemId,Guid AccountId,string Server,string Name,BitwardenItemKind Kind=BitwardenItemKind.Login)
{
    public void Validate()
    {
        if(!Enum.IsDefined(Kind) || ItemId==Guid.Empty || AccountId==Guid.Empty || Name==null || Name.Length>200 || Name.Any(char.IsControl) || Server!=CanonicalServer(Server))
            throw new ArgumentException(L.T("Niepoprawne przypisanie wpisu Bitwarden."));
    }
    public static string CanonicalServer(string value)
    {
        if(value==null || value.Length>2048 || value.Any(char.IsControl) || !Uri.TryCreate(value,UriKind.Absolute,out var uri) ||
            uri.Scheme is not ("https" or "http") || uri.Host.Length==0 || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0)
            throw new ArgumentException(L.T("Niepoprawny adres serwera Bitwarden."));
        return uri.AbsoluteUri.TrimEnd('/');
    }
}

public sealed record BitwardenCliOptions(string Executable="",string DataDirectory="",string Server="",string Email="",bool UnlockOnStartup=true)
{
    [System.Text.Json.Serialization.JsonIgnore] public bool UsesBundledClient=>Executable.Length==0;
    [System.Text.Json.Serialization.JsonIgnore] public string EffectiveServer=>Server.Length==0?"https://vault.bitwarden.com":BitwardenReference.CanonicalServer(Server);
    public void Validate()
    {
        foreach(var path in new[]{Executable,DataDirectory})
            if(path==null || path.Length>4096 || path.Any(char.IsControl) || path.Length>0 && !AbsoluteOnEitherPlatform(path))
                throw new ArgumentException(L.T("Podaj pełną ścieżkę do Bitwarden CLI i opcjonalnego katalogu danych, bez argumentów."));
        if(Server==null || Email==null || Email.Length>254 || Email.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)) || Email.Length>0 && (!Email.Contains('@') || Email.StartsWith('-')))
            throw new ArgumentException(L.T("Podaj poprawny adres e-mail konta sejfu."));
        if(Server.Length>0)
        {
            var uri=new Uri(BitwardenReference.CanonicalServer(Server));
            if(uri.Scheme!="https")throw new ArgumentException(L.T("Serwer sejfu musi używać HTTPS i zaufanego certyfikatu."));
        }
    }
    // Preserve an inactive other-platform configuration when restoring a backup.
    private static bool AbsoluteOnEitherPlatform(string path)=>path.StartsWith('/') || path.Length>=3 && char.IsAsciiLetter(path[0]) && path[1]==':' && path[2] is '\\' or '/' || path.StartsWith("\\\\",StringComparison.Ordinal) && path.Length>4;
}
