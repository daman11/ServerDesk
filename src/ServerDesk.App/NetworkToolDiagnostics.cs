using Avalonia.Controls;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class NetworkToolPane
{
    private static string WithErrorId(string message,string id)=>message+"\n"+L.F($"Identyfikator błędu: {id}");
    private void ReportFailure(Exception error,string operation,bool local)
    {
        // Names come only from fixed call sites and the tool enum. Do not add
        // targets, collection names, request bodies, headers or credentials.
        var id=error is ArgumentException?null:ApplicationDiagnostics.Report($"NetworkTools.{Tool}.{operation}",error,Serilog.Events.LogEventLevel.Warning);
        // Retain diagnostics for an action finishing after its tab is closed,
        // but never update the detached UI or open another window.
        if(disposed)return;
        var message=local?OperationErrorMessages.Describe(error):NetworkToolsService.Explain(error);
        status.Text=id==null?message:WithErrorId(message,id);
        DesignSystem.Resource(status,TextBlock.ForegroundProperty,"Danger");
    }
}
