using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ServerDesk.Core;

public enum NetworkToolKind { Ping,Trace,Dns,Ports,Wake,Http,Tls,Subnet,Snmp }
public sealed record NetworkRequest
{
    public NetworkToolKind Tool { get; init; }
    public string Target { get; init; }="";
    public string Family { get; init; }="Auto";
    public int TimeoutMs { get; init; }=2000;
    public int IntervalMs { get; init; }=1000;
    public int Count { get; init; }=4;
    public int PayloadBytes { get; init; }=32;
    public int MaxHops { get; init; }=30;
    public bool Continuous { get; init; }
    public bool ResolveNames { get; init; }
    public string RecordType { get; init; }="A";
    public string Resolvers { get; init; }="";
    public string Ports { get; init; }="22,80,443,445,3389";
    public int Concurrency { get; init; }=16;
    public int Port { get; init; }=443;
    public string LocalAddress { get; init; }="";
    public string Broadcast { get; init; }="255.255.255.255";
    public string Method { get; init; }="GET";
    public string Headers { get; init; }="";
    public string Query { get; init; }="";
    public string Body { get; init; }="";
    public string ContentType { get; init; }="application/json";
    public bool FollowRedirects { get; init; }=true;
    public string Authentication { get; init; }="None";
    public string Username { get; init; }="";
    public string Sni { get; init; }="";
    public int SplitPrefix { get; init; }=-1;
    public string Oid { get; init; }="1.3.6.1.2.1.1.1.0";
    public string SnmpVersion { get; init; }="v2c";
    public string SnmpOperation { get; init; }="GET";
    public string SecurityLevel { get; init; }="authPriv";
    public string AuthProtocol { get; init; }="SHA256";
    public string PrivacyProtocol { get; init; }="AES128";
    public int Retries { get; init; }=1;
    public string Context { get; init; }="";
    public void Validate()
    {
        if(new[]{Target,Family,RecordType,Resolvers,Ports,LocalAddress,Broadcast,Method,Headers,Query,Body,ContentType,Authentication,Username,Sni,Oid,SnmpVersion,SnmpOperation,SecurityLevel,AuthProtocol,PrivacyProtocol,Context}.Any(value=>value==null))throw new ArgumentException(L.T("Parametry narzędzia są zbyt długie lub niepoprawne."));
        if(!Enum.IsDefined(Tool) || TimeoutMs is <100 or >60000 || IntervalMs is <100 or >60000 || Count is <1 or >100000 || PayloadBytes is <0 or >1400 || MaxHops is <1 or >64 || Concurrency is <1 or >64 || Port is <1 or >65535 || Retries is <0 or >3)throw new ArgumentException(L.T("Niepoprawne limity narzędzia sieciowego."));
        if(Family is not ("Auto" or "IPv4" or "IPv6") || Target.Length>2048 || Headers.Length>16384 || Query.Length>16384 || Body.Length>262144 || Resolvers.Length>512 || Oid.Length>512 || Sni.Length>253 || Username.Length>256 || Context.Length>256)throw new ArgumentException(L.T("Parametry narzędzia są zbyt długie lub niepoprawne."));
        if(Tool==NetworkToolKind.Http)
        {
            if(!Uri.TryCreate(Target,UriKind.Absolute,out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length>0 || !new[]{"GET","POST","PUT","PATCH","DELETE","HEAD"}.Contains(Method) || !new[]{"application/json","text/plain","application/x-www-form-urlencoded"}.Contains(ContentType) || Authentication is not ("None" or "Basic" or "Bearer"))throw new ArgumentException(L.T("Podaj adres HTTP/HTTPS bez danych logowania w URL."));
        }
        else if(Tool==NetworkToolKind.Subnet)_=SubnetCalculation.Calculate(Target,SplitPrefix);
        else if(Tool==NetworkToolKind.Wake) { _=MagicPacket(Target); if(!IPAddress.TryParse(Broadcast,out var ip) || ip.AddressFamily!=AddressFamily.InterNetwork)throw new ArgumentException(L.T("Podaj adres broadcast IPv4.")); }
        else if(Tool==NetworkToolKind.Dns)_=DnsName(Target);
        else _=Host(Target);
        if(Tool==NetworkToolKind.Ports)_=ParsePorts(Ports);
        if(Tool==NetworkToolKind.Snmp && (!System.Text.RegularExpressions.Regex.IsMatch(Oid,@"^\.?[0-2](?:\.\d+){1,127}$") || SnmpVersion is not ("v2c" or "v3") || SnmpOperation is not ("GET" or "GETNEXT" or "WALK" or "GETBULK") || SecurityLevel is not ("noAuthNoPriv" or "authNoPriv" or "authPriv") || AuthProtocol is not ("SHA256" or "SHA384" or "SHA512") || PrivacyProtocol!="AES128"))throw new ArgumentException(L.T("Niepoprawne parametry SNMP lub OID."));
    }
    public static string Host(string value)
    {
        value=value.Trim(); if(IPAddress.TryParse(value.Trim('[',']'),out var ip))return ip.ToString();
        if(value.Length is <1 or >253 || value.Any(c=>char.IsWhiteSpace(c) || char.IsControl(c)) || Uri.CheckHostName(value)!=UriHostNameType.Dns)throw new ArgumentException(L.T("Podaj poprawny adres IP lub nazwę hosta, bez schematu i portu."));
        return new IdnMapping().GetAscii(value);
    }
    public static string DnsName(string value)
    {
        value=value.Trim(); if(value==".")return value; if(IPAddress.TryParse(value,out var address))return address.ToString();
        if(value.Length is <1 or >254)throw new ArgumentException(L.T("Podaj poprawny adres IP lub nazwę hosta, bez schematu i portu."));
        var ascii=new IdnMapping().GetAscii(value.TrimEnd('.')); var labels=ascii.Split('.');
        if(ascii.Length>253 || labels.Where((label,index)=>label.Length is <1 or >63 || !(label=="*" && index==0) && label.Any(c=>!char.IsAsciiLetterOrDigit(c) && c!='-' && c!='_')).Any())throw new ArgumentException(L.T("Podaj poprawny adres IP lub nazwę hosta, bez schematu i portu."));
        return ascii+(value.EndsWith('.')?".":"");
    }
    public static int[] ParsePorts(string value)
    {
        if(value.Length>16384)throw new ArgumentException(L.T("Lista portów jest zbyt długa.")); var ports=new SortedSet<int>();
        foreach(var token in value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var ends=token.Split('-'); if(ends.Length>2 || !int.TryParse(ends[0],out var a) || a is <1 or >65535)throw new ArgumentException(L.T("Porty: podaj liczby 1–65535 lub zakres, oddzielone przecinkami."));
            var b=a; if(ends.Length==2 && (!int.TryParse(ends[1],out b) || b<a || b>65535))throw new ArgumentException(L.T("Niepoprawny zakres portów."));
            for(var p=a;p<=b;p++)ports.Add(p);
        }
        if(ports.Count==0)throw new ArgumentException(L.T("Wybierz co najmniej jeden port.")); return ports.ToArray();
    }
    public static byte[] MagicPacket(string mac)
    {
        var hex=mac.Replace(":","").Replace("-",""); if(hex.Length!=12 || hex.Any(c=>!Uri.IsHexDigit(c)))throw new ArgumentException(L.T("Podaj MAC w formacie AA:BB:CC:DD:EE:FF."));
        var bytes=Convert.FromHexString(hex); if((bytes[0]&1)!=0 || bytes.All(b=>b==0))throw new ArgumentException(L.T("Wymagany jest indywidualny adres MAC urządzenia."));
        var packet=Enumerable.Repeat((byte)255,102).ToArray(); for(var i=0;i<16;i++)bytes.CopyTo(packet,6+6*i); return packet;
    }
}
// Secrets never enter operation history. Request bodies/headers are persisted only in the OS credential store.
public sealed record NetworkSecrets(string Password="",string Token="",string Community="public",string AuthPassword="",string PrivacyPassword="");
public sealed record NetworkRow(string Key,string[] Cells,bool Error=false,double? Latency=null);
public sealed record NetworkUpdate(NetworkRow? Row=null,int Completed=0,int Total=0,string Summary="",bool Replace=false);
public sealed record NetworkResult(string Summary,string Body="",string Headers="",string Pem="");
public sealed record NetworkHistory(Guid Id,DateTimeOffset When,NetworkRequest Request);
public sealed record NetworkCollection(Guid Id,string Name,NetworkToolKind Tool);
public static class NetworkExport
{
    public static string Csv(IEnumerable<string[]> rows)=>string.Join("\r\n",rows.Select(row=>string.Join(",",row.Select(Cell))));
    private static string Cell(string value)
    { if(value.Length>0 && "=+-@\t\r".Contains(value[0]))value="'"+value; return "\""+value.Replace("\"","\"\"")+"\""; }
    public static string Render(string format,string[] columns,IEnumerable<NetworkRow> rows)=>format switch { "JSON"=>JsonSerializer.Serialize(rows.Select(r=>columns.Select((column,i)=>(column,value:i<r.Cells.Length?r.Cells[i]:"")).ToDictionary(pair=>pair.column,pair=>pair.value)),new JsonSerializerOptions { WriteIndented=true }),"CSV"=>Csv(new[]{columns}.Concat(rows.Select(row=>row.Cells))),_=>string.Join(Environment.NewLine,new[]{string.Join("\t",columns)}.Concat(rows.Select(row=>string.Join("\t",row.Cells)))) };
}
