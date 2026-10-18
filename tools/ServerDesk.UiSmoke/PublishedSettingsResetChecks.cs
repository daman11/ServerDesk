using System.Diagnostics;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class PublishedSettingsResetChecks
{
    internal static async Task Run(string root,string executable)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        var paths=new AppPaths(Path.Combine(root,"native-reset-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); await repo.InitializeAsync();
        var profile=new ServerProfile { Name="Disposable native reset fixture",Host="fixture.invalid",Username="fixture",Folder="LAB" }; await repo.SaveAsync(profile); await repo.ConnectedAsync(profile);
        await repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Theme="nord",Preferences=new() { Language="en",InterfaceFontSize=16 } });
        await repo.SetSettingAsync("lastWorkspace","old-layout-sentinel"); await repo.SetSettingAsync("mainWindowPlacement","old-placement-sentinel"); await repo.SetSettingAsync("networkTools.native-fixture","keep");
        var copy=await repo.ScheduleFactorySettingsResetAsync();
        var start=new ProcessStartInfo(Path.GetFullPath(executable)) { WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(executable))!,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden };
        start.Environment["SERVERDESK_DATA_HOME"]=paths.Data;
        using var process=Process.Start(start)??throw new Exception("Cannot start owned reset fixture"); using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try
        {
            while(!process.HasExited && (process.MainWindowHandle==0 || await repo.IsFactorySettingsResetPendingAsync(timeout.Token))) { await Task.Delay(100,timeout.Token); process.Refresh(); }
            if(process.HasExited)throw new Exception("Factory reset startup exited prematurely");
            if(await repo.ApplicationSettingsAsync(timeout.Token)!=ApplicationSettings.Defaults || await repo.SettingAsync("lastWorkspace",timeout.Token)!=null)throw new Exception("Application did not consume the reset before loading preferences/workspace");
            if(!process.CloseMainWindow())throw new Exception("Cannot close owned native reset window"); await process.WaitForExitAsync(timeout.Token);
            if(process.ExitCode!=0 || await repo.IsFactorySettingsResetPendingAsync() || (await repo.ListAsync()).Count!=0 || (await repo.HistoryAsync()).Count!=0 || await repo.SettingAsync("networkTools.native-fixture")!=null || !File.Exists(copy.Path))throw new Exception("Native reset did not clear application data or shut down cleanly");
            if((await DatabaseBackupCatalog.ReadAsync(repo.DatabaseBackupDirectory)).Entries.Count(c=>c.BeforeReset)!=1)throw new Exception("Native reset omitted its final startup backup");
            var logs=string.Join("\n",Directory.GetFiles(paths.Logs,"serverdesk-*.log").Select(File.ReadAllText));
            if(!logs.Contains("Application started in pl",StringComparison.Ordinal) || !logs.Contains("Factory settings reset applied",StringComparison.Ordinal))throw new Exception("Reset did not run before startup language selection");
            Console.WriteLine("PASS actual Windows application startup applies full reset before language/workspace, clears profile/history/plugin data, preserves both backups, and shuts down cleanly; disposable data only");
        }
        finally { if(!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
    }
}
