using System.Text.Json;

namespace ServerDesk.App;

// Only constructed by the opt-in isolated --check-rdp-input workbench. This API
// controls its own fixture session; it cannot select arbitrary windows or input,
// execute commands, accept certificates or open saved production connections.
internal sealed class RdpMatrixControl(string root,Func<string,Task<object>> execute)
{
    private bool busy;
    private string? previous;
    internal async Task PollAsync()
    {
        if(busy)return;
        busy=true;
        try
        {
            var path=Path.Combine(root,"matrix-request.json");
            if(!File.Exists(path) || new FileInfo(path).Length>1024)return;
            using var request=JsonDocument.Parse(await File.ReadAllTextAsync(path));
            if(request.RootElement.ValueKind!=JsonValueKind.Object || !request.RootElement.TryGetProperty("Id",out var identity) || identity.ValueKind!=JsonValueKind.String || !request.RootElement.TryGetProperty("Operation",out var action) || action.ValueKind!=JsonValueKind.String)return;
            var id=identity.GetString();
            if(!Guid.TryParse(id,out _) || id==previous)return;
            previous=id;object result;
            try { result=await execute(action.GetString()!); }
            catch(Exception error) { result=new { Error=error.GetType().Name,HResult=$"0x{error.HResult:X8}",StackTrace=error.StackTrace }; }
            var output=Path.Combine(root,"matrix-response-"+id+".json");
            await File.WriteAllTextAsync(output+".part",JsonSerializer.Serialize(result));File.Move(output+".part",output);
        }
        catch(IOException) { } catch(JsonException) { }
        finally { busy=false; }
    }
}
