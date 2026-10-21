using System.Diagnostics;
using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed record PreparedApplicationUpdate(string Directory,ApplicationUpdate Package);

public static class ApplicationUpdateInstaller
{
    public static bool IsSupported(string installation)=>OperatingSystem.IsWindows() && Environment.Is64BitProcess &&
        File.Exists(Path.Combine(installation,"install-state.ini")) && File.Exists(Path.Combine(installation,"Uninstall.exe")) &&
        File.Exists(Path.Combine(installation,"ServerDesk.Updater.exe"));

    public static async Task PrepareAsync(PreparedApplicationUpdate update,string installation,string currentVersion,CancellationToken ct=default)
    {
        if(!IsSupported(installation))throw new NotSupportedException("In-app installation requires a Windows x64 installation made with ServerDesk Setup.");
        using var parent=Process.GetCurrentProcess();
        var request=new UpdateInstallRequest(update.Package,Path.GetFullPath(installation),parent.Id,parent.StartTime.ToUniversalTime().Ticks,currentVersion);
        request.Validate(update.Directory);
        await using(var file=File.OpenRead(Path.Combine(update.Directory,update.Package.FileName)))await update.Package.VerifyAsync(file,ct);
        File.Copy(Path.Combine(installation,"ServerDesk.Updater.exe"),Path.Combine(update.Directory,"ServerDesk.Updater.exe"),false);
        await File.WriteAllTextAsync(Path.Combine(update.Directory,"update.json"),JsonSerializer.Serialize(request),ct);
    }
    public static void Start(PreparedApplicationUpdate update)
    {
        // Runs from the cache, so the installer can replace every installed file.
        using var child=Process.Start(new ProcessStartInfo(Path.Combine(update.Directory,"ServerDesk.Updater.exe")) { UseShellExecute=false,WorkingDirectory=update.Directory,ArgumentList={"--install"} })??throw new IOException("Cannot start the update helper.");
    }
}
