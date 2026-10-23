using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public enum BitwardenFailure { Configuration,LoginRequired,Locked,AccountChanged,InvalidResponse,CommandFailed,Timeout,NoPassword,ServerNotIdentified,InvalidSshKey,EncryptedSshKey,ComponentUnavailable,AccountBusy }
public sealed class BitwardenException(BitwardenFailure reason):Exception(reason switch
{
    BitwardenFailure.InvalidSshKey=>L.T("Wpis nie zawiera poprawnego klucza SSH RSA lub Ed25519 albo dane klucza nie pasują do odcisku. Wybierz wpis typu SSH Key."),
    BitwardenFailure.EncryptedSshKey=>L.T("Klucz w sejfie wymaga dodatkowego hasła. Zaimportuj go funkcją importu klucza w Bitwarden, podając tam hasło klucza, i zsynchronizuj sejf."),
    BitwardenFailure.Configuration=>L.T("Sprawdź konfigurację sejfu w Ustawienia → Pluginy → Bitwarden / Vaultwarden."),
    BitwardenFailure.ComponentUnavailable=>L.T("Brakuje dołączonego komponentu sejfu lub jego pliki są uszkodzone. Zainstaluj ponownie pełny pakiet ServerDesk."),
    BitwardenFailure.AccountBusy=>L.T("Inna operacja używa tego konta sejfu. Poczekaj na jej zakończenie i spróbuj ponownie."),
    BitwardenFailure.ServerNotIdentified=>L.T("CLI nie podaje adresu sejfu. Skonfiguruj jawny adres serwera przed zalogowaniem, najlepiej w osobnym profilu CLI. Zobacz Pomoc → Bitwarden / Vaultwarden."),
    BitwardenFailure.LoginRequired=>L.T("Zaloguj konto w oknie sejfu. Konfiguracja jest w Ustawienia → Pluginy → Bitwarden / Vaultwarden."),
    BitwardenFailure.Locked=>L.T("Odblokuj sejf hasłem głównym."),
    BitwardenFailure.AccountChanged=>L.T("Konto lub serwer sejfu jest inne niż w przypisaniu. Wybierz właściwe konto albo przypisz wpis ponownie."),
    BitwardenFailure.InvalidResponse=>L.T("Bitwarden CLI zwrócił nieprawidłowe lub zbyt duże dane."),
    BitwardenFailure.Timeout=>L.T("Bitwarden CLI nie odpowiedział na czas. Sprawdź dostępność serwera sejfu."),
    BitwardenFailure.NoPassword=>L.T("Wybrany wpis nie zawiera hasła logowania albo został usunięty."),
    _=>L.T("Bitwarden CLI odrzucił operację. Sprawdź hasło główne, stan konta i połączenie z sejfem.")
})
{ public BitwardenFailure Reason { get; }=reason; }

// Only metadata leaves a search operation. Passwords are fetched for one exact ID.
public sealed record BitwardenEntry(BitwardenReference Reference,string Username,string Fingerprint="");
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
public sealed partial class BitwardenCli(IBitwardenCommandRunner runner):IDisposable
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
    public async Task<IReadOnlyList<BitwardenEntry>> SearchAsync(string query,CancellationToken ct,BitwardenItemKind kind=BitwardenItemKind.Login)
    {
        if(!Enum.IsDefined(kind))throw new BitwardenException(BitwardenFailure.InvalidResponse);
        if(query==null || query.Trim().Length is <2 or >200 || query.TrimStart().StartsWith('-') || query.Any(char.IsControl))throw new ArgumentException(L.T("Wpisz co najmniej 2 znaki nazwy wpisu lub adresu."));
        await CheckAccountAsync(null,ct);
        using var json=await JsonAsync(["list","items","--search",query.Trim()],null,ct);
        try
        {
            if(json.RootElement.ValueKind!=JsonValueKind.Array || json.RootElement.GetArrayLength()>2000)throw new BitwardenException(BitwardenFailure.InvalidResponse);
            var result=new List<BitwardenEntry>(); var ids=new HashSet<Guid>();
            foreach(var item in json.RootElement.EnumerateArray())
            {
                if(item.GetProperty("type").GetInt32()!=(int)kind || item.TryGetProperty("deletedDate",out var deleted) && deleted.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))continue;
                if(!Guid.TryParse(Text(item,"id",64),out var id) || id==Guid.Empty || !ids.Add(id))throw new BitwardenException(BitwardenFailure.InvalidResponse);
                var name=Text(item,"name",200); var user=kind==BitwardenItemKind.Login?Text(item.GetProperty("login"),"username",1024,true):"";
                var fingerprint=kind==BitwardenItemKind.SshKey?Text(item.GetProperty("sshKey"),"keyFingerprint",128,true):"";
                var reference=new BitwardenReference(id,account!.Id,account.Server,name,kind); reference.Validate();
                if(result.Count<100)result.Add(new(reference,user,fingerprint));
            }
            return result;
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException) { throw new BitwardenException(BitwardenFailure.InvalidResponse); }
    }
    public async Task<BitwardenPassword> GetPasswordAsync(BitwardenReference reference,CancellationToken ct)
    {
        reference.Validate(); if(reference.Kind!=BitwardenItemKind.Login)throw new BitwardenException(BitwardenFailure.NoPassword); await CheckAccountAsync(reference,ct);
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
    public async Task<SshKeyMaterial> GetSshKeyAsync(BitwardenReference reference,CancellationToken ct)
    {
        reference.Validate(); if(reference.Kind!=BitwardenItemKind.SshKey)throw new BitwardenException(BitwardenFailure.InvalidSshKey);
        await CheckAccountAsync(reference,ct);
        using var json=await JsonAsync(["get","item",reference.ItemId.ToString("D")],null,ct);
        SshKeyMaterial? material=null;
        try
        {
            var root=json.RootElement;
            if(!Guid.TryParse(Text(root,"id",64),out var id) || id!=reference.ItemId || root.GetProperty("type").GetInt32()!=5 || root.TryGetProperty("deletedDate",out var deleted) && deleted.ValueKind!=JsonValueKind.Null)throw new BitwardenException(BitwardenFailure.InvalidSshKey);
            var key=root.GetProperty("sshKey");
            material=await Task.Run(()=>SshKeyMaterial.Create(Text(key,"privateKey",SshKeyMaterial.MaximumBytes,false,true),Text(key,"publicKey",16384,true),Text(key,"keyFingerprint",128,true)),ct);
            await CheckAccountAsync(reference,ct); ct.ThrowIfCancellationRequested();
            var result=material; material=null; return result;
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException) { throw new BitwardenException(BitwardenFailure.InvalidResponse); }
        finally { material?.Dispose(); }
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

public sealed partial class BitwardenProcessRunner:IBitwardenAccountCommandRunner
{
    private readonly BitwardenCliOptions options;
    private readonly bool managed;
    public bool OwnsProfile=>managed;
    public string Server { get; }
    public BitwardenProcessRunner(BitwardenCliOptions options)
    {
        options.Validate(); managed=options.UsesBundledClient; Server=options.EffectiveServer;
        this.options=managed?BitwardenRuntime.Resolve(options):options;
    }
    public Task<byte[]> RunAsync(IReadOnlyList<string> arguments,string? session,string? masterPassword,CancellationToken ct)
        =>RunCoreAsync(arguments,session,masterPassword,null,ct);
    private async Task<byte[]> RunCoreAsync(IReadOnlyList<string> arguments,string? session,string? masterPassword,Func<BitwardenChallenge,CancellationToken,Task<string>>? challenge,CancellationToken ct)
    {
        if(managed)await BitwardenRuntime.VerifyAsync(options.Executable,ct).ConfigureAwait(false);
        if(options.Executable.Length==0 || !Path.IsPathFullyQualified(options.Executable) || !File.Exists(options.Executable) || OperatingSystem.IsWindows() && !options.Executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) || options.DataDirectory.Length>0 && (!Path.IsPathFullyQualified(options.DataDirectory) || !Directory.Exists(options.DataDirectory)))throw new BitwardenException(BitwardenFailure.Configuration);
        var start=new ProcessStartInfo(options.Executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(options.Executable)! };
        foreach(var key in start.Environment.Keys.Where(k=>k.StartsWith("BW_",StringComparison.OrdinalIgnoreCase) || k.StartsWith("BITWARDENCLI_",StringComparison.OrdinalIgnoreCase) || k is "NODE_OPTIONS" or "NODE_PATH" or "NODE_TLS_REJECT_UNAUTHORIZED" or "SERVERDESK_BW_PASSWORD").ToArray())start.Environment.Remove(key);
        if(options.DataDirectory.Length>0)start.Environment["BITWARDENCLI_APPDATA_DIR"]=options.DataDirectory;
        if(session!=null)start.Environment["BW_SESSION"]=session;
        if(masterPassword!=null)start.Environment["SERVERDESK_BW_PASSWORD"]=masterPassword;
        start.Environment["NO_COLOR"]="1"; start.Environment["FORCE_COLOR"]="0";
        foreach(var arg in arguments)start.ArgumentList.Add(arg);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(challenge==null?60:180));
        using var profileLock=managed?LockProfile(options.DataDirectory):null;
        using var process=new Process { StartInfo=start };
        byte[]? output=null; byte[]? error=null;
        try
        {
            ct.ThrowIfCancellationRequested(); if(!process.Start())throw new BitwardenException(BitwardenFailure.CommandFailed);
            if(challenge==null)process.StandardInput.Close();
            // Process.Start has copied the environment. Do not retain secrets in StartInfo.
            start.Environment.Remove("BW_SESSION"); start.Environment.Remove("SERVERDESK_BW_PASSWORD");
            start.Environment.Remove("BW_CLIENTID"); start.Environment.Remove("BW_CLIENTSECRET");
            using var cancelled=timeout.Token.Register(()=> { try { if(!process.HasExited)process.Kill(true); } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { } });
            async Task<byte[]> Read(Stream stream,int limit)
            { try { return await ReadOutputAsync(stream,limit,timeout.Token).ConfigureAwait(false); } catch { timeout.Cancel(); throw; } }
            var stdout=Read(process.StandardOutput.BaseStream,8*1024*1024);
            async Task<byte[]> Interactive()
            { try { return await ReadChallengesAsync(process,challenge!,timeout.Token).ConfigureAwait(false); } catch { timeout.Cancel(); throw; } }
            var stderr=challenge==null?Read(process.StandardError.BaseStream,64*1024):Interactive();
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
            start.Environment.Remove("BW_CLIENTID"); start.Environment.Remove("BW_CLIENTSECRET");
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
