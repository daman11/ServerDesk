using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
// All integration endpoints below are ephemeral loopback fixtures. No public DNS or devices.
public sealed class NetworkServicesTests
{
    private sealed class Capture : IProgress<NetworkUpdate>
    { internal readonly ConcurrentQueue<NetworkUpdate> Values=new(); public void Report(NetworkUpdate value)=>Values.Enqueue(value); internal NetworkRow[] Rows=>Values.Where(value=>value.Row!=null).Select(value=>value.Row!).ToArray(); }
    private sealed class ScriptedIcmp(params IPStatus[] replies) : IIcmpProbe
    {
        internal readonly List<int> Ttls=[]; private int index;
        public Task<IcmpReply> SendAsync(IPAddress address,int ttl,int bytes,int timeoutMs,CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Ttls.Add(ttl); var state=replies[index++%replies.Length]; return Task.FromResult(new IcmpReply(address,state,index*10)); }
    }
    [Fact] public async Task Ping_statistics_include_timeouts_and_chart_gaps()
    { var results=new Capture(); var service=new NetworkToolsService(new ScriptedIcmp(IPStatus.Success,IPStatus.TimedOut,IPStatus.DestinationHostUnreachable,IPStatus.Success)); var final=await service.RunAsync(new() { Target="127.0.0.1",Count=4,IntervalMs=100 },new(),results,CancellationToken.None); Assert.Equal(4,results.Rows.Length); Assert.Null(results.Rows[1].Latency); Assert.True(results.Rows[2].Error); Assert.Contains("50%",final.Summary); Assert.Contains("10 / 25 / 40",final.Summary); }
    [Fact] public async Task Trace_uses_incrementing_ttl_and_keeps_intermediate_timeout()
    { var icmp=new ScriptedIcmp(IPStatus.TtlExpired,IPStatus.TimedOut,IPStatus.Success); var results=new Capture(); await new NetworkToolsService(icmp).RunAsync(new() { Tool=NetworkToolKind.Trace,Target="127.0.0.1",MaxHops=8 },new(),results,CancellationToken.None); Assert.Equal(new[]{1,2,3},icmp.Ttls); Assert.Equal("*",results.Rows[1].Cells[1]); Assert.Equal("100%",results.Rows[1].Cells[5]); }
    [Fact] public async Task Continuous_route_can_be_cancelled_during_cycle_delay()
    { using var ct=new CancellationTokenSource(300); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new NetworkToolsService(new ScriptedIcmp(IPStatus.Success)).RunAsync(new() { Tool=NetworkToolKind.Trace,Target="127.0.0.1",Continuous=true,IntervalMs=1000 },new(),new Capture(),ct.Token)); }
    [Theory] [InlineData("127.0.0.1")] [InlineData("localhost")]
    public async Task Actual_icmp_loopback_and_hostname(string host)
    { var output=new Capture(); await new NetworkToolsService().RunAsync(new() { Target=host,Family="IPv4",Count=1 },new(),output,CancellationToken.None); Assert.Single(output.Rows); Assert.False(output.Rows[0].Error); }
    [Fact] public async Task Actual_ipv6_loopback_ping_and_tcp_scan()
    {
        if(!Socket.OSSupportsIPv6)return;
        var ping=new Capture(); await new NetworkToolsService().RunAsync(new() { Target="::1",Family="IPv6",Count=1 },new(),ping,CancellationToken.None); Assert.False(ping.Rows.Single().Error);
        using var listener=new TcpListener(IPAddress.IPv6Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; var result=new Capture(); await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Ports,Target="::1",Family="IPv6",Ports=port.ToString() },new(),result,CancellationToken.None); Assert.Equal("Open",result.Rows.Single().Cells[1]);
    }
    [Theory] [InlineData("64 bytes from 127.0.0.1: icmp_seq=1 ttl=64 time=0.031 ms",IPStatus.Success)] [InlineData("From 192.0.2.1 icmp_seq=1 Time to live exceeded",IPStatus.TtlExpired)] [InlineData("From 2001:db8::1 icmp_seq=1 Time exceeded: Hop limit",IPStatus.TtlExpired)] [InlineData("From 192.0.2.1 icmp_seq=1 Destination Host Unreachable",IPStatus.DestinationHostUnreachable)] [InlineData("1 packets transmitted, 0 received, 100% packet loss",IPStatus.TimedOut)]
    public void Linux_ping_parser_handles_rtt_hops_and_timeouts(string text,IPStatus expected)=>Assert.Equal(expected,LinuxIcmpProbe.Parse(text,IPAddress.Loopback).Status);
    [Fact] public async Task Tcp_scanner_distinguishes_open_and_refused_ports()
    {
        using var open=new TcpListener(IPAddress.Loopback,0); open.Start(); var port=((IPEndPoint)open.LocalEndpoint).Port;
        using var closed=new TcpListener(IPAddress.Loopback,0); closed.Start(); var closedPort=((IPEndPoint)closed.LocalEndpoint).Port; closed.Stop();
        var output=new Capture(); await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Ports,Target="127.0.0.1",Ports=port+","+closedPort,Concurrency=2,TimeoutMs=3000 },new(),output,CancellationToken.None);
        Assert.Equal("Open",output.Rows.Single(row=>row.Key==port.ToString()).Cells[1]); Assert.Equal("Closed",output.Rows.Single(row=>row.Key==closedPort.ToString()).Cells[1]);
    }
    [Fact] public async Task Actual_wake_packet_is_received_over_udp()
    { using var receiver=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)); var port=((IPEndPoint)receiver.Client.LocalEndPoint!).Port; await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Wake,Target="02:12:34:56:78:9A",Broadcast="127.0.0.1",Port=port,LocalAddress="127.0.0.1" },new(),new Capture(),CancellationToken.None); var packet=await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(3)); Assert.Equal(NetworkRequest.MagicPacket("02:12:34:56:78:9A"),packet.Buffer); }
    [Fact] public async Task Dns_all_records_custom_resolvers_comparison_ttl_and_nxdomain()
    {
        await using var a=new DnsFixture(10); await using var b=new DnsFixture(20); var output=new Capture();
        await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Dns,Target="fixture.test",RecordType="All",Resolvers=a.Endpoint+","+b.Endpoint },new(),output,CancellationToken.None);
        Assert.Equal(20,output.Rows.Length); Assert.Equal(2,output.Rows.Select(row=>row.Cells[0]).Distinct().Count()); foreach(var type in NetworkToolsService.DnsTypes.Where(type=>type!="All"))Assert.Equal(2,output.Rows.Count(row=>row.Cells[2]==type)); Assert.All(output.Rows,row=>Assert.Equal("120",row.Cells[3]));
        var reverse=new Capture(); await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Dns,Target="127.0.0.1",RecordType="PTR",Resolvers=a.Endpoint },new(),reverse,CancellationToken.None); Assert.Contains("1.0.0.127.in-addr.arpa",reverse.Rows.Single().Cells[1]);
        var missing=new Capture(); await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Dns,Target="missing.fixture.test",Resolvers=a.Endpoint },new(),missing,CancellationToken.None); Assert.True(missing.Rows.Single().Error);
        var srv=new Capture(); await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Dns,Target="_ldap._tcp.fixture.test",RecordType="SRV",Resolvers=a.Endpoint },new(),srv,CancellationToken.None); Assert.StartsWith("_ldap._tcp.fixture.test",srv.Rows.Single().Cells[1]);
    }
    [Theory] [InlineData("GET")] [InlineData("POST")] [InlineData("PUT")] [InlineData("PATCH")] [InlineData("DELETE")] [InlineData("HEAD")]
    public async Task Http_methods_body_query_auth_and_response_redaction(string method)
    {
        HttpInput? seen=null; await using var server=new HttpFixture(input=> { seen=input; return new("{\"access_token\":\"response-secret\",\"ok\":true}",Headers:"Set-Cookie: session=private-cookie\r\n"); });
        var result=await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Http,Target=server.Url,Method=method,Query="name=hello world",Headers="X-Fixture: yes",Body=method is "GET" or "HEAD"?"":"{\"value\":123}",Authentication="Bearer",TimeoutMs=5000 },new(Token:"request-secret"),new Capture(),CancellationToken.None);
        Assert.Equal(method,seen!.Method); Assert.Contains("name=hello%20world",seen.Path); Assert.Equal("Bearer request-secret",seen.Headers["Authorization"]); Assert.DoesNotContain("private-cookie",result.Headers); Assert.DoesNotContain("response-secret",result.Body); if(method is not ("GET" or "HEAD"))Assert.Contains("123",seen.Body);
    }
    [Fact] public async Task Http_cross_origin_redirect_does_not_forward_credentials_or_custom_headers()
    {
        HttpInput? received=null; await using var destination=new HttpFixture(input=> { received=input; return new("ok"); }); await using var redirect=new HttpFixture(_=>new("",302,"Location: "+destination.Url+"\r\n"));
        await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Http,Target=redirect.Url,Authentication="Basic",Username="user",Headers="X-API-Key: private-key",TimeoutMs=5000 },new(Password:"private-password"),new Capture(),CancellationToken.None);
        Assert.False(received!.Headers.ContainsKey("Authorization")); Assert.False(received.Headers.ContainsKey("X-API-Key"));
    }
    [Fact] public async Task Http_echoed_custom_credentials_are_redacted()
    {
        await using var server=new HttpFixture(_=>new("{\"echo\":\"fixture-api-secret and fixture-url-secret\"}"));
        var result=await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Http,Target=server.Url+"?api_key=fixture-url-secret",Headers="X-API-Key: fixture-api-secret",TimeoutMs=5000 },new(),new Capture(),CancellationToken.None);
        Assert.DoesNotContain("fixture-api-secret",result.Body); Assert.DoesNotContain("fixture-url-secret",result.Body);
    }
    [Fact] public async Task Http_honors_declared_response_charset()
    {
        await using var server=new HttpFixture(_=>new("{\"message\":\"żółć\"}",Charset:"utf-16"));
        var result=await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Http,Target=server.Url,TimeoutMs=5000 },new(),new Capture(),CancellationToken.None); Assert.Contains("żółć",result.Body);
    }
    [Fact] public async Task Http_limits_response_and_timeout_is_cancellable()
    {
        await using var server=new HttpFixture(_=>new(new string('x',2*1024*1024+100)));
        var result=await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Http,Target=server.Url,TimeoutMs=5000 },new(),new Capture(),CancellationToken.None); Assert.Equal(2*1024*1024,result.Body.Length); Assert.Contains("2 MiB",result.Summary);
        using var idle=new TcpListener(IPAddress.Loopback,0); idle.Start(); var port=((IPEndPoint)idle.LocalEndpoint).Port;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Http,Target="http://127.0.0.1:"+port,TimeoutMs=100 },new(),new Capture(),CancellationToken.None));
    }
    [Fact] public async Task Tls_inspection_reports_untrusted_certificate_and_exports_pem()
    {
        using var key=RSA.Create(2048); var request=new CertificateRequest("CN=fixture.test",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1); var san=new SubjectAlternativeNameBuilder(); san.AddDnsName("fixture.test"); request.CertificateExtensions.Add(san.Build()); using var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(10)); using var cert=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.DefaultKeySet);
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var server=Task.Run(async()=> { using var tcp=await listener.AcceptTcpClientAsync(); using var ssl=new SslStream(tcp.GetStream()); await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate=cert },CancellationToken.None); });
        var output=new Capture(); var result=await new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Tls,Target="127.0.0.1",Sni="fixture.test",Port=port,TimeoutMs=5000 },new(),output,CancellationToken.None); await server;
        Assert.Contains("BEGIN CERTIFICATE",result.Pem); Assert.Contains(output.Rows,row=>row.Key=="SHA-256" && row.Cells[1]==cert.GetCertHashString(HashAlgorithmName.SHA256)); Assert.Contains(output.Rows,row=>row.Key=="TLS" && row.Cells[1].StartsWith("Tls")); Assert.True(output.Rows.Single(row=>row.Key=="Status").Error);
        var reject=Task.Run(async()=> { using var tcp=await listener.AcceptTcpClientAsync(); using var ssl=new SslStream(tcp.GetStream()); try { await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate=cert },CancellationToken.None); } catch(Exception ex) when(ex is AuthenticationException or IOException) { } });
        await Assert.ThrowsAsync<HttpRequestException>(()=>new NetworkToolsService().RunAsync(new() { Tool=NetworkToolKind.Http,Target="https://127.0.0.1:"+port,TimeoutMs=5000 },new(),new Capture(),CancellationToken.None)); await reject;
    }
    [Fact] public async Task Snmp_v2_get_walk_and_bulk_use_real_udp_messages()
    {
        await using var agent=new SnmpFixture(); var service=new NetworkToolsService();
        foreach(var operation in new[]{"GET","GETNEXT","WALK","GETBULK"})
        {
            var output=new Capture(); await service.RunAsync(new() { Tool=NetworkToolKind.Snmp,Target="127.0.0.1",Port=agent.Port,Oid=operation=="WALK"?"1.3.6.1.2.1.1":"1.3.6.1.2.1.1.1.0",SnmpOperation=operation,TimeoutMs=1000 },new(Community:"test-community"),output,CancellationToken.None);
            Assert.NotEmpty(output.Rows); Assert.Contains(output.Rows,row=>row.Cells[2].Contains("fixture")); if(operation=="WALK")Assert.Equal(2,output.Rows.Length);
        }
    }
    private sealed record HttpInput(string Method,string Path,Dictionary<string,string> Headers,string Body);
    private sealed record HttpOutput(string Body,int Status=200,string Headers="",string Charset="utf-8");
    private sealed class HttpFixture : IAsyncDisposable
    {
        private readonly TcpListener listener=new(IPAddress.Loopback,0); private readonly CancellationTokenSource stop=new(TimeSpan.FromSeconds(15)); private readonly Task loop; private readonly ConcurrentBag<Task> handlers=[];
        internal string Url { get; }
        internal HttpFixture(Func<HttpInput,HttpOutput> response)
        {
            listener.Start(); Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/fixture";
            loop=Task.Run(async()=> { try { while(!stop.IsCancellationRequested) { var client=await listener.AcceptTcpClientAsync(stop.Token); handlers.Add(Handle(client,response)); } } catch(Exception ex) when(ex is OperationCanceledException or SocketException) { } });
        }
        private async Task Handle(TcpClient client,Func<HttpInput,HttpOutput> responder)
        {
            using(client)try
            {
                var stream=client.GetStream(); var head=new List<byte>(); var one=new byte[1];
                while(head.Count<32768) { if(await stream.ReadAsync(one,stop.Token)==0)return; head.Add(one[0]); if(head.Count>=4 && head.TakeLast(4).SequenceEqual(new byte[]{13,10,13,10}))break; }
                var lines=Encoding.ASCII.GetString(head.ToArray()).Split("\r\n"); var first=lines[0].Split(' '); var headers=lines.Skip(1).Where(line=>line.Contains(':')).Select(line=>line.Split(':',2)).ToDictionary(pair=>pair[0],pair=>pair[1].Trim(),StringComparer.OrdinalIgnoreCase);
                var length=headers.TryGetValue("Content-Length",out var value)?int.Parse(value):0; var body=new byte[length]; await stream.ReadExactlyAsync(body,stop.Token);
                var result=responder(new(first[0],first[1],headers,Encoding.UTF8.GetString(body))); var bytes=Encoding.GetEncoding(result.Charset).GetBytes(result.Body); var prefix=Encoding.ASCII.GetBytes($"HTTP/1.1 {result.Status} Fixture\r\nContent-Type: application/json; charset={result.Charset}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n{result.Headers}\r\n"); await stream.WriteAsync(prefix,stop.Token); if(first[0]!="HEAD")await stream.WriteAsync(bytes,stop.Token);
            }
            catch(Exception ex) when(ex is IOException or OperationCanceledException or SocketException) { }
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await loop; await Task.WhenAll(handlers); stop.Dispose(); }
    }
    private sealed class DnsFixture : IAsyncDisposable
    {
        private readonly UdpClient udp=new(new IPEndPoint(IPAddress.Loopback,0)); private readonly CancellationTokenSource stop=new(); private readonly Task loop;
        internal string Endpoint=>"127.0.0.1:"+((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        internal DnsFixture(byte address)
        {
            loop=Task.Run(async()=>
            {
                try { while(!stop.IsCancellationRequested)
                {
                    var packet=await udp.ReceiveAsync(stop.Token); var query=packet.Buffer; var end=12; while(query[end]!=0)end+=1+query[end]; end++; var type=query[end]*256+query[end+1]; var missing=Encoding.ASCII.GetString(query).Contains("missing");
                    byte[] U16(int n)=>[(byte)(n>>8),(byte)n]; byte[] Domain(string name)=>name.Split('.').SelectMany(label=>new[]{(byte)label.Length}.Concat(Encoding.ASCII.GetBytes(label))).Append((byte)0).ToArray();
                    var data=type switch {1=>new byte[]{127,0,0,address},28=>IPAddress.IPv6Loopback.GetAddressBytes(),15=>U16(10).Concat(Domain("mail.fixture.test")).ToArray(),16=>new byte[]{5,104,101,108,108,111},6=>Domain("ns.fixture.test").Concat(Domain("admin.fixture.test")).Concat(Enumerable.Repeat((byte)0,20)).ToArray(),33=>U16(1).Concat(U16(1)).Concat(U16(443)).Concat(Domain("service.fixture.test")).ToArray(),257=>new byte[]{0,5,105,115,115,117,101}.Concat(Encoding.ASCII.GetBytes("fixture.test")).ToArray(),_=>Domain("host.fixture.test")};
                    var response=query.Take(2).Concat(new byte[]{0x81,(byte)(missing?0x83:0x80),0,1,0,(byte)(missing?0:1),0,0,0,0}).Concat(query.Skip(12).Take(end+4-12)).ToList();
                    if(!missing)response.AddRange(new byte[]{0xc0,0x0c}.Concat(U16(type)).Concat(new byte[]{0,1,0,0,0,120}).Concat(U16(data.Length)).Concat(data));
                    await udp.SendAsync(response.ToArray(),packet.RemoteEndPoint,stop.Token);
                } }
                catch(Exception ex) when(ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
            });
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); udp.Dispose(); await loop; stop.Dispose(); }
    }
    private sealed class SnmpFixture : IAsyncDisposable
    {
        private readonly UdpClient udp=new(new IPEndPoint(IPAddress.Loopback,0)); private readonly CancellationTokenSource stop=new(); private readonly Task loop;
        internal int Port=>((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        internal SnmpFixture()
        {
            var values=new[]{new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.1.0"),new OctetString("fixture description")),new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.5.0"),new OctetString("fixture name"))};
            loop=Task.Run(async()=>
            { try { while(!stop.IsCancellationRequested)
            {
                var datagram=await udp.ReceiveAsync(stop.Token); var message=MessageFactory.ParseMessages(datagram.Buffer,0,datagram.Buffer.Length,new UserRegistry()).Single(); var oid=message.Variables().Single().Id.ToString();
                var next=message.TypeCode()!=SnmpType.GetRequestPdu; var found=next?values.FirstOrDefault(value=>string.CompareOrdinal(value.Id.ToString(),oid)>0):values.FirstOrDefault(value=>value.Id.ToString()==oid);
                var result=found??new Variable(new ObjectIdentifier("1.3.6.1.2.1.2.1.0"),new OctetString("outside fixture"));
                var reply=new ResponseMessage(message.Pdu().RequestId.ToInt32(),VersionCode.V2,new OctetString("test-community"),ErrorCode.NoError,0,new List<Variable>{result}); await udp.SendAsync(reply.ToBytes(),datagram.RemoteEndPoint,stop.Token);
            } } catch(Exception ex) when(ex is OperationCanceledException or SocketException or ObjectDisposedException) { } });
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); udp.Dispose(); await loop; stop.Dispose(); }
    }
}
