using System.Security.Cryptography;
using System.Text.Json;

namespace ServerDesk.Infrastructure;

// An optional optimization for the pinned bundled client, never for shared CLI
// profiles. Its LowdbStorageService persists account, environment, encrypted keys
// and encrypted vault data together in data.json (pinned source in the component).
internal interface IBitwardenProfileSnapshotRunner
{
    Task<string?> ProfileSnapshotAsync(string session,Guid accountId,CancellationToken ct);
}

public sealed partial class BitwardenProcessRunner:IBitwardenProfileSnapshotRunner
{
    public Task<string?> ProfileSnapshotAsync(string session,Guid accountId,CancellationToken ct)
        =>managed?SnapshotAsync(options.Executable,options.DataDirectory,session,accountId,ct):Task.FromResult<string?>(null);

    internal static async Task<string?> SnapshotAsync(string executable,string directory,string session,Guid accountId,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            // The CLI gives a portable bw-data directory precedence over its
            // configured profile. Never prove identity using a different store.
            if(Path.Exists(Path.Combine(Path.GetDirectoryName(executable)!,"bw-data")) ||
                (File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return null;
            using var profileLock=LockProfile(directory);
            return await BitwardenProfileSnapshot.ReadAsync(Path.Combine(directory,"data.json"),session,accountId,ct).ConfigureAwait(false);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { return null; }
    }
}

internal static class BitwardenProfileSnapshot
{
    internal const long MaximumBytes=64L*1024*1024;
    internal static async Task<string?> ReadAsync(string path,string session,Guid accountId,CancellationToken ct)
    {
        var info=new FileInfo(path);
        if(!info.Exists || info.Length is <=0 or >MaximumBytes || (info.Attributes&FileAttributes.ReparsePoint)!=0)return null;
        await using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.Asynchronous|FileOptions.SequentialScan);
        var buffer=new byte[(int)info.Length];
        try
        {
            await input.ReadExactlyAsync(buffer,ct).ConfigureAwait(false);
            if(input.Length!=buffer.Length)return null;
            ct.ThrowIfCancellationRequested();
            return Digest(buffer,session,accountId);
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }
    internal static string? Digest(ReadOnlySpan<byte> profile,string session,Guid accountId)
    {
        byte[]? sessionKey=null,wrapped=null,unwrapped=null;
        try
        {
            // Every CLI auto-unlock rewrites this SAME key with a fresh random IV.
            // Compare its authenticated value, not its changing ciphertext. The
            // remaining bytes (including all identity/vault data) stay in the hash.
            // No vault password/item is decrypted here. Unknown formats fall back
            // to CLI status; never ignore a missing, changed or invalid key.
            var property="__PROTECTED__"+accountId.ToString("D")+"_user_auto";
            var reader=new Utf8JsonReader(profile,new JsonReaderOptions { MaxDepth=64 });
            var start=-1; var end=-1;
            while(reader.Read())
            {
                if(reader.TokenType!=JsonTokenType.PropertyName || reader.CurrentDepth!=1 || !reader.ValueTextEquals(property))continue;
                if(start>=0 || !reader.Read() || reader.TokenType!=JsonTokenType.String || reader.ValueSpan.Length>4096)return null;
                start=(int)reader.TokenStartIndex; end=(int)reader.BytesConsumed;
                sessionKey=Convert.FromBase64String(session);
                wrapped=Convert.FromBase64String(reader.GetString()!);
            }
            if(start<0 || sessionKey?.Length!=64 || wrapped==null || wrapped.Length<65 || wrapped[0]!=2 || (wrapped.Length-49)%16!=0)return null;
            using(var mac=IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256,sessionKey.AsSpan(32,32)))
            {
                mac.AppendData(wrapped.AsSpan(1,16)); mac.AppendData(wrapped.AsSpan(49));
                Span<byte> tag=stackalloc byte[32]; mac.GetHashAndReset(tag);
                if(!CryptographicOperations.FixedTimeEquals(tag,wrapped.AsSpan(17,32)))return null;
            }
            using(var aes=Aes.Create())
            {
                aes.SetKey(sessionKey.AsSpan(0,32));
                unwrapped=aes.DecryptCbc(wrapped.AsSpan(49),wrapped.AsSpan(1,16),PaddingMode.PKCS7);
            }
            if(unwrapped.Length!=64)return null;
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(profile[..start]); hash.AppendData(unwrapped); hash.AppendData(profile[end..]);
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        catch(Exception ex) when(ex is JsonException or FormatException or CryptographicException) { return null; }
        finally
        {
            if(sessionKey!=null)CryptographicOperations.ZeroMemory(sessionKey);
            if(wrapped!=null)CryptographicOperations.ZeroMemory(wrapped);
            if(unwrapped!=null)CryptographicOperations.ZeroMemory(unwrapped);
        }
    }
}
