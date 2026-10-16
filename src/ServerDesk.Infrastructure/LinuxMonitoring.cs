using System.Globalization;
using System.Diagnostics;
using Renci.SshNet;

namespace ServerDesk.Infrastructure;

public sealed record CpuCounters(ulong Total, ulong Idle)
{
    public static CpuCounters Parse(string line)
    {
        var values = line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(value=>ulong.Parse(value,CultureInfo.InvariantCulture)).ToArray();
        if (values.Length < 4) throw new FormatException(L.T("Niepoprawny /proc/stat."));
        return new(values.Aggregate(0UL,(a,b)=>checked(a+b)),values[3]+(values.Length>4?values[4]:0));
    }
    public double? Usage(CpuCounters? previous)
    {
        if (previous == null || Total <= previous.Total || Idle < previous.Idle || Idle>Total || previous.Idle>previous.Total) return null;
        var total=Total-previous.Total; var idle=Idle-previous.Idle;
        // Hotplug/reset or non-atomic kernel counters can make a delta inconsistent.
        // Treat that interval as unavailable instead of clamping it to fake 0% usage.
        return idle>total?null:100d*(1-(double)idle/total);
    }
}
public sealed record MemoryInfo(long Total, long Available, long SwapTotal, long SwapFree)
{
    public double Percent => Total == 0 ? 0 : 100d * (Total-Available)/Total;
    public static MemoryInfo Parse(string content)
    {
        var data=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var line in content.Split('\n',StringSplitOptions.RemoveEmptyEntries))
        {
            var pair=line.Split(':',2); if(pair.Length!=2)continue;
            var fields=pair[1].Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(fields.Length!=2 || fields[1]!="kB" || !long.TryParse(fields[0],NumberStyles.None,CultureInfo.InvariantCulture,out var value) || value>long.MaxValue/1024)continue;
            data.TryAdd(pair[0].Trim(),value*1024);
        }
        if(!data.TryGetValue("MemTotal",out var total) || total<=0 || !data.TryGetValue("MemAvailable",out var available) && !data.TryGetValue("MemFree",out available))return new(0,0,0,0) { HasSwapData=false };
        if(available>total)return new(0,0,0,0) { HasSwapData=false };
        var swap=data.TryGetValue("SwapTotal",out var swapTotal) && data.TryGetValue("SwapFree",out var swapFree) && swapFree<=swapTotal;
        return new(total,available,swap?swapTotal:0,swap?data["SwapFree"]:0) { HasSwapData=swap,AvailableEstimated=!data.ContainsKey("MemAvailable") };
    }
    public bool HasSwapData { get; init; }=true;
    public bool AvailableEstimated { get; init; }
}
public enum MonitoringSection { Cpu,Memory,Disks,Topology,Addresses,Traffic,Load }
public sealed record ServerSnapshot(string System, CpuCounters Cpu, double? CpuPercent, MemoryInfo Memory, string Disks, string Network, string Load)
{
    public bool CpuAvailable { get; init; }=true;
    public IReadOnlyList<MonitoringSection> UnavailableSections { get; init; }=[];
    public IReadOnlyList<DiskInfo> DiskDetails { get; init; }=[];
    public NetworkDetails NetworkDetails { get; init; }=new([],"","");
    public CpuTopology? Topology { get; init; }
    public SystemInfo SystemDetails { get; init; }=SystemInfo.Parse("");
    public NetworkRate? Traffic { get; init; }
    public string RemoteTime { get; init; }="";
}
public sealed class LinuxMonitoring(SshClient client) : IDisposable
{
    private readonly MonitoringSampleParser parser=new();
    private long previousSample;
    private readonly SemaphoreSlim gate = new(1);
    private readonly ConnectionInfo connectionInfo=client.ConnectionInfo;
    private int disposed;
    internal const string Command = """
        export LC_ALL=C
        printf '__SYSTEM__\n'
        hostname; cat /etc/os-release 2>/dev/null; uname -srmo; id -un; cat /proc/uptime; getconf _NPROCESSORS_ONLN
        sed -n 's/^model name[[:space:]]*: /CPU: /p' /proc/cpuinfo | head -n 1
        printf '__CPU__\n'; head -n 1 /proc/stat
        printf '__MEM__\n'; cat /proc/meminfo
        printf '__DISKS__\n'; df -PT -k 2>/dev/null
        printf '__NET__\n'; ip -brief address 2>/dev/null; ip link 2>/dev/null; ip route show default 2>/dev/null; cat /etc/resolv.conf 2>/dev/null
        printf '__LOAD__\n'; cat /proc/loadavg
        printf '__TOPOLOGY__\n'; lscpu -p=SOCKET,CORE,CPU 2>/dev/null
        printf '__IPJSON__\n'
        if ! ip -json address 2>/dev/null; then
            printf '__IPTEXT__\n'; ip -o address show 2>/dev/null
            printf '__LINKTEXT__\n'; ip -o link show 2>/dev/null
        fi
        printf '__ROUTES__\n'
        if ! ip -json route show default 2>/dev/null; then
            printf '__ROUTETEXT__\n'; ip route show default 2>/dev/null
        fi
        printf '__ROUTES6__\n'
        if ! ip -6 -json route show default 2>/dev/null; then
            printf '__ROUTETEXT6__\n'; ip -6 route show default 2>/dev/null
        fi
        printf '__DNS__\n'; cat /etc/resolv.conf 2>/dev/null
        printf '__TRAFFIC__\n'; cat /proc/net/dev 2>/dev/null
        printf '__CLOCK__\n'; date '+%H:%M' 2>/dev/null
        """;
    public Task<ServerSnapshot> SampleAsync(CancellationToken ct)=>Task.Run(()=>SampleCoreAsync(ct),ct);
    private async Task<ServerSnapshot> SampleCoreAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var cmd = client.CreateCommand(Command); cmd.CommandTimeout = TimeSpan.FromSeconds(15);
            await cmd.ExecuteAsync(ct).ConfigureAwait(false); var text = cmd.Result;
            var now=Stopwatch.GetTimestamp(); var elapsed=previousSample==0?0:Stopwatch.GetElapsedTime(previousSample,now).TotalSeconds;
            previousSample=now; return parser.Parse(text,elapsed);
        }
        finally { gate.Release(); }
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        connectionInfo.ChannelCloseTimeout=TimeSpan.FromMilliseconds(500); client.Dispose();
    }
}
