using System.Globalization;
using System.Text.Json;

namespace ServerDesk.Infrastructure;

public sealed record DiskInfo(string Device,string FileSystem,string MountPoint,long Total,long Used,long Available,double Percent)
{
    public static IReadOnlyList<DiskInfo> Parse(string text)
    {
        var result=new List<DiskInfo>();
        foreach(var line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries))
        {
            var fields=line.Split((char[]?)null,7,StringSplitOptions.RemoveEmptyEntries);
            if(fields.Length!=7 || !long.TryParse(fields[2],NumberStyles.None,CultureInfo.InvariantCulture,out var total) || !long.TryParse(fields[3],NumberStyles.None,CultureInfo.InvariantCulture,out var used) || !long.TryParse(fields[4],NumberStyles.None,CultureInfo.InvariantCulture,out var available) || !double.TryParse(fields[5].TrimEnd('%'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var percent))continue;
            if(total>long.MaxValue/1024 || used>long.MaxValue/1024 || available>long.MaxValue/1024)continue;
            result.Add(new(fields[0],fields[1],fields[6].Trim(),total*1024,used*1024,available*1024,Math.Clamp(percent,0,100)));
        }
        return result;
    }
}
public sealed record CpuTopology(int Sockets,int Cores,int Threads)
{
    public static CpuTopology? Parse(string text)
    {
        var entries=text.Split('\n',StringSplitOptions.RemoveEmptyEntries).Where(x=>!x.TrimStart().StartsWith('#')).Select(x=>x.Split(',')).Where(x=>x.Length==3 && x.All(v=>int.TryParse(v,out var number)&&number>=0)).ToArray();
        if(entries.Length==0)return null;
        return new(entries.Select(x=>x[0]).Distinct().Count(),entries.Select(x=>(x[0],x[1])).Distinct().Count(),entries.Select(x=>x[2]).Distinct().Count());
    }
}
public sealed record NetworkInterfaceInfo(string Name,string State,string Mac,string IPv4,string IPv6);
public sealed record SystemInfo(string Hostname,string Distribution,string Kernel,string Architecture,string User,TimeSpan? Uptime,string CpuModel)
{
    public static SystemInfo Parse(string text)
    {
        var lines=text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(lines.Length==0)return new("","","","","",null,"");
        var index=1; var os=new Dictionary<string,string>();
        while(index<lines.Length && lines[index].Contains('=')) { var pair=lines[index++].Split('=',2); os[pair[0]]=pair[1].Trim('"','\''); }
        var kernel=index<lines.Length?lines[index++]:""; var kernelParts=kernel.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        var user=index<lines.Length?lines[index++]:""; TimeSpan? uptime=null;
        if(index<lines.Length && double.TryParse(lines[index].Split(' ')[0],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var seconds) && seconds>=0 && seconds<31557600000)uptime=TimeSpan.FromSeconds(seconds);
        return new(lines[0],os.GetValueOrDefault("PRETTY_NAME",os.GetValueOrDefault("NAME","")),kernel,kernelParts.Length>2?kernelParts[2]:"",user,uptime,lines.FirstOrDefault(x=>x.StartsWith("CPU: "))?[5..]??"");
    }
}
public sealed record NetworkDetails(IReadOnlyList<NetworkInterfaceInfo> Interfaces,string Gateways,string Dns)
{
    public static NetworkDetails Parse(string addressJson,string routeJson,string resolv)
    {
        var interfaces=new List<NetworkInterfaceInfo>(); var gateways=new List<string>();
        static string Field(JsonElement e,string key) => e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??"":"";
        try
        {
            using var document=JsonDocument.Parse(addressJson);
            if(document.RootElement.ValueKind==JsonValueKind.Array)foreach(var element in document.RootElement.EnumerateArray())
            {
                if(element.ValueKind!=JsonValueKind.Object || Field(element,"ifname")=="")continue;
                var v4=new List<string>(); var v6=new List<string>();
                if(element.TryGetProperty("addr_info",out var addresses)&&addresses.ValueKind==JsonValueKind.Array)foreach(var a in addresses.EnumerateArray())
                {
                    if(a.ValueKind!=JsonValueKind.Object)continue;
                    var address=Field(a,"local"); if(address.Length==0)continue;
                    if(a.TryGetProperty("prefixlen",out var p)&&p.ValueKind==JsonValueKind.Number&&p.TryGetInt32(out var prefix))address+="/"+prefix;
                    if(Field(a,"family")=="inet")v4.Add(address); else if(Field(a,"family")=="inet6")v6.Add(address);
                }
                interfaces.Add(new(Field(element,"ifname"),Field(element,"operstate"),Field(element,"address"),string.Join(", ",v4),string.Join(", ",v6)));
            }
        }
        catch(JsonException) { }
        try
        {
            using var document=JsonDocument.Parse(routeJson);
            if(document.RootElement.ValueKind==JsonValueKind.Array)foreach(var route in document.RootElement.EnumerateArray())
                if(route.ValueKind==JsonValueKind.Object && Field(route,"gateway") is { Length:>0 } gateway)gateways.Add(gateway+" · "+Field(route,"dev"));
        }
        catch(JsonException) { }
        var dns=resolv.Split('\n').Select(line=>line.Split('#',2)[0].Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)).Where(parts=>parts.Length>=2&&parts[0]=="nameserver").Select(parts=>parts[1]).Distinct();
        return new(interfaces,string.Join("; ",gateways),string.Join(", ",dns));
    }
}
