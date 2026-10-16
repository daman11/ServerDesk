using System.Globalization;
using System.Text;

namespace ServerDesk.Infrastructure;

internal sealed class MonitoringSampleParser
{
    private CpuCounters? previousCpu;
    private IReadOnlyDictionary<string,NetworkCounter>? previousTraffic;
    private static readonly HashSet<string> Markers=["__SYSTEM__","__CPU__","__MEM__","__DISKS__","__NET__","__LOAD__","__TOPOLOGY__","__IPJSON__","__ROUTES__","__DNS__","__TRAFFIC__","__CLOCK__"];
    internal ServerSnapshot Parse(string text,double elapsedSeconds)
    {
        var sections=new Dictionary<string,StringBuilder>(StringComparer.Ordinal); StringBuilder? current=null;
        foreach(var raw in text.Split('\n'))
        {
            var line=raw.TrimEnd('\r');
            if(Markers.Contains(line)) { current=new(); sections[line]=current; }
            else current?.AppendLine(line);
        }
        string Section(string marker)=>sections.TryGetValue(marker,out var value)?value.ToString().Trim():"";
        var unavailable=new List<MonitoringSection>(); CpuCounters? cpu=null;
        var cpuText=Section("__CPU__");
        try { if(cpuText.StartsWith("cpu ",StringComparison.Ordinal) || cpuText.StartsWith("cpu\t",StringComparison.Ordinal))cpu=CpuCounters.Parse(cpuText); }
        catch(Exception ex) when(ex is FormatException or OverflowException) { }
        if(cpu==null)unavailable.Add(MonitoringSection.Cpu);
        var usage=cpu?.Usage(previousCpu); previousCpu=cpu;
        var memory=MemoryInfo.Parse(Section("__MEM__")); if(memory.Total==0)unavailable.Add(MonitoringSection.Memory);
        var disks=Section("__DISKS__"); var diskRows=DiskInfo.Parse(disks); if(diskRows.Count==0)unavailable.Add(MonitoringSection.Disks);
        var topology=CpuTopology.Parse(Section("__TOPOLOGY__")); if(topology==null)unavailable.Add(MonitoringSection.Topology);
        var network=NetworkDetails.Parse(Section("__IPJSON__"),Section("__ROUTES__"),Section("__DNS__")); if(network.Interfaces.Count==0)unavailable.Add(MonitoringSection.Addresses);
        var counters=NetworkTraffic.Parse(Section("__TRAFFIC__")); var traffic=NetworkTraffic.Rate(counters,previousTraffic,elapsedSeconds);
        previousTraffic=counters.Count>0?counters:null; if(counters.Count==0)unavailable.Add(MonitoringSection.Traffic);
        var load=Section("__LOAD__"); if(load.Length==0)unavailable.Add(MonitoringSection.Load);
        var clock=Section("__CLOCK__"); var system=Section("__SYSTEM__");
        return new(system,cpu??new(0,0),usage,memory,disks,Section("__NET__"),load)
        {
            CpuAvailable=cpu!=null,UnavailableSections=unavailable.ToArray(),DiskDetails=diskRows,Topology=topology,NetworkDetails=network,SystemDetails=SystemInfo.Parse(system),Traffic=traffic,
            RemoteTime=TimeSpan.TryParseExact(clock,"hh\\:mm",CultureInfo.InvariantCulture,out var time) && time<TimeSpan.FromDays(1)?clock:""
        };
    }
}
