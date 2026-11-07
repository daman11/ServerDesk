using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class NetworkDiagnosticsChecks
{
    private const string Private="private-network-body-password-token-canary";
    private sealed class Capture:ILogEventSink
    { internal readonly ConcurrentQueue<LogEvent> Events=new(); public void Emit(LogEvent entry)=>Events.Enqueue(entry); }
    private sealed class NoVault:ICredentialStore
    {
        internal int Calls;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected fixture vault read"); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected fixture vault save"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected fixture vault delete"); }
    }
    private sealed class Probe:IIcmpProbe
    {
        internal bool Fail;
        public Task<IcmpReply> SendAsync(IPAddress address,int ttl,int bytes,int timeoutMs,CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Fail?Task.FromException<IcmpReply>(Failure()):Task.FromResult(new IcmpReply(address,IPStatus.Success,1)); }
    }
    private static Exception Failure()
    {
        try { throw new IOException(Private); }
        catch(Exception inner) { try { throw new PingException(Private,inner); } catch(Exception error) { error.Data["password"]=Private; return error; } }
    }
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Verify(bool condition,string message) { if(!condition)throw new Exception(message); }
    private static Task Guard(NetworkToolPane pane,Func<Task> action,string operation)=>(Task)typeof(NetworkToolPane).GetMethod("GuardAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,[action,operation])!;
    private static void Click(NetworkToolPane pane,string action)=>pane.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==action).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Sql(ProfileRepository repo,string sql)
    {
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()); db.Open();
        using var command=db.CreateCommand(); command.CommandText=sql; command.ExecuteNonQuery();
    }
    private static LogEvent Event(Capture sink,int before,NetworkToolPane pane,string operation,bool visible=true)
    {
        Verify(sink.Events.Count==before+1,"A failure was lost or logged twice"); var entry=sink.Events.Last();
        Verify(entry.Level==LogEventLevel.Warning && entry.Exception==null,"Network operation used the wrong severity or raw exception logging");
        Verify(((ScalarValue)entry.Properties["Operation"]).Value as string=="NetworkTools.Ping."+operation,"Diagnostic context is not a fixed tool/operation name");
        var id=(string)((ScalarValue)entry.Properties["ErrorId"]).Value!;
        Verify(id.Length==32 && Guid.TryParseExact(id,"N",out _),"Invalid error correlation ID");
        if(visible)Verify(pane.StatusText.Contains(id) && pane.StatusText.Contains(AppText.T("Identyfikator błędu: {0}").Split('{')[0]),"Status and log IDs differ or are not localized");
        Verify(!entry.RenderMessage().Contains(Private) && !pane.StatusText.Contains(Private),"Private exception, request or path data leaked");
        Verify(entry.RenderMessage().Contains("HResult=0x"),"Missing safe diagnostic codes"); return entry;
    }
    internal static void Run(string root)
    {
        var previous=Log.Logger; var language=AppText.Language; var sink=new Capture();
        using var logger=new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger(); Log.Logger=logger;
        try
        {
            foreach(var chosen in new[]{"pl","en"})Check(root,chosen,sink);
            Verify(sink.Events.Select(e=>e.Properties["ErrorId"].ToString()).Distinct().Count()==sink.Events.Count,"Different failures reused an ID");
        }
        finally { Log.Logger=previous; AppText.SetLanguage(language); }
        File.WriteAllText(Path.Combine(root,"result.txt"),"PASS PL/EN network diagnostics: run/local/optional errors, correlation IDs, safe stacks, validation/cancel, retry, copied status, compact layout and late completion. No real ICMP or OS credential access.\n");
        Console.WriteLine("PASS network diagnostics PL/EN: run/local/optional failures with distinct safe log IDs; validation/cancellation quiet; retries preserve results/data; selectable full status, bounded compact layout and late closed-tab failure; no real ICMP or OS vault");
    }
    private static void Check(string root,string language,Capture sink)
    {
        AppText.SetLanguage(language); var directory=Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N")); var repo=new ProfileRepository(new AppPaths(directory)); RedesignChecks.Wait(repo.InitializeAsync());
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { Language=language } }));
        var vault=new NoVault(); var store=new NetworkToolsStore(repo,vault); RedesignChecks.Wait(store.AddHistoryAsync(new() { Tool=NetworkToolKind.Ping,Target="127.0.0.1",Count=1 }));
        var owner=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); owner.Show(); RedesignChecks.Frames(8);
        var probe=new Probe { Fail=true }; var pane=new NetworkToolPane(owner,NetworkToolKind.Ping,store,new(probe),[],""); owner.RegisterPane(pane); owner.FocusSession(pane); RedesignChecks.Frames(6); var registered=true;
        try
        {
            pane.ApplyRequest(new() { Tool=NetworkToolKind.Ping,Target="127.0.0.1",Count=1 });
            var before=sink.Events.Count; RedesignChecks.Wait(pane.StartAsync(false)); var failed=Event(sink,before,pane,"Run");
            Verify(failed.RenderMessage().Contains(nameof(Failure)) && failed.RenderMessage().Contains(nameof(IOException)),"Inner stack/cause missing");
            before=sink.Events.Count; pane.ApplyRequest(new() { Tool=NetworkToolKind.Ping,Target="bad host" }); RedesignChecks.Wait(pane.StartAsync(false));
            Verify(sink.Events.Count==before && !pane.StatusText.Contains("Identyfikator") && !pane.StatusText.Contains("Error ID"),"Ordinary input validation was logged as an operational failure");
            probe.Fail=false; pane.ApplyRequest(new() { Tool=NetworkToolKind.Ping,Target="127.0.0.1",Count=1 }); RedesignChecks.Wait(pane.StartAsync(false));
            Verify(sink.Events.Count==before && !pane.IsRunning && Field<ListBox>(pane,"resultList").ItemCount==1 && pane.StatusText==AppText.T("Zakończono"),"Retry did not clear the old failure or retain its result");

            pane.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="NetworkParameters").IsExpanded=true; RedesignChecks.Frames(3);
            pane.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header as string==AppText.T("Historia i kolekcje")).IsExpanded=true; RedesignChecks.Frames(3);
            Sql(repo,"CREATE TRIGGER reject_history BEFORE INSERT ON settings WHEN NEW.key='networkTools.history.v1' BEGIN SELECT RAISE(ABORT,'"+Private+"'); END");
            Field<ComboBox>(pane,"historyChoice").SelectedIndex=0; before=sink.Events.Count; Click(pane,"Usuń wpis historii"); RedesignChecks.Wait(pane.Shutdown);
            Event(sink,before,pane,"History.Delete"); Verify(pane.StatusText.Contains(language=="en"?"local database":"lokalnej bazie"),"Local database failure was presented as a network error");
            Verify(store.HistoryAsync().GetAwaiter().GetResult().Count==1,"Failed history deletion lost the saved entry");
            before=sink.Events.Count; RedesignChecks.Wait(pane.StartAsync()); Event(sink,before,pane,"History.Append");
            Verify(Field<ListBox>(pane,"resultList").ItemCount==1 && pane.StatusText.Contains(AppText.T("Diagnostyka zakończona. Wyniki zachowano, ale nie udało się zapisać lub odczytać historii i ustawień lokalnych.")),"Optional storage failure hid successful diagnostics");
            Sql(repo,"DROP TRIGGER reject_history"); Field<ComboBox>(pane,"historyChoice").SelectedIndex=0; before=sink.Events.Count; Click(pane,"Usuń wpis historii"); RedesignChecks.Wait(pane.Shutdown);
            Verify(sink.Events.Count==before && store.HistoryAsync().GetAwaiter().GetResult().Count==0,"History retry failed after removing the fixture write refusal");

            var blocker=Path.Combine(directory,Private); File.WriteAllText(blocker,"preserve fixture bytes"); before=sink.Events.Count;
            RedesignChecks.Wait(Guard(pane,()=>pane.SaveExportAsync(Path.Combine(blocker,"results.txt"),Private),"Results.Export")); Event(sink,before,pane,"Results.Export");
            Verify(File.ReadAllText(blocker)=="preserve fixture bytes" && pane.StatusText.Contains(AppText.T("Nie udało się zapisać eksportu. Poprzedni plik pozostał zachowany. Sprawdź wolne miejsce, uprawnienia i blokadę pliku, a następnie ponów zapis.")),"Failed export damaged existing bytes or lost save guidance");
            owner.Width=920; owner.Height=620; RedesignChecks.Frames(6);
            var status=Field<SelectableTextBlock>(pane,"status"); var viewport=pane.GetVisualDescendants().OfType<ScrollViewer>().Single(c=>c.Name=="NetworkStatusScroll");
            Verify(viewport.Bounds.Height<=96.5 && status.MaxLines==0 && viewport.IsEffectivelyVisible,"Status truncates content or consumes an unbounded result area");
            status.SelectAll(); status.Copy(); RedesignChecks.Frames(4);
            var clipboard=owner.Clipboard!.TryGetTextAsync(); RedesignChecks.Wait(clipboard); Verify(clipboard.Result==pane.StatusText,"Copied status lost the message or error ID");
            var start=Field<Button>(pane,"start"); var point=start.TranslatePoint(default,pane)!.Value;
            Verify(point.Y>=0 && point.Y+start.Bounds.Height<=pane.Bounds.Height+1,"Recovery status hides Start in a compact window");
            owner.CaptureRenderedFrame()!.Save(Path.Combine(root,"network-error-"+language+".png"));
            before=sink.Events.Count; var savedStatus=pane.StatusText; using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
            RedesignChecks.Wait(Guard(pane,()=>Task.FromCanceled(cancelled.Token),"Results.Export")); Verify(sink.Events.Count==before && savedStatus==pane.StatusText,"Expected cancellation changed status or created an error log");
            var delayed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var late=Guard(pane,()=>delayed.Task,"FixtureLate");
            owner.RemovePane(pane); registered=false; delayed.SetException(Failure()); RedesignChecks.Wait(late); RedesignChecks.Wait(pane.Shutdown); Event(sink,before,pane,"FixtureLate",visible:false);
            Verify(pane.StatusText==savedStatus && owner.OwnedWindows.Count==0 && vault.Calls==0,"Late failure changed a closed pane, opened a dialog or used OS credentials");
        }
        finally { if(registered)owner.RemovePane(pane); RedesignChecks.Wait(pane.Shutdown); owner.Close(); RedesignChecks.Frames(5); }
    }
}
