using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private Guid commandConnectionId;
    private CommandEnvironment? commandEnvironment;
    private Task<CommandEnvironment>? commandDiscovery;
    internal Guid CommandConnectionId=>session is { Completion.IsCompleted:false }?commandConnectionId:Guid.Empty;
    internal bool CanAcceptCommands=>Profile.Protocol==Protocol.SSH&&CommandConnectionId!=Guid.Empty&&disposed==0&&!connecting;
    internal CommandEnvironment? KnownCommandEnvironment=>commandEnvironment;
    private async Task WarmCommandDiscoveryAsync()
    {
        try { if(await owner.CommandsAutomaticAsync(Profile))await DiscoverCommandsAsync(); }
        catch { /* Discovery is optional. The panel exposes unknown state / manual refresh. */ }
    }
    internal Task<CommandEnvironment> DiscoverCommandsAsync(bool refresh=false)
    {
        if(!CanAcceptCommands)return Task.FromResult(CommandDetection.Unknown());
        if(!refresh&&commandEnvironment!=null)return Task.FromResult(commandEnvironment);
        if(commandDiscovery is { IsCompleted:false })return commandDiscovery;
        var connection=session!; var identity=CommandConnectionId; var ct=connectionLifetime.Token;
        return commandDiscovery=Discover();
        async Task<CommandEnvironment> Discover()
        {
            var result=await CommandDetection.DetectAsync(connection.ProbeAsync,ct);
            if(disposed==0 && CommandConnectionId==identity && ReferenceEquals(session,connection))commandEnvironment=result;
            else throw new OperationCanceledException();
            return result;
        }
    }
    internal async Task SendCommandAsync(CommandDispatchTicket ticket,RenderedCommand command,CommandDefinition definition,bool execute,bool confirmed,bool dangerousConfirmed)
    {
        if(!CanAcceptCommands||!ReferenceEquals(owner.CommandTarget,this))throw new InvalidOperationException(L.T("Sesja zmieniła się. Wybierz komendę ponownie."));
        if(!CommandCatalog.Scope(definition,Profile.Id,Profile.Folder))throw new InvalidOperationException(L.T("Komenda nie pasuje do wykrytego systemu lub wymaganego narzędzia."));
        if(command.ContainsSecret)throw new InvalidOperationException(L.T("Sekretne parametry są dostępne tylko do kopiowania; nie wysyłamy ich do historii powłoki."));
        var text=CommandDispatch.Prepare(ticket,WorkspaceId,CommandConnectionId,command,execute,definition.Risk,confirmed,dangerousConfirmed);
        if(commandEnvironment is { Platform:not CommandPlatform.General } actual && (!CommandCatalog.Supports(definition,actual)||definition.Tools.Any(t=>actual.Tool(t)==ToolAvailability.NotDetected)||!CompatibleShell(command.Shell,actual.Shell)))throw new InvalidOperationException(L.T("Komenda nie pasuje do wykrytego systemu lub wymaganego narzędzia."));
        SelectPanel(0); await terminal.SendInputAsync(text,connectionLifetime.Token); terminal.Focus();
    }
    internal static bool CompatibleShell(CommandShell rendered,CommandShell actual)=>actual==CommandShell.Any||rendered==CommandShell.Any||rendered==actual||actual==CommandShell.Zsh&&rendered==CommandShell.Bash;
}
