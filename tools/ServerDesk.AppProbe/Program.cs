using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
if(args.FirstOrDefault()=="--com-types") { ComTypeChecks.Run(); return; }
var interaction=args.Contains("--interaction"); var network=args.Contains("--network"); args=args.Where(a=>a is not ("--interaction" or "--network")).ToArray();
if(network && !interaction)throw new ArgumentException("--network requires --interaction.");

if(!OperatingSystem.IsWindows() || args.Length is not (2 or 4))throw new ArgumentException("AppProbe EXE REPORT [HOST USER]");
var exe=Path.GetFullPath(args[0]); var report=Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(report)!);
var password="";
if(args.Length==4)
{
    Console.WriteLine("RDP password (hidden; memory only):");
    if(Console.IsInputRedirected)password=Console.ReadLine()??"";
    else
    {
        var input=new StringBuilder();
        while(true) { var key=Console.ReadKey(true); if(key.Key==ConsoleKey.Enter)break; if(key.Key==ConsoleKey.Backspace) { if(input.Length>0)input.Length--; } else if(!char.IsControl(key.KeyChar))input.Append(key.KeyChar); }
        password=input.ToString(); input.Clear();
    }
}
var pipeName="serverdesk-release-"+Guid.NewGuid().ToString("N");
using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
var matrixMinutes=int.TryParse(Environment.GetEnvironmentVariable("SERVERDESK_RDP_MATRIX_MINUTES"),out var configuredMinutes)?Math.Clamp(configuredMinutes,1,120):20;
using var timeout=new CancellationTokenSource(interaction?TimeSpan.FromMinutes(matrixMinutes+2):TimeSpan.FromSeconds(115));
var start=new ProcessStartInfo(exe) { UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe)!,CreateNoWindow=true,WindowStyle=interaction?ProcessWindowStyle.Normal:ProcessWindowStyle.Hidden };
start.ArgumentList.Add(interaction?"--check-rdp-input":"--check-rdp-host"); start.ArgumentList.Add(report);
if(args.Length==4) { start.ArgumentList.Add(args[2]); start.ArgumentList.Add(args[3]); start.ArgumentList.Add(pipeName); }
if(network)start.ArgumentList.Add("--proxy");
using var process=Process.Start(start)??throw new IOException("Unable to start app.");
try
{
    if(args.Length==4)
    {
        await pipe.WaitForConnectionAsync(timeout.Token);
        await using var writer=new StreamWriter(pipe,new UTF8Encoding(false),leaveOpen:true) { AutoFlush=true };
        await writer.WriteAsync(password+"\n"); password="";
    }
    await process.WaitForExitAsync(timeout.Token);
    Console.WriteLine(await File.ReadAllTextAsync(report));
    using var result=JsonDocument.Parse(await File.ReadAllTextAsync(report));
    if(process.ExitCode!=0 || !interaction && !result.RootElement.GetProperty("Success").GetBoolean())throw new Exception("Published EXE failed native RDP check.");
    Console.WriteLine(interaction?"Completed interactive RDP report; individual results are listed above":"PASS published EXE native RDP check");
}
finally { password=""; if(!process.HasExited)process.Kill(entireProcessTree:true); }
