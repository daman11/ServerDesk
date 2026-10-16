using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class LinuxDesktopChecks
{
    private sealed class Client(string executable,string[]? fixtureArgs=null):IRdpClient
    {
        public RdpLaunch Prepare(ServerProfile profile,RdpOptions options)=>new(executable,fixtureArgs??RdpConfiguration.FreeRdpArguments(profile,options),true);
        public async Task<RdpEndpointInfo?> CheckAsync(ServerProfile profile,CancellationToken ct)=>fixtureArgs!=null?null:await new RdpEndpointProbe().CheckAsync(profile,ct);
    }
    private static TerminalControl Terminal(SessionPane pane)=>(TerminalControl)typeof(SessionPane).GetField("terminal",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static Button Toggle(Window window)=>window.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="RdpConsoleToggle");
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(3); }
    private static void Until(Func<bool> ready,string message)
    { var end=DateTime.UtcNow.AddSeconds(20); while(!ready() && DateTime.UtcNow<end)RedesignChecks.Frames(1); if(!ready())throw new Exception(message); }
    private static (MainWindow Main,ProfileRepository Repo) Owner(string root)
    {
        var paths=new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
        var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); return (main,repo);
    }
    internal static void Run(string root)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Linux-only client-side resize and PTY check");
        var previous=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var (main,repo)=Owner(root);
            try
            {
                DesignSystem.Apply("graphite"); DesignSystem.ApplyPreferences(previous with { InterfaceFontSize=16 });
                var edges=main.GetVisualDescendants().OfType<Border>().Where(b=>b.Name?.StartsWith("WindowResize")==true).ToArray();
                if(!main.CanResize || edges.Length!=8)throw new Exception("Linux resize edges missing");
                foreach(var scale in new[]{1d,1.5,2})
                {
                    main.SetRenderScaling(scale); main.Width=1000; main.Height=660; RedesignChecks.Frames(5);
                    using var frame=main.CaptureRenderedFrame(); // Commit the compositor scene before its hit test.
                    foreach(var edge in edges)
                    {
                        var point=edge.TranslatePoint(new Point(edge.Bounds.Width/2,edge.Bounds.Height/2),main)!.Value;
                        var hit=main.InputHitTest(point) as Control;
                        if(!edge.IsVisible || hit!=edge || WindowDecorationProperties.GetElementRole(edge)==WindowDecorationsElementRole.None)throw new Exception($"Resize edge cannot receive its pointer: {edge.Name}, scale={scale}, point={point}, bounds={edge.Bounds}, client={main.ClientSize}, hit={hit?.GetType().Name}/{hit?.Name}");
                    }
                }
                main.WindowState=WindowState.Maximized; RedesignChecks.Frames(); if(edges.Any(b=>b.IsVisible))throw new Exception("Resize edges cover maximized controls");
                main.WindowState=WindowState.Normal; RedesignChecks.Frames(); if(edges.Any(b=>!b.IsVisible))throw new Exception("Restore lost resize edges");
                main.CanResize=false; RedesignChecks.Frames(); if(edges.Any(b=>b.IsVisible))throw new Exception("Non-resizable window retained grips"); main.CanResize=true;
                main.SetRenderScaling(1); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"linux-resize-"+language+".png"));
                var script=Path.Combine(root,"rdp-console-fixture.sh");
                File.WriteAllText(script,"#!/bin/bash\nprintf 'RDP_FIXTURE_READY\\n'\nread -r answer\nfor ((i=0;i<180;i++)); do printf 'disposable TLS diagnostic %s\\n' \"$i\"; done\nprintf 'RDP_FIXTURE_TLS_DETAIL\\n'\nexit 133\n");
                using var pane=new SessionPane(main,new() { Name="Disposable RDP console",Protocol=Protocol.RDP,Host="fixture.invalid",Port=3389 },new SshConnectionFactory(repo),new Client("/bin/bash",[script]),repo);
                var window=new Window { Content=pane,Width=780,Height=570 }; window.Show(); RedesignChecks.Frames();
                try
                {
                    RedesignChecks.Wait(pane.ConnectAsync("",default)); var terminal=Terminal(pane); Until(()=>SshAcceptanceChecks.Text(terminal).Contains("RDP_FIXTURE_READY"),"Owned PTY did not start");
                    RedesignChecks.Wait(terminal.SendInputAsync("finish\r",default)); Until(()=>!pane.HasActiveClient && pane.VisualState==SessionVisualState.Failed,"PTY failure did not update state");
                    if(!terminal.IsVisible || !SshAcceptanceChecks.Text(terminal).Contains("RDP_FIXTURE_TLS_DETAIL") || !Toggle(window).IsVisible || !Toggle(window).IsEnabled)throw new Exception("RDP failure hid console output");
                    var bar=terminal.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().Single(); if(bar.Maximum<=0)throw new Exception("RDP console lost scrollback");
                    Click(Toggle(window)); if(terminal.IsVisible)throw new Exception("RDP summary toggle failed");
                    window.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-summary-"+language+".png"));
                    Click(Toggle(window)); if(!terminal.IsVisible || !SshAcceptanceChecks.Text(terminal).Contains("RDP_FIXTURE_TLS_DETAIL"))throw new Exception("Reopening console lost diagnostics");
                    window.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-console-"+language+".png"));
                    RedesignChecks.Wait(pane.ReconnectAsync()); Until(()=>terminal.IsLive && SshAcceptanceChecks.Text(terminal).Contains("RDP_FIXTURE_READY"),"PTY reconnect failed");
                    RedesignChecks.Wait(pane.CloseAsync()); if(pane.HasActiveClient)throw new Exception("Owned fixture survived pane close");
                }
                finally { RedesignChecks.Wait(pane.CloseAsync()); window.Close(); }
                Console.WriteLine("PASS "+language+": 8 Linux resize hit areas at 100/150/200%, maximize/restore/CanResize; actual owned PTY exit133 retains console/scrollback, summary toggle and reconnect/close");
            }
            finally { main.Close(); RedesignChecks.Frames(20); }
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(previous); DesignSystem.Apply("dark");
    }
    internal static void Certificate(string root,string executable)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException();
        using var server=new CertificateEndpoint(); var (main,repo)=Owner(root);
        using var pane=new SessionPane(main,new() { Name="Disposable certificate endpoint",Protocol=Protocol.RDP,Host="127.0.0.1",Port=server.Port,Username="rdp_fixture" },new SshConnectionFactory(repo),new Client(executable),repo);
        var window=new Window { Content=pane,Width=1050,Height=720 }; window.Show(); RedesignChecks.Frames();
        try
        {
            RedesignChecks.Wait(pane.ConnectAsync("",default)); var terminal=Terminal(pane); var supplied=false;
            Until(()=>
            {
                var text=SshAcceptanceChecks.Text(terminal);
                if(!supplied && text.Contains("Password:")) { supplied=true; RedesignChecks.Wait(terminal.SendInputAsync("disposable-fixture-only\r",default)); }
                if(!terminal.IsLive && !text.Contains("Do you trust the above certificate?"))throw new Exception("FreeRDP exited before certificate prompt: "+text);
                return text.Contains("Do you trust the above certificate?");
            },"FreeRDP did not offer certificate verification");
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,"freerdp-certificate-prompt.png"));
            RedesignChecks.Wait(terminal.SendInputAsync("n\r",default)); Until(()=>!terminal.IsLive && pane.VisualState==SessionVisualState.Failed,"Rejecting certificate did not stop FreeRDP");
            RedesignChecks.Wait(server.Completion);
            if(!terminal.IsVisible || !Toggle(window).IsEnabled || server.ReceivedApplicationData)throw new Exception("Certificate rejection lost details or allowed application data");
            Console.WriteLine("PASS actual FreeRDP + loopback TLS fixture: normal interactive certificate prompt available, explicit rejection stops client, no CredSSP application data received; diagnostics remain visible");
        }
        finally { RedesignChecks.Wait(pane.CloseAsync()); window.Close(); main.Close(); RedesignChecks.Frames(20); }
    }
    private sealed class CertificateEndpoint:IDisposable
    {
        private readonly TcpListener listener=new(IPAddress.Loopback,0);
        private readonly CancellationTokenSource stop=new(TimeSpan.FromSeconds(35));
        private readonly RSA key=RSA.Create(2048);
        private readonly X509Certificate2 certificate;
        internal readonly Task Completion;
        internal bool ReceivedApplicationData;
        internal int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
        internal CertificateEndpoint()
        {
            var request=new CertificateRequest("CN=rdp-fixture.invalid",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1)); listener.Start(); Completion=Serve();
        }
        private async Task Serve()
        {
            for(var connection=0;connection<2;connection++)
            {
                using var socket=await listener.AcceptTcpClientAsync(stop.Token); var stream=socket.GetStream(); var header=new byte[4]; await stream.ReadExactlyAsync(header,stop.Token);
                var length=(header[2]<<8)|header[3]; if(length<11 || length>1024)throw new Exception("Unexpected fixture request length"); await stream.ReadExactlyAsync(new byte[length-4],stop.Token);
                await stream.WriteAsync(new byte[]{3,0,0,19,14,208,0,0,0,0,0,2,0,8,0,2,0,0,0},stop.Token);
                if(connection==0)continue; // The production preflight does not send credentials or TLS here.
                using var tls=new SslStream(stream,false);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls12 },stop.Token);
                try { ReceivedApplicationData=await tls.ReadAsync(new byte[1],stop.Token)>0; } catch(IOException) { }
            }
        }
        public void Dispose() { stop.Cancel(); listener.Stop(); certificate.Dispose(); key.Dispose(); stop.Dispose(); }
    }
}
