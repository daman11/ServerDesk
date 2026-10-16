using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace ServerDesk.Core;

public sealed record SubnetCalculation(string Address,string Expanded,string Network,string Last,string Mask,string Wildcard,string FirstUsable,string LastUsable,BigInteger Addresses,BigInteger Hosts,int Prefix,bool IPv6,IReadOnlyList<string> Subnets)
{
    public static SubnetCalculation Calculate(string input,int split=-1)
    {
        var pieces=input.Trim().Split(new[]{'/',' '},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(pieces.Length!=2 || !IPAddress.TryParse(pieces[0],out var address) || address.ScopeIdSafe()!=0)throw new ArgumentException(L.T("Podaj adres i prefiks, np. 192.168.1.10/24 lub 2001:db8::1/64."));
        var v6=address.AddressFamily==AddressFamily.InterNetworkV6; var bits=v6?128:32; int prefix;
        if(!int.TryParse(pieces[1],out prefix))
        {
            if(v6 || !IPAddress.TryParse(pieces[1],out var mask) || mask.AddressFamily!=AddressFamily.InterNetwork)throw new ArgumentException(L.T("Niepoprawna maska sieci."));
            var maskBits=string.Concat(mask.GetAddressBytes().Select(b=>Convert.ToString(b,2).PadLeft(8,'0'))); if(maskBits.Contains("01"))throw new ArgumentException(L.T("Maska musi być ciągła.")); prefix=maskBits.Count(c=>c=='1');
        }
        if(prefix<0 || prefix>bits || split!=-1 && (split<prefix || split>bits || split-prefix>12))throw new ArgumentException(L.T("Niepoprawny prefiks. Można wygenerować najwyżej 4096 podsieci."));
        var number=new BigInteger(address.GetAddressBytes(),isUnsigned:true,isBigEndian:true); var size=BigInteger.One<<(bits-prefix); var first=number/size*size; var last=first+size-1; var all=(BigInteger.One<<bits)-1; var netmask=all-(size-1);
        string Ip(BigInteger n) { var bytes=n.ToByteArray(isUnsigned:true,isBigEndian:true); var padded=new byte[bits/8]; bytes.CopyTo(padded,padded.Length-bytes.Length); return new IPAddress(padded).ToString(); }
        var expanded=v6?string.Join(":",Enumerable.Range(0,8).Select(i=>Convert.ToHexString(address.GetAddressBytes().AsSpan(i*2,2)).ToLowerInvariant())):address.ToString();
        var usable=v6 || prefix>=31?size:BigInteger.Max(0,size-2); var networks=new List<string>();
        if(split!=-1) { var step=BigInteger.One<<(bits-split); for(var i=0;i<(1<<(split-prefix));i++)networks.Add(Ip(first+i*step)+"/"+split); }
        return new(address.ToString(),expanded,Ip(first),Ip(last),v6?"—":Ip(netmask),v6?"—":Ip(size-1),Ip(first+(v6 || prefix>=31?0:1)),Ip(last-(v6 || prefix>=31?0:1)),size,usable,prefix,v6,networks);
    }
}
internal static class AddressScope { internal static long ScopeIdSafe(this IPAddress address)=>address.AddressFamily==AddressFamily.InterNetworkV6?address.ScopeId:0; }
