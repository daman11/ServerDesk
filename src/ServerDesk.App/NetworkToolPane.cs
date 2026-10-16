using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;
internal sealed partial class NetworkToolPane : WorkspacePane
{
    internal NetworkToolKind Tool { get; }
    public static readonly StyledProperty<string> CaptionProperty=AvaloniaProperty.Register<NetworkToolPane,string>(nameof(Caption),"");
    public string Caption { get=>GetValue(CaptionProperty); private set=>SetValue(CaptionProperty,value); }
    internal override string DisplayName=>Caption;
    internal string StatusText=>status.Text??"";
    public static readonly StyledProperty<bool> IsRunningProperty=AvaloniaProperty.Register<NetworkToolPane,bool>(nameof(IsRunning));
    public bool IsRunning { get=>GetValue(IsRunningProperty); private set=>SetValue(IsRunningProperty,value); }
    private readonly MainWindow owner;
    private readonly NetworkToolsStore store;
    private readonly NetworkToolsService service;
    private readonly TextBox target=new() { Name="NetworkTarget" };
    private readonly TextBlock status=DesignSystem.Label(L.T("Gotowy"),true,12),summary=DesignSystem.Label("",true,12);
    private readonly ProgressBar progress=new() { Maximum=100,Height=3,IsVisible=false };
    private readonly ObservableCollection<NetworkRow> rows=[];
    private readonly ListBox resultList=new() { Name="NetworkResults" };
    private readonly TextBlock emptyResults=DesignSystem.Label(L.T("Wpisz cel i wybierz Start. Wyniki pojawią się tutaj."),true,12);
    private readonly TextBox resultDetails=new() { Name="NetworkResultDetails",IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxHeight=150 };
    private readonly List<Control> requestControls=[];
    private readonly TextBox response=new() { IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,FontFamily=new FontFamily("Cascadia Mono,DejaVu Sans Mono,monospace") };
    private readonly TextBox responseHeaders=new() { IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap };
    private readonly NetworkLatencyChart chart=new() { Height=78,IsVisible=false };
    private readonly ComboBox exportFormat=new() { Width=90 };
    private readonly Dictionary<string,Control> fields=[];
    private readonly Dictionary<string,Control> fieldGroups=[];
    private readonly WrapPanel form=new() { Orientation=Orientation.Horizontal };
    private readonly Button start,stop,restart,copy,export;
    private CancellationTokenSource? operation,subnetDelay;
    private readonly CancellationTokenSource lifetime=new();
    private Task running=Task.CompletedTask;
    internal Task Shutdown=>running;
    private bool disposed,applying;
    private string pem="";
    private readonly string[] columns;
    private int dropped;
    internal NetworkToolPane(MainWindow owner,NetworkToolKind tool,NetworkToolsStore store,NetworkToolsService service,IReadOnlyList<ServerProfile> profiles,string host)
    {
        this.owner=owner; Tool=tool; this.store=store; this.service=service; columns=Columns(tool); ClipToBounds=true; RowDefinitions=new("Auto,Auto,Auto,*,Auto");
        var heading=DesignSystem.PageHeading(NetworkToolCatalog.Icon(tool),NetworkToolCatalog.Name(tool),NetworkToolCatalog.Hint(tool));
        target.PlaceholderText=tool switch { NetworkToolKind.Http=>"https://example.com/api",NetworkToolKind.Wake=>"AA:BB:CC:DD:EE:FF",NetworkToolKind.Subnet=>"192.168.1.10/24 · 2001:db8::1/64",_=>L.T("Adres IP lub nazwa hosta") };
        target.Text=HostTarget(host); AutomationProperties.SetName(target,L.T("Cel narzędzia"));
        UpdateCaption();
        var clearTarget=DeskIcons.Button("close","Wyczyść cel",()=>target.Text="",26); clearTarget.IsVisible=!string.IsNullOrEmpty(target.Text); target.InnerRightContent=clearTarget;
        var targetRow=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=8 }; targetRow.Children.Add(target);
        var saved=new ComboBox { Name="NetworkSavedHost",PlaceholderText=L.T("Zapisany host"),Width=165,ItemsSource=profiles.Where(p=>p.Protocol!=Protocol.Serial && !string.IsNullOrWhiteSpace(p.Host)).Select(p=>new HostChoice(p.Name,p.Host)).ToArray() };
        AutomationProperties.SetName(saved,L.T("Zapisany host")); saved.SelectionChanged+=(_,_)=> { if(saved.SelectedItem is HostChoice choice)UseHost(choice.Host); };
        Grid.SetColumn(saved,1); targetRow.Children.Add(saved); targetRow.SizeChanged+=(_,_)=> { var narrow=targetRow.Bounds.Width<520; saved.IsVisible=!narrow; targetRow.ColumnDefinitions[1].Width=narrow?new GridLength(0):GridLength.Auto; };
        start=Dialogs.Button("Start",()=>_=StartAsync()); stop=Dialogs.Button("Stop",()=>operation?.Cancel()); restart=Dialogs.Button("Restart",()=>_=RestartAsync());
        copy=Dialogs.Button("Kopiuj wyniki",()=>_=GuardAsync(CopyAsync)); export=Dialogs.Button("Eksportuj wyniki",()=>_=GuardAsync(ExportAsync)); start.Classes.Add("primary"); stop.IsEnabled=false; copy.IsEnabled=export.IsEnabled=false;
        exportFormat.ItemsSource=tool==NetworkToolKind.Tls?new[]{"TXT","CSV","JSON","PEM"}:tool==NetworkToolKind.Http?new[]{"TXT","CSV","JSON","Response"}:new[]{"TXT","CSV","JSON"}; exportFormat.SelectedIndex=0;
        exportFormat.ItemTemplate=OptionTemplate();
        var actions=new WrapPanel { Orientation=Orientation.Horizontal,Children={start,stop,restart,copy,export,exportFormat} }; foreach(var action in actions.Children)action.Margin=new Thickness(0,2,6,2);
        BuildForm();
        if(host.Length>0)UseHost(host);
        var options=new Expander { Name="NetworkParameters",Header=L.T("Parametry"),IsExpanded=false,HorizontalAlignment=HorizontalAlignment.Stretch,Content=form,Classes={"network-options"} };
        var savedControls=BuildSavedControls(); requestControls.AddRange(new Control[]{targetRow,form,savedControls});
        var header=new StackPanel { Spacing=10,Margin=new Thickness(16,12),Children={heading,targetRow,savedControls,options} };
        var headerScroll=new ScrollViewer { Content=header,MaxHeight=280,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; Children.Add(headerScroll);
        var actionSurface=new Border { Child=actions,Margin=new Thickness(16,2) }; Grid.SetRow(actionSurface,1); Children.Add(actionSurface);
        chart.IsVisible=false; summary.TextWrapping=TextWrapping.Wrap; summary.MaxLines=3; summary.TextTrimming=TextTrimming.CharacterEllipsis; status.TextWrapping=TextWrapping.Wrap; status.MaxLines=2;
        SizeChanged+=(_,_)=> { headerScroll.MaxHeight=Math.Max(100,Bounds.Height*.42); chart.IsVisible=tool==NetworkToolKind.Ping && rows.Count>0 && Bounds.Height>=450; };
        var live=new StackPanel { Spacing=5,Margin=new Thickness(16,0,16,8),Children={summary,chart,progress} }; Grid.SetRow(live,2); Children.Add(live);
        var results=BuildTable();
        if(tool==NetworkToolKind.Http)
        {
            var views=new TabControl { Items={new TabItem { Header=L.T("Wyniki"),Content=results,Classes={"page-navigation"} },new TabItem { Header=L.T("Odpowiedź"),Content=new NetworkJsonPreview(response),Classes={"page-navigation"} },new TabItem { Header=L.T("Nagłówki"),Content=responseHeaders,Classes={"page-navigation"} }} }; foreach(var tab in views.Items.OfType<TabItem>())DesignSystem.Resource(tab,TabItem.FontSizeProperty,"FontBody"); Grid.SetRow(views,3); Children.Add(views);
        }
        else { Grid.SetRow(results,3); Children.Add(results); }
        status.Margin=new Thickness(16,6); Grid.SetRow(status,4); Children.Add(status);
        AddHandler(KeyDownEvent,(_,e)=> { if(e.Key==Key.Escape && IsRunning) { operation?.Cancel(); e.Handled=true; } else if(e.Key==Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _=StartAsync(); e.Handled=true; } else if(e.Key==Key.F5) { _=RestartAsync(); e.Handled=true; } },RoutingStrategies.Tunnel);
        target.TextChanged+=(_,_)=> { clearTarget.IsVisible=!string.IsNullOrEmpty(target.Text); UpdateCaption(); if(Tool==NetworkToolKind.Subnet && !applying)_=RecalculateAsync(); };
        Loaded+=(_,_)=>_=GuardAsync(RefreshSavedAsync);
    }
    private sealed record HostChoice(string Name,string Host) { public override string ToString()=>Name+" · "+Host; }
    private string HostTarget(string host)
    {
        if(host.Length==0 || Tool==NetworkToolKind.Wake)return "";
        var address=System.Net.IPAddress.TryParse(host.Trim('[',']'),out var ip)?ip:null;
        if(Tool==NetworkToolKind.Http)return "https://"+(address?.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6?"["+address+"]":host);
        if(Tool==NetworkToolKind.Subnet && address!=null)return address+(address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork?"/32":"/128");
        return host;
    }
    internal void UseHost(string host)
    {
        if(Tool!=NetworkToolKind.Wake) { target.Text=HostTarget(host); UpdateCaption(); return; }
        if(System.Net.IPAddress.TryParse(host,out var address) && address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork)
        { ((TextBox)fields["Broadcast"]).Text=address.ToString(); status.Text=L.T("Uzupełniono docelowy IPv4. Podaj MAC urządzenia i sprawdź adres wysyłki."); }
        else status.Text=L.T("WoL wymaga MAC i docelowego IPv4. Nazwa hosta nie uzupełnia tych danych.");
    }
    private Control BuildTable()
    {
        var minWidth=Math.Max(320,columns.Length*105); var definition=string.Join(",",columns.Select((_,i)=>Tool is NetworkToolKind.Tls or NetworkToolKind.Subnet?i==0?"180":"*":"*"));
        var header=new Grid { ColumnDefinitions=new(definition),ColumnSpacing=10,Margin=new Thickness(10,7) };
        for(var i=0;i<columns.Length;i++) { var label=DesignSystem.Label(columns[i],true,12); label.TextTrimming=TextTrimming.CharacterEllipsis; Grid.SetColumn(label,i); header.Children.Add(label); }
        resultList.ItemsSource=rows; resultList.Classes.Add("network-results"); resultList.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);
        resultList.ItemTemplate=new FuncDataTemplate<NetworkRow>((row,_)=>
        {
            var grid=new Grid { ColumnDefinitions=new(definition),ColumnSpacing=10,Margin=new Thickness(6,4) }; if(row==null)return grid;
            for(var i=0;i<Math.Min(row.Cells.Length,columns.Length);i++) { var label=DesignSystem.Label(row.Cells[i],false,12); label.TextWrapping=TextWrapping.Wrap; label.MaxLines=3; label.TextTrimming=TextTrimming.CharacterEllipsis; if(row.Error)DesignSystem.Resource(label,TextBlock.ForegroundProperty,Tool==NetworkToolKind.Trace?"Warning":"Danger"); else if(Tool==NetworkToolKind.Tls && row.Key=="Status")DesignSystem.Resource(label,TextBlock.ForegroundProperty,row.Cells[1]==L.T("Wkrótce wygasa")?"Warning":"Success"); Grid.SetColumn(label,i); grid.Children.Add(label); }
            ToolTip.SetTip(grid,string.Join("\n",row.Cells)); return grid;
        });
        emptyResults.TextWrapping=TextWrapping.Wrap; emptyResults.HorizontalAlignment=HorizontalAlignment.Center; emptyResults.VerticalAlignment=VerticalAlignment.Center; emptyResults.Margin=new Thickness(20);
        var results=new Grid { Children={resultList,emptyResults} };
        var details=new Expander { Name="NetworkDetails",Header=L.T("Szczegóły zaznaczonego wyniku"),IsVisible=false,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Content=resultDetails,Classes={"network-options"} };
        resultList.SelectionChanged+=(_,_)=>
        {
            details.IsVisible=resultList.SelectedItem is NetworkRow;
            resultDetails.Text=resultList.SelectedItem is NetworkRow selected?string.Join(Environment.NewLine,columns.Select((column,index)=>column+": "+(index<selected.Cells.Length?selected.Cells[index]:""))):"";
        };
        AutomationProperties.SetName(resultDetails,L.T("Szczegóły zaznaczonego wyniku"));
        var table=new Grid { MinWidth=minWidth,RowDefinitions=new("Auto,*,Auto"),Children={header,results,details} }; Grid.SetRow(results,1); Grid.SetRow(details,2);
        return new ScrollViewer { Content=table,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(10,0) };
    }
    private sealed class UiProgress(NetworkToolPane pane) : IProgress<NetworkUpdate>
    {
        public void Report(NetworkUpdate value)
        {
            // Backpressure bounds live results even for a fast localhost port scan.
            if(pane.disposed)return;
            Dispatcher.UIThread.InvokeAsync(()=> { if(!pane.disposed)pane.Update(value); },DispatcherPriority.Background).GetAwaiter().GetResult();
        }
    }
    private void Update(NetworkUpdate update)
    {
        if(update.Row is {} row)
        {
            emptyResults.IsVisible=false;
            var index=update.Replace?rows.Select((item,i)=>(item,i)).FirstOrDefault(pair=>pair.item.Key==row.Key, (null!,-1)).i:-1;
            var limit=Tool==NetworkToolKind.Ports?65535:Tool==NetworkToolKind.Subnet?4112:2000;
            if(index>=0)rows[index]=row; else { rows.Add(row); if(rows.Count>limit) { rows.RemoveAt(0); dropped++; } }
            if(Tool==NetworkToolKind.Ping) { chart.Append(row.Latency); chart.IsVisible=Bounds.Height>=450; }
        }
        if(update.Summary.Length>0) { summary.Text=update.Summary; ToolTip.SetTip(summary,update.Summary); }
        progress.IsIndeterminate=update.Total==0; if(update.Total>0)progress.Value=Math.Max(progress.Value,Math.Clamp(100d*update.Completed/update.Total,0,100));
        copy.IsEnabled=export.IsEnabled=rows.Count>0;
        status.Text=dropped>0?L.F($"Zachowano ostatnie 2000 wyników; pominięte starsze: {dropped}. Eksport obejmuje zachowane wyniki."):L.F($"Wyniki: {rows.Count} · Ctrl+Enter: Start · Escape: Stop · F5: Restart");
    }
    internal Task StartAsync(bool history=true)
    {
        if(disposed || IsRunning)return Task.CompletedTask;
        NetworkRequest request; try { request=ReadRequest(); request.Validate(); } catch(Exception ex) { status.Text=NetworkToolsService.Explain(ex); DesignSystem.Resource(status,TextBlock.ForegroundProperty,"Danger"); return Task.CompletedTask; }
        operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); IsRunning=true; running=ExecuteAsync(request,ReadSecrets(),operation,history); return running;
    }
    private async Task ExecuteAsync(NetworkRequest request,NetworkSecrets secrets,CancellationTokenSource cancellation,bool history)
    {
        start.IsEnabled=false; stop.IsEnabled=true; rows.Clear(); chart.Clear(); dropped=0; summary.Text=""; pem=""; response.Text=responseHeaders.Text=""; copy.IsEnabled=export.IsEnabled=false; progress.IsVisible=true; progress.Value=0; progress.IsIndeterminate=true; status.Text=L.T("Operacja w toku…"); DesignSystem.Resource(status,TextBlock.ForegroundProperty,"ForegroundSecondary");
        emptyResults.Text=L.T("Oczekiwanie na pierwszy wynik…"); emptyResults.IsVisible=true;
        if(Tool!=NetworkToolKind.Subnet)foreach(var control in requestControls)control.IsEnabled=false;
        try
        {
            var reporter=new UiProgress(this);
            var result=await Task.Run(async()=> { if(history)await store.AddHistoryAsync(request,cancellation.Token).ConfigureAwait(false); return await service.RunAsync(request,secrets,reporter,cancellation.Token).ConfigureAwait(false); },cancellation.Token);
            if(disposed)return; summary.Text=result.Summary+(dropped>0?"\n"+L.F($"Zachowano ostatnie 2000 wyników; pominięte starsze: {dropped}. Eksport obejmuje zachowane wyniki."):""); response.Text=result.Body; responseHeaders.Text=result.Headers; pem=result.Pem; status.Text=L.T("Zakończono");
            if(request.Tool==NetworkToolKind.Dns && request.Resolvers.Length>0)await store.RememberAsync("resolvers",request.Resolvers,lifetime.Token);
            if(request.Tool==NetworkToolKind.Snmp)await store.RememberAsync("oids",request.Oid,lifetime.Token);
            await RefreshSavedAsync();
        }
        catch(OperationCanceledException) { if(!disposed)status.Text=cancellation.IsCancellationRequested?L.T("Zatrzymano"):L.T("Przekroczono czas oczekiwania."); }
        catch(Exception ex) { if(!disposed) { status.Text=NetworkToolsService.Explain(ex); DesignSystem.Resource(status,TextBlock.ForegroundProperty,"Danger"); } }
        finally { IsRunning=false; start.IsEnabled=!disposed; stop.IsEnabled=false; progress.IsVisible=false; foreach(var control in requestControls)control.IsEnabled=!disposed; if(rows.Count==0)emptyResults.Text=L.T("Brak wyników. Sprawdź komunikat poniżej i parametry narzędzia."); if(ReferenceEquals(operation,cancellation))operation=null; cancellation.Dispose(); }
    }
    private async Task RestartAsync() { operation?.Cancel(); await running; if(!disposed)await StartAsync(); }
    private async Task RecalculateAsync()
    {
        subnetDelay?.Cancel(); var delay=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); subnetDelay=delay;
        try { await Task.Delay(250,delay.Token); operation?.Cancel(); await running; delay.Token.ThrowIfCancellationRequested(); await StartAsync(false); }
        catch(OperationCanceledException) { }
        finally { if(ReferenceEquals(subnetDelay,delay))subnetDelay=null; delay.Dispose(); }
    }
    private async Task GuardAsync(Func<Task> action)
    { try { await action(); } catch(OperationCanceledException) { } catch(Exception ex) { if(!disposed) { status.Text=NetworkToolsService.Explain(ex); DesignSystem.Resource(status,TextBlock.ForegroundProperty,"Danger"); } } }
    private async Task CopyAsync()
    {
        if(TopLevel.GetTopLevel(this)?.Clipboard is not {} clipboard)return;
        var snapshot=rows.ToArray(); var body=response.Text;
        var text=Tool==NetworkToolKind.Http && !string.IsNullOrEmpty(body)?body:await Task.Run(()=>NetworkExport.Render("TXT",columns,snapshot)); await clipboard.SetTextAsync(text);
    }
    private async Task ExportAsync()
    {
        var format=exportFormat.SelectedItem as string??"TXT"; var extension=format=="Response"?"txt":format.ToLowerInvariant();
        var snapshot=rows.ToArray(); var content=format=="PEM"?pem:format=="Response"?response.Text??"":await Task.Run(()=>NetworkExport.Render(format,columns,snapshot));
        if(content.Length==0) { status.Text=L.T("Brak danych do eksportu."); return; }
        if(TopLevel.GetTopLevel(this)?.StorageProvider is not {} storage)return;
        var file=await storage.SaveFilePickerAsync(new() { Title=L.T("Eksport wyników"),SuggestedFileName=$"ServerDesk-{Tool}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",DefaultExtension=extension });
        if(file==null)return; await using var stream=await file.OpenWriteAsync(); stream.SetLength(0); await using var writer=new StreamWriter(stream); await writer.WriteAsync(content);
    }
    internal override void FocusContent()=>target.Focus();
    private void UpdateCaption()
    {
        var value=target.Text??""; if(Tool==NetworkToolKind.Http)value=Uri.TryCreate(value,UriKind.Absolute,out var uri) && uri.Scheme is "http" or "https"?new UriBuilder(uri.Scheme,uri.Host,uri.Port).Uri.GetLeftPart(UriPartial.Authority):"";
        Caption=NetworkToolCatalog.Name(Tool)+(value.Length>0?" · "+value:"");
    }
    public override void Dispose() { if(disposed)return; disposed=true; lifetime.Cancel(); operation?.Cancel(); subnetDelay?.Cancel(); }
    private static string[] Columns(NetworkToolKind tool)=>tool switch {
        NetworkToolKind.Ping=>new[]{"#",L.T("Czas"),"IP","Status","RTT ms"},NetworkToolKind.Trace=>new[]{"Hop","IP","DNS","Status","RTT ms",L.T("Strata"),L.T("Wysłane"),"Min/Avg/Max ms"},NetworkToolKind.Dns=>new[]{"Resolver",L.T("Nazwa"),L.T("Typ"),"TTL",L.T("Wartość"),"ms","RCODE"},NetworkToolKind.Ports=>new[]{"TCP","Status","ms",L.T("Znana usługa")},NetworkToolKind.Wake=>new[]{"MAC","Broadcast","UDP","Status"},NetworkToolKind.Http=>new[]{"HTTP","ms","Bytes","Content-Type",L.T("Uwagi")},NetworkToolKind.Snmp=>new[]{"OID",L.T("Typ"),L.T("Wartość")},_=>new[]{L.T("Parametr"),L.T("Wartość")} };
}
