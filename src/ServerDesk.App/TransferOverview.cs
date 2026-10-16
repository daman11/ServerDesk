using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private sealed record TransferOverviewRow(Border View,TextBlock Server,TextBlock Status,TextBlock Detail,ProgressBar Progress,Button Cancel,Button Retry);
    private readonly Grid transferOverview=new() { Name="TransferOverview",IsVisible=false,RowDefinitions=new RowDefinitions("Auto,*,Auto"),ClipToBounds=true };
    private readonly StackPanel transferRows=new() { Spacing=8,Margin=new Thickness(10,0,10,10) };
    private readonly Dictionary<Guid,TransferOverviewRow> transferViews=[];
    private readonly TextBox transferSearch=new() { PlaceholderText=L.T("Szukaj pliku lub serwera"),Margin=new Thickness(0,0,0,8) };
    private readonly ComboBox transferFilter=new() { ItemsSource=new[]{L.T("Wszystkie zadania"),L.T("Aktywne"),L.T("Zakończone"),L.T("Błędy, anulowane i ostrzeżenia")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch,Classes={"quiet-select"} };
    private readonly TextBlock transferCount=DesignSystem.Label("",true,11);
    private readonly TextBlock transferEmptyTitle=DesignSystem.Label("",false,16);
    private readonly TextBlock transferEmptyHint=DesignSystem.Label("",true,12);
    private readonly StackPanel transferEmpty=new() { Name="TransferEmpty",Spacing=10,Margin=new Thickness(18),VerticalAlignment=VerticalAlignment.Center };
    private Button clearTransferHistory=null!,resetTransferFilters=null!;

    private void BuildTransferOverview()
    {
        var header=new StackPanel { Spacing=8,Margin=new Thickness(10,0,10,10) };
        var hint=DesignSystem.Label(L.T("Przesyłanie plików przez SFTP · SSH"),true,12); hint.TextWrapping=TextWrapping.Wrap; header.Children.Add(hint);
        transferSearch.InnerLeftContent=new Border { Child=DeskIcons.Create("search",14,"ForegroundSecondary"),Margin=new Thickness(8,0,0,0) };
        AutomationProperties.SetName(transferSearch,L.T("Szukaj transferów")); transferSearch.TextChanged+=(_,_)=>UpdateTransferOverview();
        transferSearch.KeyDown+=(_,e)=> { if(e.Key==Key.Escape && !string.IsNullOrEmpty(transferSearch.Text)) { transferSearch.Text=""; e.Handled=true; } };
        header.Children.Add(transferSearch); header.Children.Add(transferFilter); AutomationProperties.SetName(transferFilter,L.T("Status transferów")); transferFilter.SelectionChanged+=(_,_)=>UpdateTransferOverview();
        var summary=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=4 }; transferCount.Name="TransferResultCount"; transferCount.TextWrapping=TextWrapping.Wrap; summary.Children.Add(transferCount);
        clearTransferHistory=DeskIcons.Button("trash","Wyczyść zakończone zadania (także błędy i anulowane)",()=> { foreach(var pane in sessions.Where(p=>p.Profile.Protocol==Protocol.SSH).ToArray())pane.ClearFinishedTransfers(); UpdateTransferOverview(); },26);
        Grid.SetColumn(clearTransferHistory,1); summary.Children.Add(clearTransferHistory); header.Children.Add(summary); transferOverview.Children.Add(header);
        transferEmptyTitle.TextWrapping=transferEmptyHint.TextWrapping=TextWrapping.Wrap;
        transferEmpty.Children.Add(DeskIcons.Create("transfers",28,"ForegroundSecondary")); transferEmpty.Children.Add(transferEmptyTitle); transferEmpty.Children.Add(transferEmptyHint);
        resetTransferFilters=Dialogs.Button("Wyczyść filtry",()=> { transferSearch.Text=""; transferFilter.SelectedIndex=0; UpdateTransferOverview(); }); resetTransferFilters.HorizontalAlignment=HorizontalAlignment.Left; transferEmpty.Children.Add(resetTransferFilters);
        var viewport=new Grid { Children={transferRows,transferEmpty} };
        var scroll=new ScrollViewer { Name="TransferOverviewScroll",Content=viewport,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; Grid.SetRow(scroll,1); transferOverview.Children.Add(scroll);
        var footer=DesignSystem.Label(L.T("Historia dotyczy otwartych kart."),true,11); footer.TextWrapping=TextWrapping.Wrap; footer.Margin=new Thickness(12,8); Grid.SetRow(footer,2); transferOverview.Children.Add(footer);
    }

    private TransferOverviewRow CreateTransferOverviewRow(SessionPane pane,SessionPane.TransferSnapshot job)
    {
        var title=DesignSystem.Label(job.Label); title.TextTrimming=TextTrimming.CharacterEllipsis; title.FontWeight=FontWeight.SemiBold;
        var server=DesignSystem.Label("",true,12); server.TextTrimming=TextTrimming.CharacterEllipsis;
        var status=DesignSystem.Label("",false,12); status.TextWrapping=TextWrapping.Wrap;
        var detail=DesignSystem.Label("",true,12); detail.TextWrapping=TextWrapping.Wrap;
        var progress=new ProgressBar { Maximum=100,Height=3 };
        var cancel=DeskIcons.Button("x",L.F($"Anuluj transfer: {job.Label}"),()=>pane.CancelTransfer(job.Id),26);
        var retry=DeskIcons.Button("refresh",L.F($"Ponów transfer: {job.Label}"),()=>_=Safe(()=>pane.RetryTransferAsync(job.Id)),26);
        var open=DeskIcons.Button("external-link",L.F($"Pokaż kolejkę: {pane.Profile.Name}"),()=>
        {
            FocusSession(pane); pane.ExpandTransferQueue();
            if(floatingDocks.TryGetValue(pane.WorkspaceId,out var dock))dock.Window.Activate();
        },26);
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Children={cancel,retry,open} };
        var body=new StackPanel { Spacing=6,Children={title,server,status,detail,progress,actions} };
        var card=DesignSystem.Card(body,new Thickness(10)); card.Name="TransferOverviewJob"; AutomationProperties.SetName(card,L.T("Transfer: ")+job.Label);
        return new(card,server,status,detail,progress,cancel,retry);
    }

    private void UpdateTransferOverview()
    {
        if(clearTransferHistory==null)return;
        var jobs=sessions.Where(p=>p.Profile.Protocol==Protocol.SSH).SelectMany(p=>p.TransferSnapshots.Select(job=>(Pane:p,Job:job))).ToArray();
        var ids=jobs.Select(item=>item.Job.Id).ToHashSet();
        foreach(var id in transferViews.Keys.Where(id=>!ids.Contains(id)).ToArray()) { transferRows.Children.Remove(transferViews[id].View); transferViews.Remove(id); }
        var words=(transferSearch.Text??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries); var visible=0;
        for(var index=0;index<jobs.Length;index++)
        {
            var (pane,job)=jobs[index];
            if(!transferViews.TryGetValue(job.Id,out var row)) { row=CreateTransferOverviewRow(pane,job); transferViews[job.Id]=row; }
            // Retain controls during progress updates so scrolling and keyboard focus stay put.
            if(index>=transferRows.Children.Count || !ReferenceEquals(transferRows.Children[index],row.View)) { transferRows.Children.Remove(row.View); transferRows.Children.Insert(index,row.View); }
            row.Server.Text=pane.PresentationProfile.Name+" · SFTP"; row.Status.Text=job.Status; row.Detail.Text=job.Detail;
            DesignSystem.Resource(row.Status,TextBlock.ForegroundProperty,job.State==TransferState.Failed?"Danger":job.HasCleanupWarning?"Warning":job.State==TransferState.Completed?"Success":"ForegroundSecondary");
            row.Progress.IsVisible=job.IsActive; row.Progress.IsIndeterminate=job.Indeterminate && job.IsActive; row.Progress.Value=job.Progress;
            row.Cancel.IsVisible=job.IsActive; row.Cancel.IsEnabled=job.CanCancel;
            row.Retry.IsVisible=job.State is TransferState.Failed or TransferState.Cancelled; row.Retry.IsEnabled=job.CanRetry;
            ToolTip.SetTip(row.View,pane.PresentationProfile.Name+"\n"+job.Label+"\n"+job.Paths+"\n"+job.Status+"\n"+job.Detail);
            var matchesStatus=transferFilter.SelectedIndex switch { 1=>job.IsActive,2=>!job.IsActive,3=>job.State is TransferState.Failed or TransferState.Cancelled || job.HasCleanupWarning,_=>true };
            var text=pane.PresentationProfile.Name+" "+pane.Profile.Host+" "+job.Label+" "+job.Paths+" "+job.Detail;
            row.View.IsVisible=matchesStatus && words.All(word=>text.Contains(word,StringComparison.OrdinalIgnoreCase)); if(row.View.IsVisible)visible++;
        }
        var active=jobs.Count(item=>item.Job.IsActive); var finished=jobs.Length-active;
        transferCount.Text=L.F($"Aktywne: {active} · zakończone: {finished}")+(visible!=jobs.Length?L.F($" · widoczne: {visible}"):"");
        clearTransferHistory.IsEnabled=finished>0; transferRows.IsVisible=visible>0; transferEmpty.IsVisible=visible==0;
        var filtered=words.Length>0 || transferFilter.SelectedIndex>0;
        transferEmptyTitle.Text=filtered?L.T("Brak pasujących transferów"):L.T("Brak transferów");
        transferEmptyHint.Text=filtered?L.T("Zmień wyszukiwanie lub status zadania."):L.T("Otwórz SFTP w połączonej sesji SSH i wyślij lub pobierz pliki. Tutaj zobaczysz ich postęp.");
        resetTransferFilters.IsVisible=filtered;
    }
}
