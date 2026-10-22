using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Security;
using Renci.SshNet;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class BitwardenSshKeyTests
{
    private static readonly Guid Account=Guid.Parse("ba9257b9-a57c-42d3-8709-b2d4d4984559"),Item=Guid.Parse("024606e6-7a83-45d0-a7e8-828a5a21174d");
    private static BitwardenReference Reference=>new(Item,Account,"https://fixture.invalid","Fixture key",BitwardenItemKind.SshKey);
    private static string Key(string format)
    {
        if(format=="ed25519")
        {
            var bytes=OpenSshPrivateKeyUtilities.EncodePrivateKey(new Ed25519PrivateKeyParameters(new SecureRandom()));
            try { return "-----BEGIN OPENSSH PRIVATE KEY-----\n"+Convert.ToBase64String(bytes,Base64FormattingOptions.InsertLineBreaks)+"\n-----END OPENSSH PRIVATE KEY-----\n"; }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        using var rsa=RSA.Create(2048);
        return format switch { "pkcs8"=>rsa.ExportPkcs8PrivateKeyPem(),"encrypted"=>rsa.ExportEncryptedPkcs8PrivateKeyPem("fixture-only",new(PbeEncryptionAlgorithm.Aes256Cbc,HashAlgorithmName.SHA256,1000)),_=>rsa.ExportRSAPrivateKeyPem() };
    }
    private static (string Public,string Fingerprint) Public(string pem)
    { using var bytes=new MemoryStream(Encoding.UTF8.GetBytes(pem)); using var key=new PrivateKeyFile(bytes); var data=key.HostKeyAlgorithms.First().Data; return ((key.Key is Renci.SshNet.Security.RsaKey?"ssh-rsa":"ssh-ed25519")+" "+Convert.ToBase64String(data),"SHA256:"+Convert.ToBase64String(SHA256.HashData(data)).TrimEnd('=')); }
    private sealed class Runner(string key):IBitwardenCommandRunner
    {
        internal Guid User=Account; internal bool ChangeAfterGet,Deleted,CancelGet; internal int Type=5; internal readonly List<string[]> Calls=[]; internal readonly List<byte[]> Buffers=[]; internal CancellationTokenSource? Cancel;
        public Task<byte[]> RunAsync(IReadOnlyList<string> arguments,string? session,string? masterPassword,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); var args=arguments.ToArray(); Calls.Add(args);
            Assert.DoesNotContain(args,a=>a.Contains("PRIVATE KEY",StringComparison.Ordinal)||a.Contains("fixture-master",StringComparison.Ordinal));
            var metadata=Public(key);
            var item=JsonSerializer.Serialize(new { id=Item,type=Type,name="Fixture key",deletedDate=Deleted?"2026-10-22":null,sshKey=new { privateKey=key,publicKey=metadata.Public,keyFingerprint=metadata.Fingerprint },notes="PRIVATE_NOTE_CANARY",login=new { password="PASSWORD_CANARY",username="fixture" } });
            var value=args[0] switch { "status"=>JsonSerializer.Serialize(new { userId=User,serverUrl="https://fixture.invalid",status=session==null?"locked":"unlocked" }),"unlock"=>"FIXTURE_SESSION_abcdefghijklmnop==","list"=>"["+item+"]","get"=>item,_=>throw new Exception("Unexpected CLI mutation") };
            var output=Encoding.UTF8.GetBytes(value); Buffers.Add(output);
            if(args[0]=="get") { if(ChangeAfterGet)User=Guid.NewGuid(); if(CancelGet)Cancel!.Cancel(); }
            return Task.FromResult(output);
        }
    }
    [Theory][InlineData("rsa")][InlineData("pkcs8")][InlineData("ed25519")]
    public void Owned_material_validates_metadata_clones_and_erases_its_buffers(string format)
    {
        var pem=Key(format); var metadata=Public(pem); using var key=SshKeyMaterial.Create(pem,metadata.Public,metadata.Fingerprint);
        Assert.Equal(metadata.Fingerprint,key.Fingerprint); Assert.DoesNotContain("PRIVATE KEY",key.ToString()); Assert.DoesNotContain(pem,JsonSerializer.Serialize(key));
        using var clone=key.Clone(); var buffer=(byte[])typeof(SshKeyMaterial).GetField("bytes",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(key)!;
        key.Dispose(); Assert.True(key.IsDisposed); Assert.All(buffer,b=>Assert.Equal(0,b)); Assert.False(clone.IsDisposed); Assert.Throws<ObjectDisposedException>(()=>key.Clone()); key.Dispose();
    }
    [Theory][InlineData("")][InlineData("PRIVATE_KEY_CANARY")]
    public void Malformed_material_has_a_controlled_exception_without_raw_key(string value)
    { var ex=Assert.Throws<BitwardenException>(()=>SshKeyMaterial.Create(value)); Assert.Equal(BitwardenFailure.InvalidSshKey,ex.Reason); Assert.DoesNotContain("PRIVATE_KEY_CANARY",ex.ToString()); Assert.Null(ex.InnerException); }
    [Fact] public void Limits_encryption_and_metadata_mismatches_never_export_parser_messages()
    {
        Assert.Equal(BitwardenFailure.InvalidSshKey,Assert.Throws<BitwardenException>(()=>SshKeyMaterial.Create(new('x',SshKeyMaterial.MaximumBytes+1))).Reason);
        Assert.Equal(BitwardenFailure.EncryptedSshKey,Assert.Throws<BitwardenException>(()=>SshKeyMaterial.Create(Key("encrypted"))).Reason);
        var pem=Key("rsa"); var metadata=Public(pem);
        Assert.Equal(BitwardenFailure.InvalidSshKey,Assert.Throws<BitwardenException>(()=>SshKeyMaterial.Create(pem,metadata.Public,"SHA256:wrong")).Reason);
        Assert.Throws<BitwardenException>(()=>SshKeyMaterial.Create(pem,"ssh-ed25519 "+metadata.Public.Split(' ')[1]));
        Assert.Throws<BitwardenException>(()=>SshKeyMaterial.Create(pem,"ssh-rsa AAAA"));
    }
    [Fact] public async Task Key_search_retains_metadata_only_and_retrieval_is_type_and_account_scoped()
    {
        var pem=Key("ed25519"); var runner=new Runner(pem); using var cli=new BitwardenCli(runner); await cli.UnlockAsync("fixture-master",default);
        Assert.Empty(await cli.SearchAsync("fixture",default)); var found=Assert.Single(await cli.SearchAsync("fixture",default,BitwardenItemKind.SshKey)); Assert.Equal(Reference,found.Reference); Assert.Equal("",found.Username);
        var json=JsonSerializer.Serialize(found); Assert.DoesNotContain("PRIVATE KEY",json); Assert.DoesNotContain("PASSWORD_CANARY",json); Assert.DoesNotContain("PRIVATE_NOTE_CANARY",json); Assert.DoesNotContain(runner.Calls,a=>a[0]=="get");
        await Assert.ThrowsAsync<BitwardenException>(()=>cli.GetPasswordAsync(found.Reference,default));
        using var key=await cli.GetSshKeyAsync(found.Reference,default); Assert.Equal(found.Fingerprint,key.Fingerprint);
        Assert.All(runner.Buffers,b=>Assert.All(b,v=>Assert.Equal(0,v)));
        runner.User=Guid.NewGuid(); var before=runner.Calls.Count(a=>a[0]=="get"); var ex=await Assert.ThrowsAsync<BitwardenException>(()=>cli.GetSshKeyAsync(found.Reference,default)); Assert.Equal(BitwardenFailure.AccountChanged,ex.Reason); Assert.Equal(before,runner.Calls.Count(a=>a[0]=="get"));
    }
    [Theory][InlineData(1,false)][InlineData(5,true)]
    public async Task Replaced_and_deleted_items_are_rejected(int type,bool deleted)
    {
        var runner=new Runner(Key("rsa")) { Type=type,Deleted=deleted }; using var cli=new BitwardenCli(runner); await cli.UnlockAsync("fixture-master",default);
        Assert.Equal(BitwardenFailure.InvalidSshKey,(await Assert.ThrowsAsync<BitwardenException>(()=>cli.GetSshKeyAsync(Reference,default))).Reason);
    }
    [Fact] public async Task Account_change_or_cancellation_after_retrieval_cannot_return_key_material()
    {
        var runner=new Runner(Key("rsa")) { ChangeAfterGet=true }; using var cli=new BitwardenCli(runner); await cli.UnlockAsync("fixture-master",default);
        Assert.Equal(BitwardenFailure.AccountChanged,(await Assert.ThrowsAsync<BitwardenException>(()=>cli.GetSshKeyAsync(Reference,default))).Reason);
        using var cancel=new CancellationTokenSource(); runner.User=Account; runner.ChangeAfterGet=false; runner.CancelGet=true; runner.Cancel=cancel; await cli.UnlockAsync("fixture-master",default);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cli.GetSshKeyAsync(Reference,cancel.Token)); Assert.All(runner.Buffers,b=>Assert.All(b,v=>Assert.Equal(0,v)));
    }
    [Fact] public async Task Key_reference_survives_restart_but_export_import_and_duplication_remain_unbound()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-vault-ssh-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repo=new ProfileRepository(new(root)); await repo.InitializeAsync(); var p=new ServerProfile { Name="Fixture",Host="fixture.invalid",Username="fixture",Authentication=Authentication.VaultSshKey,Bitwarden=Reference }; await repo.SaveAsync(p);
            Assert.Equal(Reference,Assert.Single(await new ProfileRepository(new(root)).ListAsync()).Bitwarden);
            using var export=new MemoryStream(); await repo.ExportAsync(export); var json=Encoding.UTF8.GetString(export.ToArray()); Assert.DoesNotContain("Bitwarden",json); Assert.DoesNotContain("PRIVATE KEY",json);
            export.Position=0; await repo.ImportAsync(export); var imported=(await repo.ListAsync()).Single(x=>x.Id!=p.Id); Assert.Null(imported.Bitwarden); Assert.Equal(Authentication.VaultSshKey,imported.Authentication); imported.Validate();
            Assert.False(CredentialReference.CanReuse(p,p with { Authentication=Authentication.Password,Bitwarden=null }));
            Assert.Throws<ArgumentException>(()=>(p with { Protocol=Protocol.RDP }).Validate()); Assert.Throws<ArgumentException>(()=>(p with { Bitwarden=Reference with { Kind=BitwardenItemKind.Login } }).Validate());
        }
        finally { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
    }
    [Fact] public void Older_references_default_to_login_and_do_not_become_key_bindings()
    {
        var json=JsonSerializer.Serialize(new { Reference.ItemId,Reference.AccountId,Reference.Server,Reference.Name });
        Assert.Equal(BitwardenItemKind.Login,JsonSerializer.Deserialize<BitwardenReference>(json)!.Kind);
    }
    [Fact] public async Task Key_authentication_never_falls_back_to_a_saved_password_or_key_file()
    {
        var factory=new SshConnectionFactory(new ProfileRepository(new(Path.Combine(Path.GetTempPath(),"missing-vault-key-fixture"))));
        var p=new ServerProfile { Name="Fixture",Host="127.0.0.1",Port=1,Username="fixture",Authentication=Authentication.VaultSshKey,PrivateKeyPath="NEVER_READ_THIS_FILE" };
        await Assert.ThrowsAsync<BitwardenException>(()=>factory.SshAsync(p,"saved-password-canary",default)); await Assert.ThrowsAsync<BitwardenException>(()=>factory.SftpAsync(p,"",default));
        using var key=SshKeyMaterial.Create(Key("rsa")); using var cancel=new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>factory.SshAsync(p,"",key,cancel.Token)); Assert.False(key.IsDisposed);
    }
}
