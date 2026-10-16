using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DnsClient;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;
public sealed partial class NetworkToolsService
{
    public static string[] DnsTypes { get; }=["A","AAAA","CNAME","MX","TXT","NS","PTR","SOA","SRV","CAA","All"];
    private static async Task<NetworkResult> DnsAsync(NetworkRequest r,IProgress<NetworkUpdate> output,CancellationToken ct)
    {
        if(!DnsTypes.Contains(r.RecordType))throw new ArgumentException(L.T("Wybierz poprawny typ rekordu DNS."));
        var resolvers=r.Resolvers.Split(new[]{',','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries); if(resolvers.Length>4)throw new ArgumentException(L.T("Porównaj najwyżej cztery resolvery jednocześnie."));
        if(resolvers.Length==0)resolvers=[""];
        var types=r.RecordType=="All"?DnsTypes.Where(t=>t!="All").ToArray():[r.RecordType]; var done=0; var total=resolvers.Length*types.Length;
        foreach(var resolver in resolvers)
        {
            LookupClientOptions options;
            if(resolver.Length==0)options=new();
            else
            {
                IPEndPoint endpoint;
                if(IPAddress.TryParse(resolver,out var ip))endpoint=new(ip,53);
                else if(!IPEndPoint.TryParse(resolver,out endpoint!) || endpoint.Port==0)throw new ArgumentException(L.T("Resolver DNS: podaj IP lub IP:port; IPv6 z portem w nawiasach []."));
                options=new(endpoint);
            }
            options.UseCache=false; options.Retries=r.Retries; options.Timeout=TimeSpan.FromMilliseconds(r.TimeoutMs); options.ThrowDnsErrors=false; options.UseRandomNameServer=false;
            var client=new LookupClient(options);
            foreach(var type in types)
            {
                ct.ThrowIfCancellationRequested(); var name=NetworkRequest.DnsName(r.Target); if(type=="PTR" && IPAddress.TryParse(name,out var ip))name=ReverseName(ip);
                var watch=Stopwatch.StartNew(); var response=await client.QueryAsync(name,Enum.Parse<QueryType>(type),QueryClass.IN,ct).ConfigureAwait(false); var ms=Number(watch.Elapsed.TotalMilliseconds); var source=new IPEndPoint(IPAddress.Parse(response.NameServer.Address),response.NameServer.Port).ToString();
                var records=response.Answers.Concat(response.Authorities).Take(1000).ToArray();
                done++;
                foreach(var record in records)output.Report(new(new(Guid.NewGuid().ToString(),new[]{source,record.DomainName.Value,record.RecordType.ToString(),record.InitialTimeToLive.ToString(),record.ToString(),ms,response.Header.ResponseCode.ToString()}),done,total));
                if(records.Length==0)output.Report(new(new(Guid.NewGuid().ToString(),new[]{source,name,type,"—",L.T("Brak rekordów"),ms,response.Header.ResponseCode.ToString()},response.Header.ResponseCode!=DnsHeaderResponseCode.NoError),done,total));
            }
        }
        return new(L.T("Zapytania DNS zakończone. Odpowiedzi są oznaczone adresem resolvera; TTL pochodzi z odpowiedzi DNS."));
    }
    public static string ReverseName(IPAddress address)=>address.AddressFamily==AddressFamily.InterNetwork?string.Join(".",address.GetAddressBytes().Reverse())+".in-addr.arpa":string.Join(".",Convert.ToHexString(address.GetAddressBytes()).ToLowerInvariant().Reverse())+".ip6.arpa";
}
