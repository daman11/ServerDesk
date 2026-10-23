using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class BitwardenPerformanceTests
{
    private static readonly Guid Account=Guid.Parse("7cc33741-dd40-4c11-82d7-d8ea7f752d36"),Item=Guid.Parse("5355c225-7897-4bc7-8f16-d8cc8fd1171f");
    private static BitwardenReference Reference=>new(Item,Account,"https://performance.fixture.invalid","Synthetic fixture");
    private static readonly byte[] SessionKey=Enumerable.Range(0,64).Select(i=>(byte)i).ToArray();
    private static readonly string Token=Convert.ToBase64String(SessionKey);
    private static readonly byte[] AutoKey=Enumerable.Repeat((byte)42,64).ToArray();
    private static string Property=>"__PROTECTED__"+Account.ToString("D")+"_user_auto";
    private static string Wrap(byte[] key)
    {
        using var aes=Aes.Create(); aes.Key=SessionKey[..32]; aes.GenerateIV();
        var ciphertext=aes.EncryptCbc(key,aes.IV,PaddingMode.PKCS7);
        var authenticated=aes.IV.Concat(ciphertext).ToArray();
        var tag=HMACSHA256.HashData(SessionKey[32..],authenticated);
        return Convert.ToBase64String(new byte[]{2}.Concat(aes.IV).Concat(tag).Concat(ciphertext).ToArray());
    }
    private sealed class Runner:IBitwardenCommandRunner,IBitwardenProfileSnapshotRunner,IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"serverdesk-vault-performance-"+Guid.NewGuid().ToString("N"));
        internal string Store=>Path.Combine(Root,"data.json");
        internal Guid User=Account;
        internal string State="locked",Server=Reference.Server;
        internal bool SnapshotEnabled=true;
        internal string? ItemResponse;
        internal Action? DuringGet,DuringStatus;
        internal readonly List<string> Calls=[];
        internal readonly List<byte[]> Buffers=[];
        internal Runner() { Directory.CreateDirectory(Root); File.WriteAllText(Store,JsonSerializer.Serialize(new Dictionary<string,string> { ["encryptedFixture"]="aaaa",[Property]=Wrap(AutoKey) })); }
        public Task<string?> ProfileSnapshotAsync(string session,Guid accountId,CancellationToken ct)=>SnapshotEnabled?
            BitwardenProcessRunner.SnapshotAsync(Path.Combine(Root,"bw.exe"),Root,session,accountId,ct):Task.FromResult<string?>(null);
        public Task<byte[]> RunAsync(IReadOnlyList<string> args,string? session,string? masterPassword,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add(args[0]);
            var response=args[0] switch {
                "status"=>JsonSerializer.Serialize(new { status=session!=null&&State=="locked"?"unlocked":State,userId=User,serverUrl=Server }),
                "unlock"=>Token,
                "get"=>ItemResponse??JsonSerializer.Serialize(new { id=Item,type=1,login=new { username="fixture",password="performance-synthetic-password" } }),
                "sync"=>"synced",
                _=>throw new InvalidOperationException("Unexpected fixture command") };
            if(args[0] is "status" or "get" && session!=null)RotateAutoKey();
            if(args[0]=="status")DuringStatus?.Invoke();
            if(args[0]=="get")DuringGet?.Invoke();
            var bytes=Encoding.UTF8.GetBytes(response); Buffers.Add(bytes); return Task.FromResult(bytes);
        }
        internal void ChangeStore()
        {
            var time=File.GetLastWriteTimeUtc(Store);
            // Same size AND timestamp must still invalidate the proof.
            File.WriteAllText(Store,File.ReadAllText(Store).Replace("aaaa","bbbb",StringComparison.Ordinal));
            File.SetLastWriteTimeUtc(Store,time);
        }
        private void RotateAutoKey()
        {
            if(!File.Exists(Store))return;
            var data=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Store))!;
            data[Property]=Wrap(AutoKey); File.WriteAllText(Store,JsonSerializer.Serialize(data));
        }
        public void Dispose()=>Directory.Delete(Root,true);
    }

    [Fact] public async Task Unchanged_owned_store_fetches_each_password_using_one_command_without_caching_secrets()
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner);
        await client.UnlockAsync("synthetic-master",default,Reference); runner.Calls.Clear();
        for(var i=0;i<3;i++)Assert.Equal("performance-synthetic-password",(await client.GetPasswordAsync(Reference,default)).Password);
        Assert.Equal(new[]{"get","get","get"},runner.Calls);
        Assert.All(runner.Buffers,b=>Assert.All(b,value=>Assert.Equal(0,value)));
        Assert.DoesNotContain("performance-synthetic-password",File.ReadAllText(runner.Store));
    }
    [Fact] public async Task Ssh_key_reads_also_keep_one_exact_lookup_and_clear_cli_buffers()
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner); using var rsa=RSA.Create(2048);
        runner.ItemResponse=JsonSerializer.Serialize(new { id=Item,type=5,sshKey=new { privateKey=rsa.ExportRSAPrivateKeyPem() } });
        await client.UnlockAsync("synthetic-master",default); runner.Calls.Clear();
        using var first=await client.GetSshKeyAsync(Reference with { Kind=BitwardenItemKind.SshKey },default);
        using var second=await client.GetSshKeyAsync(Reference with { Kind=BitwardenItemKind.SshKey },default);
        Assert.Equal(first.Fingerprint,second.Fingerprint); Assert.Equal(new[]{"get","get"},runner.Calls);
        Assert.All(runner.Buffers,b=>Assert.All(b,value=>Assert.Equal(0,value)));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Changed_store_checks_account_and_blocks_a_different_identity_before_fetch(bool changeServer)
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        runner.Calls.Clear(); runner.ChangeStore();
        if(changeServer)runner.Server="https://different.fixture.invalid"; else runner.User=Guid.NewGuid();
        Assert.Equal(BitwardenFailure.AccountChanged,(await Assert.ThrowsAsync<BitwardenException>(()=>client.GetPasswordAsync(Reference,default))).Reason);
        Assert.Equal(new[]{"status"},runner.Calls); Assert.False(client.IsUnlocked);
    }
    [Fact] public async Task Changed_account_during_fetch_never_returns_the_secret()
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        runner.Calls.Clear(); runner.DuringGet=()=> { runner.ChangeStore(); runner.User=Guid.NewGuid(); };
        Assert.Equal(BitwardenFailure.AccountChanged,(await Assert.ThrowsAsync<BitwardenException>(()=>client.GetPasswordAsync(Reference,default))).Reason);
        Assert.Equal(new[]{"get","status"},runner.Calls); Assert.False(client.IsUnlocked);
        Assert.All(runner.Buffers,b=>Assert.All(b,value=>Assert.Equal(0,value)));
    }
    [Fact] public async Task Changed_store_with_same_account_is_revalidated_then_the_fast_path_resumes()
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        runner.Calls.Clear(); runner.ChangeStore(); await client.GetPasswordAsync(Reference,default); await client.GetPasswordAsync(Reference,default);
        Assert.Equal(new[]{"status","get","get"},runner.Calls);
    }
    [Theory][InlineData("unauthenticated",BitwardenFailure.LoginRequired)][InlineData("invalid",BitwardenFailure.InvalidResponse)]
    public async Task Logout_or_invalid_status_after_store_change_locks_the_workspace(string state,BitwardenFailure failure)
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        runner.ChangeStore(); runner.State=state; runner.Calls.Clear();
        Assert.Equal(failure,(await Assert.ThrowsAsync<BitwardenException>(()=>client.GetPasswordAsync(Reference,default))).Reason);
        Assert.Equal(new[]{"status"},runner.Calls); Assert.False(client.IsUnlocked);
    }
    [Fact] public async Task A_store_changing_during_status_does_not_establish_a_proof()
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner);
        runner.DuringStatus=()=>
        {
            var data=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(runner.Store))!;
            data["epoch"]=Guid.NewGuid().ToString("N"); File.WriteAllText(runner.Store,JsonSerializer.Serialize(data));
        };
        await client.UnlockAsync("synthetic-master",default);
        runner.Calls.Clear(); await client.GetPasswordAsync(Reference,default);
        Assert.Equal(new[]{"status","get","status"},runner.Calls);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task External_or_missing_store_keeps_both_status_checks(bool missing)
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        if(missing)File.Delete(runner.Store); else runner.SnapshotEnabled=false;
        runner.Calls.Clear(); await client.GetPasswordAsync(Reference,default);
        Assert.Equal(new[]{"status","get","status"},runner.Calls);
    }
    [Fact] public async Task Wrong_binding_and_manual_lock_block_even_when_the_store_is_unchanged()
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default); runner.Calls.Clear();
        Assert.Equal(BitwardenFailure.AccountChanged,(await Assert.ThrowsAsync<BitwardenException>(()=>client.GetPasswordAsync(Reference with { AccountId=Guid.NewGuid() },default))).Reason);
        Assert.Empty(runner.Calls); Assert.False(client.IsUnlocked);
        await client.UnlockAsync("synthetic-master",default); client.Lock(); runner.Calls.Clear();
        Assert.Equal(BitwardenFailure.Locked,(await Assert.ThrowsAsync<BitwardenException>(()=>client.GetPasswordAsync(Reference,default))).Reason); Assert.Empty(runner.Calls);
    }
    [Fact] public async Task Cancellation_stops_before_any_secret_lookup()
    {
        using var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default); runner.Calls.Clear();
        using var cancellation=new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.GetPasswordAsync(Reference,cancellation.Token)); Assert.Empty(runner.Calls);
    }
    [Fact] public async Task Oversized_store_and_portable_profile_override_disable_the_optimization()
    {
        using var runner=new Runner();
        using(var file=File.OpenWrite(runner.Store))file.SetLength(BitwardenProfileSnapshot.MaximumBytes+1);
        Assert.Null(await runner.ProfileSnapshotAsync(Token,Account,default));
        File.WriteAllText(runner.Store,"{}"); Directory.CreateDirectory(Path.Combine(runner.Root,"bw-data"));
        Assert.Null(await runner.ProfileSnapshotAsync(Token,Account,default));
    }
    [Fact] public void Rewrapped_auto_key_keeps_the_digest_but_a_changed_key_does_not()
    {
        byte[] Profile(byte[] key)=>JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,string>{ [Property]=Wrap(key),["encryptedFixture"]="aaaa" });
        var first=BitwardenProfileSnapshot.Digest(Profile(AutoKey),Token,Account); Assert.NotNull(first);
        Assert.Equal(first,BitwardenProfileSnapshot.Digest(Profile(AutoKey),Token,Account));
        Assert.NotEqual(first,BitwardenProfileSnapshot.Digest(Profile(Enumerable.Repeat((byte)43,64).ToArray()),Token,Account));
        Assert.Null(BitwardenProfileSnapshot.Digest(Profile(AutoKey),Token,Guid.NewGuid()));
        Assert.Null(BitwardenProfileSnapshot.Digest(Profile(AutoKey),Convert.ToBase64String(new byte[64]),Account));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(17)][InlineData(49)][InlineData(80)]
    public void Tampering_with_the_auto_key_never_produces_a_proof(int offset)
    {
        var wrapped=Convert.FromBase64String(Wrap(AutoKey)); wrapped[offset]^=1;
        var profile=JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,string>{ [Property]=Convert.ToBase64String(wrapped) });
        Assert.Null(BitwardenProfileSnapshot.Digest(profile,Token,Account));
    }
    [Theory][InlineData("{}")][InlineData("{")][InlineData("[]")][InlineData("null")]
    public void Missing_or_invalid_profile_does_not_produce_a_proof(string profile)
        =>Assert.Null(BitwardenProfileSnapshot.Digest(Encoding.UTF8.GetBytes(profile),Token,Account));
    [Fact] public void Duplicate_auto_key_fields_and_invalid_session_are_rejected()
    {
        var field=JsonSerializer.Serialize(Property)+":"+JsonSerializer.Serialize(Wrap(AutoKey));
        Assert.Null(BitwardenProfileSnapshot.Digest(Encoding.UTF8.GetBytes("{"+field+","+field+"}"),Token,Account));
        Assert.Null(BitwardenProfileSnapshot.Digest(Encoding.UTF8.GetBytes("{"+field+"}"),"not-a-session",Account));
    }
    [Theory][InlineData(16)][InlineData(32)][InlineData(65)]
    public void Unsupported_key_lengths_disable_the_fast_path(int size)
    {
        var profile=JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,string>{ [Property]=Wrap(new byte[size]) });
        Assert.Null(BitwardenProfileSnapshot.Digest(profile,Token,Account));
    }
    [Fact] public async Task A_busy_owned_profile_does_not_bypass_its_lock()
    {
        using var runner=new Runner();
        using var held=new FileStream(Path.Combine(runner.Root,".serverdesk.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        Assert.Equal(BitwardenFailure.AccountBusy,(await Assert.ThrowsAsync<BitwardenException>(()=>runner.ProfileSnapshotAsync(Token,Account,default))).Reason);
    }
}
