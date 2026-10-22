namespace ServerDesk.Core;

/// <summary>Non-secret, account-scoped reference; never a cached vault password.</summary>
public sealed record BitwardenReference(Guid ItemId,Guid AccountId,string Server,string Name)
{
    public void Validate()
    {
        if(ItemId==Guid.Empty || AccountId==Guid.Empty || Name==null || Name.Length>200 || Name.Any(char.IsControl) || Server!=CanonicalServer(Server))
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

public sealed record BitwardenCliOptions(string Executable="",string DataDirectory="")
{
    public void Validate()
    {
        foreach(var path in new[]{Executable,DataDirectory})
            if(path==null || path.Length>4096 || path.Any(char.IsControl) || path.Length>0 && !AbsoluteOnEitherPlatform(path))
                throw new ArgumentException(L.T("Podaj pełną ścieżkę do Bitwarden CLI i opcjonalnego katalogu danych, bez argumentów."));
    }
    // Preserve an inactive other-platform configuration when restoring a backup.
    private static bool AbsoluteOnEitherPlatform(string path)=>path.StartsWith('/') || path.Length>=3 && char.IsAsciiLetter(path[0]) && path[1]==':' && path[2] is '\\' or '/' || path.StartsWith("\\\\",StringComparison.Ordinal) && path.Length>4;
}
