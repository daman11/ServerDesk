using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed class ServerMetricsBar : Grid
{
    private readonly Dictionary<string,(Border Cell,TextBlock Text)> cells=[];
    private ApplicationPreferences preferences=new();
    private bool warning;
    private static readonly string[] order=["cpu","ram","rx","tx","uptime","disk","os","clock"];
    internal ServerMetricsBar()
    {
        Name="ServerMetricsBar"; ColumnDefinitions=new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"); VerticalAlignment=VerticalAlignment.Center; ClipToBounds=true; HorizontalAlignment=HorizontalAlignment.Right;
        Add("cpu","cpu","CPU"); Add("ram","layers","RAM"); Add("rx","download",L.T("Odbieranie")); Add("tx","upload",L.T("Wysyłanie"));
        Add("uptime","clock-arrow-left","Uptime"); Add("disk","hard-drive",L.T("Dysk /")); Add("os","server","System"); Add("clock","clock-arrow-left",L.T("Zegar serwera"));
    }
    private void Add(string key,string icon,string label)
    {
        var text=DesignSystem.Label("—",false,DesignTokens.Caption); text.Name="Metric_"+key; text.TextTrimming=TextTrimming.CharacterEllipsis;
        text.MaxWidth=key=="os"?110:163; text.ClipToBounds=true;
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions("14,*"),ColumnSpacing=5,ClipToBounds=true }; row.Children.Add(DeskIcons.Create(icon,14,"ForegroundSecondary")); Grid.SetColumn(text,1); row.Children.Add(text);
        var cell=new Border { Name="MetricCell_"+key,Padding=new Thickness(9,0),BorderThickness=new Thickness(1,0,0,0),Child=row,MaxWidth=key=="os"?148:200,ClipToBounds=true };
        DesignSystem.Resource(cell,Border.BorderBrushProperty,"BorderDefault"); ToolTip.SetTip(cell,label); AutomationProperties.SetName(cell,label);
        Grid.SetColumn(cell,cells.Count); cells.Add(key,(cell,text)); Children.Add(cell);
    }
    internal void Update(SessionPane? pane,double width,ApplicationPreferences? options=null)
    {
        var next=options??new(); if(preferences!=next)InvalidateMeasure(); preferences=next;
        IsVisible=preferences.ShowMetrics && (preferences.MetricCpu||preferences.MetricMemory||preferences.MetricNetwork||preferences.MetricUptime||preferences.MetricDisk||preferences.MetricSystem||preferences.MetricClock) && pane is { Profile.Protocol:Protocol.SSH };
        if(!IsVisible)return;
        Render(pane!.LatestSnapshot,pane.LastSampleAt,pane.MetricsError,pane.IsConnected,width,pane.MetricsStaleAfter);
    }
    internal void Render(ServerSnapshot? snapshot,DateTimeOffset? sampledAt,string error,bool connected,double width,TimeSpan? staleAfter=null)
    {
        var stale=sampledAt is {} at && DateTimeOffset.UtcNow-at>(staleAfter??TimeSpan.FromSeconds(20));
        var status=error.Length>0?error:!connected?L.T("Połącz sesję SSH, aby zobaczyć parametry serwera."):snapshot==null?L.T("Pobieranie parametrów serwera Linux…"):L.F($"Próbka: {sampledAt?.ToLocalTime():HH:mm:ss}{(stale?" · dane nieaktualne":"")}");
        if(snapshot is { UnavailableSections.Count:>0 })status+="\n"+SessionPane.MonitoringWarning(snapshot);
        var nextWarning=stale||error.Length>0||snapshot is { UnavailableSections.Count:>0 };
        if(warning!=nextWarning) { warning=nextWarning; foreach(var (_,text) in cells.Values)DesignSystem.Resource(text,TextBlock.ForegroundProperty,warning?"Warning":"ForegroundPrimary"); }
        ToolTip.SetTip(this,status); AutomationProperties.SetName(this,L.T("Parametry aktywnego serwera. ")+status);
        void Set(string key,string text,string description)
        {
            cells[key].Text.Text=text; ToolTip.SetTip(cells[key].Cell,description+"\n"+text+"\n"+status);
        }
        Set("cpu",snapshot?.CpuPercent is {} cpu?$"{cpu:F0}%":"CPU —",L.T("Użycie procesora; pierwsza próbka ustala punkt odniesienia."));
        Set("ram",snapshot is { Memory.Total:>0 } s?$"{GiB(s.Memory.Total-s.Memory.Available)} / {GiB(s.Memory.Total)}":"RAM —",L.T("Używana / całkowita pamięć RAM (GiB).")+(snapshot?.Memory.AvailableEstimated==true?"\n"+L.T("RAM: przybliżenie na podstawie MemFree."):""));
        Set("rx",snapshot?.Traffic is {} traffic?Rate(traffic.ReceivedBytesPerSecond):"↓ —",L.T("Odbieranie przez interfejsy serwera poza lo; średnia między próbkami."));
        Set("tx",snapshot?.Traffic is {} outgoing?Rate(outgoing.SentBytesPerSecond):"↑ —",L.T("Wysyłanie przez interfejsy serwera poza lo; średnia między próbkami."));
        Set("uptime",snapshot?.SystemDetails.Uptime is {} uptime?$"{uptime.Days} d {uptime:hh\\:mm}":"Uptime —",L.T("Czas pracy serwera od uruchomienia."));
        var disk=snapshot?.DiskDetails.FirstOrDefault(d=>d.MountPoint=="/"); Set("disk",disk is {} d?$"/: {d.Percent:F0}%":"/: —",L.T("Zajętość systemu plików /."));
        Set("os",snapshot?.SystemDetails.Distribution is { Length:>0 } os?os:L.T("System —"),L.T("System operacyjny serwera."));
        Set("clock",snapshot?.RemoteTime is { Length:>0 } clock?clock:L.T("Zegar —"),L.T("Zegar w strefie czasowej serwera."));
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        bool Enabled(string key)=>key switch { "cpu"=>preferences.MetricCpu,"ram"=>preferences.MetricMemory,"rx" or "tx"=>preferences.MetricNetwork,"uptime"=>preferences.MetricUptime,"disk"=>preferences.MetricDisk,"os"=>preferences.MetricSystem,"clock"=>preferences.MetricClock,_=>false };
        var widths=new Dictionary<string,double>(); var visible=new HashSet<string>(); double total=0;
        foreach(var key in order) { var cell=cells[key].Cell; if(Enabled(key)) { cell.Child!.Measure(new Size(cell.MaxWidth-cell.Padding.Left-cell.Padding.Right-cell.BorderThickness.Left,availableSize.Height)); var width=cell.Child.DesiredSize.Width+cell.Padding.Left+cell.Padding.Right+cell.BorderThickness.Left; widths[key]=width; total+=width; visible.Add(key); } }
        foreach(var key in new[]{"os","disk","clock","uptime","tx","rx","ram","cpu"})
        {
            if(total<=availableSize.Width)break;
            if(widths.TryGetValue(key,out var width)) { visible.Remove(key); total-=width; }
        }
        foreach(var key in order)cells[key].Cell.IsVisible=visible.Contains(key);
        return base.MeasureOverride(availableSize);
    }
    private static string GiB(long bytes)=>$"{bytes/1073741824d:F1} GiB";
    private static string Rate(double bytes)=>bytes>=1048576?$"{bytes/1048576:F2} MiB/s":$"{bytes/1024:F1} KiB/s";
}
