using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ServerDesk.Core;

public sealed record RenderedCommand(string Text,bool ContainsSecret,bool ContainsCode,bool IsMultiline,CommandShell Shell=CommandShell.Any);
public static partial class CommandRenderer
{
    public static RenderedCommand Render(CommandDefinition command,CommandShell shell,IReadOnlyDictionary<string,string> values,bool allowCode=false)
    {
        CommandValidation.Validate(command,command.Origin==CommandOrigin.User);
        if(command.Parameters.Length>0 && shell==CommandShell.Any)CommandValidation.Fail("Wybierz powłokę przed uzupełnieniem parametrów.");
        var secret=false; var code=false;
        var text=CommandValidation.Placeholder().Replace(command.Template,match=>
        {
            var p=command.Parameters.Single(p=>p.Key==match.Groups[1].Value);
            var value=values.GetValueOrDefault(p.Key,p.Default); ValidateValue(p,value);
            secret|=p.Secret&&value.Length>0;
            if(p.Kind==CommandParameterKind.Code)
            { code=true; if(!allowCode)CommandValidation.Fail("Fragment kodu wymaga świadomego zatwierdzenia."); return value; }
            return Quote(value,shell);
        });
        if(text.Length>32000)CommandValidation.Fail("Wygenerowana komenda jest zbyt długa.");
        return new(text,secret,code,text.Contains('\n')||text.Contains('\r'),shell);
    }
    public static void ValidateValue(CommandParameter p,string value)
    {
        if(value==null||value.Length>4000||value.Any(CommandValidation.UnsafeCharacter)||p.Required&&string.IsNullOrWhiteSpace(value))CommandValidation.Fail("Uzupełnij wymagane parametry bez znaków sterujących.");
        if(value.Length==0&&!p.Required)return;
        if(p.Kind==CommandParameterKind.Number && (!decimal.TryParse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out _)))CommandValidation.Fail("Parametr wymaga liczby z kropką dziesiętną.");
        if(p.Kind==CommandParameterKind.IpAddress&&!IPAddress.TryParse(value,out _))CommandValidation.Fail("Parametr wymaga adresu IP.");
        if(p.Kind==CommandParameterKind.Hostname&&(Uri.CheckHostName(value)==UriHostNameType.Unknown||value.StartsWith('-')))CommandValidation.Fail("Parametr wymaga nazwy hosta.");
        if(p.Kind==CommandParameterKind.Choice&&!p.Choices.Contains(value,StringComparer.Ordinal))CommandValidation.Fail("Wybierz wartość z listy.");
        // Quoting does not prevent option injection. Data arguments never become flags.
        if(p.Kind is not (CommandParameterKind.Code or CommandParameterKind.Number) && value.StartsWith('-'))CommandValidation.Fail("Argument danych nie może zaczynać się od myślnika.");
    }
    public static string Quote(string value,CommandShell shell)
    {
        if(value.Any(CommandValidation.UnsafeCharacter))CommandValidation.Fail("Parametr zawiera znaki sterujące.");
        return shell switch
        {
            CommandShell.Bash or CommandShell.Zsh=>"'"+value.Replace("'","'\"'\"'",StringComparison.Ordinal)+"'",
            CommandShell.PowerShell=>"'"+value.Replace("'","''",StringComparison.Ordinal).Replace("‘","‘‘",StringComparison.Ordinal).Replace("’","’’",StringComparison.Ordinal)+"'",
            // CMD expansion (%VAR%, delayed !VAR!) and quoting rules differ between
            // built-ins and external programs. Refuse ambiguous characters; never
            // pretend that backslash or PowerShell quoting secures CMD input.
            CommandShell.Cmd when CmdData().IsMatch(value)&&!value.EndsWith('\\')=>"\""+value+"\"",
            CommandShell.Cmd=>throw new ArgumentException(L.T("CMD: użyj liter, cyfr, spacji i prostych ścieżek. Znaki składni wymagają parametru Code.")),
            _=>throw new ArgumentException(L.T("Wybierz konkretną powłokę."))
        };
    }
    [GeneratedRegex(@"^[\p{L}\p{N} _./\\:@,+-]*$")] private static partial Regex CmdData();
}

public sealed record CommandDispatchTicket(Guid SessionId,Guid ConnectionId,string CommandId);
public static class CommandDispatch
{
    public static string Prepare(CommandDispatchTicket ticket,Guid activeSession,Guid currentConnection,RenderedCommand command,bool execute,CommandRisk risk,bool confirmed,bool dangerousConfirmed)
    {
        if(ticket.SessionId!=activeSession||ticket.ConnectionId!=currentConnection||currentConnection==Guid.Empty)throw new InvalidOperationException(L.T("Sesja zmieniła się. Wybierz komendę ponownie."));
        // Reject pasted terminal controls even in trusted templates. A multiline
        // payload is copy-only: shells/editors do not consistently honour bracketed paste.
        if(command.IsMultiline||command.Text.Any(CommandValidation.UnsafeCharacter))throw new InvalidOperationException(L.T("Skrypt wieloliniowy jest dostępny tylko do kopiowania i podglądu."));
        if(execute && (!confirmed||risk==CommandRisk.Dangerous&&!dangerousConfirmed))throw new InvalidOperationException(L.T("Wykonanie wymaga potwierdzenia."));
        if(command.ContainsCode&&!confirmed)throw new InvalidOperationException(L.T("Fragment kodu wymaga świadomego zatwierdzenia."));
        return command.Text+(execute?"\r":"");
    }
}
