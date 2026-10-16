using Renci.SshNet;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed class SshConnectionFactory(ProfileRepository repository)
{
    public Func<HostKeyChallenge, CancellationToken, Task<bool>>? ConfirmHostKey { get; set; }
    private ConnectionInfo Info(ServerProfile profile, string secret, out PrivateKeyFile? key)
    {
        profile.Validate(); key = null;
        AuthenticationMethod auth;
        if (profile.Authentication == Authentication.PrivateKey)
        {
            key = string.IsNullOrEmpty(secret) ? new PrivateKeyFile(profile.PrivateKeyPath) : new PrivateKeyFile(profile.PrivateKeyPath, secret);
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
        await client.ConnectAsync(ct).ConfigureAwait(false);
        if (accepted != null) await repository.TrustHostAsync(profile.Host, profile.Port, accepted, ct);
    }
    public Task<SshClient> SshAsync(ServerProfile profile,string secret,CancellationToken ct)=>CreateAsync(profile,secret,info=>new SshClient(info),ct);
    public Task<SftpClient> SftpAsync(ServerProfile profile,string secret,CancellationToken ct)=>CreateAsync(profile,secret,info=>new SftpClient(info) { OperationTimeout=TimeSpan.FromSeconds(30) },ct);
    private Task<T> CreateAsync<T>(ServerProfile profile,string secret,Func<ConnectionInfo,T> create,CancellationToken ct) where T:BaseClient=>Task.Run(async()=>
    {
        // File reads, passphrase derivation and cleanup can all block. The caller
        // must get its Task before any of that work starts on the UI thread.
        PrivateKeyFile? key=null; T? client=null;
        try
        {
            ct.ThrowIfCancellationRequested();
            var info=Info(profile,secret,out key); ct.ThrowIfCancellationRequested();
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
