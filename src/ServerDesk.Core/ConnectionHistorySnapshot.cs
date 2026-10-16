namespace ServerDesk.Core;

// Connection identity only. No password, key path or session contents belong in history.
public sealed record ConnectionHistorySnapshot(string Name,Protocol Protocol,string Endpoint,string User)
{
    public static ConnectionHistorySnapshot Capture(ServerProfile profile)=>new(profile.Name,profile.Protocol,profile.Endpoint,profile.Protocol switch
    { Protocol.SSH=>profile.Username,Protocol.RDP=>RdpIdentity.Parse(profile.Username,profile.Domain).WindowsUsername,_=>"" });
    public bool Matches(ServerProfile profile)
    {
        var current=Capture(profile);
        var endpointComparison=Protocol==Protocol.Serial && Endpoint.StartsWith('/')?StringComparison.Ordinal:StringComparison.OrdinalIgnoreCase;
        var userComparison=Protocol==Protocol.RDP?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        return Protocol==current.Protocol && Endpoint.Equals(current.Endpoint,endpointComparison) && User.Equals(current.User,userComparison);
    }
    public string Detail=>$"{Protocol} · {(User.Length>0?User+"@":"")}{Endpoint}";
}
