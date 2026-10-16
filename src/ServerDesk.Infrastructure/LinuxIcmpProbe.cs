using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace ServerDesk.Infrastructure;
public static class LinuxIcmpProbe
{
    public static async Task<IcmpReply> SendAsync(IPAddress address,int ttl,int bytes,int timeoutMs,CancellationToken ct)
    {
        var executable=new[]{"/usr/bin/ping","/bin/ping"}.FirstOrDefault(File.Exists)??throw new FileNotFoundException("ping");
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true };
        start.Environment["LC_ALL"]="C"; start.Environment["LANG"]="C";
        foreach(var argument in new[]{address.AddressFamily==AddressFamily.InterNetworkV6?"-6":"-4","-n","-c","1","-t",ttl.ToString(CultureInfo.InvariantCulture),"-W",(timeoutMs/1000d).ToString("0.###",CultureInfo.InvariantCulture),"-s",bytes.ToString(CultureInfo.InvariantCulture),"--",address.ToString()})start.ArgumentList.Add(argument);
        using var process=Process.Start(start)??throw new IOException("ping"); using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(timeoutMs+1000);
        try
        {
            var stdout=ReadAsync(process.StandardOutput,timeout.Token); var stderr=ReadAsync(process.StandardError,timeout.Token);
            await Task.WhenAll(stdout,stderr,process.WaitForExitAsync(timeout.Token)).ConfigureAwait(false);
            if(process.ExitCode==2)
            {
                if(stderr.Result.Contains("permitted",StringComparison.OrdinalIgnoreCase) || stderr.Result.Contains("permission",StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException();
                throw new PlatformNotSupportedException("iputils-ping");
            }
            return Parse(stdout.Result,address);
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) { return new(null,IPStatus.TimedOut,0); }
        finally { if(!process.HasExited) { process.Kill(entireProcessTree:true); await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } }
    }
    private static async Task<string> ReadAsync(StreamReader reader,CancellationToken ct)
    {
        var result=new StringBuilder(); var buffer=new char[512]; int count;
        while((count=await reader.ReadAsync(buffer,ct).ConfigureAwait(false))>0) { if(result.Length+count>8192)throw new InvalidDataException("ping output limit"); result.Append(buffer,0,count); }
        return result.ToString();
    }
    public static IcmpReply Parse(string output,IPAddress target)
    {
        var time=Regex.Match(output,@"time[=<]([0-9.]+)\s*ms",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        if(time.Success && double.TryParse(time.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var ms))return new(target,IPStatus.Success,ms);
        var from=Regex.Match(output,@"From\s+(\S+)",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        IPAddress? address=null; if(from.Success && !IPAddress.TryParse(from.Groups[1].Value,out address))IPAddress.TryParse(from.Groups[1].Value.TrimEnd(':'),out address);
        if(output.Contains("Time to live exceeded",StringComparison.OrdinalIgnoreCase) || output.Contains("Hop limit",StringComparison.OrdinalIgnoreCase))return new(address,IPStatus.TtlExpired,0);
        if(output.Contains("Unreachable",StringComparison.OrdinalIgnoreCase))return new(address,IPStatus.DestinationHostUnreachable,0);
        return new(null,IPStatus.TimedOut,0);
    }
}
