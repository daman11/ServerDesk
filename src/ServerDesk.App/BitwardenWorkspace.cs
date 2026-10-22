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
    internal async Task<BitwardenChoice?> ChooseBitwardenAsync(Window owner,ServerProfile? profile,BitwardenReference? reference,bool bindingOnly=false)
    {
        if(!IsPluginEnabled(PluginCatalog.Bitwarden) || bitwardenLifetime is not {} scope || scope.IsCancellationRequested || closing || closePending)return null;
        var task=Dialogs.Bitwarden(owner,()=>BitwardenFactory(bitwardenOptions),profile,reference,bindingOnly,scope.Token);
        bitwardenWork.Add(task);
        try { var result=await task; return !scope.IsCancellationRequested && IsPluginEnabled(PluginCatalog.Bitwarden)?result:null; }
        finally { bitwardenWork.Remove(task); }
    }
}

internal sealed class BitwardenChoice(BitwardenReference? reference,string username,string? password,bool manual=false)
{
    internal BitwardenReference? Reference { get; }=reference;
    internal string Username { get; }=username;
    internal string? Password { get; }=password;
    internal bool Manual { get; }=manual;
    public override string ToString()=>"BitwardenChoice [redacted]";
}
