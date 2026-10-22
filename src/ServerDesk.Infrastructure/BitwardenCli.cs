using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public enum BitwardenFailure { Configuration,LoginRequired,Locked,AccountChanged,InvalidResponse,CommandFailed,Timeout,NoPassword,ServerNotIdentified }
public sealed class BitwardenException(BitwardenFailure reason):Exception(reason switch
{
    BitwardenFailure.Configuration=>L.T("Wskaż zainstalowany program Bitwarden CLI w Ustawienia → Pluginy."),
    BitwardenFailure.ServerNotIdentified=>L.T("CLI nie podaje adresu sejfu. Skonfiguruj jawny adres serwera przed zalogowaniem, najlepiej w osobnym profilu CLI. Zobacz Pomoc → Bitwarden / Vaultwarden."),
    BitwardenFailure.LoginRequired=>L.T("Najpierw zaloguj konto w Bitwarden CLI. Instrukcja jest w Pomoc → Bitwarden / Vaultwarden."),
    BitwardenFailure.Locked=>L.T("Odblokuj sejf hasłem głównym."),
    BitwardenFailure.AccountChanged=>L.T("Konto lub serwer sejfu jest inne niż w przypisaniu. Wybierz właściwe konto albo przypisz wpis ponownie."),
    BitwardenFailure.InvalidResponse=>L.T("Bitwarden CLI zwrócił nieprawidłowe lub zbyt duże dane."),
    BitwardenFailure.Timeout=>L.T("Bitwarden CLI nie odpowiedział na czas. Sprawdź dostępność serwera sejfu."),
    BitwardenFailure.NoPassword=>L.T("Wybrany wpis nie zawiera hasła logowania albo został usunięty."),
    _=>L.T("Bitwarden CLI odrzucił operację. Sprawdź hasło główne, stan konta i połączenie z sejfem.")
})
{ public BitwardenFailure Reason { get; }=reason; }

// Only metadata leaves a search operation. Passwords are fetched for one exact ID.
public sealed record BitwardenEntry(BitwardenReference Reference,string Username);
public sealed class BitwardenPassword(string username,string password)
{
    public string Username { get; }=username;
    public string Password { get; }=password;
    public override string ToString()=>"BitwardenPassword [redacted]";
}
public sealed record BitwardenAccount(Guid Id,string Server,string State);

public interface IBitwardenCommandRunner
{
    Task<byte[]> RunAsync(IReadOnlyList<string> arguments,string? session,string? masterPassword,CancellationToken ct);
}

/// <summary>One short-lived picker session. No plaintext persistence or HTTP listener.</summary>
public sealed class BitwardenCli(IBitwardenCommandRunner runner):IDisposable
{
    private readonly CancellationTokenSource lifetime=new();
    private string? session;
    private BitwardenAccount? account;
    private int disposed;
    public bool IsUnlocked=>session!=null && !lifetime.IsCancellationRequested;
    public static BitwardenCli Create(BitwardenCliOptions options)=>new(new BitwardenProcessRunner(options));
    public async Task<BitwardenAccount> StatusAsync(CancellationToken ct)
    {
        var result=await JsonAsync(["status"],null,ct);
        using(result)
        {
            try
            {
                var root=result.RootElement; var state=Text(root,"status",32);
                if(state=="unauthenticated")throw new BitwardenException(BitwardenFailure.LoginRequired);
                if(state is not ("locked" or "unlocked") || !Guid.TryParse(Text(root,"userId",64),out var id) || id==Guid.Empty)throw new BitwardenException(BitwardenFailure.InvalidResponse);
                var server=Text(root,"serverUrl",2048,true);
                if(server.Length==0)throw new BitwardenException(BitwardenFailure.ServerNotIdentified);
                return new(id,BitwardenReference.CanonicalServer(server),state);
            }
            catch(Exception ex) when(ex is JsonException or InvalidOperationException or ArgumentException) { throw new BitwardenException(BitwardenFailure.InvalidResponse); }
        }
    }
    public async Task UnlockAsync(string masterPassword,CancellationToken ct,BitwardenReference? expectedReference=null)
    {
        Lock(); var expected=await StatusAsync(ct);
        if(expectedReference!=null && (expectedReference.AccountId!=expected.Id || expectedReference.Server!=expected.Server))throw new BitwardenException(BitwardenFailure.AccountChanged);
        if(string.IsNullOrEmpty(masterPassword) || Encoding.UTF8.GetByteCount(masterPassword)>CredentialLimits.MaximumSecretBytes)throw new BitwardenException(BitwardenFailure.Locked);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);
        var bytes=await runner.RunAsync(["unlock","--passwordenv","SERVERDESK_BW_PASSWORD","--raw","--nointeraction"],null,masterPassword,linked.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var token=Encoding.UTF8.GetString(bytes).Trim();
            if(token.Length is <16 or >4096 || token.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '+' or '/' or '=' or '-' or '_')))throw new BitwardenException(BitwardenFailure.InvalidResponse);
            session=token; account=expected;
            await CheckAccountAsync(null,ct);
        }
        catch { Lock(); throw; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public async Task<IReadOnlyList<BitwardenEntry>> SearchAsync(string query,CancellationToken ct)
    {
        if(query==null || query.Trim().Length is <2 or >200 || query.TrimStart().StartsWith('-') || query.Any(char.IsControl))throw new ArgumentException(L.T("Wpisz co najmniej 2 znaki nazwy wpisu lub adresu."));
        await CheckAccountAsync(null,ct);
        using var json=await JsonAsync(["list","items","--search",query.Trim()],null,ct);
        try
        {
            if(json.RootElement.ValueKind!=JsonValueKind.Array || json.RootElement.GetArrayLength()>2000)throw new BitwardenException(BitwardenFailure.InvalidResponse);
            var result=new List<BitwardenEntry>(); var ids=new HashSet<Guid>();
            foreach(var item in json.RootElement.EnumerateArray())
            {
                if(item.GetProperty("type").GetInt32()!=1 || item.TryGetProperty("deletedDate",out var deleted) && deleted.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))continue;
                if(!Guid.TryParse(Text(item,"id",64),out var id) || id==Guid.Empty || !ids.Add(id))throw new BitwardenException(BitwardenFailure.InvalidResponse);
                var name=Text(item,"name",200); var user=Text(item.GetProperty("login"),"username",1024,true);
                var reference=new BitwardenReference(id,account!.Id,account.Server,name); reference.Validate();
                if(result.Count<100)result.Add(new(reference,user));
            }
            return result;
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException) { throw new BitwardenException(BitwardenFailure.InvalidResponse); }
    }
    public async Task<BitwardenPassword> GetPasswordAsync(BitwardenReference reference,CancellationToken ct)
    {
        reference.Validate(); await CheckAccountAsync(reference,ct);
        using var json=await JsonAsync(["get","item",reference.ItemId.ToString("D")],null,ct);
        try
        {
            var root=json.RootElement;
            if(!Guid.TryParse(Text(root,"id",64),out var id) || id!=reference.ItemId || root.GetProperty("type").GetInt32()!=1 || root.TryGetProperty("deletedDate",out var deleted) && deleted.ValueKind!=JsonValueKind.Null)throw new BitwardenException(BitwardenFailure.NoPassword);
            var login=root.GetProperty("login"); var password=Text(login,"password",CredentialLimits.MaximumSecretBytes,true,true);
            if(password.Length==0 || password.Contains('\0') || Encoding.UTF8.GetByteCount(password)>CredentialLimits.MaximumSecretBytes)throw new BitwardenException(BitwardenFailure.NoPassword);
            await CheckAccountAsync(reference,ct);
            return new(Text(login,"username",1024,true),password);
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException) { throw new BitwardenException(BitwardenFailure.InvalidResponse); }
    }
    private async Task CheckAccountAsync(BitwardenReference? reference,CancellationToken ct)
    {
        if(!IsUnlocked || account==null)throw new BitwardenException(BitwardenFailure.Locked);
        var actual=await StatusAsync(ct);
        if(actual.Id!=account.Id || actual.Server!=account.Server || reference!=null && (reference.AccountId!=actual.Id || reference.Server!=actual.Server))
        { Lock(); throw new BitwardenException(BitwardenFailure.AccountChanged); }
        if(actual.State!="unlocked") { Lock(); throw new BitwardenException(BitwardenFailure.Locked); }
    }
    private async Task<JsonDocument> JsonAsync(string[] args,string? password,CancellationToken ct)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);
        var bytes=await runner.RunAsync([..args,"--nointeraction"],session,password,linked.Token);
        try { linked.Token.ThrowIfCancellationRequested(); using var input=new MemoryStream(bytes,false); return JsonDocument.Parse(input,new() { MaxDepth=32 }); }
        catch(JsonException) { throw new BitwardenException(BitwardenFailure.InvalidResponse); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private static string Text(JsonElement item,string name,int limit,bool optional=false,bool controls=false)
    {
        if(!item.TryGetProperty(name,out var property) || property.ValueKind==JsonValueKind.Null)
        { if(optional)return ""; throw new BitwardenException(BitwardenFailure.InvalidResponse); }
        if(property.ValueKind!=JsonValueKind.String)throw new BitwardenException(BitwardenFailure.InvalidResponse);
        var value=property.GetString()!;
        if(value.Length>limit || !controls && value.Any(char.IsControl))throw new BitwardenException(BitwardenFailure.InvalidResponse);
        return value;
    }
    // Do not run `bw lock/logout`: this may be a CLI profile shared with the user.
    public void Lock() { session=null; account=null; }
    public void Dispose() { if(Interlocked.Exchange(ref disposed,1)!=0)return; lifetime.Cancel(); Lock(); lifetime.Dispose(); }
}

public sealed class BitwardenProcessRunner:IBitwardenCommandRunner
{
    private readonly BitwardenCliOptions options;
    public BitwardenProcessRunner(BitwardenCliOptions options) { options.Validate(); this.options=options; }
    public async Task<byte[]> RunAsync(IReadOnlyList<string> arguments,string? session,string? masterPassword,CancellationToken ct)
    {
        if(options.Executable.Length==0 || !Path.IsPathFullyQualified(options.Executable) || !File.Exists(options.Executable) || OperatingSystem.IsWindows() && !options.Executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) || options.DataDirectory.Length>0 && (!Path.IsPathFullyQualified(options.DataDirectory) || !Directory.Exists(options.DataDirectory)))throw new BitwardenException(BitwardenFailure.Configuration);
        var start=new ProcessStartInfo(options.Executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(options.Executable)! };
        foreach(var key in start.Environment.Keys.Where(k=>k.StartsWith("BW_",StringComparison.OrdinalIgnoreCase) || k.StartsWith("BITWARDENCLI_",StringComparison.OrdinalIgnoreCase) || k is "NODE_OPTIONS" or "NODE_PATH" or "NODE_TLS_REJECT_UNAUTHORIZED" or "SERVERDESK_BW_PASSWORD").ToArray())start.Environment.Remove(key);
        if(options.DataDirectory.Length>0)start.Environment["BITWARDENCLI_APPDATA_DIR"]=options.DataDirectory;
        if(session!=null)start.Environment["BW_SESSION"]=session;
        if(masterPassword!=null)start.Environment["SERVERDESK_BW_PASSWORD"]=masterPassword;
        foreach(var arg in arguments)start.ArgumentList.Add(arg);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var process=new Process { StartInfo=start };
        byte[]? output=null; byte[]? error=null;
        try
        {
            ct.ThrowIfCancellationRequested(); if(!process.Start())throw new BitwardenException(BitwardenFailure.CommandFailed);
            process.StandardInput.Close();
            // Process.Start has copied the environment. Do not retain secrets in StartInfo.
            start.Environment.Remove("BW_SESSION"); start.Environment.Remove("SERVERDESK_BW_PASSWORD");
            using var cancelled=timeout.Token.Register(()=> { try { if(!process.HasExited)process.Kill(true); } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { } });
            async Task<byte[]> Read(Stream stream,int limit)
            { try { return await ReadOutputAsync(stream,limit,timeout.Token).ConfigureAwait(false); } catch { timeout.Cancel(); throw; } }
            var stdout=Read(process.StandardOutput.BaseStream,8*1024*1024); var stderr=Read(process.StandardError.BaseStream,64*1024);
            try { await Task.WhenAll(stdout,stderr,process.WaitForExitAsync(timeout.Token)).ConfigureAwait(false); }
            finally { if(stdout.IsCompletedSuccessfully)output=stdout.Result; if(stderr.IsCompletedSuccessfully)error=stderr.Result; }
            ct.ThrowIfCancellationRequested();
            if(process.ExitCode!=0)throw new BitwardenException(BitwardenFailure.CommandFailed);
            var result=output!; output=null; return result;
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) { throw new BitwardenException(BitwardenFailure.Timeout); }
        catch(InvalidDataException) { throw new BitwardenException(BitwardenFailure.InvalidResponse); }
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException) { throw new BitwardenException(BitwardenFailure.CommandFailed); }
        finally
        {
            start.Environment.Remove("BW_SESSION"); start.Environment.Remove("SERVERDESK_BW_PASSWORD");
            if(output!=null)CryptographicOperations.ZeroMemory(output); if(error!=null)CryptographicOperations.ZeroMemory(error);
        }
    }
    private static async Task<byte[]> ReadOutputAsync(Stream source,int maximum,CancellationToken ct)
    {
        using var target=new MemoryStream(); var buffer=new byte[65536];
        try
        {
            int count;
            while((count=await source.ReadAsync(buffer.AsMemory(0,Math.Min(buffer.Length,maximum-(int)target.Length+1)),ct).ConfigureAwait(false))>0)
            {
                if(target.Length+count>maximum)throw new InvalidDataException();
                target.Write(buffer,0,count);
            }
            return target.ToArray();
        }
        finally { CryptographicOperations.ZeroMemory(buffer); CryptographicOperations.ZeroMemory(target.GetBuffer()); }
    }
}
