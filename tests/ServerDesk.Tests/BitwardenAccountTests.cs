using System.Text;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class BitwardenAccountTests
{
    private const string Master="synthetic-master-only",Otp="654321",Token="SESSION_CANARY_abcdefghijklmnop==";
    private sealed class Runner:IBitwardenAccountCommandRunner
    {
        public bool OwnsProfile { get; set; }=true;
        public string Server=>"https://vault.example.invalid";
        internal string State="unauthenticated",Actual="https://vault.example.invalid";
        internal Guid User=Guid.Parse("77058411-1bab-422f-bce3-d895e7891fdc");
        internal List<byte[]> Buffers=[];
        internal List<string[]> Calls=[];
        internal bool Block;
        private byte[] Bytes(string text) { var bytes=Encoding.UTF8.GetBytes(text); Buffers.Add(bytes); return bytes; }
        public Task<byte[]> RunAsync(IReadOnlyList<string> args,string? session,string? password,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add(args.ToArray());
            Assert.DoesNotContain(args,a=>a.Contains(Master)||a.Contains(Otp)||a.Contains(Token));
            return Task.FromResult(Bytes(args[0] switch {
                "status"=>JsonSerializer.Serialize(new { status=session!=null?"unlocked":State,userId=User,serverUrl=Actual }),
                "config"=>Configure(args),
                "unlock"=>password==Master?Token:throw new BitwardenException(BitwardenFailure.Locked),
                "logout"=>LoggedOut(),
                "sync"=>"sync complete",
                _=>throw new Exception("Unexpected account operation") }));
        }
        private string Configure(IReadOnlyList<string> args) { Assert.Equal("unauthenticated",State); Actual=args[2]; return Actual; }
        private string LoggedOut() { State="unauthenticated"; return "logged out"; }
        public async Task<byte[]> LoginAsync(BitwardenLogin login,Func<BitwardenChallenge,CancellationToken,Task<string>> challenge,CancellationToken ct)
        {
            Calls.Add(["login"]); Assert.Equal(Master,login.MasterPassword);
            if(Block)await Task.Delay(Timeout.Infinite,ct);
            Assert.Equal(Otp,await challenge(BitwardenChallenge.AuthenticatorCode,ct));
            ct.ThrowIfCancellationRequested(); State="locked"; return Bytes(Token);
        }
    }
    [Fact] public void Managed_options_are_backward_compatible_and_reject_insecure_remote_authentication()
    {
        new BitwardenCliOptions().Validate();
        var old=JsonSerializer.Deserialize<BitwardenCliOptions>("{\"Executable\":\"C:\\\\Apps\\\\bw.exe\",\"DataDirectory\":\"\"}")!;
        Assert.False(old.UsesBundledClient); Assert.Equal("",old.Server); Assert.Equal("",old.Email);
        Assert.True(new BitwardenCliOptions().UsesBundledClient);
        Assert.Throws<ArgumentException>(()=>new BitwardenCliOptions(Server:"http://192.0.2.20").Validate());
        Assert.Throws<ArgumentException>(()=>new BitwardenCliOptions(Server:"http://127.0.0.1:12345").Validate());
        new BitwardenCliOptions(Server:"https://vault.example.invalid/base",Email:"fixture@example.invalid").Validate();
        Assert.Throws<ArgumentException>(()=>new BitwardenCliOptions(Server:"https://u:p@vault.example.invalid").Validate());
        Assert.Throws<ArgumentException>(()=>new BitwardenCliOptions(Email:"bad\nemail").Validate());
    }
    [Fact] public void Managed_profiles_are_confined_and_separated_by_server_and_email_without_paths_from_input()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-profile-fixture");
        var one=BitwardenRuntime.ProfileDirectory(root,new(Server:"https://vault.example.invalid",Email:"one@example.invalid"));
        Assert.Equal(one,BitwardenRuntime.ProfileDirectory(root,new(Server:"https://vault.example.invalid/",Email:"ONE@example.invalid")));
        Assert.NotEqual(one,BitwardenRuntime.ProfileDirectory(root,new(Server:"https://vault.example.invalid",Email:"two@example.invalid")));
        Assert.NotEqual(one,BitwardenRuntime.ProfileDirectory(root,new(Server:"https://other.example.invalid",Email:"one@example.invalid")));
        Assert.Equal("bitwarden-accounts",Path.GetFileName(Path.GetDirectoryName(one))); Assert.Equal(64,Path.GetFileName(one).Length);
    }
    [Fact] public async Task GUI_login_configures_only_owned_unauthenticated_profile_and_zeroes_responses()
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner);
        await client.PrepareAccountAsync(default); Assert.True(client.CanConfigureAccount);
        await client.LoginAsync(new("fixture@example.invalid",Master),(_,_)=>Task.FromResult(Otp),default);
        Assert.True(client.IsUnlocked); await client.SyncAsync(default);
        await client.LogoutAccountAsync(default); Assert.False(client.IsUnlocked);
        Assert.Contains(runner.Calls,c=>c[0]=="config"&&c[2]==runner.Server);
        Assert.All(runner.Buffers,b=>Assert.All(b,value=>Assert.Equal(0,value)));
        Assert.DoesNotContain(Master,new BitwardenLogin("fixture@example.invalid",Master).ToString());
    }
    [Fact] public async Task Authenticated_wrong_server_is_never_reconfigured_and_shared_profiles_are_not_logged_out()
    {
        var runner=new Runner { State="locked",Actual="https://other.example.invalid" }; using var client=new BitwardenCli(runner);
        var failure=await Assert.ThrowsAsync<BitwardenException>(()=>client.PrepareAccountAsync(default));
        Assert.Equal(BitwardenFailure.AccountChanged,failure.Reason); Assert.DoesNotContain(runner.Calls,c=>c[0]=="config");
        runner.OwnsProfile=false;
        await Assert.ThrowsAsync<BitwardenException>(()=>client.LogoutAccountAsync(default)); Assert.DoesNotContain(runner.Calls,c=>c[0]=="logout");
    }
    [Fact] public async Task Bound_account_identity_is_checked_before_any_item_or_unlock_secret_is_sent()
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner);
        var reference=new BitwardenReference(Guid.NewGuid(),Guid.NewGuid(),runner.Server,"Fixture");
        var failure=await Assert.ThrowsAsync<BitwardenException>(()=>client.LoginAsync(new("fixture@example.invalid",Master),(_,_)=>Task.FromResult(Otp),default,reference));
        Assert.Equal(BitwardenFailure.AccountChanged,failure.Reason); Assert.False(client.IsUnlocked);
        Assert.DoesNotContain(runner.Calls,c=>c[0] is "unlock" or "get");
    }
    [Fact] public async Task Closing_account_login_cancels_owned_work_without_retaining_unlock_state()
    {
        var runner=new Runner { Block=true }; var client=new BitwardenCli(runner);
        var task=client.LoginAsync(new("fixture@example.invalid",Master),(_,_)=>Task.FromResult(Otp),default);
        client.Dispose(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task); Assert.False(client.IsUnlocked);
    }
    [Theory][InlineData("bad","valid",0)][InlineData("--email@example.invalid","valid",0)][InlineData("fixture@example.invalid","bad\nline",0)][InlineData("fixture@example.invalid","valid",9)]
    public void GUI_credentials_reject_invalid_input_without_exposing_values(string email,string password,int method)
    {
        Assert.Throws<ArgumentException>(()=>new BitwardenLogin(email,password,method).Validate());
    }
    [Fact] public async Task Missing_or_corrupt_bundled_executables_are_rejected_before_process_start()
    {
        var file=Path.Combine(Path.GetTempPath(),"serverdesk-component-fixture-"+Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(BitwardenFailure.ComponentUnavailable,(await Assert.ThrowsAsync<BitwardenException>(()=>BitwardenRuntime.VerifyAsync(file,default))).Reason);
            await File.WriteAllTextAsync(file,"not a Bitwarden executable");
            Assert.Equal(BitwardenFailure.ComponentUnavailable,(await Assert.ThrowsAsync<BitwardenException>(()=>BitwardenRuntime.VerifyAsync(file,default))).Reason);
        }
        finally { if(File.Exists(file))File.Delete(file); }
    }
    [Fact] public void Completed_prompt_redraw_does_not_hide_another_challenge_in_the_same_read()
    {
        var text="Two-step login code: [completed]\nNew device verification required. Enter OTP sent to login email:";
        var answered=new HashSet<BitwardenChallenge> { BitwardenChallenge.AuthenticatorCode };
        Assert.Equal(BitwardenChallenge.EmailCode,BitwardenProcessRunner.NextChallenge(text,answered));
        answered.Add(BitwardenChallenge.EmailCode);
        Assert.Null(BitwardenProcessRunner.NextChallenge(text,answered));
        Assert.Equal(BitwardenChallenge.ApiSecret,BitwardenProcessRunner.NextChallenge(text+"\nclient_secret:",answered));
        Assert.Null(BitwardenProcessRunner.NextChallenge("Unknown question:",answered));
    }
}
