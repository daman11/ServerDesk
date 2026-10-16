using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace ServerDesk.Core;

public sealed record CommandMatch(CommandDefinition Command,bool Compatible,string[] MissingTools,string[] UnknownTools,int Score);
public sealed class CommandCatalog
{
    private sealed record Document(int SchemaVersion,string Version,CommandDefinition[] Commands);
    private readonly Dictionary<string,string> index;
    private sealed record SearchEntry(string Text);
    private readonly ConditionalWeakTable<CommandDefinition,SearchEntry> userIndex=new();
    private static readonly Dictionary<string,string[]> Synonyms=new(StringComparer.Ordinal)
    {
        ["serwis"]=["service","uslug"],["uslugi"]=["service","uslug"],["sprawdz"]=["check","status"],
        ["pamiec"]=["memory","ram"],["aktualizacje"]=["updates","upgrade","aktualizac"],
        ["polaczenia"]=["connections","polaczen"],["bledy"]=["errors","failed","blad"],
        ["permissions"]=["uprawnienia","acl"],["zalogowani"]=["logged","who"],["dyski"]=["storage","dysk","disk"]
    };
    public IReadOnlyList<CommandDefinition> BuiltIns { get; }
    public string Version { get; }
    public static CommandCatalog Default { get; }=Load();
    public CommandCatalog(IEnumerable<CommandDefinition> commands,string version="user")
    {
        var rows=commands.ToArray(); foreach(var command in rows)
        { try { CommandValidation.Validate(command); } catch(ArgumentException ex) { throw new InvalidDataException("Invalid command definition: "+command.Id,ex); } }
        if(rows.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=rows.Length)throw new InvalidDataException("Duplicate command IDs.");
        BuiltIns=Array.AsReadOnly(rows); Version=version; index=rows.ToDictionary(c=>c.Id,SearchText,StringComparer.Ordinal);
    }
    private static CommandCatalog Load()
    {
        using var stream=typeof(CommandCatalog).Assembly.GetManifestResourceStream("ServerDesk.Core.Commands.library.v1.json")??throw new InvalidDataException("Missing command library.");
        var library=JsonSerializer.Deserialize<Document>(stream,CommandValidation.Json)??throw new InvalidDataException("Invalid command library.");
        if(library.SchemaVersion!=1||library.Commands.Any(c=>c.Origin!=CommandOrigin.BuiltIn))throw new InvalidDataException("Invalid built-in commands.");
        return new(library.Commands,library.Version);
    }
    public IReadOnlyList<CommandMatch> Find(CommandEnvironment environment,IEnumerable<CommandDefinition>? user=null,string query="",Guid? host=null,string folder="",bool allVariants=false,bool includeIncompatible=false)
    {
        var tokens=Normalize(query).Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(20).ToArray();
        var found=BuiltIns.Concat(user??[]).Where(c=>includeIncompatible||Scope(c,host,folder)).Select(c=>Match(c,environment,tokens,includeIncompatible)).Where(m=>m!=null).Select(m=>m! with { Compatible=m.Compatible&&Scope(m.Command,host,folder) }).ToArray();
        if(!allVariants)found=found.GroupBy(m=>m.Command.Origin==CommandOrigin.User?m.Command.Id:(environment.Platform==CommandPlatform.General?m.Command.Platform+"/"+m.Command.Shell+"/":"")+m.Command.Operation).Select(g=>g.OrderByDescending(m=>m.Score).ThenBy(m=>m.Command.Id,StringComparer.Ordinal).First()).ToArray();
        return found.OrderByDescending(m=>m.Score).ThenBy(m=>m.Command.Title,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public static bool Scope(CommandDefinition c,Guid? host,string folder)=>
        (!c.HostId.HasValue||c.HostId==host)&&(c.HostFolder.Length==0||folder==c.HostFolder||folder.StartsWith(c.HostFolder+"/",StringComparison.Ordinal));
    public static bool Supports(CommandDefinition c,CommandEnvironment env)
    {
        if(env.Platform!=CommandPlatform.General&&c.Platform!=CommandPlatform.General&&c.Platform!=env.Platform)return false;
        if(env.Shell!=CommandShell.Any&&c.Shell!=CommandShell.Any&&c.Shell!=env.Shell && !(c.Shell==CommandShell.Bash&&env.Shell==CommandShell.Zsh))return false;
        if(c.Family.Length>0&&env.Family!=c.Family)return false;
        if(c.Distributions.Length>0&&!c.Distributions.Contains(env.Distribution))return false;
        if(c.MinVersion.Length>0&&(env.Version.Length==0||CompareVersion(env.Version,c.MinVersion)<0))return false;
        if(c.MaxVersion.Length>0&&(env.Version.Length==0||CompareVersion(env.Version,c.MaxVersion)>0))return false;
        return true;
    }
    private CommandMatch? Match(CommandDefinition c,CommandEnvironment env,string[] tokens,bool includeIncompatible)
    {
        var compatible=Supports(c,env); if(!compatible&&!includeIncompatible)return null;
        var text=index.GetValueOrDefault(c.Id)??userIndex.GetValue(c,command=>new(SearchText(command))).Text; if(tokens.Any(t=>!Contains(text,t)))return null;
        var missing=c.Tools.Where(t=>env.Tool(t)==ToolAvailability.NotDetected).ToArray(); var unknown=c.Tools.Where(t=>env.Tool(t)==ToolAvailability.Unknown).ToArray();
        var title=Normalize(c.Name+" "+c.NameEn); var template=Normalize(c.Template);
        var score=tokens.Sum(t=>Contains(title,t)?20:Contains(template,t)?12:4);
        score+=c.Distributions.Length>0?8:c.Family.Length>0?4:0; score+=c.MinVersion.Length>0||c.MaxVersion.Length>0?2:0;
        score+=c.Priority; score+=c.Tools.Count(t=>env.Tool(t)==ToolAvailability.Available)*30; score-=missing.Length*1000;
        if(!compatible)score-=10000;
        return new(c,compatible,missing,unknown,score);
    }
    private static bool Contains(string text,string token)=>text.Contains(token,StringComparison.Ordinal)||Synonyms.TryGetValue(token,out var alternatives)&&alternatives.Any(t=>text.Contains(t,StringComparison.Ordinal));
    private static int CompareVersion(string a,string b)
    {
        var left=a.Split('.','-'); var right=b.Split('.','-');
        for(var i=0;i<Math.Max(left.Length,right.Length);i++)
        { var l=i<left.Length?left[i]:"0"; var r=i<right.Length?right[i]:"0"; var c=int.TryParse(l,out var li)&&int.TryParse(r,out var ri)?li.CompareTo(ri):StringComparer.OrdinalIgnoreCase.Compare(l,r); if(c!=0)return c; }
        return 0;
    }
    private static string SearchText(CommandDefinition c)=>Normalize(string.Join(" ",c.Name,c.NameEn,c.Summary,c.SummaryEn,c.Explanation,c.ExplanationEn,c.Category,c.Subcategory,string.Join(" ",c.Tags),c.Template,string.Join(" ",c.Tools),c.Platform,c.Family,string.Join(" ",c.Distributions)));
    public static string Normalize(string text)
    {
        var result=new StringBuilder(); foreach(var ch in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))if(CharUnicodeInfo.GetUnicodeCategory(ch)!=UnicodeCategory.NonSpacingMark)result.Append(ch=='ł'?'l':char.IsLetterOrDigit(ch)?ch:' ');
        return string.Join(' ',result.ToString().Split(' ',StringSplitOptions.RemoveEmptyEntries));
    }
}
