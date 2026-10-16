using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class RdpEndpointTests
{
    [Fact] public async Task StalledServerTimesOutWithoutLosingCancellationSemantics()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var server=Task.Run(async()=> { using var socket=await listener.AcceptTcpClientAsync(); var stream=socket.GetStream(); await stream.ReadExactlyAsync(new byte[19]); Assert.Equal(0,await stream.ReadAsync(new byte[1])); });
        await Assert.ThrowsAsync<TimeoutException>(()=>new RdpEndpointProbe().CheckAsync(new() { Name="Fixture",Protocol=Protocol.RDP,Host="127.0.0.1",Port=port },CancellationToken.None,timeout:TimeSpan.FromMilliseconds(150)));
        await server.WaitAsync(TimeSpan.FromSeconds(3));
    }
    [Fact] public async Task UntrustedTlsCertificateIsReportedWithoutAcceptingIt()
    {
        using var key=RSA.Create(2048); var request=new CertificateRequest("CN=fixture.invalid",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") },false));
        using var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
        // Give Schannel an imported key rather than CreateSelfSigned's ephemeral CNG key.
        using var certificate=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pkcs12),null,X509KeyStorageFlags.Exportable);
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var server=Task.Run(async()=>
        {
            using var socket=await listener.AcceptTcpClientAsync(); var stream=socket.GetStream(); await stream.ReadExactlyAsync(new byte[19]);
            await stream.WriteAsync(new byte[]{3,0,0,19,14,208,0,0,0,0,0,2,0,8,0,2,0,0,0});
            using var tls=new SslStream(stream);
            try { await tls.AuthenticateAsServerAsync(certificate,false,System.Security.Authentication.SslProtocols.Tls12,false); Assert.Equal(0,await tls.ReadAsync(new byte[1])); }
            catch(AuthenticationException) { }
            catch(IOException) { }
        });
        var result=await new RdpEndpointProbe().CheckAsync(new() { Name="Fixture",Protocol=Protocol.RDP,Host="127.0.0.1",Port=port },CancellationToken.None,inspectCertificate:true);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(certificate.RawData)),result.CertificateFingerprint);
        Assert.True(result.CertificateErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors)); Assert.True(result.CertificateErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch));
        await server.WaitAsync(TimeSpan.FromSeconds(3));
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(8)]
    public void NegotiatedSecurityIsReadFromLittleEndianResponse(int protocol)
    { Assert.Equal((uint)protocol,RdpEndpointProbe.ParseNegotiation(new byte[]{14,208,0,0,0,0,0,2,0,8,0,(byte)protocol,0,0,0})); }
    [Fact] public void LegacyAndMalformedResponsesAreNotMistakenForNla()
    {
        Assert.Equal(0u,RdpEndpointProbe.ParseNegotiation(new byte[]{6,208,0,0,0,0,0}));
        Assert.Throws<IOException>(()=>RdpEndpointProbe.ParseNegotiation(new byte[]{14,208,0,0,0,0,0,3,0,8,0,5,0,0,0}));
        Assert.Throws<IOException>(()=>RdpEndpointProbe.ParseNegotiation(new byte[]{14,208,0,0,0,0,0,2,0,8,0,3,0,0,0}));
        Assert.Throws<IOException>(()=>RdpEndpointProbe.ParseNegotiation(new byte[]{14,208,0}));
    }
    [Fact] public async Task ProbeRecognizesNlaWithoutSendingCredentials()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var server=Task.Run(async()=> { using var socket=await listener.AcceptTcpClientAsync(); var stream=socket.GetStream(); var request=new byte[19]; await stream.ReadExactlyAsync(request); Assert.Equal(new byte[]{3,0,0,19,14,224,0,0,0,0,0,1,0,8,0,11,0,0,0},request); await stream.WriteAsync(new byte[]{3,0,0,19,14,208,0,0,0,0,0,2,0,8,0,2,0,0,0}); Assert.Equal(0,await stream.ReadAsync(new byte[1])); });
        var result=await new RdpEndpointProbe().CheckAsync(new() { Name="Fixture",Protocol=Protocol.RDP,Host="127.0.0.1",Port=port },CancellationToken.None);
        Assert.True(result.SupportsNla); await server.WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Fact] public async Task CancellationIsNotReportedAsServerTimeout()
    {
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new RdpEndpointProbe().CheckAsync(new() { Name="Fixture",Protocol=Protocol.RDP,Host="127.0.0.1",Port=3389 },cancelled.Token));
    }
    [Fact] public async Task WrongServiceIsReportedBeforeLaunchingClient()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var server=Task.Run(async()=> { using var socket=await listener.AcceptTcpClientAsync(); var stream=socket.GetStream(); await stream.ReadExactlyAsync(new byte[19]); await stream.WriteAsync("HTTP/1.1"u8.ToArray()); });
        var failure=await Assert.ThrowsAsync<IOException>(()=>new RdpEndpointProbe().CheckAsync(new() { Name="Fixture",Protocol=Protocol.RDP,Host="127.0.0.1",Port=port },CancellationToken.None));
        Assert.Contains("odpowiedzi RDP",failure.Message); await server;
    }
    [Fact] public async Task PeerClosingDuringNegotiationHasReadableErrorAndFreshRetryWorks()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var server=Task.Run(async()=>
        {
            using(var first=await listener.AcceptTcpClientAsync())await first.GetStream().ReadExactlyAsync(new byte[19]);
            using var second=await listener.AcceptTcpClientAsync(); var stream=second.GetStream(); await stream.ReadExactlyAsync(new byte[19]);
            await stream.WriteAsync(new byte[]{3,0,0,19,14,208,0,0,0,0,0,2,0,8,0,2,0,0,0});
        });
        var profile=new ServerProfile { Name="Fixture",Protocol=Protocol.RDP,Host="127.0.0.1",Port=port };
        var failure=await Assert.ThrowsAsync<IOException>(()=>new RdpEndpointProbe().CheckAsync(profile,CancellationToken.None));
        Assert.Contains("połączenie",failure.Message,StringComparison.OrdinalIgnoreCase);
        Assert.True((await new RdpEndpointProbe().CheckAsync(profile,CancellationToken.None)).SupportsNla); await server.WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Fact] public async Task ResetDuringNegotiationIsLocalized()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var server=Task.Run(async()=> { using var socket=await listener.AcceptTcpClientAsync(); await socket.GetStream().ReadExactlyAsync(new byte[19]); socket.Client.LingerState=new(true,0); });
        var failure=await Assert.ThrowsAsync<IOException>(()=>new RdpEndpointProbe().CheckAsync(new() { Name="Fixture",Protocol=Protocol.RDP,Host="127.0.0.1",Port=port },CancellationToken.None));
        Assert.Contains("połączenie",failure.Message,StringComparison.OrdinalIgnoreCase); await server;
    }
}
