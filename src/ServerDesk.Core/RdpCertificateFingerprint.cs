namespace ServerDesk.Core;

public static class RdpCertificateFingerprint
{
    public static string Normalize(string fingerprint)
    {
        var compact=(fingerprint??"").Replace(":","").Replace(" ","");
        if(compact.Length!=64 || compact.Any(c=>!char.IsAsciiHexDigit(c)))throw new ArgumentException(L.T("Brak poprawnego odcisku SHA-256 certyfikatu RDP. Sprawdź serwer ponownie."));
        return compact.ToUpperInvariant();
    }
    public static string Display(string fingerprint)=>string.Join(":",Normalize(fingerprint).Chunk(2).Select(chars=>new string(chars)));
}
