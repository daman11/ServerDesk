using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class BitwardenNativeChecks
{
    private const string Master="managed-native-master-fixture",Otp="623491";
    private sealed class Host: IAsyncDisposable
    {
        private readonly TcpListener listener=new(IPAddress.Loopback,0);
        private readonly CancellationTokenSource stop=new();
        private readonly List<Task> requests=[];
        private readonly Task work;
        private readonly X509Certificate2 certificate;
        internal readonly List<object> Metadata=[];
        internal bool ReceivedOtp,Block;
        internal TaskCompletionSource Endpoint=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string Url { get; }
        internal Host(string root)
        {
            using var key=RSA.Create(2048);
            var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            var names=new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build()); request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.DigitalSignature,true));
            using var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
            certificate=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pkcs12),null,OperatingSystem.IsWindows()?X509KeyStorageFlags.DefaultKeySet:X509KeyStorageFlags.EphemeralKeySet);
            File.WriteAllText(Path.Combine(root,"fixture-ca.crt"),certificate.ExportCertificatePem());
            listener.Start(); Url="https://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port; work=Loop();
        }
        private async Task Loop()
        {
            try { while(!stop.IsCancellationRequested) { var client=await listener.AcceptTcpClientAsync(stop.Token); lock(requests)requests.Add(Serve(client)); } }
            catch(OperationCanceledException) { } catch(SocketException) when(stop.IsCancellationRequested) { }
        }
        private async Task Serve(TcpClient client)
        {
            using(client)
            {
                using var stream=new SslStream(client.GetStream(),false);
                try { await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate=certificate },stop.Token); }
                catch(System.Security.Authentication.AuthenticationException ex) { lock(Metadata)Metadata.Add(new { tlsError=ex.Message }); return; }
                var header=new List<byte>(); var one=new byte[1];
                while(header.Count<65536)
                {
                    if(await stream.ReadAsync(one,stop.Token)==0)return; header.Add(one[0]);
                    if(header.Count>=4 && header.TakeLast(4).SequenceEqual(new byte[]{13,10,13,10}))break;
                }
                var lines=Encoding.ASCII.GetString(header.ToArray()).Split("\r\n"); var target=lines[0].Split(' ')[1];
                var lengthLine=lines.FirstOrDefault(l=>l.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase));
                var length=lengthLine==null?0:int.Parse(lengthLine.Split(':',2)[1]); if(length>65536)throw new Exception("Fixture body too large");
                var body=new byte[length]; await stream.ReadExactlyAsync(body,stop.Token); var form=Encoding.UTF8.GetString(body);
                if(form.Contains(Master,StringComparison.Ordinal))throw new Exception("Master password sent in plaintext to identity endpoint");
                var path=target.Split('?')[0]; lock(Metadata)Metadata.Add(new { method=lines[0].Split(' ')[0],path });
                string json; var status=200;
                if(path.EndsWith("/config",StringComparison.Ordinal))json=JsonSerializer.Serialize(new { version="2026.10.0",gitHash="fixture",server=new { name="Vaultwarden",url=Url },environment=new { vault=Url,api=Url+"/api",identity=Url+"/identity",notifications=Url+"/notifications",sso=Url+"/sso" },featureStates=new Dictionary<string,bool>() });
                else if(path.EndsWith("/accounts/prelogin",StringComparison.Ordinal))json="{\"kdf\":0,\"kdfIterations\":600000}";
                else if(path.EndsWith("/accounts/prelogin/password",StringComparison.Ordinal))json="{\"KdfSettings\":{\"KdfType\":0,\"Iterations\":600000},\"Salt\":\"fixture@example.invalid\"}";
                else if(path.EndsWith("/connect/token",StringComparison.Ordinal))
                {
                    Endpoint.TrySetResult();
                    if(Block)await Task.Delay(Timeout.Infinite,stop.Token);
                    if(form.Contains("twoFactorToken="+Otp,StringComparison.Ordinal)) { ReceivedOtp=true; status=400; json="{\"error\":\"invalid_grant\",\"error_description\":\"Disposable fixture intentionally refuses login\"}"; }
                    else { status=400; json="{\"error\":\"invalid_grant\",\"error_description\":\"Two factor required.\",\"TwoFactorProviders\":[0],\"TwoFactorProviders2\":{\"0\":null}}"; }
                }
                else { status=404; json="{}"; }
                var bytes=Encoding.UTF8.GetBytes(json);
                var response=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status==200?"OK":"Bad Request")}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                try { await stream.WriteAsync(response,stop.Token); await stream.WriteAsync(bytes,stop.Token); }
                catch(IOException) { }
            }
        }
        public async ValueTask DisposeAsync()
        {
            stop.Cancel(); listener.Stop(); try { await work; } catch(OperationCanceledException) { }
            Task[] pending; lock(requests)pending=requests.ToArray();
            foreach(var task in pending)try { await task; } catch(OperationCanceledException) { } catch(IOException) { }
            stop.Dispose(); certificate.Dispose();
        }
    }
    internal static async Task Run(string root)
    {
        var old=Environment.GetEnvironmentVariable("SERVERDESK_DATA_HOME");
        var oldCa=Environment.GetEnvironmentVariable("NODE_EXTRA_CA_CERTS");
        var data=Path.Combine(root,"native-profile"); Directory.CreateDirectory(data); Environment.SetEnvironmentVariable("SERVERDESK_DATA_HOME",data);
        try
        {
            await BitwardenRuntime.VerifyAsync(BitwardenRuntime.Executable(AppContext.BaseDirectory),default);
            await using var host=new Host(root); Environment.SetEnvironmentVariable("NODE_EXTRA_CA_CERTS",Path.Combine(root,"fixture-ca.crt"));
            var options=new BitwardenCliOptions(Server:host.Url,Email:"fixture@example.invalid");
            using var client=BitwardenCli.Create(options); var codes=0;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));
            try { await client.LoginAsync(new(options.Email,Master),(_,_)=> { codes++; return Task.FromResult(Otp); },timeout.Token); throw new Exception("Fixture must not authenticate"); }
            catch(BitwardenException ex) when(ex.Reason==BitwardenFailure.CommandFailed) { }
            await File.WriteAllTextAsync(Path.Combine(root,"requests.json"),JsonSerializer.Serialize(host.Metadata));
            if(codes!=1 || !host.ReceivedOtp)throw new Exception("Native GUI-to-stdin 2FA bridge did not reach the identity endpoint; prompts="+codes);
            if(client.IsUnlocked)throw new Exception("Rejected fixture login became unlocked");
            host.Block=true; host.Endpoint=new(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancel=new CancellationTokenSource(); var pending=client.LoginAsync(new(options.Email,Master),(_,_)=>Task.FromResult(Otp),cancel.Token);
            await host.Endpoint.Task.WaitAsync(TimeSpan.FromSeconds(90)); cancel.Cancel();
            try { await pending; throw new Exception("Native login ignored cancellation"); } catch(OperationCanceledException) { }
            using(var held=new FileStream(Path.Combine(BitwardenRuntime.ProfileDirectory(data,options),".serverdesk.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) { }
            foreach(var file in Directory.EnumerateFiles(data,"*",SearchOption.AllDirectories))
            {
                var text=await File.ReadAllTextAsync(file);
                if(text.Contains(Master,StringComparison.Ordinal)||text.Contains(Otp,StringComparison.Ordinal))throw new Exception("Login input reached a native CLI profile file");
            }
            await File.WriteAllTextAsync(Path.Combine(root,"native.json"),JsonSerializer.Serialize(new { passed=true,version=BitwardenRuntime.Version,prompts=codes,receivedOtp=host.ReceivedOtp,requests=host.Metadata,cancellation=true,plaintextInputFound=false,realAccountUsed=false },new JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine("PASS bundled official OSS CLI: pinned executable, synthetic TLS loopback authentication, real Inquirer 2FA stdin bridge, duplicate redraw suppression, cancelled native child, profile lock release and no plaintext master/code in isolated profile; login deliberately refused, no real account used");
        }
        finally { Environment.SetEnvironmentVariable("SERVERDESK_DATA_HOME",old); Environment.SetEnvironmentVariable("NODE_EXTRA_CA_CERTS",oldCa); }
    }
}
