using ServerDesk.Core;
using System.Security.Cryptography;
using System.Text;

namespace ServerDesk.Infrastructure;

public sealed class CredentialStore(AppPaths paths) : ICredentialStore
{
    private readonly SemaphoreSlim gate=new(1);
    private string SecretPath(Guid id) => Path.Combine(paths.Data, "credentials", id + ".bin");
    public async Task<string?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return await new SecretServiceCredentialStore().GetAsync(id,ct);
        }
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var file=new FileStream(SecretPath(id),FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete,4096,true);
            var encrypted=await StreamLimits.ReadAsync(file,CredentialLimits.MaximumProtectedBytes,ct).ConfigureAwait(false);
            var plain=await Task.Run(()=>Unprotect(encrypted),ct).ConfigureAwait(false);
            try
            {
                if(plain.Length>CredentialLimits.MaximumSecretBytes)throw new InvalidDataException();
                return new UTF8Encoding(false,true).GetString(plain);
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch(FileNotFoundException) { return null; }
        catch(DirectoryNotFoundException) { return null; }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or DecoderFallbackException) { throw new CredentialReadException(ex); }
        finally { gate.Release(); }
    }
    public async Task SaveAsync(Guid id, string secret, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); CredentialLimits.Validate(secret);
        if (!OperatingSystem.IsWindows()) { await new SecretServiceCredentialStore().SaveAsync(id,secret,ct); return; }
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SecretPath(id))!);
            var plain=Encoding.UTF8.GetBytes(secret);
            try
            {
                var encrypted=await Task.Run(()=>Protect(plain),ct).ConfigureAwait(false);
                await AtomicFile.ReplaceAsync(SecretPath(id),(stream,token)=>stream.WriteAsync(encrypted,token).AsTask(),ct).ConfigureAwait(false);
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        finally { gate.Release(); }
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Forgetting an absent credential is successful, including first use
                // before the credentials directory has ever been created. Do not
                // pre-check Exists: the directory can disappear between check/delete.
                try { File.Delete(SecretPath(id)); }
                catch(FileNotFoundException) { }
                catch(DirectoryNotFoundException) { }
            }
            finally { gate.Release(); }
        }
        else { await new SecretServiceCredentialStore().DeleteAsync(id,ct); }
    }
    private static byte[] Protect(byte[] plain)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        return ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser);
    }
    private static byte[] Unprotect(byte[] encrypted)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        return ProtectedData.Unprotect(encrypted,null,DataProtectionScope.CurrentUser);
    }
}
