namespace ServerDesk.Core;

public enum TagFilterMode { Any,All,Untagged }

public static class ProfileTags
{
    private static readonly char[] Separators=[',',';','\r','\n'];
    public static string[] Parse(string? text)=>(text??"").Split(Separators,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string Format(IEnumerable<string> tags)=>string.Join(", ",Parse(string.Join(",",tags)));
    public static Func<ServerProfile,bool> CompileFilter(IEnumerable<string> selected,TagFilterMode mode)
    {
        var wanted=selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(mode!=TagFilterMode.Untagged && wanted.Count==0)return _=>true;
        return profile=>
        {
            var tags=Parse(profile.Tags);
            return mode==TagFilterMode.Untagged?tags.Length==0:mode==TagFilterMode.All?wanted.IsSubsetOf(tags):tags.Any(wanted.Contains);
        };
    }
    public static string Color(string tag,IReadOnlyDictionary<string,string>? overrides=null)
    {
        if(overrides!=null && overrides.TryGetValue(tag,out var chosen) && chosen is { Length:7 } && ProfileAppearance.IsColor(chosen))return chosen;
        // Stable across runtimes and restarts; string.GetHashCode is randomized.
        uint hash=2166136261; foreach(var c in tag.ToUpperInvariant())hash=unchecked((hash^c)*16777619);
        return ProfileAppearance.Colors[1+(int)(hash%(uint)(ProfileAppearance.Colors.Length-1))];
    }
    public static Dictionary<string,string> ValidateColors(IReadOnlyDictionary<string,string>? colors)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if(colors==null)return result;
        if(colors.Count>10000)throw new ArgumentException(L.T("Niepoprawne kolory tagów."));
        foreach(var (name,color) in colors)
        {
            if(string.IsNullOrWhiteSpace(name) || name.Length>256 || name.Trim()!=name || name.Any(char.IsControl) || name.IndexOfAny(Separators)>=0 || color==null || color.Length!=7 || !ProfileAppearance.IsColor(color) || !result.TryAdd(name,color.ToUpperInvariant()))
                throw new ArgumentException(L.T("Niepoprawne kolory tagów."));
        }
        return result;
    }
}
