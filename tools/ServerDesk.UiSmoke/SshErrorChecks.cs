using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SshErrorChecks
{
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            var paths=new AppPaths(Path.Combine(root,"ssh-errors-"+language)); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(15);
            using var bound=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp); bound.Bind(new IPEndPoint(IPAddress.Loopback,0));
            foreach(var key in new[]{true,false})
            {
                var profile=new ServerProfile { Name="Disposable diagnostic",Host="127.0.0.1",Port=((IPEndPoint)bound.LocalEndPoint!).Port,Username="fixture",Authentication=key?Authentication.PrivateKey:Authentication.Password,PrivateKeyPath=Path.Combine(paths.Data,"absent-key") };
                using var pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); var window=new Window { Width=760,Height=420,Content=pane }; window.Show();
                var attempt=pane.ConnectAsync("private-fixture-sentinel",CancellationToken.None); SshAcceptanceChecks.Until(()=>attempt.IsCompleted,"SSH error fixture did not complete");
                Exception? error=null; try { attempt.GetAwaiter().GetResult(); } catch(Exception ex) { error=ex; }
                if(error==null || pane.VisualState!=SessionVisualState.Failed || pane.SshFailureDetails!=OperationErrorMessages.Describe(error))throw new Exception("SSH pane lost its specific failure details");
                pane.MarkFailed(); RedesignChecks.Frames(3);
                var state=window.GetVisualDescendants().OfType<TextBlock>().Single(t=>ToolTip.GetTip(t) as string==pane.SshFailureDetails);
                if(state.Text!=pane.SshFailureDetails.Split('\n',2)[0] || pane.SshFailureDetails.Contains("private-fixture-sentinel"))throw new Exception("Repeated failure handling lost the heading or exposed a secret");
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"ssh-error-{language}-{(key?"key":"network")}.png"));
                if(repo.HistoryAsync(profileId:profile.Id).Result.Count!=0)throw new Exception("Failed SSH attempt entered connection history");
                RedesignChecks.Wait(pane.CloseAsync()); window.Close();
            }
            main.Close(); RedesignChecks.Frames(10);
        }
        AppText.SetLanguage("pl");
        Console.WriteLine("PASS SSH diagnostics PL/EN: real missing-key and refused-loopback failures, specific inline heading and full tooltip, repeated failure handling, no credentials or history entries");
    }
}
