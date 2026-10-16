using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;
internal sealed partial class SessionPane
{
    private sealed record FileTransferUpdate(string Name,TransferProgress Progress);
    private sealed class ForwardProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value)=>report(value); }
    private sealed class TransferJob(string label,string source,string target,Func<SftpSession,IProgress<FileTransferUpdate>,CancellationToken,Task<TransferOutcome>> action)
    {
        public Guid Id { get; }=Guid.NewGuid();
        public string Label { get; }=label;
        public string Paths { get; }=L.F($"Źródło: {source}\nCel: {target}");
        public Func<SftpSession,IProgress<FileTransferUpdate>,CancellationToken,Task<TransferOutcome>> Action { get; }=action;
        public TransferState State { get; set; }=TransferState.Queued;
        public bool IsActive=>State is TransferState.Queued or TransferState.Connecting or TransferState.Running;
        public CancellationTokenSource? Cancellation { get; set; }
        public Border View { get; set; }=null!;
        public TextBlock Status { get; set; }=null!;
        public TextBlock Detail { get; set; }=null!;
        public ProgressBar Bar { get; set; }=null!;
        public Button Cancel { get; set; }=null!;
        public Button Retry { get; set; }=null!;
    }
    private readonly List<TransferJob> transferJobs=[];
    private readonly ScrollViewer queueScroll=new() { MaxHeight=170,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private Button queueToggle=null!,queueCancel=null!,queueClear=null!;
    private bool queueExpanded=true;

    private void BuildTransferQueue()
    {
        queueScroll.Content=jobList;
        queueToggle=DeskIcons.Button("chevron-right","Rozwiń / zwiń kolejkę transferów",()=> { queueExpanded=!queueExpanded; UpdateTransferQueue(); });
        queueCancel=DeskIcons.Button("circle-x","Anuluj wszystkie aktywne transfery",()=>_=CancelTransfersAsync());
        queueClear=DeskIcons.Button("trash","Wyczyść zakończone zadania (także błędy i anulowane)",()=>
            ClearFinishedTransfers());
        transfer.TextTrimming=TextTrimming.CharacterEllipsis; transfer.VerticalAlignment=VerticalAlignment.Center;
        var header=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*,Auto,Auto"),ColumnSpacing=6,ClipToBounds=true };
        header.Children.Add(queueToggle); Grid.SetColumn(transfer,1); header.Children.Add(transfer); Grid.SetColumn(queueCancel,2); header.Children.Add(queueCancel); Grid.SetColumn(queueClear,3); header.Children.Add(queueClear);
        transferFooter=new StackPanel { Spacing=4,Margin=new Thickness(8,4),Children={header,queueScroll},ClipToBounds=true,IsVisible=false };
        Grid.SetRow(transferFooter,4); Children.Add(transferFooter);
    }
    private Task TransferAsync(string label,string source,string target,Func<SftpSession,IProgress<FileTransferUpdate>,CancellationToken,Task<TransferOutcome>> action)
    {
        if(!IsConnected || disposed!=0)throw new InvalidOperationException(L.T("Połącz sesję przed rozpoczęciem transferu."));
        var job=new TransferJob(label,source,target,action); BuildTransferRow(job); transferJobs.Add(job); jobList.Children.Insert(0,job.View);
        queueExpanded=true; UpdateTransferQueue(); return RunTransferAsync(job);
    }
    private void BuildTransferRow(TransferJob job)
    {
        var title=DesignSystem.Label(job.Label); title.TextTrimming=TextTrimming.CharacterEllipsis;
        job.Status=DesignSystem.Label(L.T("W kolejce"),true,12); job.Status.TextTrimming=TextTrimming.CharacterEllipsis;
        job.Detail=DesignSystem.Label(L.T("Oczekiwanie na wolne miejsce"),true,12); job.Detail.TextTrimming=TextTrimming.CharacterEllipsis;
        job.Bar=new ProgressBar { Maximum=100,Height=3,IsIndeterminate=true };
        job.Cancel=DeskIcons.Button("x","Anuluj: "+job.Label,()=> { job.Cancellation?.Cancel(); job.Cancel.IsEnabled=false; });
        job.Retry=DeskIcons.Button("refresh","Ponów: "+job.Label,()=>_=owner.Safe(()=>
        {
            if(job.IsActive)return Task.CompletedTask;
            if(!IsConnected)throw new InvalidOperationException(L.T("Połącz sesję, aby ponowić transfer."));
            return RunTransferAsync(job);
        })); job.Retry.IsVisible=false;
        var text=new StackPanel { Spacing=2,Children={title,job.Status,job.Detail},ClipToBounds=true };
        var actions=new StackPanel { Orientation=Orientation.Horizontal,Children={job.Cancel,job.Retry} };
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=6,ClipToBounds=true }; row.Children.Add(text); Grid.SetColumn(actions,1); row.Children.Add(actions);
        job.View=new Border { Padding=new Thickness(8,6),CornerRadius=new CornerRadius(4),ClipToBounds=true,Child=new StackPanel { Spacing=5,Children={row,job.Bar} } };
        DesignSystem.Resource(job.View,Border.BackgroundProperty,"BackgroundSecondary"); ToolTip.SetTip(job.View,job.Paths); AutomationProperties.SetName(job.View,L.T("Transfer: ")+job.Label);
    }
    private async Task RunTransferAsync(TransferJob job)
    {
        if(disposed!=0)throw new ObjectDisposedException(nameof(SessionPane));
        var work=RunTransferCoreAsync(job); transferTasks.Add(work);
        try { await work; }
        finally { transferTasks.Remove(work); }
    }
    private async Task RunTransferCoreAsync(TransferJob job)
    {
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(connectionLifetime.Token);
        transferJobs.Remove(job); transferJobs.Add(job); jobList.Children.Remove(job.View); jobList.Children.Insert(0,job.View);
        job.Cancellation=cancel; job.State=TransferState.Queued; job.Detail.Text=L.T("Oczekiwanie na wolne miejsce"); job.Bar.Value=0; job.Bar.IsVisible=true; job.Bar.IsIndeterminate=true;
        transfers.Add(cancel); UpdateTransferQueue(); var acquired=false; var finished=false; var refresh=false;
        try
        {
            await MainWindow.TransferSlots.WaitAsync(cancel.Token); acquired=true;
            job.State=TransferState.Connecting; job.Detail.Text=L.T("Nawiązywanie połączenia SFTP…"); UpdateTransferQueue();
            await using var connection=new SftpSession(await factory.SftpAsync(Profile,secret,cancel.Token));
            job.State=TransferState.Running; UpdateTransferQueue();
            var last=Stopwatch.StartNew();
            using var report=new CoalescingProgress<FileTransferUpdate>(action=>Avalonia.Threading.Dispatcher.UIThread.Post(action,Avalonia.Threading.DispatcherPriority.Background),update=>
            {
                var p=update.Progress;
                if(finished || cancel.IsCancellationRequested || last.ElapsedMilliseconds<100 && p.Bytes!=p.Total)return;
                last.Restart(); job.Bar.IsIndeterminate=false; job.Bar.Value=p.Total==0?0:Math.Clamp(100d*p.Bytes/p.Total,0,100);
                job.Detail.Text=L.F($"Bieżący plik: {update.Name} · {FormatBytes(p.Bytes)} / {FormatBytes(p.Total)} · {FormatBytes((long)p.BytesPerSecond)}/s");
                ToolTip.SetTip(job.Detail,job.Detail.Text);
            });
            var outcome=await job.Action(connection,report,cancel.Token); cancel.Token.ThrowIfCancellationRequested(); finished=true;
            job.State=outcome.State; job.Detail.Text=L.F($"Pliki: {outcome.Files} · katalogi: {outcome.Directories} · pominięte: {outcome.Skipped}");
            job.Bar.Value=100; job.Bar.IsIndeterminate=false;
            refresh=true;
        }
        catch(Exception) when(cancel.IsCancellationRequested) { job.State=TransferState.Cancelled; job.Detail.Text=L.T("Możesz ponowić zadanie po połączeniu sesji."); }
        catch(Exception ex) { job.State=TransferState.Failed; job.Detail.Text=ex.Message; ToolTip.SetTip(job.Detail,ex.Message); owner.ReportBackgroundError(L.T("Transfer SFTP nie powiódł się."),ex); }
        finally
        {
            finished=true; job.Cancellation=null; transfers.Remove(cancel); if(acquired)MainWindow.TransferSlots.Release();
            job.Bar.IsIndeterminate=false; job.Bar.IsVisible=false;
            // Keep active work and the 50 most recent terminal jobs. Only finished rows may be evicted.
            foreach(var old in transferJobs.Where(j=>!j.IsActive).Reverse().Skip(50).ToArray()) { transferJobs.Remove(old); jobList.Children.Remove(old.View); }
            UpdateTransferQueue();
        }
        // Directory browsing uses a different connection. Release the transfer
        // connection and global slot first so a stalled refresh cannot block
        // queued copies in this or any other session. Keep this task tracked
        // until refresh ends so reconnect/close can still drain it safely.
        if(refresh && !cancel.IsCancellationRequested && disposed==0)
        {
            try { await RefreshAsync(); }
            catch(OperationCanceledException) when(cancel.IsCancellationRequested || disposed!=0) { }
            catch(Exception ex)
            {
                job.Detail.Text+=L.T(" · odśwież listę ręcznie");
                owner.ReportBackgroundError(L.T("Nie udało się odświeżyć listy po transferze."),ex);
                UpdateTransferQueue();
            }
        }
    }
    private void UpdateTransferActions()
    {
        foreach(var job in transferJobs)
        {
            job.Cancel.IsVisible=job.IsActive; job.Cancel.IsEnabled=job.IsActive && job.Cancellation?.IsCancellationRequested!=true;
            job.Retry.IsVisible=job.State is TransferState.Failed or TransferState.Cancelled;
            job.Retry.IsEnabled=job.Retry.IsVisible && IsConnected && disposed==0 && !connecting;
        }
    }
    private void UpdateTransferQueue()
    {
        foreach(var job in transferJobs)
        {
            job.Status.Text=job.State switch { TransferState.Queued=>L.T("W kolejce"),TransferState.Connecting=>L.T("Łączenie…"),TransferState.Running=>L.T("Przesyłanie…"),TransferState.Completed=>L.T("Zakończony"),TransferState.CompletedWithSkips=>L.T("Zakończony z pominięciami"),TransferState.Skipped=>L.T("Pominięty"),TransferState.Cancelled=>L.T("Anulowany"),_=>L.T("Błąd") };
            DesignSystem.Resource(job.Status,TextBlock.ForegroundProperty,job.State==TransferState.Failed?"Danger":job.State==TransferState.Completed?"Success":"ForegroundSecondary");
            ToolTip.SetTip(job.Detail,job.Detail.Text);
            ToolTip.SetTip(job.View,job.Paths+"\n"+job.Status.Text+"\n"+job.Detail.Text);
        }
        UpdateTransferActions();
        var active=transferJobs.Count(j=>j.IsActive); var failed=transferJobs.Count(j=>j.State==TransferState.Failed);
        transfer.Text=L.F($"Transfery · aktywne {active} · zakończone {transferJobs.Count-active}")+(failed>0?L.F($" · błędy {failed}"):"");
        ToolTip.SetTip(transfer,transfer.Text+L.T("\nDwa transfery jednocześnie w całej aplikacji. Historia: do 50 zakończonych zadań."));
        transferFooter.IsVisible=transferJobs.Count>0; queueScroll.IsVisible=queueExpanded;
        queueToggle.Content=DeskIcons.Create(queueExpanded?"arrow-up":"chevron-right"); queueCancel.IsEnabled=active>0; queueClear.IsEnabled=transferJobs.Any(j=>!j.IsActive);
        StatusChanged?.Invoke();
    }
    internal void ExpandTransferQueue() { queueExpanded=true; UpdateTransferQueue(); }
    internal sealed record TransferSnapshot(Guid Id,string Label,string Paths,string Status,string Detail,TransferState State,bool IsActive,bool CanCancel,bool CanRetry,double Progress,bool Indeterminate);
    internal IReadOnlyList<TransferSnapshot> TransferSnapshots=>transferJobs.AsEnumerable().Reverse().Select(job=>new TransferSnapshot(job.Id,job.Label,job.Paths,job.Status.Text??"",job.Detail.Text??"",job.State,job.IsActive,job.Cancel.IsEnabled,job.Retry.IsVisible && job.Retry.IsEnabled,job.Bar.Value,job.Bar.IsIndeterminate)).ToArray();
    internal void CancelTransfer(Guid id)
    {
        var job=transferJobs.FirstOrDefault(j=>j.Id==id);
        if(job?.IsActive!=true)return;
        job.Cancellation?.Cancel(); UpdateTransferQueue();
    }
    internal Task RetryTransferAsync(Guid id)
    {
        var job=transferJobs.FirstOrDefault(j=>j.Id==id);
        if(job==null || job.IsActive || job.State is not (TransferState.Failed or TransferState.Cancelled))return Task.CompletedTask;
        if(!IsConnected || disposed!=0)throw new InvalidOperationException(L.T("Połącz sesję, aby ponowić transfer."));
        return RunTransferAsync(job);
    }
    internal void ClearFinishedTransfers()
    {
        foreach(var job in transferJobs.Where(j=>!j.IsActive).ToArray()) { transferJobs.Remove(job); jobList.Children.Remove(job.View); }
        UpdateTransferQueue();
    }
}
