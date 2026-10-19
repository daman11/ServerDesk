using Avalonia;
using System.Runtime.InteropServices;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;
internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        if(!ApplicationRestart.WaitForPreviousInstance(args))return 2;
        if(args.FirstOrDefault()=="--check-rdp-host")return RdpExecutableCheck.Run(args);
        if(args.FirstOrDefault()=="--check-rdp-input")return RdpInteractionCheck.Run(args);
        try
        {
            // Initialize diagnostics before Avalonia/native UI startup, so a
            // framework initialization failure does not disappear into a silent logger.
            Serilog.Log.Logger=ApplicationDiagnostics.CreateLogger(new AppPaths().Logs);
            AppDomain.CurrentDomain.UnhandledException+=ApplicationDiagnostics.UnhandledException;
            TaskScheduler.UnobservedTaskException+=ApplicationDiagnostics.UnobservedTask;
            if(OperatingSystem.IsLinux())
            {
                var missing=new List<string>();
                foreach(var library in new[] { "libICE.so.6","libSM.so.6","libfontconfig.so.1","libX11.so.6" })
                    if(NativeLibrary.TryLoad(library,out var handle)) NativeLibrary.Free(handle); else missing.Add(library);
                if(missing.Count>0) { Serilog.Log.Fatal("Startup.NativeDependencies: missing libraries {Libraries}",missing); Console.Error.WriteLine(L.T("ServerDesk: brak bibliotek systemowych: ")+string.Join(", ",missing)+L.T(". Ubuntu/Debian: zainstaluj libice6 libsm6 libfontconfig1 libx11-6. Zobacz README.md / docs/development.md.")); return 1; }
            }
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); return 0;
        }
        catch(DllNotFoundException error) { ApplicationDiagnostics.Report("Application.NativeDependency",error,Serilog.Events.LogEventLevel.Fatal); Console.Error.WriteLine(L.T("ServerDesk: brak zależności natywnej. Sprawdź pakiety systemowe opisane w docs/development.md i rozpakuj całe wydanie aplikacji.")); return 1; }
        catch(Exception error) { ApplicationDiagnostics.Report("Application.Main",error,Serilog.Events.LogEventLevel.Fatal); Console.Error.WriteLine(ExceptionDiagnostics.Describe(error)); return 1; }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException-=ApplicationDiagnostics.UnhandledException;
            TaskScheduler.UnobservedTaskException-=ApplicationDiagnostics.UnobservedTask;
            Serilog.Log.CloseAndFlush();
        }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
