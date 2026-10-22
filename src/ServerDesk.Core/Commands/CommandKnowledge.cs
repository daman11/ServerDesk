using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerDesk.Core;

public sealed record CommandDocumentationSource(string Id,string Title,string Url);
public sealed record CommandOptionReference(string Syntax,string Description,string DescriptionEn,string Example="")
{
    [JsonIgnore] public string Text=>AppText.Language=="en"?DescriptionEn:Description;
}
public sealed record CommandToolReference(string Id,string[] Tools,string Title,string Scope,string ScopeEn,string[] Sources,CommandOptionReference[] Options,CommandPlatform Platform=CommandPlatform.General)
{
    [JsonIgnore] public string Applicability=>AppText.Language=="en"?ScopeEn:Scope;
}
public sealed record CommandGuideStep(string Title,string TitleEn,string Text,string TextEn,string Code="",CommandRisk Risk=CommandRisk.ReadOnly)
{
    [JsonIgnore] public string Heading=>AppText.Language=="en"?TitleEn:Title;
    [JsonIgnore] public string Body=>AppText.Language=="en"?TextEn:Text;
}
public sealed record CommandGuide(string Id,string Title,string TitleEn,string Summary,string SummaryEn,string Category,CommandPlatform Platform,string AppliesTo,string AppliesToEn,string Warning,string WarningEn,string[] Tags,string[] Sources,CommandGuideStep[] Steps)
{
    [JsonIgnore] public string Heading=>AppText.Language=="en"?TitleEn:Title;
    [JsonIgnore] public string Description=>AppText.Language=="en"?SummaryEn:Summary;
    [JsonIgnore] public string Applicability=>AppText.Language=="en"?AppliesToEn:AppliesTo;
    [JsonIgnore] public string Caution=>AppText.Language=="en"?WarningEn:Warning;
    [JsonIgnore] public CommandRisk Risk=>Steps.Max(s=>s.Risk);
}

/// <summary>Reviewed, offline education. Separate from executable user templates.</summary>
public sealed class CommandKnowledgeCatalog
{
    private sealed record Document(int SchemaVersion,string Version,string ReviewedOn,CommandDocumentationSource[] Sources,CommandToolReference[] Tools,CommandGuide[] Guides);
    public IReadOnlyList<CommandDocumentationSource> Sources { get; }
    public IReadOnlyList<CommandToolReference> Tools { get; }
    public IReadOnlyList<CommandGuide> Guides { get; }
    public string Version { get; }
    public DateOnly ReviewedOn { get; }
    private readonly Dictionary<string,string> guideSearch;
    private readonly Dictionary<string,string> toolSearch;
    private CommandKnowledgeCatalog(Document document)
    {
        if(document.SchemaVersion!=1 || document.Version.Length>30 || !DateOnly.TryParseExact(document.ReviewedOn,"yyyy-MM-dd",out var reviewed) || document.Sources.Length is <1 or >100 || document.Tools.Length>100 || document.Guides.Length>100)throw new InvalidDataException("Invalid command knowledge catalog");
        var sources=new HashSet<string>(StringComparer.Ordinal); var tools=new HashSet<string>(StringComparer.Ordinal); var guides=new HashSet<string>(StringComparer.Ordinal);
        foreach(var source in document.Sources)
        {
            Id(source.Id); Text(source.Title,160);
            if(!sources.Add(source.Id) || !Uri.TryCreate(source.Url,UriKind.Absolute,out var uri) || uri.Scheme!="https" || !uri.IsDefaultPort || uri.UserInfo.Length>0 || !Publishers.Contains(uri.Host,StringComparer.OrdinalIgnoreCase) || source.Url.Length>1000)throw new InvalidDataException("Invalid documentation source");
        }
        void References(string[] ids) { if(ids.Length is <1 or >6 || ids.Distinct().Count()!=ids.Length || ids.Any(id=>!sources.Contains(id)))throw new InvalidDataException("Invalid documentation references"); }
        foreach(var tool in document.Tools)
        {
            Id(tool.Id); Text(tool.Title,100); Text(tool.Scope,500); Text(tool.ScopeEn,500); References(tool.Sources);
            if(!tools.Add(tool.Id) || !Enum.IsDefined(tool.Platform) || tool.Tools.Length is <1 or >12 || tool.Options.Length is <1 or >40 || tool.Options.Select(o=>o.Syntax).Distinct().Count()!=tool.Options.Length)throw new InvalidDataException("Invalid tool reference");
            foreach(var name in tool.Tools)Text(name,80);
            foreach(var option in tool.Options) { Text(option.Syntax,120); Text(option.Description,300); Text(option.DescriptionEn,300); Text(option.Example,500,optional:true); }
        }
        foreach(var guide in document.Guides)
        {
            Id(guide.Id); Text(guide.Title,160); Text(guide.TitleEn,160); Text(guide.Summary,400); Text(guide.SummaryEn,400); CommandValidation.Category(guide.Category); Text(guide.AppliesTo,500); Text(guide.AppliesToEn,500); Text(guide.Warning,1000,true); Text(guide.WarningEn,1000,true); References(guide.Sources);
            if(!guides.Add(guide.Id) || !Enum.IsDefined(guide.Platform) || guide.Steps.Length is <2 or >10 || guide.Tags.Length>24)throw new InvalidDataException("Invalid mini guide");
            foreach(var tag in guide.Tags)Text(tag,80);
            foreach(var step in guide.Steps)
            { Text(step.Title,160); Text(step.TitleEn,160); Text(step.Text,1600); Text(step.TextEn,1600); Text(step.Code,2000,true,true); if(!Enum.IsDefined(step.Risk))throw new InvalidDataException("Invalid step risk"); }
        }
        Sources=Array.AsReadOnly(document.Sources); Tools=Array.AsReadOnly(document.Tools); Guides=Array.AsReadOnly(document.Guides); Version=document.Version; ReviewedOn=reviewed;
        guideSearch=Guides.ToDictionary(g=>g.Id,g=>CommandCatalog.Normalize(string.Join(' ',g.Title,g.TitleEn,g.Summary,g.SummaryEn,g.AppliesTo,g.AppliesToEn,g.Category,string.Join(' ',g.Tags),string.Join(' ',g.Steps.Select(s=>s.Title+" "+s.TitleEn+" "+s.Text+" "+s.TextEn+" "+s.Code)))));
        toolSearch=Tools.ToDictionary(t=>t.Id,t=>CommandCatalog.Normalize(t.Title+" "+string.Join(' ',t.Tools)+" "+t.Scope+" "+t.ScopeEn+" "+string.Join(' ',t.Options.Select(o=>o.Syntax+" "+o.Description+" "+o.DescriptionEn))));
    }
    // Only reviewed publisher hosts; never populated from profiles, templates or HTTP responses.
    public static IReadOnlyList<string> Publishers { get; }=Array.AsReadOnly(new[]{"www.gnu.org","man.openbsd.org","networkmanager.dev","netplan.readthedocs.io","www.freedesktop.org","www.debian.org","learn.microsoft.com","curl.se","docs.docker.com","git-scm.com","docs.kernel.org","manpages.debian.org","kubernetes.io"});
    public static CommandKnowledgeCatalog Default { get; }=Load();
    private static void Id(string value) { if(value.Length is <1 or >80 || value.Any(c=>c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-'))throw new InvalidDataException("Invalid knowledge identifier"); }
    private static void Text(string value,int limit,bool optional=false,bool multiline=false)
    { if(value==null || value.Length>limit || !optional && string.IsNullOrWhiteSpace(value) || value.Any(c=>CommandValidation.UnsafeCharacter(c)&&!(multiline&&c is '\n' or '\t')))throw new InvalidDataException("Invalid knowledge text"); }
    private static CommandKnowledgeCatalog Load()
    {
        using var input=typeof(CommandKnowledgeCatalog).Assembly.GetManifestResourceStream("ServerDesk.Core.Commands.knowledge.v1.json")??throw new InvalidDataException("Missing command knowledge");
        return new(JsonSerializer.Deserialize<Document>(input,CommandValidation.Json)??throw new InvalidDataException("Invalid command knowledge"));
    }
    public CommandDocumentationSource Source(string id)=>Sources.SingleOrDefault(s=>s.Id==id)??throw new ArgumentException("Unknown documentation source",nameof(id));
    public IReadOnlyList<CommandGuide> FindGuides(CommandEnvironment environment,string query="")
    {
        var tokens=CommandCatalog.Normalize(query).Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(20).ToArray();
        return Guides.Where(g=>(environment.Platform==CommandPlatform.General || g.Platform==CommandPlatform.General || g.Platform==environment.Platform) && tokens.All(t=>ContainsToken(guideSearch[g.Id],t))).ToArray();
    }
    public IReadOnlyList<CommandToolReference> ForCommand(CommandDefinition command)=>command.Origin!=CommandOrigin.BuiltIn?[]:Tools.Where(t=>
        t.Tools.Any(tool=>command.Tools.Contains(tool,StringComparer.OrdinalIgnoreCase)) &&
        (t.Id!="docker-logs" || command.Template.StartsWith("docker logs ",StringComparison.Ordinal))).ToArray();
    public IReadOnlyList<CommandToolReference> FindTools(CommandEnvironment environment,string query="")
    {
        var tokens=CommandCatalog.Normalize(query).Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(20).ToArray();
        return Tools.Where(t=>(environment.Platform==CommandPlatform.General || t.Platform==CommandPlatform.General || t.Platform==environment.Platform) && tokens.All(token=>ContainsToken(toolSearch[t.Id],token)))
            .OrderByDescending(t=>t.Tools.Any(name=>CommandCatalog.Normalize(name)==CommandCatalog.Normalize(query))).ThenBy(t=>t.Title,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private static bool ContainsToken(string text,string token)=>token.Length<=2?text.Split(' ').Contains(token,StringComparer.Ordinal):text.Contains(token,StringComparison.Ordinal);
}
