using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ServerDesk.Core;

public enum CommandPlatform { General, Linux, Windows }
public enum CommandShell { Any, Bash, Zsh, PowerShell, Cmd }
public enum CommandRisk { ReadOnly, Modify, Dangerous }
public enum CommandOrigin { BuiltIn, User }
public enum CommandParameterKind { Text, Number, IpAddress, Hostname, FilePath, Choice, Code }
public enum ToolAvailability { Unknown, Available, NotDetected }
public sealed record CommandParameter
{
    public string Key { get; init; }="";
    public string Label { get; init; }="";
    public CommandParameterKind Kind { get; init; }
    public bool Required { get; init; }=true;
    public bool Secret { get; init; }
    public string Default { get; init; }="";
    public string[] Choices { get; init; }=[];
}
public sealed record CommandDefinition
{
    public string Id { get; init; }="";
    public string Operation { get; init; }="";
    public string Name { get; init; }="";
    public string NameEn { get; init; }="";
    public string Summary { get; init; }="";
    public string SummaryEn { get; init; }="";
    public string Explanation { get; init; }="";
    public string ExplanationEn { get; init; }="";
    public string Template { get; init; }="";
    public string Category { get; init; }="";
    public string Subcategory { get; init; }="";
    public string[] Tags { get; init; }=[];
    public CommandPlatform Platform { get; init; }
    public string Family { get; init; }="";
    public string[] Distributions { get; init; }=[];
    public string MinVersion { get; init; }="";
    public string MaxVersion { get; init; }="";
    public CommandShell Shell { get; init; }
    public string[] Tools { get; init; }=[];
    public string Privileges { get; init; }="user";
    public CommandRisk Risk { get; init; }
    public CommandOrigin Origin { get; init; }
    public int Priority { get; init; }
    public CommandParameter[] Parameters { get; init; }=[];
    public string[] Examples { get; init; }=[];
    public Guid? HostId { get; init; }
    public string HostFolder { get; init; }="";
    [JsonIgnore] public string Title=>AppText.Language=="en" && NameEn.Length>0?NameEn:Name;
    [JsonIgnore] public string Description=>AppText.Language=="en" && SummaryEn.Length>0?SummaryEn:Summary;
    [JsonIgnore] public string Detail=>AppText.Language=="en" && ExplanationEn.Length>0?ExplanationEn:Explanation;
}
public sealed record CommandEnvironment
{
    public CommandPlatform Platform { get; init; }
    public string Distribution { get; init; }="";
    public string Family { get; init; }="";
    public string Version { get; init; }="";
    public string PrettyName { get; init; }="";
    public CommandShell Shell { get; init; }
    public IReadOnlyDictionary<string,ToolAvailability> Tools { get; init; }=new Dictionary<string,ToolAvailability>();
    public bool Detected { get; init; }
    public string Diagnostic { get; init; }="";
    public ToolAvailability Tool(string name)=>Tools.GetValueOrDefault(name,ToolAvailability.Unknown);
    public static CommandEnvironment Manual(string id,string version="",CommandShell shell=CommandShell.Any)=>new()
    {
        Platform=id=="general"?CommandPlatform.General:id.StartsWith("windows",StringComparison.Ordinal)?CommandPlatform.Windows:CommandPlatform.Linux,
        Distribution=id is "general" or "linux"?"":id,Family=CommandDetection.Family(id,""),Version=version,
        Shell=shell,PrettyName=id
    };
}
public sealed record CommandRecent(string CommandId,DateTimeOffset UsedAt);
public sealed record CommandPanelPreferences
{
    public bool Automatic { get; init; }=true;
    public bool Pinned { get; init; }
    public string System { get; init; }="general";
    public string Version { get; init; }="";
    public CommandShell Shell { get; init; }
    public bool RememberRecent { get; init; }
}
public sealed record CommandUserLibrary
{
    public int SchemaVersion { get; init; }=1;
    public CommandDefinition[] Commands { get; init; }=[];
    public string[] Categories { get; init; }=[];
}
public sealed record CommandUserState
{
    public string[] Favorites { get; init; }=[];
    public CommandRecent[] Recent { get; init; }=[];
    public CommandPanelPreferences Preferences { get; init; }=new();
    public Dictionary<Guid,CommandPanelPreferences> Hosts { get; init; }=[];
}
public static partial class CommandValidation
{
    public const int MaxCommands=2000,MaxDocumentBytes=4*1024*1024;
    public static JsonSerializerOptions Json { get; }=new()
    {
        PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        Converters={new JsonStringEnumConverter(allowIntegerValues:false)},MaxDepth=20
    };
    public static void Validate(CommandDefinition c,bool user=false)
    {
        ArgumentNullException.ThrowIfNull(c);
        if(c.Id==null || !Identifier().IsMatch(c.Id) || user && (!c.Id.StartsWith("user.",StringComparison.Ordinal) || c.Origin!=CommandOrigin.User))Fail("Niepoprawny identyfikator komendy.");
        Text(c.Operation,120); Text(c.Name,120,true); Text(c.NameEn,120); Text(c.Summary,400); Text(c.SummaryEn,400); Text(c.Explanation,6000); Text(c.ExplanationEn,6000);
        Text(c.Template,16000,true,true); Category(c.Category); Text(c.Subcategory,160); if(c.Subcategory.Length>0)Category(c.Subcategory);
        Text(c.Family,40); Text(c.MinVersion,40); Text(c.MaxVersion,40); Text(c.Privileges,120); Text(c.HostFolder,240);
        if(!Enum.IsDefined(c.Platform)||!Enum.IsDefined(c.Shell)||!Enum.IsDefined(c.Risk)||!Enum.IsDefined(c.Origin)||c.Priority is <0 or >20)Fail("Niepoprawne metadane komendy.");
        if(c.Tags==null||c.Tools==null||c.Distributions==null||c.Examples==null||c.Parameters==null||c.Tags.Length>40||c.Tools.Length>24||c.Parameters.Length>24||c.Distributions.Length>24||c.Examples.Length>12)Fail("Za dużo metadanych komendy.");
        foreach(var value in c.Tags.Concat(c.Tools).Concat(c.Distributions))Text(value,100,true);
        foreach(var value in c.Examples)Text(value,2000,false,true);
        if(c.Tools.Any(t=>!ToolName().IsMatch(t)))Fail("Niepoprawna nazwa narzędzia.");
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in c.Parameters)
        {
            if(p==null || p.Key==null || !ParameterName().IsMatch(p.Key) || !keys.Add(p.Key) || !Enum.IsDefined(p.Kind))Fail("Niepoprawny parametr komendy.");
            Text(p.Label,100); Text(p.Default,2000); if(p.Choices==null||p.Choices.Length>100)Fail("Niepoprawna lista wyboru.");
            foreach(var choice in p.Choices)Text(choice,2000);
            if(p.Secret && p.Default.Length>0)Fail("Sekretny parametr nie może mieć zapisanej wartości domyślnej.");
            if(p.Kind==CommandParameterKind.Choice && p.Choices.Length==0)Fail("Parametr wyboru wymaga opcji.");
            if(!c.Template.Contains("{{"+p.Key+"}}",StringComparison.Ordinal))Fail("Parametr nie występuje w szablonie.");
            if(p.Default.Length>0)CommandRenderer.ValidateValue(p,p.Default);
        }
        foreach(Match match in Placeholder().Matches(c.Template))if(!keys.Contains(match.Groups[1].Value))Fail("Szablon zawiera niezdefiniowany parametr.");
        // Parameters must occupy a complete shell token. Quoting an already quoted
        // placeholder or embedding it in arbitrary syntax cannot be made safe generically.
        foreach(Match match in Placeholder().Matches(c.Template))
        {
            var p=c.Parameters.Single(p=>p.Key==match.Groups[1].Value); if(p.Kind==CommandParameterKind.Code)continue;
            var before=match.Index==0?' ':c.Template[match.Index-1]; var end=match.Index+match.Length; var after=end==c.Template.Length?' ':c.Template[end];
            if(!char.IsWhiteSpace(before)||!char.IsWhiteSpace(after)||InsideQuote(c.Template,match.Index)||c.Template.Contains("<<",StringComparison.Ordinal)||CodeInterpreter.IsMatch(c.Template))Fail("Umieść parametr jako osobny argument bez cudzysłowów. Fragmenty składni oznacz jako Code.");
        }
        if(user && SecretLiteral().IsMatch(c.Template+"\n"+string.Join("\n",c.Examples)))Fail("Usuń jawne hasła, tokeny i klucze z komendy. Użyj sekretnego parametru.");
    }
    public static void Validate(CommandUserLibrary library)
    {
        if(library==null||library.SchemaVersion!=1||library.Commands==null||library.Categories==null||library.Commands.Length>MaxCommands||library.Categories.Length>500)Fail("Nieobsługiwany lub zbyt duży plik komend.");
        var ids=new HashSet<string>(StringComparer.Ordinal); foreach(var c in library.Commands) { Validate(c,true); if(!ids.Add(c.Id))Fail("Powtórzony identyfikator komendy."); }
        foreach(var category in library.Categories)Category(category);
    }
    public static void Category(string value)
    {
        Text(value,160,true); if(value.Split('/').Any(p=>string.IsNullOrWhiteSpace(p)||p is "." or ".."))Fail("Niepoprawna kategoria komendy.");
    }
    private static bool InsideQuote(string text,int end)
    {
        char quote='\0';
        for(var i=0;i<end;i++)
        {
            var c=text[i] switch { '‘' or '’'=>'\'','“' or '”'=>'"',_=>text[i] }; if((c is '\\' or '`' or '^')&&quote!='\'') { i++; continue; }
            if(c==quote)quote='\0'; else if(quote=='\0'&&c is '\'' or '"')quote=c;
        }
        return quote!='\0';
    }
    private static void Text(string? value,int max,bool required=false,bool multiline=false)
    { if(value==null||value.Length>max||required&&string.IsNullOrWhiteSpace(value)||value.Any(c=>UnsafeCharacter(c)&&!(multiline&&c is '\n' or '\r' or '\t')))Fail("Niepoprawna lub zbyt długa treść komendy."); }
    public static bool UnsafeCharacter(char c)=>char.IsControl(c)||c is '\u061c' or '\u200e' or '\u200f' or >= '\u2028' and <= '\u202e' or >= '\u2066' and <= '\u2069';
    [System.Diagnostics.CodeAnalysis.DoesNotReturn] public static void Fail(string message)=>throw new ArgumentException(L.T(message));
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._-]{0,119}$")] private static partial Regex Identifier();
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]{0,39}$")] private static partial Regex ParameterName();
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$")] private static partial Regex ToolName();
    [GeneratedRegex(@"\{\{([a-zA-Z][a-zA-Z0-9_]*)\}\}")] internal static partial Regex Placeholder();
    private static readonly Regex CodeInterpreter=new(@"(\beval\b|\bInvoke-Expression\b|\biex\b|\b(?:ba|z|da)?sh\b[^;\r\n|]*\s-[a-z]*c[a-z]*\b|\b(?:powershell|pwsh)(?:\.exe)?\b[^;\r\n|]*\s-(?:c(?:ommand)?|e(?:ncodedcommand)?)\b|\bcmd(?:\.exe)?\b[^;\r\n|]*\s/[ck]\b|\b(?:python[0-9.]*|node|ruby|perl)(?:\.exe)?\b[^;\r\n|]*\s-[ce]\b)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.NonBacktracking);
    [GeneratedRegex(@"(?i)(-----BEGIN [A-Z ]*PRIVATE KEY|(?:password|passwd|token|secret|api[_-]?key)\s*[=:]\s*['""\w](?!\{\{)|://[^\s/:]+:[^\s/@]+@)",RegexOptions.CultureInvariant)] private static partial Regex SecretLiteral();
}
