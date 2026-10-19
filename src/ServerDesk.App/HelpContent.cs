using System.Globalization;
using System.Text.Json;

namespace ServerDesk.App;

internal sealed record HelpSection(string Title,string[] Paragraphs);
internal sealed record HelpTopic(string Id,string Icon,string Title,string Summary,string Keywords,HelpSection[] Sections)
{
    internal string PlainText=>Title+"\n"+Summary+"\n\n"+string.Join("\n\n",Sections.Select(section=>section.Title+"\n"+string.Join("\n\n",section.Paragraphs)));
    internal bool Matches(string query)=>query.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).All(term=>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(PlainText+" "+Keywords,term,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0);
}

// Documentation is trusted, bundled content. Reading help never opens a URL,
// runs a command or reads the user's profiles, credentials or diagnostics.
internal static class HelpContent
{
    internal static HelpTopic[] Read(string language)
    {
        if(!ServerDesk.Core.AppText.IsSupported(language))throw new ArgumentException("Unsupported help language.",nameof(language));
        using var stream=typeof(HelpContent).Assembly.GetManifestResourceStream("ServerDesk.App.Help."+language+".json")??throw new InvalidDataException("Missing bundled help.");
        var topics=JsonSerializer.Deserialize<HelpTopic[]>(stream)??throw new InvalidDataException("Invalid bundled help.");
        if(topics.Length==0 || topics.Any(topic=>string.IsNullOrWhiteSpace(topic.Id)||string.IsNullOrWhiteSpace(topic.Title)||string.IsNullOrWhiteSpace(topic.Summary)||topic.Sections.Length==0||topic.Sections.Any(section=>string.IsNullOrWhiteSpace(section.Title)||section.Paragraphs.Length==0||section.Paragraphs.Any(string.IsNullOrWhiteSpace))) || topics.Select(topic=>topic.Id).Distinct(StringComparer.Ordinal).Count()!=topics.Length)
            throw new InvalidDataException("Incomplete bundled help.");
        return topics;
    }
}
