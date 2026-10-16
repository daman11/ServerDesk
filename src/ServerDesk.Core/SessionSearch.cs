using System.Globalization;

namespace ServerDesk.Core;
public sealed record SessionSearchEntry(Guid Key,string Name,string Host,string User,string Protocol,string Location,string State);
public static class SessionSearch
{
    public static IReadOnlyList<SessionSearchEntry> Filter(IEnumerable<SessionSearchEntry> entries,string? query)
    {
        var terms=(query??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        return entries.Where(entry=>terms.All(term=>CultureInfo.InvariantCulture.CompareInfo.IndexOf($"{entry.Name} {entry.Host} {entry.User} {entry.Protocol} {entry.Location} {entry.State}",term,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0)).ToArray();
    }
}
