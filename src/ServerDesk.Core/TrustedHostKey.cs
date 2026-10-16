namespace ServerDesk.Core;

public sealed record TrustedHostKey(string Host,int Port,string Fingerprint)
{
    public string Endpoint=>Host.Contains(':')?$"[{Host}]:{Port}":$"{Host}:{Port}";
}
