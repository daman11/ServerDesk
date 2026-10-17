using System.Net.Security;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class RdpGuiChecks
{
    private sealed class Vault:ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<string?>(null);
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new Exception("Fixture must not save credentials");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>Task.CompletedTask;
    }
    private sealed class CertificateClient(RdpEndpointInfo certificate):IRdpClient
    {
        public bool SupportsEmbeddedLinux=>true;
        public int Starts;
        public RdpLaunch Prepare(ServerProfile profile,RdpOptions options)=>throw new Exception("GUI RDP must not launch a CLI terminal");
        public Task<RdpEndpointInfo?> InspectCertificateAsync(ServerProfile profile,CancellationToken ct)=>Task.FromResult<RdpEndpointInfo?>(certificate);
        public RdpLaunch PrepareEmbeddedLinux(ServerProfile profile,RdpOptions options,ulong parent,string fingerprint) { Starts++; throw new Exception("Cancelled certificate must prevent launch"); }
    }
    private static void Click(Window dialog,string tag) { dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(3); }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var certificate=new RdpEndpointInfo(TimeSpan.FromMilliseconds(7),true,"NLA",new string('S',160),new string('A',64),SslPolicyErrors.RemoteCertificateChainErrors|SslPolicyErrors.RemoteCertificateNameMismatch,DateTime.Now.AddDays(-1),"Disposable <issuer>",DateTime.Now.AddYears(-1));
            var adapter=new CertificateClient(certificate); var main=new MainWindow(new MainViewModel(repo),repo,new Vault(),new SshConnectionFactory(repo),adapter); main.Show(); RedesignChecks.Frames(20);
            var profile=new ServerProfile { Name="GUI fixture",Protocol=Protocol.RDP,Host="fixture.invalid",Port=3390,Username="LAB\\rdp_fixture" };
            try
            {
                var login=Dialogs.Credentials(main,false,rdp:true,profile:profile); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single();
                var input=dialog.GetVisualDescendants().OfType<TextBox>().Single(); if(input.PasswordChar=='\0')throw new Exception("RDP password not masked");
                if(!dialog.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="fixture.invalid:3390") || !dialog.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="LAB\\rdp_fixture"))throw new Exception("RDP login lost target identity");
                dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-login-"+language+".png")); Click(dialog,"Anuluj"); RedesignChecks.Wait(login); if(login.Result!=null)throw new Exception("Cancel returned a password");
                foreach(var theme in new[]{"graphite","light"})
                {
                    DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 });
                    var confirm=Dialogs.RdpCertificate(main,profile,certificate,default); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); dialog.Width=480; dialog.SetRenderScaling(2); RedesignChecks.Frames();
                    var accept=dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="RdpCertificateAccept");
                    if(accept.IsEnabled || accept.IsDefault || dialog.Name!="RdpCertificateDialog")throw new Exception("Certificate trust enabled without fingerprint verification");
                    if(!dialog.GetVisualDescendants().OfType<SelectableTextBlock>().Any(t=>t.Text==RdpCertificateFingerprint.Display(new string('A',64))))throw new Exception("Certificate fingerprint not selectable/readable");
                    dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"rdp-certificate-{language}-{theme}.png"));
                    foreach(var button in dialog.GetVisualDescendants().OfType<Button>().Where(b=>b.Tag is string)) { var point=button.TranslatePoint(default,dialog)!.Value; if(point.X<0 || point.Y<0 || point.X+button.Bounds.Width>dialog.ClientSize.Width+1 || point.Y+button.Bounds.Height>dialog.ClientSize.Height+1)throw new Exception($"Certificate action {button.Tag} clipped: {point}, {button.Bounds}, client {dialog.ClientSize}"); }
                    dialog.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=true; if(!accept.IsEnabled)throw new Exception("Verified fingerprint did not enable trust");
                    Click(dialog,"Zaufaj w tej karcie"); RedesignChecks.Wait(confirm); if(!confirm.Result)throw new Exception("Explicit trust rejected");
                }
                var escape=Dialogs.RdpCertificate(main,profile,certificate,default); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); dialog.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,null); RedesignChecks.Wait(escape); if(escape.Result)throw new Exception("Escape trusted certificate");
                using var cancelled=new CancellationTokenSource(); var pending=Dialogs.RdpCertificate(main,profile,certificate,cancelled.Token); RedesignChecks.Frames(); cancelled.Cancel(); RedesignChecks.Wait(pending); if(pending.Result)throw new Exception("Cancellation trusted certificate");
                if(OperatingSystem.IsLinux())
                {
                    var connect=main.Safe(()=>main.ConnectAsync(profile)); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single();
                    dialog.GetVisualDescendants().OfType<TextBox>().Single().Text="disposable-rdp-fixture"; Click(dialog,"Połącz"); RedesignChecks.Frames();
                    dialog=main.OwnedWindows.Single(); if(dialog.Name!="RdpCertificateDialog")throw new Exception("RDP connection did not ask in GUI"); Click(dialog,"Anuluj"); RedesignChecks.Wait(connect);
                    var history=repo.HistoryAsync(); RedesignChecks.Wait(history);
                    if(adapter.Starts!=0 || main.OpenSessions.Any(p=>p.HasActiveClient || p.EmbeddedRdpHandle!=0) || main.OpenSessions.Any(p=>p.VisualState!=SessionVisualState.Idle) || history.Result.Count!=0)throw new Exception("Certificate cancellation launched or recorded a session");
                    foreach(var pane in main.OpenSessions.ToArray()) { main.RemovePane(pane); RedesignChecks.Wait(pane.CloseAsync()); }
                }
                Console.WriteLine("PASS "+language+": masked RDP GUI with target identity; certificate details/selectable SHA-256, verified-checkbox gate, explicit trust, Escape/cancellation; 480px/200% font16 Graphite/Light; Linux cancel precedes native host and child launch");
            }
            finally { main.Close(); RedesignChecks.Frames(20); }
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
    }
}
