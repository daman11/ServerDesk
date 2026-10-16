using System.Net;
using System.Net.NetworkInformation;
namespace ServerDesk.Infrastructure;
public sealed record IcmpReply(IPAddress? Address,IPStatus Status,double Milliseconds);
public interface IIcmpProbe { Task<IcmpReply> SendAsync(IPAddress address,int ttl,int bytes,int timeoutMs,CancellationToken ct); }
public sealed class NativeIcmpProbe : IIcmpProbe
{
    private bool utility;
    public async Task<IcmpReply> SendAsync(IPAddress address,int ttl,int bytes,int timeoutMs,CancellationToken ct)
    {
        if(utility)return await LinuxIcmpProbe.SendAsync(address,ttl,bytes,timeoutMs,ct).ConfigureAwait(false);
        try { using var ping=new Ping(); var reply=await ping.SendPingAsync(address,TimeSpan.FromMilliseconds(timeoutMs),new byte[bytes],new PingOptions(ttl,true),ct).ConfigureAwait(false); return new(reply.Address,reply.Status,reply.RoundtripTime); }
        catch(PlatformNotSupportedException) when(OperatingSystem.IsLinux()) { utility=true; return await LinuxIcmpProbe.SendAsync(address,ttl,bytes,timeoutMs,ct).ConfigureAwait(false); }
    }
}
