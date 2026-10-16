using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace ServerDesk.Core;

public enum ProfileSort { Name,Host,Protocol,LastConnected }
public static class ProfileOrdering
{
    public static IEnumerable<ServerProfile> Sort(IEnumerable<ServerProfile> source,ProfileSort mode)
        =>Sort(source,mode,mode==ProfileSort.LastConnected);
    public static IEnumerable<ServerProfile> Sort(IEnumerable<ServerProfile> source,ProfileSort mode,bool descending)
    {
        var comparer=NaturalStringComparer.CurrentCultureIgnoreCase;
        if(mode==ProfileSort.Host)
        {
            var keyed=source.Select(profile=>(Profile:profile,Key:AddressKey.Create(profile)));
            var ordered=descending?keyed.OrderByDescending(row=>row.Key,AddressKey.Comparer):keyed.OrderBy(row=>row.Key,AddressKey.Comparer);
            var ports=descending?ordered.ThenByDescending(row=>row.Profile.Protocol==Protocol.Serial?0:row.Profile.Port):ordered.ThenBy(row=>row.Profile.Protocol==Protocol.Serial?0:row.Profile.Port);
            return ports.ThenBy(row=>row.Profile.Name,comparer).ThenBy(row=>row.Profile.Id).Select(row=>row.Profile);
        }
        IOrderedEnumerable<ServerProfile> result=mode switch {
            ProfileSort.Protocol=>descending?source.OrderByDescending(p=>p.Protocol.ToString(),comparer):source.OrderBy(p=>p.Protocol.ToString(),comparer),
            ProfileSort.LastConnected=>descending?source.OrderBy(p=>p.LastConnected==null).ThenByDescending(p=>p.LastConnected):source.OrderBy(p=>p.LastConnected==null).ThenBy(p=>p.LastConnected),
            _=>descending?source.OrderByDescending(p=>p.Name,comparer):source.OrderBy(p=>p.Name,comparer)
        };
        return result.ThenBy(p=>p.Name,comparer).ThenBy(p=>p.Id);
    }
    private sealed record AddressKey(int Group,byte[] Bytes,long Scope,string Name)
    {
        internal static readonly IComparer<AddressKey> Comparer=System.Collections.Generic.Comparer<AddressKey>.Create((left,right)=>
        {
            var compared=left.Group.CompareTo(right.Group); if(compared!=0)return compared;
            compared=left.Bytes.AsSpan().SequenceCompareTo(right.Bytes); if(compared!=0)return compared;
            compared=left.Scope.CompareTo(right.Scope); return compared!=0?compared:NaturalStringComparer.OrdinalIgnoreCase.Compare(left.Name,right.Name);
        });
        internal static AddressKey Create(ServerProfile profile)
        {
            if(profile.Protocol==Protocol.Serial)return new(3,[],0,profile.Serial.PortName);
            var host=profile.Host.Trim().Trim('[',']');
            if(IPAddress.TryParse(host,out var address))
            {
                if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
                var v6=address.AddressFamily==AddressFamily.InterNetworkV6;
                return new(v6?1:0,address.GetAddressBytes(),v6?address.ScopeId:0,"");
            }
            host=host.TrimEnd('.'); try { host=new IdnMapping().GetAscii(host); } catch(ArgumentException) { }
            return new(2,[],0,host);
        }
    }
}
public sealed record ConnectionHistoryEntry(long Id,Guid ProfileId,DateTimeOffset Time,ServerProfile? Profile)
{
    public ConnectionHistorySnapshot? Snapshot { get; init; }
    public bool CanReconnect=>Profile!=null && (Snapshot==null || Snapshot.Matches(Profile));
    public string Name=>Snapshot?.Name??Profile?.Name??L.T("Profil usunięty / szybkie połączenie");
    public string Detail=>Snapshot?.Detail??(Profile is {} p?ConnectionHistorySnapshot.Capture(p).Detail:ProfileId.ToString());
    public Protocol? Protocol=>Snapshot?.Protocol??Profile?.Protocol;
}
