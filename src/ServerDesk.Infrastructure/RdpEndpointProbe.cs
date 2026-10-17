using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed record RdpEndpointInfo(TimeSpan RoundTrip,bool SupportsNla,string Security,string? CertificateSubject=null,string? CertificateFingerprint=null,SslPolicyErrors CertificateErrors=SslPolicyErrors.None,DateTime? CertificateExpires=null,string? CertificateIssuer=null,DateTime? CertificateStarts=null);

/// <summary>Checks RDP negotiation without sending an identity or credentials.</summary>
public sealed class RdpEndpointProbe
{
    public async Task<RdpEndpointInfo> CheckAsync(ServerProfile profile,CancellationToken ct,bool inspectCertificate=false,TimeSpan? timeout=null)
    {
        profile.Validate(); if(profile.Protocol!=Protocol.RDP)throw new ArgumentException(L.T("Wybierz profil RDP."));
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(timeout??TimeSpan.FromSeconds(8));
        var token=deadline.Token; var watch=Stopwatch.StartNew();
        try
        {
            using var socket=new TcpClient(); await socket.ConnectAsync(profile.Host,profile.Port,token);
            using var stream=socket.GetStream();
            // TPKT + X.224 connection request + SSL/HYBRID/HYBRID_EX negotiation.
            await stream.WriteAsync(new byte[]{3,0,0,19,14,224,0,0,0,0,0,1,0,8,0,11,0,0,0},token);
            var header=new byte[4]; await stream.ReadExactlyAsync(header,token);
            var length=BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
            if(header[0]!=3 || header[1]!=0 || length is <11 or >4096)throw new IOException(L.T("Serwer nie zwrócił poprawnej odpowiedzi RDP. Sprawdź adres i port."));
            var reply=new byte[length-4]; await stream.ReadExactlyAsync(reply,token);
            var protocol=ParseNegotiation(reply); var nla=protocol is 2 or 8;
            if(!nla)throw new IOException(L.T("Serwer nie uzgodnił NLA. Sprawdź obsługę CredSSP i konfigurację RDP serwera; zabezpieczenia klienta pozostają włączone."));
            var elapsed=watch.Elapsed;
            if(!inspectCertificate)return new(elapsed,true,protocol==8?"NLA / CredSSP (HYBRID_EX)":"NLA / CredSSP");
            string? subject=null,fingerprint=null,issuer=null; DateTime? expires=null,starts=null; var errors=SslPolicyErrors.None;
            using var tls=new SslStream(stream,leaveInnerStreamOpen:true,(_,certificate,_,policyErrors)=>
            {
                errors=policyErrors;
                if(certificate!=null)
                {
                    using var copy=new X509Certificate2(certificate);
                    subject=copy.Subject; issuer=copy.Issuer; fingerprint=Convert.ToHexString(SHA256.HashData(copy.RawData)); expires=copy.NotAfter; starts=copy.NotBefore;
                }
                // Do not accept an untrusted certificate and do not send credentials in this diagnostic.
                return policyErrors==SslPolicyErrors.None;
            });
            try { await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost=profile.Host,EnabledSslProtocols=SslProtocols.None },token); }
            catch(AuthenticationException) when(fingerprint!=null && errors!=SslPolicyErrors.None) { }
            return new(elapsed,true,protocol==8?"NLA / CredSSP (HYBRID_EX)":"NLA / CredSSP",subject,fingerprint,errors,expires,issuer,starts);
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) { throw new TimeoutException(L.T("Serwer RDP nie odpowiedział w wyznaczonym czasie. Sprawdź adres, port, sieć/VPN i zaporę serwera.")); }
        catch(SocketException ex) { throw new IOException(L.F($"Nie można połączyć z serwerem RDP {profile.Host}:{profile.Port} ({ex.SocketErrorCode}). Sprawdź dostępność serwera i usługę Pulpit zdalny."),ex); }
        catch(EndOfStreamException ex) { throw new IOException(L.T("Serwer zamknął połączenie podczas negocjacji RDP. Sprawdź port oraz usługę Pulpit zdalny."),ex); }
        catch(IOException ex) when(ex.InnerException is SocketException) { throw new IOException(L.T("Utracono połączenie podczas negocjacji RDP. Sprawdź sieć/VPN i ponów połączenie."),ex); }
    }
    public static uint ParseNegotiation(ReadOnlySpan<byte> reply)
    {
        if(reply.Length<7 || reply[1]!=0xd0 || reply[0]<6 || reply[0]+1!=reply.Length)throw new IOException(L.T("Niepoprawna odpowiedź negocjacji RDP."));
        if(reply.Length==7)return 0;
        var negotiation=reply[7..];
        if(negotiation.Length!=8 || BinaryPrimitives.ReadUInt16LittleEndian(negotiation[2..])!=8)throw new IOException(L.T("Niepoprawna długość negocjacji RDP."));
        var value=BinaryPrimitives.ReadUInt32LittleEndian(negotiation[4..]);
        if(negotiation[0]==3)throw new IOException(L.T("Serwer odrzucił negocjację RDP (kod ")+value+L.T("). Sprawdź konfigurację TLS/NLA po stronie serwera."));
        if(negotiation[0]!=2 || value is not (0 or 1 or 2 or 8))throw new IOException(L.T("Nieobsługiwany protokół w odpowiedzi RDP."));
        return value;
    }
}
