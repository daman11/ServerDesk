using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.Updater;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if(!OperatingSystem.IsWindows() || args.Length!=1 || args[0]!="--install")return 2;
        var cache=AppContext.BaseDirectory;
        try
        {
            var planFile=Path.Combine(cache,"update.json");
            if(new FileInfo(planFile).Length>16384)throw new InvalidDataException("Update request too large.");
            var plan=JsonSerializer.Deserialize<UpdateInstallRequest>(await File.ReadAllTextAsync(planFile))??throw new InvalidDataException("Missing update request.");
            plan.Validate(cache);
            if(!plan.WaitForParent())throw new TimeoutException("ServerDesk is still running. No files were replaced.");
            using var client=new GitHubUpdateClient(); using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            // Re-fetch the trusted HTTPS metadata after shutdown. A changed or
            // removed asset must never be installed using stale local metadata.
            if(await client.GetReleaseAsync(plan.Package.Version,timeout.Token)!=plan.Package)throw new InvalidDataException("GitHub update metadata changed. Download the update again.");
            var executable=Path.Combine(plan.InstallDirectory,"ServerDesk.App.exe");
            if(!Version.TryParse(FileVersionInfo.GetVersionInfo(executable).FileVersion,out var before) || before.ToString(3)!=plan.CurrentVersion)throw new IOException("Installed version changed. Check for updates again.");
            using var file=new FileStream(Path.Combine(cache,plan.Package.FileName),FileMode.Open,FileAccess.Read,FileShare.Read);
            await plan.Package.VerifyAsync(file,timeout.Token);
            using var installer=Process.Start(plan.InstallerStartInfo(cache))??throw new IOException("Cannot start the installer.");
            using var installTimeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await installer.WaitForExitAsync(installTimeout.Token);
            if(installer.ExitCode!=0)throw new IOException($"Installer failed (exit {installer.ExitCode}). The cached installer can be run manually.");
            if(!Version.TryParse(FileVersionInfo.GetVersionInfo(executable).FileVersion,out var installed) || installed.ToString(3)!=plan.Package.Version)throw new IOException("Installed version mismatch. Run the cached installer manually.");
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute=false,WorkingDirectory=plan.InstallDirectory })?.Dispose();
            File.WriteAllText(Path.Combine(cache,"result.txt"),"Update installed: "+plan.Package.Version);
            return 0;
        }
        catch(Exception ex)
        {
            try { File.WriteAllText(Path.Combine(cache,"error.log"),DateTimeOffset.Now+"\n"+ex); } catch { }
            MessageBox(IntPtr.Zero,"ServerDesk could not complete the update. Your data is preserved.\n\n"+ex.Message+"\n\nDetails and downloaded installer:\n"+cache,"ServerDesk update",0x10);
            return 1;
        }
    }
    [DllImport("user32.dll",EntryPoint="MessageBoxW",CharSet=CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner,string text,string caption,uint flags);
}
