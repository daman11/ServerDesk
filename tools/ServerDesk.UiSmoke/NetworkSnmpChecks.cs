using ServerDesk.Core;
using ServerDesk.Infrastructure;
internal static class NetworkSnmpChecks
{
    private sealed class Capture : IProgress<NetworkUpdate>
    { internal readonly List<NetworkRow> Rows=[]; public void Report(NetworkUpdate update) { if(update.Row!=null)Rows.Add(update.Row); } }
    internal static void Run(string host,int port)
    {
        foreach(var level in new[]{"noAuthNoPriv","authNoPriv","authPriv"})foreach(var auth in level=="noAuthNoPriv"?new[]{"SHA256"}:new[]{"SHA256","SHA384","SHA512"})foreach(var operation in new[]{"GET","GETNEXT","WALK","GETBULK"})
        {
            var output=new Capture();
            var request=new NetworkRequest { Tool=NetworkToolKind.Snmp,Target=host,Port=port,SnmpVersion="v3",SecurityLevel=level,AuthProtocol=auth,Username=level=="noAuthNoPriv"?"fixture-none":auth switch { "SHA384"=>"fixture-384","SHA512"=>"fixture-512",_=>"fixture-256" },Oid=operation=="WALK"?"1.3.6.1.2.1.1":"1.3.6.1.2.1.1.1.0",SnmpOperation=operation,TimeoutMs=2000 };
            var result=new NetworkToolsService().RunAsync(request,new(AuthPassword:"fixture-auth-2026",PrivacyPassword:"fixture-priv-2026"),output,CancellationToken.None); RedesignChecks.Wait(result);
            if(output.Rows.Count==0 || output.Rows.Any(row=>row.Cells[1].Contains("NoSuch")))throw new Exception("SNMP v3 fixture did not return data");
            Console.WriteLine($"PASS actual Net-SNMP v3 {level} {auth} AES-128 {operation}: {output.Rows.Count} rows");
        }
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        try { new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Snmp,Target=host,Port=port },new(),new Capture(),cancelled.Token).GetAwaiter().GetResult(); throw new Exception("Cancelled SNMP operation started"); }
        catch(OperationCanceledException) { Console.WriteLine("PASS SNMP cancellation before operation"); }
    }
}
