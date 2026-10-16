using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class RestartProcessChecks
{
    private sealed record Handoff(int Child,string Data,bool Waited,bool Saved,bool Closed);
    private static string ReadLogs(string data)=>string.Join("\n",Directory.EnumerateFiles(data,"serverdesk-*.log",SearchOption.AllDirectories).Select(path=>
    { using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite); using var reader=new StreamReader(stream); return reader.ReadToEnd(); }));
    internal static void Helper(string root,string executable)
    {
        AppText.SetLanguage("pl");
        var paths=new AppPaths(); var repo=new ProfileRepository(paths);
        var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient());
        using var done=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        Exception? failure=null; Process? child=null; var finished=false;
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };
        main.Closed+=(_,_)=>done.Cancel();
        timer.Tick+=async (_,_)=>
        {
            timer.Stop();
            try
            {
                await repo.SaveAsync(new ServerProfile { Name="Restart żółć",Host="localhost",Username="fixture" });
                var preferences=await repo.ApplicationSettingsAsync();
                await repo.SaveApplicationSettingsAsync(preferences with { Preferences=preferences.Preferences with { Language="en" } });
                main.UpdateLanguageRestartNotice("en");
                main.StartReplacement=()=>
                {
                    using var parent=Process.GetCurrentProcess();
                    child=Process.Start(ApplicationRestart.CreateStartInfo(executable,Path.Combine(Path.GetDirectoryName(executable)!,"ServerDesk.App.dll"),parent.Id,parent.StartTime.ToUniversalTime().Ticks))!;
                };
                main.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="LanguageRestartButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch(Exception ex) { failure=ex; done.Cancel(); }
        };
        try
        {
            main.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(done.Token);
            if(failure!=null)throw failure;
            if(child==null || child.HasExited || main.IsVisible)throw new Exception("Native restart button did not close the root and start its replacement");
            Thread.Sleep(300);
            // App.Initialize also logs the helper's original Polish startup.
            child.Refresh(); var waited=child.MainWindowHandle==0 && !ReadLogs(paths.Data).Contains("Application started in en");
            var saved=repo.PreferencesAsync().GetAwaiter().GetResult().Language=="en" && repo.ListAsync().GetAwaiter().GetResult().Any(p=>p.Name=="Restart żółć");
            File.WriteAllText(Path.Combine(root,"handoff.json"),JsonSerializer.Serialize(new Handoff(child.Id,paths.Data,waited,saved,!main.IsVisible)));
            if(!waited || !saved)throw new Exception("Replacement opened data before the old process exited, or settings were lost");
            finished=true;
        }
        finally { timer.Stop(); if(!finished && child is { HasExited:false })child.Kill(true); child?.Dispose(); }
    }

    internal static async Task Run(string root,string executable)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Native HWND restart acceptance runs on Windows");
        executable=Path.GetFullPath(executable);
        var helperRoot=Path.Combine(root,"restart-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(helperRoot);
        var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach(var arg in new[]{helperRoot,"--native-restart-helper",executable})start.ArgumentList.Add(arg);
        using var helper=Process.Start(start)!; using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40)); Process? replacement=null;
        var output=helper.StandardOutput.ReadToEndAsync(); var error=helper.StandardError.ReadToEndAsync();
        try
        {
            await helper.WaitForExitAsync(timeout.Token);
            if(helper.ExitCode!=0)throw new Exception("Native restart helper failed with exit code "+helper.ExitCode);
            var handoff=JsonSerializer.Deserialize<Handoff>(File.ReadAllText(Path.Combine(helperRoot,"handoff.json")))!;
            replacement=Process.GetProcessById(handoff.Child);
            while(replacement.MainWindowHandle==0)
            {
                timeout.Token.ThrowIfCancellationRequested(); if(replacement.HasExited)throw new Exception("Replacement exited before showing its window");
                await Task.Delay(100,timeout.Token); replacement.Refresh();
            }
            await Task.Delay(1000,timeout.Token);
            var logs=ReadLogs(handoff.Data);
            if(!logs.Contains("Application started in en"))throw new Exception("Replacement did not load destination language at normal startup");
            if(!replacement.CloseMainWindow())throw new Exception("Cannot close owned replacement window"); await replacement.WaitForExitAsync(timeout.Token);
            if(replacement.ExitCode!=0)throw new Exception("Replacement shutdown failed");
            // The replacement inherits the helper's standard handles. Drain
            // captured output after it closes, not while it still owns the pipe.
            File.WriteAllText(Path.Combine(root,"restart-helper.txt"),await output.WaitAsync(timeout.Token)+await error.WaitAsync(timeout.Token));
            File.WriteAllText(Path.Combine(root,"published-restart.json"),JsonSerializer.Serialize(new { Success=true,Parent=helper.Id,Replacement=handoff.Child,handoff.Waited,handoff.Saved,handoff.Closed,Language="en",CleanExit=true },new JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine("PASS native restart button: original root shuts down; replacement waits for exact parent exit, opens published EXE in English with preserved Unicode profile/settings, and closes cleanly");
        }
        finally
        {
            if(!helper.HasExited)helper.Kill(true);
            if(replacement is { HasExited:false })replacement.Kill(true);
            replacement?.Dispose();
        }
    }
}
