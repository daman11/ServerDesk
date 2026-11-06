using System.Security.Cryptography;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class SshKeyPassphraseTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-key-prompt-"+Guid.NewGuid().ToString("N"));
    private string Write(string content)
    {
        Directory.CreateDirectory(root); var path=Path.Combine(root,"fixture-key"); File.WriteAllText(path,content); return path;
    }
    [Theory]
    [InlineData("rsa")]
    [InlineData("pkcs8")]
    [InlineData("ed25519")]
    public async Task ValidUnencryptedKeyDoesNotNeedAPassphrase(string format)
    {
        Assert.False(await SshConnectionFactory.PrivateKeyNeedsPassphraseAsync(Write(BitwardenSshKeyTests.Key(format))));
    }
    [Fact]
    public async Task EncryptedKeyNeedsAPassphrase()
    {
        using var rsa=RSA.Create(2048);
        var pem=rsa.ExportEncryptedPkcs8PrivateKeyPem("synthetic-fixture-passphrase",new(PbeEncryptionAlgorithm.Aes256Cbc,HashAlgorithmName.SHA256,1000));
        Assert.True(await SshConnectionFactory.PrivateKeyNeedsPassphraseAsync(Write(pem)));
    }
    [Fact]
    public async Task MissingKeyHasAFileErrorInsteadOfAskingForAPassword()
    {
        var error=await Assert.ThrowsAsync<SshPrivateKeyException>(()=>SshConnectionFactory.PrivateKeyNeedsPassphraseAsync(Path.Combine(root,"absent","key")));
        Assert.Equal(SshPrivateKeyFailure.Missing,error.Reason);
    }
    [Fact]
    public async Task MalformedKeyIsNotReportedAsEncrypted()
    {
        var error=await Assert.ThrowsAsync<SshPrivateKeyException>(()=>SshConnectionFactory.PrivateKeyNeedsPassphraseAsync(Write("not a key")));
        Assert.Equal(SshPrivateKeyFailure.InvalidOrPassphrase,error.Reason);
    }
    [Fact]
    public async Task CancellationDoesNotReadTheKey()
    {
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>SshConnectionFactory.PrivateKeyNeedsPassphraseAsync(Path.Combine(root,"absent"),cancelled.Token));
    }
    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
}
