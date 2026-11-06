using Renci.SshNet;
using Renci.SshNet.Common;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed class SshConnectionFactory(ProfileRepository repository)
{
    public Func<HostKeyChallenge, CancellationToken, Task<bool>>? ConfirmHostKey { get; set; }
    private static PrivateKeyFile OpenPrivateKey(string path,string secret)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(path))throw new FileNotFoundException();
            using var input=File.OpenRead(path);
            if(string.IsNullOrEmpty(secret))
            {
                // SSH.NET's PKCS#8 parser attempts decryption with an empty
                // password instead of reporting a missing passphrase. Detect
                // this explicit envelope before asking the shared parser.
                Span<byte> header=stackalloc byte[256];
                bool encrypted;
                try { var count=input.Read(header); encrypted=header[..count].IndexOf("-----BEGIN ENCRYPTED PRIVATE KEY-----"u8)>=0; }
                finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(header); }
                if(encrypted)throw new SshPassPhraseNullOrEmptyException("The private key requires a passphrase.");
                input.Position=0;
            }
            return new PrivateKeyFile(input,secret);
        }
        catch(Exception ex) when(ex is SshException or System.Security.Cryptography.CryptographicException or Org.BouncyCastle.Crypto.CryptoException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var reason=ex switch {
                FileNotFoundException or DirectoryNotFoundException=>SshPrivateKeyFailure.Missing,
                UnauthorizedAccessException=>SshPrivateKeyFailure.AccessDenied,
                SshPassPhraseNullOrEmptyException=>SshPrivateKeyFailure.PassphraseRequired,
                IOException=>SshPrivateKeyFailure.ReadFailure,
                _=>SshPrivateKeyFailure.InvalidOrPassphrase };
            throw new SshPrivateKeyException(reason,ex);
        }
    }
    public static Task<bool> PrivateKeyNeedsPassphraseAsync(string path,CancellationToken ct=default)=>Task.Run(()=>
    {
        ct.ThrowIfCancellationRequested();
        try { using var key=OpenPrivateKey(path,""); ct.ThrowIfCancellationRequested(); return false; }
        catch(SshPrivateKeyException ex) when(ex.Reason==SshPrivateKeyFailure.PassphraseRequired) { ct.ThrowIfCancellationRequested(); return true; }
    },ct);
    private ConnectionInfo Info(ServerProfile profile,string secret,SshKeyMaterial? vaultKey,out PrivateKeyFile? key)
    {
        profile.Validate(); key = null;
        if((vaultKey!=null)!=(profile.Authentication==Authentication.VaultSshKey) || profile.Authentication==Authentication.VaultSshKey && secret.Length>0)throw new BitwardenException(BitwardenFailure.InvalidSshKey);
        AuthenticationMethod auth;
        if(profile.Authentication==Authentication.VaultSshKey)
        {
            if(vaultKey==null)throw new BitwardenException(BitwardenFailure.InvalidSshKey);
            key=vaultKey.Open(); auth=new PrivateKeyAuthenticationMethod(profile.Username,key);
        }
        else if (profile.Authentication == Authentication.PrivateKey)
        {
            key=OpenPrivateKey(profile.PrivateKeyPath,secret);
            auth = new PrivateKeyAuthenticationMethod(profile.Username, key);
        }
        else auth = new PasswordAuthenticationMethod(profile.Username, secret);
        return new ConnectionInfo(profile.Host, profile.Port, profile.Username, auth) { Timeout = TimeSpan.FromSeconds(20) };
    }
    private async Task VerifyAndConnect(BaseClient client, ServerProfile profile, CancellationToken ct)
    {
        var previous = await repository.HostKeyAsync(profile.Host, profile.Port, ct);
        string? accepted = null;
        client.KeepAliveInterval = TimeSpan.FromSeconds(30);
        client.HostKeyReceived += (_, e) =>
        {
            var fingerprint = "SHA256:" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(e.HostKey)).TrimEnd('=');
            e.CanTrust = false;
            if (previous == fingerprint) { e.CanTrust = true; return; }
            // Changed host keys are never replaced through a connection prompt.
            if (previous != null) throw new InvalidOperationException(L.F($"ZMIANA KLUCZA HOSTA {profile.Host}:{profile.Port}. Zapisany: {previous}, otrzymany: {fingerprint}. Sprawdź tożsamość serwera przed usunięciem zaufania.")+"\n"+L.T("Zaufaniem zarządzisz w Ustawienia → Bezpieczeństwo → Zaufane klucze SSH."));
            if (ConfirmHostKey?.Invoke(new(profile.Host, profile.Port, e.HostKeyName, fingerprint, null), ct).GetAwaiter().GetResult() == true)
            { accepted = fingerprint; e.CanTrust = true; }
        };
        try { await client.ConnectAsync(ct).ConfigureAwait(false); }
        catch(SshAuthenticationException ex) { throw new SshLoginException(profile.Authentication,client.ConnectionInfo.AuthenticationMethods.SelectMany(method=>method.AllowedAuthentications??[]),ex); }
        if (accepted != null) await repository.TrustHostAsync(profile.Host, profile.Port, accepted, ct);
    }
    public Task<SshClient> SshAsync(ServerProfile profile,string secret,CancellationToken ct)=>SshAsync(profile,secret,null,ct);
    public Task<SftpClient> SftpAsync(ServerProfile profile,string secret,CancellationToken ct)=>SftpAsync(profile,secret,null,ct);
    public Task<SshClient> SshAsync(ServerProfile profile,string secret,SshKeyMaterial? vaultKey,CancellationToken ct)=>CreateAsync(profile,secret,vaultKey,info=>new SshClient(info),ct);
    public Task<SftpClient> SftpAsync(ServerProfile profile,string secret,SshKeyMaterial? vaultKey,CancellationToken ct)=>CreateAsync(profile,secret,vaultKey,info=>new SftpClient(info) { OperationTimeout=TimeSpan.FromSeconds(30) },ct);
    private Task<T> CreateAsync<T>(ServerProfile profile,string secret,SshKeyMaterial? vaultKey,Func<ConnectionInfo,T> create,CancellationToken ct) where T:BaseClient=>Task.Run(async()=>
    {
        // File reads, passphrase derivation and cleanup can all block. The caller
        // must get its Task before any of that work starts on the UI thread.
        PrivateKeyFile? key=null; T? client=null;
        try
        {
            ct.ThrowIfCancellationRequested();
            var info=Info(profile,secret,vaultKey,out key); ct.ThrowIfCancellationRequested();
            client=create(info); await VerifyAndConnect(client,profile,ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested(); var connected=client; client=null; return connected;
        }
        finally
        {
            try
            {
                if(client!=null) { client.ConnectionInfo.ChannelCloseTimeout=TimeSpan.FromMilliseconds(500); client.Dispose(); }
            }
            finally { key?.Dispose(); }
        }
    },ct);
}
