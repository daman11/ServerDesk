using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    internal ServerSnapshot? LatestSnapshot { get; private set; }
    internal DateTimeOffset? LastSampleAt { get; private set; }
    internal string MetricsError { get; private set; }="";
    internal TimeSpan MetricsStaleAfter=>TimeSpan.FromSeconds(Math.Max(20,(double)(interval.Value??5)*2));
    private readonly Avalonia.Controls.TextBlock metricsNotice=Dialogs.Text("",12);
    private Avalonia.Controls.Viewbox? cpuChartHost;
    private void ClearMetrics()
    {
        LatestSnapshot=null; LastSampleAt=null; MetricsError=""; metricsNotice.IsVisible=false;
        cpuHistory.Clear(); cpuChart.Points=[]; cpuChart.IsVisible=false; cpuBar.IsVisible=ramBar.IsVisible=false;
        if(cpuChartHost!=null)cpuChartHost.IsVisible=false;
        cpu.Text=L.T("CPU: dane niedostępne"); ram.Text=L.T("RAM: dane niedostępne"); system.Text=L.T("Dane systemu niedostępne");
        diskRows.Children.Clear(); networkRows.Children.Clear(); networkSummary.Text=""; topology.Text="";
        disks.IsVisible=network.IsVisible=true; disks.Text=L.T("Dane dysków niedostępne"); network.Text=L.T("Dane interfejsów niedostępne");
    }
    internal static string MonitoringWarning(ServerSnapshot snapshot)=>snapshot.UnavailableSections.Count==0?"":L.F($"Dane niedostępne: {string.Join(", ",snapshot.UnavailableSections.Select(section=>section switch
    {
        MonitoringSection.Cpu=>"CPU",MonitoringSection.Memory=>"RAM",MonitoringSection.Disks=>L.T("Dyski"),MonitoringSection.Topology=>L.T("Topologia CPU"),MonitoringSection.Addresses=>L.T("Adresy IP"),MonitoringSection.Traffic=>L.T("Ruch sieciowy"),_=>"Load"
    }))}. Pozostałe parametry są odświeżane.");
    private void RenderMonitoringSnapshot(ServerSnapshot snapshot)
    {
        LatestSnapshot=snapshot; LastSampleAt=DateTimeOffset.UtcNow; MetricsError="";
        metricsNotice.Text=MonitoringWarning(snapshot); metricsNotice.IsVisible=metricsNotice.Text.Length>0;
        cpu.Text=snapshot.CpuAvailable?$"CPU: {(snapshot.CpuPercent.HasValue?snapshot.CpuPercent.Value.ToString("F1")+"%":L.T("oczekiwanie na drugą próbkę"))} · Load: {(snapshot.Load.Length>0?snapshot.Load:"—")}":L.T("CPU: dane niedostępne");
        cpuBar.Value=snapshot.CpuPercent??0; cpuBar.IsVisible=snapshot.CpuPercent.HasValue;
        if(snapshot.CpuPercent is {} usage) { cpuHistory.Enqueue(usage); while(cpuHistory.Count>60)cpuHistory.Dequeue(); }
        else cpuHistory.Clear();
        cpuChart.Points=new Avalonia.Collections.AvaloniaList<Avalonia.Point>(cpuHistory.Select((v,i)=>new Avalonia.Point(i*8,80-v*.8))); cpuChart.IsVisible=cpuHistory.Count>0;
        if(cpuChartHost!=null)cpuChartHost.IsVisible=cpuHistory.Count>0;
        var memory=snapshot.Memory; ramBar.IsVisible=memory.Total>0; ramBar.Value=memory.Percent;
        ram.Text=memory.Total>0?L.F($"{memory.Percent:F1}% · {FormatBytes(memory.Total-memory.Available)} używane\n{FormatBytes(memory.Total)} łącznie · {FormatBytes(memory.Available)} dostępne")+"\n"+(memory.HasSwapData?$"Swap: {FormatBytes(memory.SwapTotal-memory.SwapFree)} / {FormatBytes(memory.SwapTotal)}":"Swap: —")+(memory.AvailableEstimated?"\n"+L.T("RAM: przybliżenie na podstawie MemFree."):""):L.T("RAM: dane niedostępne");
        var details=snapshot.SystemDetails;
        system.Text=L.F($"{details.Hostname} · {details.Distribution}\n{details.Kernel}\nUżytkownik: {details.User} · Uptime: {(details.Uptime is {} uptime?$"{uptime.Days} d {uptime:hh\\:mm\\:ss}":L.T("dane niedostępne"))}\n{details.CpuModel}");
        RenderDetails(snapshot); StatusChanged?.Invoke();
    }
    private async Task EnsureMonitoringAsync(CancellationToken ct)
    {
        if(Profile.Protocol!=ServerDesk.Core.Protocol.SSH || monitor!=null || !IsConnected)return;
        var client=await factory.SshAsync(Profile,secret,ct);
        if(ct.IsCancellationRequested || !IsConnected) { await Task.Run(client.Dispose); ct.ThrowIfCancellationRequested(); return; }
        monitor=new LinuxMonitoring(client);
        if(monitorLoop.IsCompleted)monitorLoop=MonitorAsync(ct);
    }
    private async Task StartMonitoringAsync(CancellationToken ct)
    {
        var acquired=false;
        try { await connectionGate.WaitAsync(ct); acquired=true; await EnsureMonitoringAsync(ct); }
        catch(Exception ex) { if(ct.IsCancellationRequested || disposed!=0)return; MetricsError=L.F($"Parametry niedostępne ({ex.GetType().Name}). Otwórz Monitoring, aby ponowić próbę."); StatusChanged?.Invoke(); }
        finally { if(acquired)connectionGate.Release(); }
    }
}
