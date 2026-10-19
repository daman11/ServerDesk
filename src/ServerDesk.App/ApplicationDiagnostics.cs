using Serilog;
using Serilog.Events;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static class ApplicationDiagnostics
{
    internal const long LogFileBytes=4*1024*1024;
    internal const int RetainedFiles=14;
    internal static Serilog.Core.Logger CreateLogger(string directory,long fileBytes=LogFileBytes,int retainedFiles=RetainedFiles)=>
        new LoggerConfiguration().Enrich.WithProperty("ProcessId",Environment.ProcessId)
            .WriteTo.File(Path.Combine(directory,"serverdesk-.log"),rollingInterval:RollingInterval.Day,
                fileSizeLimitBytes:fileBytes,rollOnFileSizeLimit:true,retainedFileCountLimit:retainedFiles,shared:true,
                outputTemplate:"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [pid:{ProcessId}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

    // Never pass the Exception itself to Serilog: its message/data may contain
    // server replies, command contents or credentials. IDs correlate UI and logs.
    internal static string Report(string operation,Exception error,LogEventLevel level=LogEventLevel.Error)
    {
        var id=Guid.NewGuid().ToString("N");
        Log.Write(level,"Operation {Operation} failed [ErrorId={ErrorId}]: {ExceptionDetails}",operation,id,ExceptionDiagnostics.Describe(error));
        return id;
    }
    internal static void UnobservedTask(object? sender,UnobservedTaskExceptionEventArgs args)
    {
        Report("Runtime.UnobservedTask",args.Exception,LogEventLevel.Warning);
        args.SetObserved();
    }
    internal static void UnhandledException(object? sender,UnhandledExceptionEventArgs args)
    {
        if(args.ExceptionObject is Exception error)Report("Runtime.UnhandledException",error,LogEventLevel.Fatal);
        else Log.Fatal("Runtime failure of type {Type}",args.ExceptionObject?.GetType().FullName);
        // Record a terminating failure; do not claim the process can recover.
        if(args.IsTerminating)Log.CloseAndFlush();
    }
}
