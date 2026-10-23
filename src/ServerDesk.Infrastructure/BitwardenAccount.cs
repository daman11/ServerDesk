using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public enum BitwardenChallenge { AuthenticatorCode,EmailCode,ApiSecret }
public sealed class BitwardenLogin(string email,string masterPassword,int method=0)
{
    public string Email { get; }=email;
    public string MasterPassword { get; }=masterPassword;
    public int Method { get; }=method;
    public override string ToString()=>"BitwardenLogin [redacted]";
    public void Validate()
    {
        if(Email==null || MasterPassword==null || Method is not (0 or 1 or 3) || Email.Length is <3 or >254 || !Email.Contains('@') || Email.StartsWith('-') || Email.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)) ||
            MasterPassword.Length is <1 or >4096 || MasterPassword.Any(c=>c is '\r' or '\n' or '\0'))
            throw new ArgumentException(L.T("Sprawdź e-mail i dane logowania do sejfu."));
    }
}
public interface IBitwardenAccountCommandRunner:IBitwardenCommandRunner
{
    bool OwnsProfile { get; }
    string Server { get; }
    Task<byte[]> LoginAsync(BitwardenLogin login,Func<BitwardenChallenge,CancellationToken,Task<string>> challenge,CancellationToken ct);
}
public sealed partial class BitwardenCli
{
    public bool CanConfigureAccount=>runner is IBitwardenAccountCommandRunner { OwnsProfile:true };
    public string? ConfiguredServer=>(runner as IBitwardenAccountCommandRunner)?.Server;
    public async Task PrepareAccountAsync(CancellationToken ct)
    {
        if(!CanConfigureAccount)return;
        using var json=await JsonAsync(["status"],null,ct);
        await ConfigureAccountAsync(json.RootElement,ct);
    }
    public async Task<BitwardenAccount> PreparedStatusAsync(CancellationToken ct)
    {
        if(!CanConfigureAccount)return await StatusAsync(ct);
        using var json=await JsonAsync(["status"],null,ct);
        await ConfigureAccountAsync(json.RootElement,ct);
        return ReadAccount(json.RootElement);
    }
    private async Task ConfigureAccountAsync(JsonElement root,CancellationToken ct)
    {
        if(Text(root,"status",32)=="unauthenticated")
        {
            var bytes=await runner.RunAsync(["config","server",ConfiguredServer!,"--nointeraction"],null,null,ct);
            CryptographicOperations.ZeroMemory(bytes);
        }
        else if(BitwardenReference.CanonicalServer(Text(root,"serverUrl",2048))!=ConfiguredServer)
            throw new BitwardenException(BitwardenFailure.AccountChanged);
    }
    public async Task LoginAsync(BitwardenLogin login,Func<BitwardenChallenge,CancellationToken,Task<string>> challenge,CancellationToken ct,BitwardenReference? expected=null)
    {
        login.Validate(); Lock();
        if(runner is not IBitwardenAccountCommandRunner { OwnsProfile:true } accountRunner)throw new BitwardenException(BitwardenFailure.Configuration);
        if(expected!=null && expected.Server!=accountRunner.Server)throw new BitwardenException(BitwardenFailure.AccountChanged);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);
        await PrepareAccountAsync(linked.Token);
        using(var status=await JsonAsync(["status"],null,linked.Token))
            if(Text(status.RootElement,"status",32)!="unauthenticated")throw new BitwardenException(BitwardenFailure.AccountChanged);
        var bytes=await accountRunner.LoginAsync(login,challenge,linked.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var token=Encoding.UTF8.GetString(bytes).Trim();
            if(token.Length is <16 or >4096 || token.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '+' or '/' or '=' or '-' or '_')))throw new BitwardenException(BitwardenFailure.InvalidResponse);
            session=token;
            var actual=await StatusAsync(linked.Token);
            if(actual.State!="unlocked" || actual.Server!=accountRunner.Server || expected!=null && (actual.Id!=expected.AccountId || actual.Server!=expected.Server))throw new BitwardenException(BitwardenFailure.AccountChanged);
            account=actual;
        }
        catch { Lock(); throw; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public async Task LogoutAccountAsync(CancellationToken ct)
    {
        if(!CanConfigureAccount)throw new BitwardenException(BitwardenFailure.Configuration);
        Lock(); using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);
        var bytes=await runner.RunAsync(["logout","--nointeraction"],null,null,linked.Token);
        CryptographicOperations.ZeroMemory(bytes);
    }
    public async Task SyncAsync(CancellationToken ct)
    {
        metadata.Clear();
        await CheckAccountAsync(null,ct); using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);
        var bytes=await runner.RunAsync(["sync","--nointeraction"],session,null,linked.Token);
        CryptographicOperations.ZeroMemory(bytes);
    }
}
public sealed partial class BitwardenProcessRunner
{
    public Task<byte[]> LoginAsync(BitwardenLogin login,Func<BitwardenChallenge,CancellationToken,Task<string>> challenge,CancellationToken ct)
    {
        login.Validate(); if(!managed)throw new BitwardenException(BitwardenFailure.Configuration);
        string[] args=["login",login.Email,"--passwordenv","SERVERDESK_BW_PASSWORD","--method",login.Method.ToString(System.Globalization.CultureInfo.InvariantCulture),"--raw"];
        return RunCoreAsync(args,null,login.MasterPassword,challenge,ct);
    }
    private static FileStream LockProfile(string directory)
    {
        try { return new(Path.Combine(directory,".serverdesk.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
        catch(IOException) { throw new BitwardenException(BitwardenFailure.AccountBusy); }
    }
    internal static BitwardenChallenge? NextChallenge(string text,IReadOnlySet<BitwardenChallenge> answered)
    {
        // A completed prompt may be redrawn in the same read as a new challenge.
        // Never let the old label hide the next unhandled question.
        foreach(var (label,kind) in new[]{
            ("Two-step login code:",BitwardenChallenge.AuthenticatorCode),
            ("New device verification required. Enter OTP sent to login email:",BitwardenChallenge.EmailCode),
            ("client_secret:",BitwardenChallenge.ApiSecret)})
            if(!answered.Contains(kind) && text.Contains(label,StringComparison.Ordinal))return kind;
        return null;
    }
    private static async Task<byte[]> ReadChallengesAsync(System.Diagnostics.Process process,Func<BitwardenChallenge,CancellationToken,Task<string>> challenge,CancellationToken ct)
    {
        var buffer=new char[1024]; var pending=new StringBuilder(); var total=0; var prompts=new HashSet<BitwardenChallenge>();
        try
        {
            int count;
            while((count=await process.StandardError.ReadAsync(buffer,ct).ConfigureAwait(false))>0)
            {
                total+=count; if(total>65536)throw new BitwardenException(BitwardenFailure.InvalidResponse);
                pending.Append(buffer,0,count);
                var clean=Regex.Replace(pending.ToString(),@"\x1B\[[0-?]*[ -/]*[@-~]","",RegexOptions.NonBacktracking);
                var kind=NextChallenge(clean,prompts);
                if(kind is {} prompt)
                {
                    pending.Clear();
                    // Inquirer redraws the same question when it accepts a line.
                    // A CLI login attempt asks each supported challenge at most once.
                    if(!prompts.Add(prompt))continue;
                    var answer=await challenge(prompt,ct).ConfigureAwait(false);
                    if(answer.Length is <1 or >4096 || answer.Any(char.IsControl))throw new BitwardenException(BitwardenFailure.CommandFailed);
                    await process.StandardInput.WriteLineAsync(answer.AsMemory(),ct).ConfigureAwait(false); await process.StandardInput.FlushAsync(ct).ConfigureAwait(false); answer="";
                }
                else if(pending.Length>8192)pending.Remove(0,pending.Length-4096);
                Array.Clear(buffer);
            }
            return [];
        }
        finally { Array.Clear(buffer); pending.Clear(); }
    }
}
