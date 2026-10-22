using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security;

namespace ServerDesk.Infrastructure;

/// <summary>Owned, bounded key bytes. Never serialized, written to disk or used as a password.</summary>
public sealed class SshKeyMaterial:IDisposable
{
    public const int MaximumBytes=64*1024;
    private readonly object gate=new();
    private byte[]? bytes;
    public string Fingerprint { get; }
    public string Algorithm { get; }
    public bool IsDisposed { get { lock(gate)return bytes==null; } }
    private SshKeyMaterial(byte[] bytes,string fingerprint,string algorithm) { this.bytes=bytes; Fingerprint=fingerprint; Algorithm=algorithm; }
    public static SshKeyMaterial Create(string privateKey,string publicKey="",string fingerprint="")
    {
        if(string.IsNullOrWhiteSpace(privateKey) || publicKey==null || fingerprint==null || publicKey.Length>16384 || fingerprint.Length>128 || privateKey.Contains('\0') || Encoding.UTF8.GetByteCount(privateKey)>MaximumBytes)throw new BitwardenException(BitwardenFailure.InvalidSshKey);
        if(privateKey.Contains("-----BEGIN ENCRYPTED PRIVATE KEY-----",StringComparison.Ordinal) || privateKey.Contains("Proc-Type: 4,ENCRYPTED",StringComparison.OrdinalIgnoreCase))throw new BitwardenException(BitwardenFailure.EncryptedSshKey);
        var bytes=Encoding.UTF8.GetBytes(privateKey);
        try
        {
            using var key=Parse(bytes);
            if(key.Key is not (RsaKey or ED25519Key))throw new BitwardenException(BitwardenFailure.InvalidSshKey);
            if(key.Key is RsaKey && key.Key.KeyLength is <2048 or >8192)throw new BitwardenException(BitwardenFailure.InvalidSshKey);
            var algorithm=key.HostKeyAlgorithms.First();
            var actual="SHA256:"+Convert.ToBase64String(SHA256.HashData(algorithm.Data)).TrimEnd('=');
            if(fingerprint.Length>0 && fingerprint.TrimEnd('=')!=actual)throw new BitwardenException(BitwardenFailure.InvalidSshKey);
            if(publicKey.Length>0)
            {
                var parts=publicKey.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length<2 || parts[0]!=(key.Key is RsaKey?"ssh-rsa":"ssh-ed25519") || !CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[1]),algorithm.Data))throw new BitwardenException(BitwardenFailure.InvalidSshKey);
            }
            var result=new SshKeyMaterial(bytes,actual,key.Key is RsaKey?"RSA":"Ed25519"); bytes=null!; return result;
        }
        catch(BitwardenException) { throw; }
        catch(Exception ex) when(ex is FormatException or ArgumentException or InvalidOperationException) { throw new BitwardenException(BitwardenFailure.InvalidSshKey); }
        finally { if(bytes!=null)CryptographicOperations.ZeroMemory(bytes); }
    }
    private static PrivateKeyFile Parse(byte[] value)
    {
        try { using var stream=new MemoryStream(value,false); return new PrivateKeyFile(stream); }
        catch(SshPassPhraseNullOrEmptyException) { throw new BitwardenException(BitwardenFailure.EncryptedSshKey); }
        // Parser messages can include key fragments. Do not retain them as an inner exception.
        catch(Exception ex) when(ex is not OperationCanceledException and not OutOfMemoryException) { throw new BitwardenException(BitwardenFailure.InvalidSshKey); }
    }
    internal PrivateKeyFile Open()
    {
        byte[] copy; lock(gate)copy=(bytes??throw new ObjectDisposedException(nameof(SshKeyMaterial))).ToArray();
        try { return Parse(copy); } finally { CryptographicOperations.ZeroMemory(copy); }
    }
    public SshKeyMaterial Clone()
    { lock(gate)return new((bytes??throw new ObjectDisposedException(nameof(SshKeyMaterial))).ToArray(),Fingerprint,Algorithm); }
    public void Dispose() { lock(gate) { if(bytes==null)return; CryptographicOperations.ZeroMemory(bytes); bytes=null; } }
    public override string ToString()=>"SshKeyMaterial [redacted]";
}
