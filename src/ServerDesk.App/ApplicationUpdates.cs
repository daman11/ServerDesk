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
    private bool startupUpdateCheckStarted;
    private readonly TextBlock updateNotificationText=DesignSystem.Hint("");
    internal Func<GitHubUpdateClient>? StartupUpdateClientFactory { get; set; }
    internal ProfileRepository UpdateRepository=>repository;
    internal Action<PreparedApplicationUpdate> StartUpdateHelper { get; set; }=ApplicationUpdateInstaller.Start;

    private Control BuildUpdateBanner()
    {
        var open=Dialogs.Button("Aktualizuj",()=>_ = Safe(OpenUpdatesAsync)); open.Name="OpenAvailableUpdate"; open.Classes.Add("primary");
        var skip=Dialogs.Button("Pomiń tę wersję",()=>_ = Safe(SkipAvailableUpdateAsync)); skip.Name="SkipAvailableUpdate";
        ToolTip.SetTip(skip,L.T("Nie przypominaj o tej wersji. Kolejne wydania będą nadal sprawdzane."));
        var remind=Dialogs.Button("Przypomnij przy następnym uruchomieniu",()=>updateBanner.IsVisible=false); remind.Name="RemindAvailableUpdate";
        var actions=new WrapPanel { Orientation=Orientation.Horizontal };
        foreach(var button in new[]{open,skip,remind}) { button.Margin=new Thickness(0,4,8,0); actions.Children.Add(button); }
        var row=new StackPanel { Spacing=4,Children={updateNotificationText,actions} };
        updateBanner.Child=row; DesignSystem.Resource(updateBanner,Border.BackgroundProperty,"SurfaceSelected");
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(updateBanner,Avalonia.Input.WindowDecorationsElementRole.User);
        return updateBanner;
    }
    internal async Task CheckUpdatesAtStartupAsync()
    {
        if(startupUpdateCheckStarted || !preferences.CheckUpdatesOnStartup || StartupUpdateClientFactory==null)return;
        startupUpdateCheckStarted=true;
        try
        {
            using var client=StartupUpdateClientFactory();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var update=await client.CheckAsync(typeof(App).Assembly.GetName().Version!,timeout.Token);
            if(lifetime.IsCancellationRequested || closing)return;
            await PresentAvailableUpdateAsync(update);
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { ApplicationDiagnostics.Report("CheckUpdatesAtStartup",ex); }
    }
    internal async Task PresentAvailableUpdateAsync(ApplicationUpdate? update)
    {
        update?.Validate();
        var skipped=await repository.SettingAsync("skippedUpdateVersion",lifetime.Token);
        if(closing || lifetime.IsCancellationRequested)return;
        availableUpdate=update;
        updateNotificationText.Text=update==null?"":L.F($"Dostępna wersja ServerDesk: {update.Version}");
        updateBanner.IsVisible=update!=null && update.Version!=skipped;
    }
    private async Task SkipAvailableUpdateAsync()
    {
        if(availableUpdate is not {} update || closing)return;
        await repository.SetSkippedApplicationUpdateAsync(update.Version,lifetime.Token);
        if(availableUpdate==update)updateBanner.IsVisible=false;
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
