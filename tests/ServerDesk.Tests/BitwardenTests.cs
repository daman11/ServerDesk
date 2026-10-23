using System.Text;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class BitwardenTests
{
    private static readonly Guid Account=Guid.Parse("4b0d9831-488a-41e2-9577-d3b091f0e70a");
    private static readonly Guid Item=Guid.Parse("230caa7f-62da-4379-8d7f-798e77ac3e0a");
    private const string Password="fixture-password-canary-ąć";
    private static BitwardenReference Reference=>new(Item,Account,"https://vault.example.invalid","Fixture server");
    private sealed class Runner:IBitwardenCommandRunner
    {
        internal Guid User=Account;
        internal string? Server="https://vault.example.invalid/";
        internal string State="locked";
        internal string? ListOverride,ItemOverride;
        internal readonly List<string[]> Calls=[];
        internal readonly List<byte[]> Buffers=[];
        internal bool Block;
        public async Task<byte[]> RunAsync(IReadOnlyList<string> arguments,string? session,string? masterPassword,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); var args=arguments.ToArray(); Calls.Add(args);
            Assert.DoesNotContain(args,a=>a.Contains(Password,StringComparison.Ordinal)||a.Contains("SESSION_CANARY",StringComparison.Ordinal));
            if(Block)await Task.Delay(Timeout.Infinite,ct);
            var response=args[0] switch
            {
                "status"=>JsonSerializer.Serialize(new { status=session!=null&&State=="locked"?"unlocked":State,userId=User,serverUrl=Server }),
                "unlock"=>masterPassword=="synthetic-master"?"SESSION_CANARY_abcdefghijklmnop==":throw new BitwardenException(BitwardenFailure.CommandFailed),
                "list"=>ListOverride??"["+ItemJson()+"]",
                "get"=>ItemOverride??ItemJson(),
                "sync"=>"synced",
                _=>throw new Exception("Unexpected vault mutation")
            };
            if(args[0] is "list" or "get")Assert.Equal("SESSION_CANARY_abcdefghijklmnop==",session);
            var bytes=Encoding.UTF8.GetBytes(response); Buffers.Add(bytes); return bytes;
        }
        internal static string ItemJson(Guid? id=null,int type=1)=>JsonSerializer.Serialize(new { id=id??Item,type,name="Fixture server",deletedDate=(string?)null,login=new { username="fixture",password=Password,totp="fixture-totp-canary" },notes="fixture-private-note" });
    }
    [Fact] public async Task Search_retains_only_metadata_and_exact_selection_returns_one_password()
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner);
        Assert.False(client.IsUnlocked); await client.UnlockAsync("synthetic-master",default); Assert.True(client.IsUnlocked);
        var entry=Assert.Single(await client.SearchAsync("server",default)); Assert.Equal(Reference,entry.Reference); Assert.Equal("fixture",entry.Username);
        var serialized=JsonSerializer.Serialize(entry); Assert.DoesNotContain(Password,serialized); Assert.DoesNotContain("fixture-totp-canary",serialized); Assert.DoesNotContain("fixture-private-note",serialized);
        var secret=await client.GetPasswordAsync(entry.Reference,default); Assert.Equal(Password,secret.Password); Assert.DoesNotContain(Password,secret.ToString());
        Assert.Contains(runner.Calls,a=>a.SequenceEqual(new[]{"get","item",Item.ToString("D"),"--nointeraction"}));
        Assert.All(runner.Buffers,b=>Assert.All(b,value=>Assert.Equal(0,value)));
        client.Lock(); Assert.False(client.IsUnlocked); var error=await Assert.ThrowsAsync<BitwardenException>(()=>client.SearchAsync("server",default)); Assert.Equal(BitwardenFailure.Locked,error.Reason);
        Assert.DoesNotContain(runner.Calls,a=>a[0] is "lock" or "logout" or "edit" or "create" or "delete");
    }
    [Fact] public async Task Account_mismatch_blocks_unlock_and_password_access_without_looking_up_an_item()
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner);
        var wrong=Reference with { AccountId=Guid.NewGuid() };
        var error=await Assert.ThrowsAsync<BitwardenException>(()=>client.UnlockAsync("synthetic-master",default,wrong)); Assert.Equal(BitwardenFailure.AccountChanged,error.Reason); Assert.DoesNotContain(runner.Calls,a=>a[0]=="unlock");
        await client.UnlockAsync("synthetic-master",default,Reference); runner.Server="https://different.example.invalid";
        error=await Assert.ThrowsAsync<BitwardenException>(()=>client.GetPasswordAsync(Reference,default)); Assert.Equal(BitwardenFailure.AccountChanged,error.Reason); Assert.False(client.IsUnlocked); Assert.DoesNotContain(runner.Calls,a=>a[0]=="get");
    }
    [Fact] public async Task Metadata_cache_reuses_only_search_metadata_and_explicit_refresh_stays_fresh()
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        var first=Assert.Single(await client.SearchAsync("server",default)); var count=runner.Calls.Count(c=>c[0]=="list");
        Assert.Equal(first,Assert.Single(await client.SearchAsync("server",default,preferCachedMetadata:true)));
        Assert.Equal(count,runner.Calls.Count(c=>c[0]=="list")); Assert.DoesNotContain(runner.Calls,c=>c[0]=="get");
        runner.ListOverride="[]";
        Assert.Empty(await client.SearchAsync("server",default)); Assert.Equal(count+1,runner.Calls.Count(c=>c[0]=="list"));
        Assert.DoesNotContain(Password,JsonSerializer.Serialize(client.UnlockedAccount));
    }
    [Fact] public async Task Cached_search_still_checks_account_before_returning_any_metadata()
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        await client.SearchAsync("server",default); runner.User=Guid.NewGuid();
        Assert.Equal(BitwardenFailure.AccountChanged,(await Assert.ThrowsAsync<BitwardenException>(()=>client.SearchAsync("server",default,preferCachedMetadata:true))).Reason);
        Assert.False(client.IsUnlocked);
    }
    [Fact] public async Task Sync_lock_and_logout_invalidate_cached_search_metadata()
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        await client.SearchAsync("server",default); await client.SyncAsync(default); runner.ListOverride="[]";
        Assert.Empty(await client.SearchAsync("server",default,preferCachedMetadata:true));
        client.Lock(); runner.ListOverride="["+Runner.ItemJson()+"]"; await client.UnlockAsync("synthetic-master",default);
        Assert.Single(await client.SearchAsync("server",default,preferCachedMetadata:true));
        runner.State="unauthenticated";
        Assert.Equal(BitwardenFailure.LoginRequired,(await Assert.ThrowsAsync<BitwardenException>(()=>client.SearchAsync("server",default,preferCachedMetadata:true))).Reason);
        Assert.False(client.IsUnlocked);
    }
    [Fact] public async Task Logged_out_accounts_and_failed_unlocks_are_safe_and_do_not_include_raw_output()
    {
        var runner=new Runner { State="unauthenticated" }; using var client=new BitwardenCli(runner);
        var error=await Assert.ThrowsAsync<BitwardenException>(()=>client.UnlockAsync("synthetic-master",default)); Assert.Equal(BitwardenFailure.LoginRequired,error.Reason); Assert.False(client.IsUnlocked);
        runner.State="locked"; error=await Assert.ThrowsAsync<BitwardenException>(()=>client.UnlockAsync(Password,default)); Assert.Equal(BitwardenFailure.CommandFailed,error.Reason); Assert.DoesNotContain(Password,error.ToString()); Assert.False(client.IsUnlocked);
    }
    [Fact] public async Task Missing_cloud_base_URL_is_not_guessed_or_bound_to_the_wrong_region()
    {
        var runner=new Runner { Server=null }; using var client=new BitwardenCli(runner);
        var error=await Assert.ThrowsAsync<BitwardenException>(()=>client.UnlockAsync("synthetic-master",default));
        Assert.Equal(BitwardenFailure.ServerNotIdentified,error.Reason); Assert.DoesNotContain(runner.Calls,a=>a[0]=="unlock"); Assert.False(client.IsUnlocked);
    }
    [Theory][InlineData("{")][InlineData("null")][InlineData("{}")][InlineData("[{\"type\":1}]")]
    public async Task Malformed_search_responses_are_rejected(string data)
    {
        var runner=new Runner { ListOverride=data }; using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        var error=await Assert.ThrowsAsync<BitwardenException>(()=>client.SearchAsync("server",default)); Assert.Equal(BitwardenFailure.InvalidResponse,error.Reason); Assert.DoesNotContain(Password,error.ToString());
    }
    [Theory][InlineData("x")][InlineData("--session evil")][InlineData("server\nother")]
    public async Task Invalid_queries_do_not_launch_cli(string query)
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner); await Assert.ThrowsAsync<ArgumentException>(()=>client.SearchAsync(query,default)); Assert.Empty(runner.Calls);
    }
    [Fact] public async Task Non_login_and_wrong_ID_items_never_return_passwords()
    {
        var runner=new Runner(); using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        runner.ItemOverride=Runner.ItemJson(Guid.NewGuid()); var error=await Assert.ThrowsAsync<BitwardenException>(()=>client.GetPasswordAsync(Reference,default)); Assert.Equal(BitwardenFailure.NoPassword,error.Reason);
        runner.ItemOverride=Runner.ItemJson(type:2); error=await Assert.ThrowsAsync<BitwardenException>(()=>client.GetPasswordAsync(Reference,default)); Assert.Equal(BitwardenFailure.NoPassword,error.Reason);
        runner.ListOverride="["+Runner.ItemJson(type:2)+"]"; Assert.Empty(await client.SearchAsync("server",default));
    }
    [Fact] public async Task Duplicate_names_remain_selectable_but_duplicate_IDs_are_rejected()
    {
        var runner=new Runner { ListOverride="["+Runner.ItemJson()+","+Runner.ItemJson(Guid.NewGuid())+"]" }; using var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default);
        Assert.Equal(2,(await client.SearchAsync("server",default)).Count);
        runner.ListOverride="["+Runner.ItemJson()+","+Runner.ItemJson()+"]"; await Assert.ThrowsAsync<BitwardenException>(()=>client.SearchAsync("server",default));
    }
    [Fact] public async Task Disposal_cancels_pending_optional_work_and_forgets_session()
    {
        var runner=new Runner(); var client=new BitwardenCli(runner); await client.UnlockAsync("synthetic-master",default); runner.Block=true;
        var work=client.SearchAsync("server",default); client.Dispose(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>work); Assert.False(client.IsUnlocked); client.Dispose();
    }
    [Fact] public async Task References_persist_across_restart_but_not_configuration_export_or_import()
    {
        var root=Path.Combine(Path.GetTempPath(),"ServerDesk-bitwarden-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repo=new ProfileRepository(new(root)); await repo.InitializeAsync();
            var original=await repo.SaveEditedProfileAsync(null,new() { Name="Fixture",Host="example.invalid",Username="fixture" });
            var linked=await repo.SaveEditedProfileAsync(original,original with { Bitwarden=Reference }); Assert.NotEqual(original.CredentialId,linked.CredentialId);
            var reopened=new ProfileRepository(new(root)); await reopened.InitializeAsync(); Assert.Equal(Reference,Assert.Single(await reopened.ListAsync()).Bitwarden);
            using var export=new MemoryStream(); await reopened.ExportAsync(export); var json=Encoding.UTF8.GetString(export.ToArray()); Assert.DoesNotContain("Bitwarden",json); Assert.DoesNotContain(Reference.ItemId.ToString(),json);
            using var import=new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Version=2,Servers=new[]{linked},Folders=Array.Empty<string>() }))); await reopened.ImportAsync(import);
            Assert.Null((await reopened.ListAsync()).Single(p=>p.Id!=linked.Id).Bitwarden); Assert.Equal(Reference,(await reopened.ListAsync()).Single(p=>p.Id==linked.Id).Bitwarden);
            var removed=await reopened.SaveEditedProfileAsync(linked,linked with { Bitwarden=null }); Assert.NotEqual(linked.CredentialId,removed.CredentialId); Assert.Null(removed.Bitwarden);
        }
        finally { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
    }
    [Theory][InlineData("bw")][InlineData("bw.exe --session secret")][InlineData("../bw")]
    public void Cli_configuration_requires_explicit_absolute_paths(string path)=>Assert.Throws<ArgumentException>(()=>new BitwardenCliOptions(path).Validate());
    [Theory][InlineData("/usr/bin/bw")][InlineData("C:\\Apps\\Bitwarden\\bw.exe")]
    public void Saved_other_platform_paths_do_not_invalidate_all_preferences(string path)
    {
        var preferences=new ApplicationPreferences { Bitwarden=new(path) }; preferences.Validate();
        Assert.Equal(preferences,JsonSerializer.Deserialize<ApplicationPreferences>(JsonSerializer.Serialize(preferences)));
    }
    [Fact] public void References_reject_empty_ids_and_ambiguous_server_urls()
    {
        Assert.Throws<ArgumentException>(()=>(Reference with { AccountId=Guid.Empty }).Validate());
        Assert.Throws<ArgumentException>(()=>(Reference with { Server="https://vault.example.invalid/" }).Validate());
        Assert.Throws<ArgumentException>(()=>BitwardenReference.CanonicalServer("https://user:pass@vault.example.invalid"));
        Assert.Throws<ArgumentException>(()=>BitwardenReference.CanonicalServer("https://vault.example.invalid/?token=secret"));
        Assert.Equal("https://vault.example.invalid",BitwardenReference.CanonicalServer("https://VAULT.example.invalid/"));
    }
}
