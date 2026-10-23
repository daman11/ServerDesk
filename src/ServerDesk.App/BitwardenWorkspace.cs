using Avalonia.Controls;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private BitwardenCliOptions bitwardenOptions=new();
    private CancellationTokenSource? bitwardenLifetime;
    private readonly HashSet<Task> bitwardenWork=[];
    internal Func<BitwardenCliOptions,BitwardenCli> BitwardenFactory { get; set; }=BitwardenCli.Create;
    internal async Task ApplyBitwardenPluginAsync(bool enabled)
    {
        var old=bitwardenLifetime; bitwardenLifetime=null; old?.Cancel();
        try { await Task.WhenAll(bitwardenWork.ToArray()); }
        catch(OperationCanceledException) { }
        finally { old?.Dispose(); }
        bitwardenOptions=preferences.Bitwarden;
        if(enabled)bitwardenLifetime=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
    }
    internal async Task<BitwardenChoice?> ChooseBitwardenAsync(Window owner,ServerProfile? profile,BitwardenReference? reference,bool bindingOnly=false,bool accountOnly=false)
    {
        if(!IsPluginEnabled(PluginCatalog.Bitwarden) || bitwardenLifetime is not {} scope || scope.IsCancellationRequested || closing || closePending)return null;
        var task=Dialogs.Bitwarden(owner,()=>BitwardenFactory(bitwardenOptions),profile,reference,bindingOnly,scope.Token,bitwardenOptions,accountOnly);
        bitwardenWork.Add(task);
        try { var result=await task; if(!scope.IsCancellationRequested && IsPluginEnabled(PluginCatalog.Bitwarden))return result; result?.Dispose(); return null; }
        finally { bitwardenWork.Remove(task); }
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
