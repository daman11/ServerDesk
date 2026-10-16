using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private long connectionAttempt,recordedAttempt=-1;
    private Task historyWrite=Task.CompletedTask;
    private Task RecordSuccessfulConnectionAsync()
    {
        if(disposed!=0 || !IsConnected && !IsRdpLoggedIn || recordedAttempt==connectionAttempt)return historyWrite;
        recordedAttempt=connectionAttempt;
        var previous=historyWrite;
        return historyWrite=WriteAsync(previous);
        async Task WriteAsync(Task pending)
        {
            try
            {
                await pending;
                await Task.Run(()=>repository.ConnectedAsync(Profile));
                await owner.RefreshConnectionHistoryAsync();
            }
            catch(Exception ex) { owner.ReportBackgroundError(L.T("Połączono, ale nie udało się zapisać lub odświeżyć historii."),ex); }
        }
    }
}

public sealed partial class MainWindow
{
    internal async Task RefreshConnectionHistoryAsync() { if(!closing)await vm.LoadAsync(); }
}
