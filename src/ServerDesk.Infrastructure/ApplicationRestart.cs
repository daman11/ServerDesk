using System.Diagnostics;
using System.Globalization;

namespace ServerDesk.Infrastructure;

public static class ApplicationRestart
{
    internal const string WaitArgument="--restart-after";

    internal static ProcessStartInfo CreateStartInfo(string executable,string assembly,int parentId,long parentStarted)
    {
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(assembly))!,CreateNoWindow=true };
        // Development builds can run through dotnet; installed apphosts run directly.
        if(string.Equals(Path.GetFileNameWithoutExtension(executable),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(assembly);
        start.ArgumentList.Add(WaitArgument);
        start.ArgumentList.Add(parentId.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(parentStarted.ToString(CultureInfo.InvariantCulture));
        return start;
    }

    public static Process Start(string assembly)
    {
        using var parent=Process.GetCurrentProcess();
        var executable=Environment.ProcessPath??throw new InvalidOperationException("Application executable is unavailable.");
        return Process.Start(CreateStartInfo(executable,assembly,parent.Id,parent.StartTime.ToUniversalTime().Ticks))??throw new InvalidOperationException("Application could not be started.");
    }

    // The replacement waits before opening SQLite, logs or windows. A PID alone
    // is insufficient because the OS may reuse it after the original exits.
    public static bool WaitForPreviousInstance(string[] args,TimeSpan? timeout=null)
    {
        if(args.Length==0 || args[0]!=WaitArgument)return true;
        if(args.Length!=3 || !int.TryParse(args[1],NumberStyles.None,CultureInfo.InvariantCulture,out var pid) || pid<=0 || pid==Environment.ProcessId ||
            !long.TryParse(args[2],NumberStyles.None,CultureInfo.InvariantCulture,out var ticks) || ticks<=0 || ticks>DateTime.MaxValue.Ticks)return false;
        try
        {
            using var parent=Process.GetProcessById(pid);
            if(parent.StartTime.ToUniversalTime().Ticks!=ticks)return true;
            return parent.WaitForExit((int)Math.Clamp((timeout??TimeSpan.FromMinutes(2)).TotalMilliseconds,0,int.MaxValue));
        }
        catch(ArgumentException) { return true; } // The original process has already exited.
        catch(InvalidOperationException) { return true; }
        catch(System.ComponentModel.Win32Exception) { return false; }
    }
}
