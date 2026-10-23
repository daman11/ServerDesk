using Avalonia.Controls;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private BitwardenCliOptions bitwardenOptions=new();
    private CancellationTokenSource? bitwardenLifetime;
    private BitwardenCli? bitwardenClient;
    private readonly SemaphoreSlim bitwardenPickerGate=new(1);
    private bool bitwardenStartupAttempted;
    private readonly HashSet<Task> bitwardenWork=[];
    internal Func<BitwardenCliOptions,BitwardenCli> BitwardenFactory { get; set; }=BitwardenCli.Create;
    internal async Task ApplyBitwardenPluginAsync(bool enabled)
    {
        var old=bitwardenLifetime; bitwardenLifetime=null; old?.Cancel();
        try { await Task.WhenAll(bitwardenWork.ToArray()); }
        catch(OperationCanceledException) { }
        finally { old?.Dispose(); bitwardenClient?.Dispose(); bitwardenClient=null; }
        bitwardenOptions=preferences.Bitwarden;
        if(enabled)bitwardenLifetime=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
    }
    internal async Task<BitwardenChoice?> ChooseBitwardenAsync(Window owner,ServerProfile? profile,BitwardenReference? reference,bool bindingOnly=false,bool accountOnly=false,bool unlockOnly=false)
    {
        if(!IsPluginEnabled(PluginCatalog.Bitwarden) || bitwardenLifetime is not {} scope || scope.IsCancellationRequested || closing || closePending)return null;
        var task=Show();
        bitwardenWork.Add(task);
        try { var result=await task; if(!scope.IsCancellationRequested && IsPluginEnabled(PluginCatalog.Bitwarden))return result; result?.Dispose(); return null; }
        finally { bitwardenWork.Remove(task); }
        async Task<BitwardenChoice?> Show()
        {
            try { await bitwardenPickerGate.WaitAsync(scope.Token); }
            catch(OperationCanceledException) when(scope.IsCancellationRequested) { return null; }
            try
            {
                if(!owner.IsVisible || scope.IsCancellationRequested || closing || closePending)return null;
                return await Dialogs.Bitwarden(owner,()=>bitwardenClient??=BitwardenFactory(bitwardenOptions),profile,reference,bindingOnly,scope.Token,bitwardenOptions,accountOnly,retainClient:true,unlockOnly:unlockOnly);
            }
            finally { bitwardenPickerGate.Release(); }
        }
    }
    internal async Task UnlockBitwardenAtStartupAsync()
    {
        if(bitwardenStartupAttempted || !preferences.Bitwarden.UnlockOnStartup || !IsPluginEnabled(PluginCatalog.Bitwarden) ||
            bitwardenOptions is { Executable:"",Email:"",Server:"" } && !vm.AllServers.Any(p=>p.Bitwarden!=null || p.Authentication==Authentication.VaultSshKey) &&
            !Directory.Exists(BitwardenRuntime.ProfileDirectory(Path.GetDirectoryName(repository.DatabasePath)!,bitwardenOptions)))return;
        bitwardenStartupAttempted=true;
        using var choice=await ChooseBitwardenAsync(this,null,null,accountOnly:true,unlockOnly:true);
    }
    internal async Task ConfigureBitwardenAccountAsync(Window owner)
    {
        if(!IsPluginEnabled(PluginCatalog.Bitwarden)) { await Dialogs.Message(owner,L.T("Bitwarden / Vaultwarden"),L.T("Włącz plugin Bitwarden / Vaultwarden i zapisz ustawienia.")); return; }
        using var choice=await ChooseBitwardenAsync(owner,null,null,accountOnly:true);
    }
    private async Task<BitwardenChoice?> VaultSshKeyAsync(ServerProfile profile)
    {
        if(!IsPluginEnabled(PluginCatalog.Bitwarden)) { await Dialogs.Message(this,L.T("Klucz SSH z sejfu"),L.T("Włącz Bitwarden / Vaultwarden w Ustawienia → Pluginy, aby użyć klucza z sejfu. Możesz też zmienić metodę logowania w edytorze połączenia.")); return null; }
        return await ChooseBitwardenAsync(this,profile,profile.Bitwarden);
    }
}

internal sealed class BitwardenChoice(BitwardenReference? reference,string username,string? password,bool manual=false,SshKeyMaterial? sshKey=null):IDisposable
{
    private SshKeyMaterial? key=sshKey;
    internal BitwardenReference? Reference { get; }=reference;
    internal string Username { get; }=username;
    internal string? Password { get; }=password;
    internal bool Manual { get; }=manual;
    internal SshKeyMaterial? TakeSshKey()=>Interlocked.Exchange(ref key,null);
    public void Dispose()=>Interlocked.Exchange(ref key,null)?.Dispose();
    public override string ToString()=>"BitwardenChoice [redacted]";
}
