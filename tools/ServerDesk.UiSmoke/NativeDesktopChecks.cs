using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using ServerDesk.Infrastructure;

internal static class NativeDesktopChecks
{
    internal static async Task ReadCredential(string root,string identity)
    {
        var id=Guid.Parse(identity);
        if(File.ReadAllText(Path.Combine(root,".native-credential-fixture"))!=identity)throw new InvalidOperationException("Not an owned credential fixture");
        Console.InputEncoding=new UTF8Encoding(false);
        var expected=await Console.In.ReadToEndAsync();
        if(string.IsNullOrEmpty(expected) || await new CredentialStore(new AppPaths(root)).GetAsync(id)!=expected)throw new Exception("Native credential process readback differs");
        Console.WriteLine("PASS separate process reads its own native credential without a secret in argv");
    }

    internal static void Run(string root)
    {
        if(Environment.GetEnvironmentVariable("SERVERDESK_DISPOSABLE_DESKTOP")!="1")throw new InvalidOperationException("Native clipboard/vault acceptance requires an explicitly disposable desktop");
        var data=Path.Combine(root,"native-desktop-"+Guid.NewGuid().ToString("N"));
        var paths=new AppPaths(data); var store=new CredentialStore(paths); var id=Guid.NewGuid();
        File.WriteAllText(Path.Combine(data,".native-credential-fixture"),id.ToString());
        var window=new Window { Title="ServerDesk — native clipboard and credential acceptance",Width=720,Height=400,Content=new TextBlock { Text="Disposable native desktop fixture. No saved connections are used.",Margin=new(24) } };
        Exception? failure=null; using var done=new CancellationTokenSource(TimeSpan.FromSeconds(100));
        window.Show();
        Dispatcher.UIThread.Post(async()=>
        {
            SynchronizationContext.SetSynchronizationContext(new AvaloniaSynchronizationContext(Dispatcher.UIThread,DispatcherPriority.Normal));
            string? previous=null; var clipboardChanged=false;
            try
            {
                var clipboard=window.Clipboard??throw new Exception("Native clipboard unavailable");
                previous=await clipboard.TryGetTextAsync();
                var text="ServerDesk żółć λ🙂\nsecond line\tcolumn";
                await clipboard.SetTextAsync(text); clipboardChanged=true;
                if(await clipboard.TryGetTextAsync()!=text)throw new Exception("Native UTF-8 clipboard text differs");
                Console.WriteLine("PASS native OS clipboard API: Unicode, newline and tab round trip (no physical key claim)");
                var secret="Disposable żółć 🙂 "+Guid.NewGuid()+" \r\n";
                await store.SaveAsync(id,secret,done.Token);
                if(await store.GetAsync(id,done.Token)!=secret)throw new Exception("Native vault save/read differs");
                var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,StandardInputEncoding=new UTF8Encoding(false),RedirectStandardOutput=true,RedirectStandardError=true };
                foreach(var value in new[]{"--native-credential-readback",data,id.ToString()})start.ArgumentList.Add(value);
                using(var child=Process.Start(start)??throw new Exception("Credential readback did not start"))
                {
                    try
                    {
                        var output=child.StandardOutput.ReadToEndAsync(); var error=child.StandardError.ReadToEndAsync();
                        await child.StandardInput.WriteAsync(secret); child.StandardInput.Close();
                        await child.WaitForExitAsync(done.Token);
                        if(child.ExitCode!=0)throw new Exception("Native credential child failed: exit="+child.ExitCode);
                        Console.WriteLine((await output).Trim()); await error;
                    }
                    finally { if(!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } }
                }
                await store.SaveAsync(id,secret+"updated",done.Token);
                if(await store.GetAsync(id,done.Token)!=secret+"updated")throw new Exception("Native vault update differs");
                await store.DeleteAsync(id,done.Token);
                if(await store.GetAsync(id,done.Token)!=null)throw new Exception("Native vault delete did not persist");
                File.WriteAllText(Path.Combine(root,"native-desktop.json"),JsonSerializer.Serialize(new { Passed=true,OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,window.DesktopScaling,Screens=window.Screens.All.Select(s=>new { s.Bounds,s.Scaling }),CredentialBackend=OperatingSystem.IsWindows()?"DPAPI":"Secret Service",CredentialId=id,PhysicalInputTested=false },new JsonSerializerOptions { WriteIndented=true }));
                Console.WriteLine("PASS real native vault save, separate process readback, update and delete; own random identity only");
            }
            catch(Exception ex) { failure=ex; }
            finally
            {
                try { await store.DeleteAsync(id); } catch(Exception ex) { failure??=ex; }
                try { if(clipboardChanged)await window.Clipboard!.SetTextAsync(previous??""); } catch(Exception ex) { failure??=ex; }
                window.Close(); done.Cancel();
            }
        });
        Dispatcher.UIThread.MainLoop(done.Token);
        if(failure!=null)throw failure;
        if(!File.Exists(Path.Combine(root,"native-desktop.json")))throw new TimeoutException("Native desktop fixture did not complete");
    }
}
