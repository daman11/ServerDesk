using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Diagnostics;
using System.Text;

if(args.Length<1)throw new ArgumentException("Podaj adres RDP i opcjonalnie port.");
var profile=new ServerProfile { Name="Endpoint diagnostic",Protocol=Protocol.RDP,Host=args[0],Port=args.Length>1 && !args[1].StartsWith("--")?int.Parse(args[1]):3389 };
var info=await new RdpEndpointProbe().CheckAsync(profile,CancellationToken.None,inspectCertificate:true);
Console.WriteLine($"RDP reachable: {info.Security}, {info.RoundTrip.TotalMilliseconds:0} ms");
Console.WriteLine("Certificate subject: "+info.CertificateSubject);
Console.WriteLine("Certificate SHA256: "+info.CertificateFingerprint);
Console.WriteLine("Certificate expiry: "+info.CertificateExpires?.ToString("O"));
Console.WriteLine("Certificate validation: "+info.CertificateErrors);
if(args.Contains("--auth"))
{
    string Option(string name)
    {
        var index=Array.IndexOf(args,name);
        return index>=0 && index+1<args.Length && !args[index+1].StartsWith("--")?args[index+1]:throw new ArgumentException("Missing option: "+name);
    }
    var pin=Option("--pin").Replace(":","").ToUpperInvariant();
    if(pin!=info.CertificateFingerprint)throw new InvalidOperationException("Certyfikat różni się od zatwierdzonego odcisku. Logowanie zablokowane.");
    var user=Option("--user"); var domain=Option("--domain"); if(domain=="-")domain=""; RdpIdentity.Parse(user,domain);
    Console.WriteLine("Podaj hasło przez stdin (nie zostanie wyświetlone ani zapisane):");
    string password;
    if(Console.IsInputRedirected)password=Console.ReadLine()??throw new IOException("Brak hasła na stdin.");
    else
    {
        var buffer=new StringBuilder();
        while(true) { var key=Console.ReadKey(intercept:true); if(key.Key==ConsoleKey.Enter)break; if(key.Key==ConsoleKey.Backspace) { if(buffer.Length>0)buffer.Length--; } else if(!char.IsControl(key.KeyChar))buffer.Append(key.KeyChar); }
        password=buffer.ToString(); buffer.Clear();
    }
    if(password.Length==0)throw new IOException("Puste hasło.");
    var start=new ProcessStartInfo(OperatingSystem.IsWindows()?"wsl.exe":"timeout") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true };
    var desktop=args.Contains("--desktop");
    if(OperatingSystem.IsWindows())
    {
        if(Environment.GetEnvironmentVariable("SERVERDESK_TEST_WSL_DISTRO") is { Length:>0 } distro) { start.ArgumentList.Add("-d"); start.ArgumentList.Add(distro); }
        start.ArgumentList.Add("--exec"); start.ArgumentList.Add("timeout");
    }
    var executable=Environment.GetEnvironmentVariable("SERVERDESK_TEST_FREERDP")??"xfreerdp3";
    foreach(var item in new[]{desktop?"95":"25",executable,"/v:"+RdpConfiguration.Address(profile),"/u:"+user,"/d:"+domain,"/sec:nla","/from-stdin:force","+auth-only","/size:1280x720","/title:ServerDesk RDP integration test","-clipboard","/cert:fingerprint:sha256:"+string.Join(":",Enumerable.Range(0,pin.Length/2).Select(i=>pin.Substring(i*2,2))),"/log-level:INFO"}.Where(item=>item!="/d:" && !(desktop && item=="+auth-only")))start.ArgumentList.Add(item);
    using var process=Process.Start(start)??throw new IOException("Nie uruchomiono FreeRDP.");
    var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
    // Linux getline strips LF; a Windows CRLF would add CR to the actual password.
    await process.StandardInput.WriteAsync(password+"\n"); process.StandardInput.Close(); await process.WaitForExitAsync();
    var combined=((await output)+(await error)).Replace(password,"[redacted]"); password="";
    Directory.CreateDirectory("artifacts");
    await File.WriteAllTextAsync(Path.Combine("artifacts",desktop?"rdp-desktop-session.txt":"rdp-authentication.txt"),combined);
    Console.WriteLine(combined);
    Console.WriteLine(desktop?"Desktop client completed; exit="+process.ExitCode:process.ExitCode==0?"PASS real RDP NLA authentication (no desktop requested)":"FAIL RDP authentication; exit="+process.ExitCode);
    Environment.ExitCode=desktop && process.ExitCode==124?0:process.ExitCode;
}
