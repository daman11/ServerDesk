using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class PublishedStartupChecks
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action,uint parameter,out Rect rect,uint flags);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint h,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint h);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint h,int attr,out Rect rect,int size);
    internal static async Task Run(string root,string executable)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        var paths=new AppPaths(Path.Combine(root,"published-data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); await repo.InitializeAsync();
        if(!SystemParametersInfo(48,0,out var primary,0))throw new Exception("Cannot read primary working area");
        foreach(var pair in new[]{("x",(primary.Right-250).ToString()),("y",(primary.Bottom-140).ToString()),("width","1320"),("height","840")})await repo.SetSettingAsync(pair.Item1,pair.Item2);
        var steps=new List<string>();
        async Task Launch(bool maximized)
        {
            var start=new ProcessStartInfo(Path.GetFullPath(executable)) { WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(executable))!,UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden };
            start.Environment["SERVERDESK_DATA_HOME"]=paths.Data;
            using var process=Process.Start(start)??throw new Exception("Cannot start published application"); using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                nint handle=0;
                while(handle==0) { timeout.Token.ThrowIfCancellationRequested(); if(process.HasExited)throw new Exception("Published app exited before showing window"); await Task.Delay(100,timeout.Token); process.Refresh(); handle=process.MainWindowHandle; }
                await Task.Delay(900,timeout.Token);
                var monitor=new MonitorInfo { Size=Marshal.SizeOf<MonitorInfo>() }; if(!GetMonitorInfo(MonitorFromWindow(handle,2),ref monitor))throw new Exception("Cannot read actual monitor");
                if(IsZoomed(handle)!=maximized)throw new Exception("Published application restored incorrect maximized state");
                if(DwmGetWindowAttribute(handle,9,out var b,Marshal.SizeOf<Rect>())!=0)throw new Exception("Cannot read published window bounds");
                if(b.Left<monitor.Work.Left-1 || b.Top<monitor.Work.Top-1 || b.Right>monitor.Work.Right+1 || b.Bottom>monitor.Work.Bottom+1)throw new Exception($"Published app off screen: [{b.Left},{b.Top},{b.Right},{b.Bottom}]");
                steps.Add($"Published process {process.Id}: visible bounds [{b.Left},{b.Top},{b.Right},{b.Bottom}], maximized={maximized}");
                if(!process.CloseMainWindow())throw new Exception("Cannot request close of owned test application"); await process.WaitForExitAsync(timeout.Token);
                if(process.ExitCode!=0)throw new Exception("Published app failed during shutdown");
            }
            finally { if(!process.HasExited) { process.Kill(entireProcessTree:true); await process.WaitForExitAsync(); } }
        }
        await Launch(false); var saved=await repo.MainWindowPlacementAsync()??throw new Exception("Published app did not persist geometry");
        await repo.SaveMainWindowPlacementAsync(saved with { Maximized=true }); await Launch(true);
        await repo.SaveMainWindowPlacementAsync(saved with { NormalBounds=saved.NormalBounds with { X=99999,Y=-99999 } }); await Launch(false);
        File.WriteAllText(Path.Combine(root,"published-startup.json"),JsonSerializer.Serialize(new { Success=true,Executable=Path.GetFullPath(executable),Steps=steps },new JsonSerializerOptions { WriteIndented=true }));
        Console.WriteLine("PASS published EXE in three isolated processes: legacy off-screen correction, maximized restart, disconnected-monitor fallback and clean shutdown");
        foreach(var step in steps)Console.WriteLine(step);
    }
}
