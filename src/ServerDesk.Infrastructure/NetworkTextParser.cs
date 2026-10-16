using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace ServerDesk.Infrastructure;

// iproute2's one-line output is available on releases predating its JSON option.
internal static class NetworkTextParser
{
    private static IEnumerable<string[]> Rows(string text)=>text.Split('\n',StringSplitOptions.RemoveEmptyEntries)
        .Select(line=>line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)).Where(fields=>fields.Length>0);
    private static string Name(string field)=>field.TrimEnd(':').Split('@',2)[0];
    private static string Value(string[] fields,string key)
    { var index=Array.IndexOf(fields,key); return index>=0 && index+1<fields.Length?fields[index+1]:""; }
    private static bool Indexed(string[] fields)=>fields.Length>=3 && fields[0].EndsWith(':')
        && int.TryParse(fields[0].AsSpan(0,fields[0].Length-1),NumberStyles.None,CultureInfo.InvariantCulture,out var index) && index>0 && Name(fields[1]).Length>0;

    internal static IReadOnlyList<NetworkInterfaceInfo> Interfaces(string addresses,string links)
    {
        var result=new Dictionary<string,NetworkInterfaceInfo>(StringComparer.Ordinal);
        foreach(var fields in Rows(links))
        {
            if(!Indexed(fields) || !fields[2].StartsWith('<'))continue;
            var name=Name(fields[1]); var link=Array.FindIndex(fields,field=>field.StartsWith("link/",StringComparison.Ordinal));
            var mac=link>=0 && link+1<fields.Length?fields[link+1]:"";
            result[name]=new(name,Value(fields,"state"),mac,"","");
        }
        foreach(var fields in Rows(addresses))
        {
            if(!Indexed(fields) || fields.Length<4 || fields[2] is not ("inet" or "inet6"))continue;
            var parts=fields[3].Split('/',2);
            var maxPrefix=fields[2]=="inet"?32:128;
            if(parts.Length!=2 || !IPAddress.TryParse(parts[0],out var address)
                || address.AddressFamily!=(maxPrefix==32?AddressFamily.InterNetwork:AddressFamily.InterNetworkV6)
                || !int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out var prefix) || prefix>maxPrefix)continue;
            var name=Name(fields[1]); var nic=result.GetValueOrDefault(name)??new(name,"","","","");
            static string Append(string current,string value)=>string.Join(", ",current.Split(", ",StringSplitOptions.RemoveEmptyEntries).Append(value).Distinct());
            result[name]=maxPrefix==32?nic with { IPv4=Append(nic.IPv4,fields[3]) }:nic with { IPv6=Append(nic.IPv6,fields[3]) };
        }
        return result.Values.ToArray();
    }

    internal static IEnumerable<string> Gateways(string text)
    {
        foreach(var fields in Rows(text))
        {
            if(fields[0]!="default")continue;
            var gateway=Value(fields,"via");
            // Some iproute2 versions include a family token: via inet6 fe80::1.
            if(gateway is "inet" or "inet6")gateway=Value(fields,gateway);
            if(!IPAddress.TryParse(gateway,out _))continue;
            var device=Value(fields,"dev"); yield return device.Length>0?gateway+" · "+device:gateway;
        }
    }
}
