using ServerDesk.Core;
using System.Diagnostics;
using System.Text;

namespace ServerDesk.Infrastructure;
public sealed class SecretServiceCredentialStore(string executable="secret-tool",TimeSpan? operationTimeout=null) : ICredentialStore
{
    public async Task<string?> GetAsync(Guid id,CancellationToken ct=default)
    {
        try { return await ExecuteAsync("lookup",id,null,ct); }
        catch(System.ComponentModel.Win32Exception) { return null; }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) { return null; }
        catch(Exception ex) when(ex is InvalidDataException or IOException) { throw new CredentialReadException(ex); }
    }
    public async Task SaveAsync(Guid id,string secret,CancellationToken ct=default)
    {
        CredentialLimits.Validate(secret);
        try { await ExecuteAsync("store",id,secret,ct); }
        catch(System.ComponentModel.Win32Exception) { throw Unavailable(); }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) { throw Unavailable(); }
        catch(Exception ex) when(ex is InvalidDataException or IOException) { throw Unavailable(); }
    }
    public async Task DeleteAsync(Guid id,CancellationToken ct=default)
    {
        try { await ExecuteAsync("clear",id,null,ct); }
        catch(System.ComponentModel.Win32Exception ex) { throw Unavailable(ex); }
        catch(OperationCanceledException ex) when(!ct.IsCancellationRequested) { throw Unavailable(ex); }
        catch(Exception ex) when(ex is InvalidDataException or IOException) { throw Unavailable(ex); }
    }
    private static InvalidOperationException Unavailable(Exception? cause=null)=>new(L.T("Magazyn Secret Service jest niedostępny lub zablokowany. Podaj hasło bez zapisywania albo odblokuj magazyn systemowy."),cause);
    private async Task<string?> ExecuteAsync(string action,Guid id,string? secret,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var start=new ProcessStartInfo(executable) { RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true };
        start.ArgumentList.Add(action); if(action=="store")start.ArgumentList.Add("--label=ServerDesk");
        start.ArgumentList.Add("application"); start.ArgumentList.Add("ServerDesk"); start.ArgumentList.Add("profile"); start.ArgumentList.Add(id.ToString());
        using var process=Process.Start(start)??throw Unavailable();
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(operationTimeout??TimeSpan.FromSeconds(30));
        InvalidDataException? overflow=null;
        async Task<string> ReadBounded(StreamReader reader,int limit)
        {
            var result=new StringBuilder(); var buffer=new char[4096];
            while(true)
            {
                var count=await reader.ReadAsync(buffer.AsMemory(),timeout.Token).ConfigureAwait(false);
                if(count==0)return result.ToString();
                if(result.Length+count>limit)
                {
                    var error=new InvalidDataException("Credential backend output exceeds its limit.");
                    Interlocked.CompareExchange(ref overflow,error,null); timeout.Cancel(); throw error;
                }
                result.Append(buffer,0,count);
            }
        }
        async Task WriteInput()
        {
            if(secret!=null)await process.StandardInput.WriteAsync(secret.AsMemory(),timeout.Token).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        try
        {
            // Drain both pipes concurrently with stdin, and abort on overflow before
            // a misbehaving backend can fill memory or deadlock on a full pipe.
            var output=ReadBounded(process.StandardOutput,CredentialLimits.MaximumSecretBytes);
            var error=ReadBounded(process.StandardError,8192);
            await Task.WhenAll(output,error,WriteInput(),process.WaitForExitAsync(timeout.Token)).ConfigureAwait(false);
            var errorText=await error;
            if(process.ExitCode!=0) { if(action=="lookup" || action=="clear" && process.ExitCode==1 && errorText.Length==0)return null; throw Unavailable(); }
            var result=await output;
            if(action=="lookup" && Encoding.UTF8.GetByteCount(result)>CredentialLimits.MaximumSecretBytes)throw new InvalidDataException("Credential backend secret exceeds its limit.");
            // libsecret writes the exact value when stdout is a pipe; only TTY output gets a LF.
            return action=="lookup"?result:null;
        }
        catch
        {
            timeout.Cancel();
            if(!process.HasExited) { try { process.Kill(entireProcessTree:true); await process.WaitForExitAsync(); } catch(InvalidOperationException) { } }
            if(overflow!=null && !ct.IsCancellationRequested)throw overflow;
            throw;
        }
    }
}
