using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;
public sealed partial class NetworkToolsService
{
    private static async Task<NetworkResult> SnmpAsync(NetworkRequest r,NetworkSecrets secrets,IProgress<NetworkUpdate> output,CancellationToken ct)
    {
        var endpoint=new IPEndPoint(await ResolveAsync(r,ct).ConfigureAwait(false),r.Port); var version=r.SnmpVersion=="v3"?VersionCode.V3:VersionCode.V2;
        if(version==VersionCode.V3 && (string.IsNullOrWhiteSpace(r.Username) || r.SecurityLevel!="noAuthNoPriv" && secrets.AuthPassword.Length<8 || r.SecurityLevel=="authPriv" && secrets.PrivacyPassword.Length<8))throw new ArgumentException(L.T("SNMP v3 wymaga użytkownika i haseł co najmniej 8 znaków dla wybranego poziomu ochrony."));
        IAuthenticationProvider auth=version==VersionCode.V2 || r.SecurityLevel=="noAuthNoPriv"?DefaultAuthenticationProvider.Instance:r.AuthProtocol switch { "SHA384"=>new SHA384AuthenticationProvider(new(secrets.AuthPassword)),"SHA512"=>new SHA512AuthenticationProvider(new(secrets.AuthPassword)),_=>new SHA256AuthenticationProvider(new(secrets.AuthPassword)) };
        IPrivacyProvider privacy=version==VersionCode.V3 && r.SecurityLevel=="authPriv"?new AESPrivacyProvider(new(secrets.PrivacyPassword),auth):new DefaultPrivacyProvider(auth);
        ISnmpMessage? report=null;
        async Task<T> Attempt<T>(Func<CancellationToken,Task<T>> action)
        {
            for(var attempt=0;;attempt++)
            {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(r.TimeoutMs);
                try { return await action(timeout.Token).ConfigureAwait(false); }
                catch(Exception ex) when(!ct.IsCancellationRequested && attempt<r.Retries && ex is OperationCanceledException or System.Net.Sockets.SocketException or System.TimeoutException or Lextm.SharpSnmpLib.Messaging.TimeoutException) { }
            }
        }
        if(version==VersionCode.V3)report=await Attempt(token=>Messenger.GetNextDiscovery(SnmpType.GetRequestPdu).GetResponseAsync(endpoint,token)).ConfigureAwait(false);
        var root=r.Oid.TrimStart('.'); var current=root; var total=0;
        while(total<2000)
        {
            ct.ThrowIfCancellationRequested(); var variables=new List<Variable> { new(new ObjectIdentifier(current)) }; var operation=r.SnmpOperation=="WALK"?"GETNEXT":r.SnmpOperation;
            ISnmpMessage request=version==VersionCode.V2?operation switch {
                "GETNEXT"=>new GetNextRequestMessage(Messenger.NextRequestId,version,new OctetString(secrets.Community),variables),
                "GETBULK"=>new GetBulkRequestMessage(Messenger.NextRequestId,version,new OctetString(secrets.Community),0,10,variables),
                _=>new GetRequestMessage(Messenger.NextRequestId,version,new OctetString(secrets.Community),variables) }:
                operation switch {
                "GETNEXT"=>new GetNextRequestMessage(version,Messenger.NextMessageId,Messenger.NextRequestId,new OctetString(r.Username),new OctetString(r.Context),variables,privacy,Messenger.MaxMessageSize,report!),
                "GETBULK"=>new GetBulkRequestMessage(version,Messenger.NextMessageId,Messenger.NextRequestId,new OctetString(r.Username),new OctetString(r.Context),0,10,variables,privacy,Messenger.MaxMessageSize,report!),
                _=>new GetRequestMessage(version,Messenger.NextMessageId,Messenger.NextRequestId,new OctetString(r.Username),new OctetString(r.Context),variables,privacy,Messenger.MaxMessageSize,report!) };
            var response=await Attempt(token=>request.GetResponseAsync(endpoint,token)).ConfigureAwait(false);
            if(response.TypeCode()==SnmpType.ReportPdu || response.Pdu().ErrorStatus.ToInt32()!=0)throw new InvalidDataException(L.T("Agent SNMP zgłosił błąd lub odrzucił parametry zabezpieczeń. Sprawdź użytkownika, poziom ochrony, hasła i kontekst."));
            var results=response.Variables(); if(results.Count==0)break;
            foreach(var variable in results)
            {
                var oid=variable.Id.ToString(); if(variable.Data.TypeCode is SnmpType.EndOfMibView or SnmpType.NoSuchInstance or SnmpType.NoSuchObject)
                { if(r.SnmpOperation=="WALK")return new(L.F($"Odczytano OID: {total}")); }
                if(r.SnmpOperation=="WALK" && (!oid.StartsWith(root+".",StringComparison.Ordinal) || CompareOid(oid,current)<=0))return new(L.F($"Odczytano OID: {total}"));
                output.Report(new(new(oid,new[]{oid,variable.Data.TypeCode.ToString(),variable.Data.ToString()??""}),++total,0)); current=oid;
                if(total>=2000)break;
            }
            if(r.SnmpOperation!="WALK")break;
        }
        return new(total>=2000?L.T("Osiągnięto limit 2000 OID. Zawęź poddrzewo, aby kontynuować."):L.F($"Odczytano OID: {total}"));
    }
    private static int CompareOid(string a,string b)
    { var x=a.Split('.').Select(ulong.Parse).ToArray(); var y=b.Split('.').Select(ulong.Parse).ToArray(); for(var i=0;i<Math.Min(x.Length,y.Length);i++) { var cmp=x[i].CompareTo(y[i]); if(cmp!=0)return cmp; } return x.Length.CompareTo(y.Length); }
}
