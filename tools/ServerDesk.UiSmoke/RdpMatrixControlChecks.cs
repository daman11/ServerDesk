using System.Text.Json;
using Avalonia.Threading;
using ServerDesk.App;

internal static class RdpMatrixControlChecks
{
    internal static void Run(string root)
    {
        SynchronizationContext.SetSynchronizationContext(new AvaloniaSynchronizationContext(Dispatcher.UIThread,DispatcherPriority.Normal));
        var folder=Path.Combine(root,"owned-matrix");Directory.CreateDirectory(folder);var calls=0;
        var control=new RdpMatrixControl(folder,async operation=>
        {
            Dispatcher.UIThread.VerifyAccess();calls++;await Task.Delay(5);Dispatcher.UIThread.VerifyAccess();
            if(operation=="fail")throw new IOException("synthetic secret must not enter response");
            return new { Count=calls };
        });
        void Poll()=>RedesignChecks.Wait(control.PollAsync());
        void Raw(string value)=>File.WriteAllText(Path.Combine(folder,"matrix-request.json"),value);
        foreach(var raw in new[]{"{","[]","{}","{\"Id\":2,\"Operation\":null}","{\"Id\":\"../../elsewhere\",\"Operation\":\"snapshot\"}",new string('x',1025)}){Raw(raw);Poll();}
        if(calls!=0 || Directory.GetFiles(folder,"matrix-response-*").Length!=0)throw new Exception("Malformed fixture request executed");
        var id=Guid.NewGuid();Raw(JsonSerializer.Serialize(new { Id=id,Operation="snapshot" }));
        RedesignChecks.Wait(Task.WhenAll(control.PollAsync(),control.PollAsync()));Poll();
        if(calls!=1 || !File.Exists(Path.Combine(folder,$"matrix-response-{id}.json")))throw new Exception("Duplicate/reentrant matrix request executed");
        id=Guid.NewGuid();Raw(JsonSerializer.Serialize(new { Id=id,Operation="fail" }));Poll();
        var response=File.ReadAllText(Path.Combine(folder,$"matrix-response-{id}.json"));
        if(!response.Contains("IOException") || response.Contains("synthetic secret"))throw new Exception("Matrix failure response contains unsafe data or lost its error type");
        Console.WriteLine("PASS bounded matrix requests: malformed IDs/JSON/oversize rejected, reentrancy and replay contained, UI thread preserved across awaits, safe exception response");
    }
}
