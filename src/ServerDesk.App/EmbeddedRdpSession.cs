using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System.Diagnostics;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private WindowsRdpHost? embeddedRdp;
    internal WindowsRdpHost? RdpHost=>embeddedRdp;
    internal bool IsRdpLoggedIn=>embeddedRdp is { IsLoggedIn:true };
    internal nint EmbeddedRdpHandle=>embeddedRdp?.WindowHandle??0;
    private async Task LaunchEmbeddedRdpAsync(CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(Profile.Username))throw new ArgumentException(L.T("Wpisz login w profilu serwera, aby połączyć RDP w zakładce."));
        if(Profile.Rdp.RedirectLocalFolder)throw new NotSupportedException(L.T("Przekazanie folderu jest dostępne w kliencie FreeRDP na Linux. Wyłącz je dla osadzonego RDP Windows."));
        try
        {
        var host=new WindowsRdpHost(); embeddedRdp=host; Grid.SetRow(host,2); Children.Add(host);
        host.LoginComplete+=()=>
        {
            if(disposed!=0 || !ReferenceEquals(embeddedRdp,host))return;
            connectionStarted=Stopwatch.GetTimestamp(); SetStatus(L.F($"RDP · {Profile.Host}:{Profile.Port} · zalogowano"),"●");
            _=RecordSuccessfulConnectionAsync();
            _=ResizeEmbeddedRdpAsync();
            if(IsActive && IsEffectivelyVisible && owner.Workspace.Focused==WorkspaceId && TopLevel.GetTopLevel(this) is Window { IsActive:true })host.FocusDesktop();
        };
        host.Disconnected+=reason=>
        {
            if(disposed!=0 || !ReferenceEquals(embeddedRdp,host))return;
            host.IsVisible=false; if(rdpNotice!=null)rdpNotice.IsVisible=true;
            var description=host.DisconnectDescription(reason);
            rdpError=reason is 0 or 1 or 2?"":description;
            SetStatus(description,"○",rdpError.Length==0?SessionVisualState.Closed:SessionVisualState.Failed);
        };
        host.SizeChanged+=(_,_)=>_ = ResizeEmbeddedRdpAsync();
        host.ViewportChanged+=()=>_ = ResizeEmbeddedRdpAsync();
        host.FocusReleased+=()=> { if(ReferenceEquals(embeddedRdp,host)) { host.ReleaseKeyboard(); optionsButton?.Focus(); } };
        host.Reconnecting+=attempt=> { if(disposed==0 && ReferenceEquals(embeddedRdp,host)) { ResetRdpFileStatus(); SetStatus(L.F($"Utracono łączność RDP · ponawianie {attempt}/5 · możesz rozłączyć sesję"),"◌"); } };
        host.Reconnected+=()=> { if(disposed==0 && ReferenceEquals(embeddedRdp,host)) { SetStatus(L.F($"RDP · {Profile.Host}:{Profile.Port} · połączenie przywrócone"),"●"); _=ResizeEmbeddedRdpAsync(); } };
        if(rdpNotice!=null)rdpNotice.IsVisible=false;
        await host.Ready.WaitAsync(TimeSpan.FromSeconds(8),ct); ct.ThrowIfCancellationRequested();
        SetStatus(L.F($"RDP · {Profile.Host}:{Profile.Port} · trwa logowanie / weryfikacja certyfikatu"),"◌");
        host.Connect(Profile,secret,clipboard.IsChecked==true);
        }
        catch(OperationCanceledException) { throw; }
        catch(Exception ex) { rdpHostUnavailable=true; throw new RdpHostUnavailableException(ex); }
    }
    private CancellationTokenSource? rdpResize;
    private async Task ResizeEmbeddedRdpAsync()
    {
        rdpResize?.Cancel(); var delay=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); rdpResize=delay;
        try
        {
            await Task.Delay(300,delay.Token);
            if(embeddedRdp is { IsLoggedIn:true } host && Profile.Rdp.DynamicResolution && host.IsEffectivelyVisible)
            {
                var scale=TopLevel.GetTopLevel(host)?.RenderScaling??1;
                if(host.Bounds.Width>0 && host.Bounds.Height>0)host.ResizeDesktop(ServerDesk.Core.RdpDisplaySettings.FromViewport(host.Bounds.Width,host.Bounds.Height,scale));
            }
        }
        catch(OperationCanceledException) when(delay.IsCancellationRequested) { }
        finally { if(ReferenceEquals(rdpResize,delay))rdpResize=null; delay.Dispose(); }
    }
    private void ResetEmbeddedRdp()
    {
        ResetRdpFileStatus();
        rdpResize?.Cancel();
        var host=embeddedRdp; embeddedRdp=null;
        if(host==null)return;
        Children.Remove(host); host.Dispose();
        if(rdpNotice!=null)rdpNotice.IsVisible=true;
    }
}
