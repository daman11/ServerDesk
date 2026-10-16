using System.Globalization;

namespace ServerDesk.Infrastructure;

public sealed record NetworkCounter(ulong Received,ulong Sent);
public sealed record NetworkRate(double ReceivedBytesPerSecond,double SentBytesPerSecond);
public static class NetworkTraffic
{
    public static IReadOnlyDictionary<string,NetworkCounter> Parse(string text)
    {
        var result=new Dictionary<string,NetworkCounter>(StringComparer.Ordinal);
        foreach(var line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries))
        {
            var parts=line.Split(':',2); if(parts.Length!=2)continue;
            var name=parts[0].Trim(); var fields=parts[1].Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(name.Length==0 || name=="lo" || fields.Length<16 || !ulong.TryParse(fields[0],NumberStyles.None,CultureInfo.InvariantCulture,out var rx) || !ulong.TryParse(fields[8],NumberStyles.None,CultureInfo.InvariantCulture,out var tx))continue;
            result[name]=new(rx,tx);
        }
        return result;
    }
    public static NetworkRate? Rate(IReadOnlyDictionary<string,NetworkCounter> current,IReadOnlyDictionary<string,NetworkCounter>? previous,double seconds)
    {
        if(previous==null || seconds<=0 || !double.IsFinite(seconds))return null;
        double rx=0,tx=0; var matched=false;
        foreach(var (name,counter) in current)
        {
            if(!previous.TryGetValue(name,out var old) || counter.Received<old.Received || counter.Sent<old.Sent)continue;
            rx+=counter.Received-old.Received; tx+=counter.Sent-old.Sent; matched=true;
        }
        return matched?new(rx/seconds,tx/seconds):null;
    }
}
