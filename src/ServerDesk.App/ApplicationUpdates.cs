using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private readonly Border updateBanner=new() { Name="UpdateAvailableBanner",IsVisible=false,Padding=new Thickness(12,8),Margin=new Thickness(12,0,12,8),CornerRadius=new CornerRadius(6) };
    private ApplicationUpdate? availableUpdate;
    private PreparedApplicationUpdate? pendingUpdate;
    private bool updateDialogOpen;
    internal ProfileRepository UpdateRepository=>repository;
    internal Action<PreparedApplicationUpdate> StartUpdateHelper { get; set; }=ApplicationUpdateInstaller.Start;

    private Control BuildUpdateBanner()
    {
        var open=Dialogs.Button("Dostępna aktualizacja",()=>_ = Safe(OpenUpdatesAsync)); open.Name="OpenAvailableUpdate";
        var dismiss=DeskIcons.Button("close","Zamknij",()=>updateBanner.IsVisible=false,28);
        var row=new Grid { ColumnDefinitions=new("*,Auto"),Children={open,dismiss} }; Grid.SetColumn(dismiss,1);
        updateBanner.Child=row; DesignSystem.Resource(updateBanner,Border.BackgroundProperty,"SurfaceSelected");
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(updateBanner,Avalonia.Input.WindowDecorationsElementRole.User);
        return updateBanner;
    }
    private async Task CheckUpdatesAtStartupAsync()
    {
        try
        {
            using var client=new GitHubUpdateClient();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var update=await client.CheckAsync(typeof(App).Assembly.GetName().Version!,timeout.Token);
            if(lifetime.IsCancellationRequested || closing)return;
            availableUpdate=update; updateBanner.IsVisible=update!=null;
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { ApplicationDiagnostics.Report("CheckUpdatesAtStartup",ex); }
    }
    internal async Task OpenUpdatesAsync()
    {
        if(updateDialogOpen || closing)return;
        updateDialogOpen=true;
        try { var prepared=await Dialogs.ApplicationUpdates(this,repository,availableUpdate); if(prepared!=null)RequestUpdateInstall(prepared); }
        finally { updateDialogOpen=false; }
    }
    internal void RequestUpdateInstall(PreparedApplicationUpdate update)
    {
        if(closing || closePending)return;
        pendingUpdate=update;
        Dispatcher.UIThread.Post(Close);
    }
}
