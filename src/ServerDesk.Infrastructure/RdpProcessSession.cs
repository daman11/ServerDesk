using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ServerDesk.Infrastructure;
public sealed class RdpProcessSession : IDisposable
{
    private readonly Process process;
    private readonly string? configuration;
    private readonly Task<int> completion;
    private readonly object stopGate=new();
    private Task? stopping;
    public Task<int> Completion=>completion;
    public bool IsRunning=>!completion.IsCompleted;
    public bool TryActivateWindow()
    {
        if(!OperatingSystem.IsWindows() || completion.IsCompleted)return false;
        try
        {
            process.Refresh(); var handle=process.MainWindowHandle; if(handle==IntPtr.Zero)return false;
            if(IsIconic(handle))ShowWindowAsync(handle,9);
            return SetForegroundWindow(handle);
        }
        catch(InvalidOperationException) { return false; }
    }
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window,int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    private RdpProcessSession(Process process,string? configuration)
    { this.process=process; this.configuration=configuration; completion=ObserveAsync(); }
    public static RdpProcessSession Start(RdpLaunch launch)
    {
        if(launch.InteractiveConsole)throw new ArgumentException(L.T("Klient z konsolą wymaga terminala PTY."));
        try
        {
            var info=new ProcessStartInfo(launch.Executable) { UseShellExecute=false,CreateNoWindow=true };
            foreach(var argument in launch.Arguments)info.ArgumentList.Add(argument);
            return new(Process.Start(info)??throw new InvalidOperationException(L.T("Nie można uruchomić klienta RDP.")),launch.ConfigurationFile);
        }
        catch { if(launch.ConfigurationFile!=null)File.Delete(launch.ConfigurationFile); throw; }
    }
    private async Task<int> ObserveAsync()
    {
        try { await process.WaitForExitAsync(); return process.ExitCode; }
        finally { if(configuration!=null)File.Delete(configuration); }
    }
    public Task StopAsync()
    { lock(stopGate)return stopping??=StopCoreAsync(); }
    private async Task StopCoreAsync()
    {
        try
        {
            if(!completion.IsCompleted)
            {
                try { if(!process.HasExited)process.CloseMainWindow(); } catch(InvalidOperationException) when(process.HasExited) { }
                try { await completion.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch(TimeoutException) { if(!process.HasExited)process.Kill(entireProcessTree:true); await completion; }
            }
            else await completion;
        }
        finally { process.Dispose(); }
    }
    public void Dispose()=>_ = StopAsync();
}
