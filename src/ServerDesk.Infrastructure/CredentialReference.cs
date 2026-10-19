using System.Security.Cryptography;
using System.Text;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public static class CredentialReference
{
    // VNC must not receive an existing SSH/RDP secret after changing a profile's protocol.
    public static Guid Vnc(Guid profileId)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("ServerDesk.VNC.v1:"+profileId.ToString("D"))).AsSpan(0,16));
    public static Guid ForProfile(ServerProfile profile)=>profile.CredentialId??(profile.Protocol==Protocol.VNC?Vnc(profile.Id):profile.Id);
    public static IEnumerable<Guid> AllForProfile(ServerProfile profile)=>new[]{ForProfile(profile),profile.Id,Vnc(profile.Id)}.Distinct();
    public static bool CanReuse(ServerProfile before,ServerProfile after)
    {
        if(before.Id!=after.Id || before.Protocol!=after.Protocol || before.Port!=after.Port || !before.Host.Equals(after.Host,StringComparison.OrdinalIgnoreCase))return false;
        if(before.Protocol==Protocol.RDP)
            return RdpIdentity.Parse(before.Username,before.Domain).WindowsUsername.Equals(RdpIdentity.Parse(after.Username,after.Domain).WindowsUsername,StringComparison.OrdinalIgnoreCase);
        if(before.Protocol==Protocol.VNC)return before.Vnc.Authentication==after.Vnc.Authentication;
        if(before.Protocol==Protocol.Serial)return before.Serial.PortName==after.Serial.PortName;
        return before.Username==after.Username && before.Authentication==after.Authentication && (before.Authentication!=Authentication.PrivateKey || before.PrivateKeyPath.Equals(after.PrivateKeyPath,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal));
    }
}
