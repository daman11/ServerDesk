using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class NetworkToolsService(IIcmpProbe? icmp=null)
{
    private readonly IIcmpProbe icmp=icmp??new NativeIcmpProbe();
    private static readonly SemaphoreSlim Runs=new(8);
    public async Task<NetworkResult> RunAsync(NetworkRequest request,NetworkSecrets secrets,IProgress<NetworkUpdate> progress,CancellationToken ct)
    {
        request.Validate(); if(new[]{secrets.Password,secrets.Token,secrets.Community,secrets.AuthPassword,secrets.PrivacyPassword}.Any(s=>s.Length>4096))throw new ArgumentException(L.T("Poświadczenia narzędzia są zbyt długie."));
        await Runs.WaitAsync(ct).ConfigureAwait(false);
        try { return request.Tool switch {
            NetworkToolKind.Ping=>await PingAsync(request,progress,ct).ConfigureAwait(false),
            NetworkToolKind.Trace=>await TraceAsync(request,progress,ct).ConfigureAwait(false),
            NetworkToolKind.Dns=>await DnsAsync(request,progress,ct).ConfigureAwait(false),
            NetworkToolKind.Ports=>await PortsAsync(request,progress,ct).ConfigureAwait(false),
            NetworkToolKind.Wake=>await WakeAsync(request,progress,ct).ConfigureAwait(false),
            NetworkToolKind.Http=>await HttpAsync(request,secrets,progress,ct).ConfigureAwait(false),
            NetworkToolKind.Tls=>await TlsAsync(request,progress,ct).ConfigureAwait(false),
            NetworkToolKind.Subnet=>Subnet(request,progress),
            NetworkToolKind.Snmp=>await SnmpAsync(request,secrets,progress,ct).ConfigureAwait(false),
            _=>throw new ArgumentOutOfRangeException(nameof(request)) }; }
        finally { Runs.Release(); }
    }
    public static string Explain(Exception error)=>error switch {
        OperationCanceledException=>L.T("Przekroczono czas oczekiwania lub zatrzymano operację."),
        ArgumentException=>error.Message,
        FileNotFoundException=>L.T("Brak systemowego narzędzia ping. Zainstaluj iputils-ping w systemie Linux."),
        UnauthorizedAccessException=>L.T("Brak uprawnień. Sprawdź uprawnienia ICMP i konfigurację systemu."),
        PlatformNotSupportedException=>L.T("Ta metoda wymaga obsługi lub uprawnień systemowych. W Linux sprawdź dostępność ping oraz uprawnienia ICMP."),
        PingException=>L.T("Nie udało się wysłać ICMP. Sprawdź uprawnienia, narzędzie ping i dostępność sieci."),
        SocketException socket=>socket.SocketErrorCode switch { SocketError.HostNotFound or SocketError.NoData=>L.T("DNS nie znalazł adresu hosta."),SocketError.ConnectionRefused=>L.T("Host odrzucił połączenie."),SocketError.NetworkUnreachable or SocketError.HostUnreachable=>L.T("Sieć lub host są nieosiągalne."),SocketError.AccessDenied=>L.T("Brak uprawnień do tej operacji sieciowej."),_=>L.T("Błąd połączenia sieciowego. Sprawdź adres i trasę.") },
        System.Security.Authentication.AuthenticationException=>L.T("Nie udało się zweryfikować certyfikatu lub uzgodnić TLS."),
        HttpRequestException=>L.T("Zapytanie HTTP nie powiodło się. Sprawdź URL, połączenie i certyfikat HTTPS."),
        DnsClient.DnsResponseException=>L.T("Resolver DNS nie odpowiedział poprawnie. Sprawdź adres, port, timeout i dostępność DNS."),
        System.Text.Json.JsonException=>L.T("Niepoprawna składnia JSON w treści zapytania."),
        InvalidDataException=>error.Message,
        _=>L.T("Operacja sieciowa nie powiodła się. Sprawdź parametry i dostępność usługi.") };
    private static async Task<IPAddress> ResolveAsync(NetworkRequest r,CancellationToken ct)
    {
        var host=NetworkRequest.Host(r.Target); var addresses=await System.Net.Dns.GetHostAddressesAsync(host,ct).ConfigureAwait(false);
        var family=r.Family=="IPv4"?AddressFamily.InterNetwork:r.Family=="IPv6"?AddressFamily.InterNetworkV6:AddressFamily.Unspecified;
        return addresses.Where(a=>family==AddressFamily.Unspecified || a.AddressFamily==family).OrderBy(a=>a.AddressFamily==AddressFamily.InterNetwork?0:1).FirstOrDefault()??throw new SocketException((int)SocketError.HostNotFound);
    }
    private static string Number(double value)=>value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
    private async Task<NetworkResult> PingAsync(NetworkRequest r,IProgress<NetworkUpdate> output,CancellationToken ct)
    {
        var address=await ResolveAsync(r,ct).ConfigureAwait(false); long sent=0,received=0; double sum=0,min=double.MaxValue,max=0; var summary="";
        while(r.Continuous || sent<r.Count)
        {
            ct.ThrowIfCancellationRequested(); var reply=await icmp.SendAsync(address,64,r.PayloadBytes,r.TimeoutMs,ct).ConfigureAwait(false); sent++;
            double? latency=reply.Status==IPStatus.Success?reply.Milliseconds:null;
            if(latency.HasValue) { received++; sum+=latency.Value; min=Math.Min(min,latency.Value); max=Math.Max(max,latency.Value); }
            summary=L.F($"Wysłane: {sent} · odebrane: {received} · strata: {Number(100d*(sent-received)/sent)}% · min/avg/max: {(received==0?"—":Number(min)+" / "+Number(sum/received)+" / "+Number(max))} ms");
            output.Report(new(new(sent.ToString(),new[]{sent.ToString(),DateTimeOffset.Now.ToString("HH:mm:ss"),address.ToString(),reply.Status.ToString(),latency.HasValue?Number(latency.Value):"—"},!latency.HasValue,latency),r.Continuous?0:(int)sent,r.Continuous?0:r.Count,summary));
            if(r.Continuous || sent<r.Count)await Task.Delay(r.IntervalMs,ct).ConfigureAwait(false);
        }
        return new(summary);
    }
    private async Task<NetworkResult> TraceAsync(NetworkRequest r,IProgress<NetworkUpdate> output,CancellationToken ct)
    {
        var target=await ResolveAsync(r,ct).ConfigureAwait(false); var stats=new Dictionary<int,(int Sent,int Replies,double Sum,double Min,double Max)>(); var names=new Dictionary<string,string>(); var cycle=0;
        do
        {
            cycle++;
            for(var ttl=1;ttl<=r.MaxHops;ttl++)
            {
                ct.ThrowIfCancellationRequested(); var watch=Stopwatch.StartNew();
                var reply=await icmp.SendAsync(target,ttl,r.PayloadBytes,r.TimeoutMs,ct).ConfigureAwait(false);
                var answered=reply.Status is IPStatus.Success or IPStatus.TtlExpired or IPStatus.TimeExceeded;
                var ms=reply.Status==IPStatus.Success?reply.Milliseconds:watch.Elapsed.TotalMilliseconds;
                var old=stats.GetValueOrDefault(ttl,(0,0,0,double.MaxValue,0)); old.Sent++; if(answered) { old.Replies++; old.Sum+=ms; old.Min=Math.Min(old.Min,ms); old.Max=Math.Max(old.Max,ms); } stats[ttl]=old;
                var ip=answered?reply.Address?.ToString()??"—":"*"; var name="";
                if(answered && r.ResolveNames && ip!="—")
                {
                    if(!names.TryGetValue(ip,out name)) { try { name=(await System.Net.Dns.GetHostEntryAsync(ip,ct).WaitAsync(TimeSpan.FromMilliseconds(500),ct).ConfigureAwait(false)).HostName; } catch(Exception ex) when(ex is SocketException or TimeoutException) { name=""; } names[ip]=name; }
                }
                var cells=new[]{ttl.ToString(),ip,name??"",reply.Status.ToString(),answered?Number(ms):"—",Number(100d*(old.Sent-old.Replies)/old.Sent)+"%",old.Sent.ToString(),old.Replies==0?"—":Number(old.Min)+" / "+Number(old.Sum/old.Replies)+" / "+Number(old.Max)};
                output.Report(new(new(ttl.ToString(),cells,!answered),ttl,r.Continuous?0:r.MaxHops,L.F($"Cykl: {cycle}. Brak odpowiedzi routera może oznaczać filtrowanie lub ograniczanie ICMP."),true));
                if(reply.Status==IPStatus.Success)break;
            }
            if(r.Continuous)await Task.Delay(r.IntervalMs,ct).ConfigureAwait(false);
        } while(r.Continuous);
        return new(L.T("Trasa zakończona. Gwiazdka oznacza brak odpowiedzi ICMP, nie potwierdzenie awarii routera."));
    }
    public static IReadOnlyDictionary<string,string> PortProfiles { get; }=new Dictionary<string,string> { ["Common Ports"]="21,22,25,53,80,110,139,143,443,445,587,993,995,1433,3306,3389,5432,5900,6379,8080",["Web Services"]="80,443,8000,8080,8443",["Windows Services"]="53,88,135,139,389,443,445,464,636,3268,3269,3389,5985,5986",["Linux Services"]="22,25,53,80,111,443,2049",["Database Services"]="1433,1521,3306,5432,6379,9042,9200,27017" };
    private async Task<NetworkResult> PortsAsync(NetworkRequest r,IProgress<NetworkUpdate> output,CancellationToken ct)
    {
        var address=await ResolveAsync(r,ct).ConfigureAwait(false); var ports=NetworkRequest.ParsePorts(r.Ports); var done=0; var open=0;
        await Parallel.ForEachAsync(ports,new ParallelOptions { MaxDegreeOfParallelism=r.Concurrency,CancellationToken=ct },async(port,token)=>
        {
            using var socket=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp); using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(r.TimeoutMs); var watch=Stopwatch.StartNew(); var state="Unknown";
            try { await socket.ConnectAsync(address,port,timeout.Token).ConfigureAwait(false); state="Open"; Interlocked.Increment(ref open); }
            catch(OperationCanceledException) when(!token.IsCancellationRequested) { state="Filtered / Timeout"; }
            catch(SocketException ex) { state=ex.SocketErrorCode==SocketError.ConnectionRefused?"Closed":ex.SocketErrorCode==SocketError.TimedOut?"Filtered / Timeout":"Unknown ("+ex.SocketErrorCode+")"; }
            var completed=Interlocked.Increment(ref done); var service=port switch {22=>"SSH",53=>"DNS",80=>"HTTP",443=>"HTTPS",445=>"SMB",3389=>"RDP",5900=>"VNC",3306=>"MySQL",5432=>"PostgreSQL",1433=>"MSSQL",_=>"—"};
            output.Report(new(new(port.ToString(),new[]{port.ToString(),state,Number(watch.Elapsed.TotalMilliseconds),service},state!="Open"),completed,ports.Length,L.T("Nazwy usług są wskazówką według portu, bez identyfikacji działającej usługi.")));
        }).ConfigureAwait(false);
        return new(L.F($"Sprawdzone porty: {done} · otwarte: {open}"));
    }
    private static async Task<NetworkResult> WakeAsync(NetworkRequest r,IProgress<NetworkUpdate> output,CancellationToken ct)
    {
        var packet=NetworkRequest.MagicPacket(r.Target); using var udp=new UdpClient(AddressFamily.InterNetwork); udp.EnableBroadcast=true;
        if(r.LocalAddress.Length>0) { if(!IPAddress.TryParse(r.LocalAddress,out var local) || local.AddressFamily!=AddressFamily.InterNetwork)throw new ArgumentException(L.T("Wybierz lokalny adres IPv4 interfejsu.")); udp.Client.Bind(new IPEndPoint(local,0)); }
        await udp.SendAsync(packet,new IPEndPoint(IPAddress.Parse(r.Broadcast),r.Port),ct).ConfigureAwait(false);
        var summary=L.T("Magic Packet wysłany. Wysyłka nie potwierdza uruchomienia urządzenia."); output.Report(new(new("wol",new[]{r.Target,r.Broadcast,r.Port.ToString(),summary}),1,1,summary)); return new(summary);
    }
    private static NetworkResult Subnet(NetworkRequest r,IProgress<NetworkUpdate> output)
    {
        var s=SubnetCalculation.Calculate(r.Target,r.SplitPrefix);
        var pairs=new List<(string,string)> { ("IP",s.Address),(L.T("Zapis rozwinięty"),s.Expanded),("CIDR",s.Network+"/"+s.Prefix),(L.T("Koniec zakresu"),s.Last),(L.T("Liczba adresów"),s.Addresses.ToString()) };
        if(!s.IPv6) { pairs.Add((L.T("Pierwszy adres hosta"),s.FirstUsable)); pairs.Add((L.T("Ostatni adres hosta"),s.LastUsable)); pairs.Add((L.T("Adresy hostów"),s.Hosts.ToString())); pairs.Add(("Broadcast",s.Last)); pairs.Add((L.T("Maska"),s.Mask)); pairs.Add(("Wildcard",s.Wildcard)); }
        foreach(var pair in pairs)output.Report(new(new(pair.Item1,new[]{pair.Item1,pair.Item2})));
        foreach(var subnet in s.Subnets)output.Report(new(new(subnet,new[]{L.T("Podsieć"),subnet})));
        return new(s.IPv6?L.T("IPv6 nie używa broadcast. Zakres obejmuje wszystkie adresy prefiksu."):L.T("Dla /31 obydwa adresy są użyteczne; /32 oznacza pojedynczy host."));
    }
}
