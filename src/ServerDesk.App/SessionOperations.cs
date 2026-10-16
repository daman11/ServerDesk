using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private readonly Dictionary<int,string> pendingOperations=[];
    private readonly Grid operationStatus=new() { Name="SessionOperationStatus",ColumnDefinitions=new ColumnDefinitions("16,*,Auto"),ColumnSpacing=DesignTokens.Medium,Margin=new Thickness(DesignTokens.Medium,0),MinHeight=28,ClipToBounds=true,IsVisible=false };
    private readonly TextBlock operationText=DesignSystem.Label("",true,DesignTokens.Caption);
    private readonly BusyIndicator operationIndicator=new();
    private string appearanceWarning="";
    private int operationId;
    internal bool IsBusy=>pendingOperations.Count>0;
    internal IDisposable WaitForCredentials()=>BeginBusy(L.T("Pobieranie poświadczeń…"));
    private void BuildOperationStatus()
    {
        operationText.TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis;
        operationStatus.Children.Add(operationIndicator); Grid.SetColumn(operationText,1); operationStatus.Children.Add(operationText);
        var dismiss=DeskIcons.Button("close","Ukryj komunikat",()=> { appearanceWarning=""; RenderOperationStatus(); }); Grid.SetColumn(dismiss,2); operationStatus.Children.Add(dismiss);
        Grid.SetRow(operationStatus,1); Children.Add(operationStatus);
    }
    private IDisposable BeginBusy(string message)
    {
        var id=++operationId; pendingOperations[id]=message; RenderOperationStatus();
        return new OperationScope(()=> { pendingOperations.Remove(id); if(disposed==0)RenderOperationStatus(); });
    }
    private void RenderOperationStatus()
    {
        UpdateConnectionActions();
        operationStatus.IsVisible=IsBusy || appearanceWarning.Length>0;
        operationText.Text=connecting && cancelRequested?L.T("Anulowanie łączenia…"):IsBusy?pendingOperations.Values.Last():appearanceWarning;
        ToolTip.SetTip(operationStatus,operationText.Text);
        operationIndicator.Glyph=IsBusy?"circle-dashed":"warning"; operationIndicator.Classes.Set("busy",IsBusy); operationStatus.Children[2].IsVisible=!IsBusy;
        DesignSystem.Resource(operationIndicator,BusyIndicator.ForegroundProperty,IsBusy?"AccentPrimary":"Warning");
        DesignSystem.Resource(operationText,TextBlock.ForegroundProperty,IsBusy?"ForegroundSecondary":"Warning");
    }
    private sealed class OperationScope(Action finish) : IDisposable
    {
        private Action? callback=finish;
        public void Dispose()=>Interlocked.Exchange(ref callback,null)?.Invoke();
    }
    private async Task TrackDurationAsync()
    {
        using var ticks=new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while(await ticks.WaitForNextTickAsync(lifetime.Token))
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(()=> { if(disposed==0)UpdateDuration(); },Avalonia.Threading.DispatcherPriority.Background,lifetime.Token);
        }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { }
    }
    private string ConnectedStatus()
    {
        var elapsed=System.Diagnostics.Stopwatch.GetElapsedTime(connectionStarted);
        var time=$"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        return $"● {Profile.Protocol} · {(Profile.Protocol==Protocol.SSH?Profile.Username+"@":"")}{(Profile.Protocol==Protocol.Serial?Profile.Serial.Summary:Profile.Endpoint)} · {time}";
    }
    private void UpdateDuration()
    {
        if(IsConnected || IsRdpLoggedIn) { if(IsActive)state.Text=ConnectionStatus; StatusChanged?.Invoke(); }
    }
}
