namespace ServerDesk.Core;

public static class ProfileSearch
{
    public static bool Matches(ServerProfile profile,string query)
        =>Compile(query)(profile);
    public static Func<ServerProfile,bool> Compile(string query)
    {
        var terms=query.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(terms.Length==0)return _=>true;
        return profile=>
        {
            var text=$"{profile.Name} {profile.Host} {profile.Username} {profile.Domain} {profile.Folder} {profile.Tags} {profile.Description} {profile.Protocol} {profile.Port} {(profile.Protocol==Protocol.Serial?profile.Serial.Summary:"")}";
            return terms.All(term=>text.Contains(term,StringComparison.OrdinalIgnoreCase));
        };
    }
}
