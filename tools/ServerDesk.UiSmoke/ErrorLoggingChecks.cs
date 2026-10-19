using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ErrorLoggingChecks
{
    private const string Private="private-error-message-and-data-canary";
    private static Exception Failure()
    {
        try { throw new IOException(Private); }
        catch(Exception inner) { try { throw new InvalidOperationException("local validation",inner); } catch(Exception error) { error.Data["password"]=Private; return error; } }
    }
    internal static void Run(string root)
    {
        var previous=Log.Logger; var language=AppText.Language; var sink=new Capture();
        using var logger=new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger(); Log.Logger=logger;
        try
        {
            foreach(var chosen in new[]{"pl","en"})CheckWindow(root,chosen,sink);
            var unobserved=new UnobservedTaskExceptionEventArgs(new AggregateException(Failure()));
            ApplicationDiagnostics.UnobservedTask(null,unobserved);
            if(!unobserved.Observed || sink.Events.Last().Level!=LogEventLevel.Warning)throw new Exception("Background task failure was not observed/logged");
            ApplicationDiagnostics.UnhandledException(null,new UnhandledExceptionEventArgs(Failure(),false));
            if(sink.Events.Last().Level!=LogEventLevel.Fatal)throw new Exception("Fatal failure was not classified");
            foreach(var entry in sink.Events)
            {
                var text=entry.RenderMessage();
                if(entry.Exception!=null || text.Contains(Private) || !text.Contains(nameof(Failure)) || !text.Contains("HResult=0x") || !entry.Properties.ContainsKey("ErrorId"))throw new Exception("Missing safe exception context or leaked exception message/data");
            }
            CheckFiles(root);
        }
        finally { Log.Logger=previous; AppText.SetLanguage(language); }
        Console.WriteLine("PASS error logging PL/EN: one modal for concurrent failures, distinct IDs match logs, retry/cancel/late closed-owner failure, redacted outer/inner stacks, observed background failures and fatal severity; shared daily/size rolling files, retention, reader compatibility and continued writes after rollover");
    }
    private static void CheckWindow(string root,string language,Capture sink)
    {
        AppText.SetLanguage(language); var repo=new ProfileRepository(new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N")))); RedesignChecks.Wait(repo.InitializeAsync());
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { Language=language } }));
        var vault=new NoSecrets(); var main=new MainWindow(new MainViewModel(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames();
        try
        {
            var before=sink.Events.Count; var first=main.Safe(()=>Task.FromException(Failure()),"Fixture.First"); RedesignChecks.Frames();
            var dialog=main.OwnedWindows.Single(); var text=dialog.GetVisualDescendants().OfType<SelectableTextBlock>().Single().Text!;
            var firstEntry=sink.Events.Last(); var id=(string)((ScalarValue)firstEntry.Properties["ErrorId"]).Value!;
            if(!text.Contains(id) || !text.Contains(AppText.T("Identyfikator błędu: {0}").Split('{')[0]))throw new Exception("Dialog and log IDs differ or are not localized");
            dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"error-dialog-"+language+".png"));
            RedesignChecks.Wait(main.Safe(()=>throw Failure(),"Fixture.Second"));
            if(main.OwnedWindows.Count!=1 || sink.Events.Count!=before+2 || sink.Events.Last().Properties["ErrorId"].Equals(firstEntry.Properties["ErrorId"]))throw new Exception("Concurrent failures stacked dialogs or lost their own log entries");
            using var cancelled=new CancellationTokenSource(); cancelled.Cancel(); RedesignChecks.Wait(main.Safe(()=>Task.FromCanceled(cancelled.Token),"Fixture.Cancel"));
            if(sink.Events.Count!=before+2)throw new Exception("Expected cancellation logged as error");
            dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zamknij").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(first);
            var retry=main.Safe(()=>Task.FromException(Failure()),"Fixture.Retry"); RedesignChecks.Frames(); if(main.OwnedWindows.Count!=1)throw new Exception("A later failure could no longer show a dialog"); main.OwnedWindows.Single().Close(); RedesignChecks.Wait(retry);
            var delayed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var late=main.Safe(()=>delayed.Task,"Fixture.Late"); main.Close(); RedesignChecks.Frames();
            if(main.IsVisible)throw new Exception("Owner did not close"); delayed.SetException(Failure()); RedesignChecks.Wait(late);
            if(main.OwnedWindows.Count!=0 || vault.Calls!=0)throw new Exception("Late failure reopened a closed window or accessed credentials");
        }
        finally { foreach(var window in main.OwnedWindows.ToArray())window.Close(); main.Close(); RedesignChecks.Frames(); }
    }
    private static void CheckFiles(string root)
    {
        var folder=Path.Combine(root,"rolling-"+Guid.NewGuid().ToString("N"));
        var errorId="";
        using(var one=ApplicationDiagnostics.CreateLogger(folder,1024,4))
        using(var two=ApplicationDiagnostics.CreateLogger(folder,1024,4))
        {
            for(var i=0;i<80;i++) { one.Information("first-process-fixture {Number} {Padding}",i,new string('a',120)); two.Information("second-process-fixture {Number} {Padding}",i,new string('b',120)); }
            one.Information("latest-first-marker"); two.Information("latest-second-marker");
            var previous=Log.Logger;
            try { Log.Logger=one; errorId=ApplicationDiagnostics.Report("Fixture.FilePrivacy",Failure()); }
            finally { Log.Logger=previous; }
        }
        var files=Directory.GetFiles(folder,"*.log"); var text=string.Join('\n',files.Select(File.ReadAllText));
        if(files.Length is <2 or >4 || files.Any(f=>new FileInfo(f).Length>1600) || !text.Contains("latest-first-marker") || !text.Contains("latest-second-marker"))throw new Exception("Rolling/shared logs lost final events or exceeded retention");
        if(text.Contains(Private) || !text.Contains(errorId) || !text.Contains("HResult=0x") || !text.Contains(nameof(Failure)))throw new Exception("Written logs lost diagnostics or exposed private exception data");
        var listing=new ApplicationLogFiles(folder).ListAsync().GetAwaiter().GetResult(); if(listing.Entries.Count!=files.Length)throw new Exception("Rolled logs cannot be read in Settings");
        File.WriteAllText(Path.Combine(root,"error-logging-result.txt"),"PASS PL/EN error containment, ID correlation, safe diagnostics, cancellation/late completion and shared log rollover/retention.\n");
    }
    private sealed class Capture:ILogEventSink { internal readonly ConcurrentQueue<LogEvent> Events=new(); public void Emit(LogEvent entry)=>Events.Enqueue(entry); }
    private sealed class NoSecrets:ICredentialStore
    {
        internal int Calls;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected vault access"); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected vault access"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected vault access"); }
    }
}
