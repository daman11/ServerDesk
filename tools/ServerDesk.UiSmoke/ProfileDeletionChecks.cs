using System.Reflection;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ProfileDeletionChecks
{
    private sealed class MissingVault:ICredentialStore
    {
        public bool Unavailable;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Deletion must not read credentials");
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default)=>throw new Exception("Deletion must not save credentials");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>Unavailable?Task.FromException(new InvalidOperationException("Fixture Secret Service unavailable")):Task.CompletedTask;
    }
    private sealed class Events:ILogEventSink
    {
        internal readonly List<LogEvent> Entries=[];
        public void Emit(LogEvent entry)=>Entries.Add(entry);
    }
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Read<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Click(Window window,string tag)=>window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static Task RequestDelete(MainWindow main)=> (Task)typeof(MainWindow).GetMethod("DeleteAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null)!;
    private static void Close(MainWindow main)
    {
        main.Close(); RedesignChecks.Frames(3); foreach(var dialog in main.OwnedWindows.ToArray())Click(dialog,"Potwierdź"); SshAcceptanceChecks.Until(()=>!main.IsVisible,"Deletion fixture did not close");
    }
    private static Exception Failure(Task task)
    {
        try { Wait(task); throw new Exception("Expected deletion failure"); }
        catch(InvalidOperationException ex) { return ex; }
    }
    private static void Sql(ProfileRepository repo,string sql)
    {
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()); db.Open(); using var cmd=db.CreateCommand(); cmd.CommandText=sql; cmd.ExecuteNonQuery();
    }
    internal static void ReadBack(string scope,string file,Guid deleted,Guid kept)
    {
        var prefix=Path.GetFullPath(scope)+Path.DirectorySeparatorChar; var full=Path.GetFullPath(file);
        if(!full.StartsWith(prefix,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) || Path.GetFileName(full)!="serverdesk.db")throw new ArgumentException("Readback must remain inside the disposable fixture root");
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=full,Mode=SqliteOpenMode.ReadOnly,Pooling=false,DefaultTimeout=3 }.ToString()); db.Open();
        using var query=db.CreateCommand(); query.CommandText="SELECT id FROM profiles"; using var rows=query.ExecuteReader(); var ids=new List<Guid>(); while(rows.Read())ids.Add(Guid.Parse(rows.GetString(0)));
        if(ids.Contains(deleted) || ids.Count!=1 || ids[0]!=kept)throw new Exception("Deleted profile returned in a fresh process");
        Console.WriteLine("PASS fresh-process read-only database verification");
    }
    private static void FreshProcess(string root,ProfileRepository repo,Guid deleted,Guid kept)
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        if(Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(ProfileDeletionChecks).Assembly.Location);
        foreach(var value in new[]{"--profile-deletion-readback",root,repo.DatabasePath,deleted.ToString(),kept.ToString()})start.ArgumentList.Add(value);
        using var process=Process.Start(start)??throw new Exception("Cannot start deletion readback fixture"); var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { Wait(process.WaitForExitAsync(timeout.Token)); }
        finally { if(!process.HasExited)process.Kill(entireProcessTree:true); }
        var text=Read(output); var diagnostic=Read(error);
        if(process.ExitCode!=0 || !text.Contains("PASS fresh-process"))throw new Exception("Fresh-process deletion readback failed: "+diagnostic);
    }
    internal static void Run(string root)
    {
        var oldLogger=Log.Logger; var events=new Events(); using var logger=new LoggerConfiguration().WriteTo.Sink(events).CreateLogger(); Log.Logger=logger;
        try
        {
            foreach(var (language,protocol) in new[]{("pl",Protocol.SSH),("en",Protocol.RDP)})
            {
                AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+" Żółte profile "+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
                Wait(repo.SaveApplicationSettingsAsync(Read(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language } }));
                var target=new ServerProfile { Name="Delete fixture",Host="192.0.2.10",Username="fixture",Protocol=protocol,Description="Target notes",Folder="Keep folder" };
                var other=target with { Id=Guid.NewGuid(),Name="Keep fixture",Host="192.0.2.11",Description="Keep notes" }; Wait(repo.SaveAsync(target)); Wait(repo.SaveAsync(other)); Wait(repo.ConnectedAsync(target));
                var otherBefore=Read(repo.ListAsync()).Single(p=>p.Id==other.Id); var file=Path.Combine(paths.Data,"keep-file.txt"); File.WriteAllText(file,"untouched");
                ICredentialStore vault=OperatingSystem.IsWindows()?new CredentialStore(paths):new MissingVault(); var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(12);
                vm.SelectedServer=target; var cancel=RequestDelete(main); RedesignChecks.Frames(3); Click(main.OwnedWindows.Single(),"Anuluj"); Wait(cancel);
                if(Read(repo.ListAsync()).Count!=2)throw new Exception("Cancelled profile deletion changed the database");
                var deleting=RequestDelete(main); RedesignChecks.Frames(3); Click(main.OwnedWindows.Single(),"Potwierdź"); Wait(deleting); RedesignChecks.Frames(4);
                if(vm.AllServers.Any(p=>p.Id==target.Id) || Read(repo.ListAsync()).Single()!=otherBefore || File.ReadAllText(file)!="untouched" || Directory.Exists(Path.Combine(paths.Data,"credentials")))throw new Exception("Missing-credential deletion lost scope or failed to refresh");
                var history=Read(repo.HistoryAsync()).Single(); if(history.Profile!=null || history.Snapshot==null || history.CanReconnect)throw new Exception("Deleted profile history was lost or can recreate a connection");
                Close(main);
                FreshProcess(root,repo,target.Id,other.Id);
                var reopened=new ProfileRepository(new AppPaths(paths.Data)); var nextVm=new MainViewModel(reopened); var next=new MainWindow(nextVm,reopened,vault,new(reopened),new ExternalRdpClient()); next.Show(); RedesignChecks.Frames(12);
                if(nextVm.AllServers.Count!=1 || nextVm.AllServers.Single()!=otherBefore || next.OpenSessions.Any(p=>p.Profile.Id==target.Id))throw new Exception("Profile returned after restarting the workspace");

                Wait(reopened.SaveAsync(target)); Wait(nextVm.LoadAsync());
                if(vault is CredentialStore windows)
                {
                    Wait(windows.SaveAsync(target.Id,"disposable-fixture-secret")); var path=Path.Combine(paths.Data,"credentials",target.Id+".bin");
                    using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
                    {
                        Wait(next.DeleteSavedProfileAsync(target));
                        if(Read(reopened.ListAsync()).Any(p=>p.Id==target.Id) || !nextVm.Status.Contains(language=="en"?"credential":"poświadczenia"))throw new Exception("Committed deletion with locked credential lost its cleanup warning");
                        var log=events.Entries.Last(e=>e.Exception is IOException);
                        if(log.Exception?.StackTrace?.Contains("CredentialStore.DeleteAsync")!=true || !log.Exception.Message.Contains(path) || log.RenderMessage().Contains("disposable-fixture-secret"))throw new Exception("Credential failure log lacks the path/stack or contains a secret");
                    }
                }
                Wait(reopened.SaveAsync(target)); Wait(nextVm.LoadAsync());
                Sql(reopened,"CREATE TRIGGER reject_profile_delete BEFORE DELETE ON profiles BEGIN SELECT RAISE(ABORT,'Fixture delete blocked'); END;");
                var databaseFailure=Failure(next.DeleteSavedProfileAsync(target));
                if(databaseFailure.InnerException is not SqliteException || !Read(reopened.ListAsync()).Any(p=>p.Id==target.Id))throw new Exception("Database failure was presented as successful deletion");
                if(vault is CredentialStore && Read(vault.GetAsync(target.Id))!="disposable-fixture-secret")throw new Exception("A failed database delete removed the still-saved password");
                var databaseLog=events.Entries.Last(e=>e.Exception is SqliteException);
                if(databaseLog.Exception?.StackTrace==null || !databaseLog.Properties.ContainsKey("DatabasePath"))throw new Exception("Database failure log lacks the database path or stack");
                Sql(reopened,"DROP TRIGGER reject_profile_delete;"); Wait(next.DeleteSavedProfileAsync(target)); if(Read(reopened.ListAsync()).Single()!=otherBefore)throw new Exception("Retry failed or changed another profile");
                Wait(reopened.SaveAsync(target)); Wait(nextVm.LoadAsync()); var colors=Read(reopened.TagColorsAsync());
                Wait(reopened.SetSettingAsync("connectionTagColors","invalid fixture json"));
                var refreshFailure=Failure(next.DeleteSavedProfileAsync(target));
                if(refreshFailure.InnerException is not System.Text.Json.JsonException || Read(reopened.ListAsync()).Single()!=otherBefore || !refreshFailure.Message.Contains(language=="en"?"permanently deleted":"trwale usunięte"))throw new Exception("Refresh failure hid or rolled back an already committed deletion");
                Wait(reopened.SaveTagColorsAsync(colors)); Wait(nextVm.LoadAsync()); FreshProcess(root,reopened,target.Id,other.Id); Close(next);
            }
            var blockedRepo=new ProfileRepository(new AppPaths(Path.Combine(root,"unavailable-vault"))); Wait(blockedRepo.InitializeAsync()); var blocked=new ServerProfile { Name="Unavailable vault fixture",Host="192.0.2.3",Username="fixture" }; Wait(blockedRepo.SaveAsync(blocked));
            var unavailable=new MissingVault { Unavailable=true }; var blockedMain=new MainWindow(new(blockedRepo),blockedRepo,unavailable,new(blockedRepo),new ExternalRdpClient()); blockedMain.Show(); RedesignChecks.Frames(12);
            Wait(blockedMain.DeleteSavedProfileAsync(blocked)); if(Read(blockedRepo.ListAsync()).Count!=0)throw new Exception("Profile-only deletion failed"); Close(blockedMain);
            Console.WriteLine("PASS profile deletion UI: SSH/PL and RDP/EN; confirmation/cancel; missing credentials; preserved peer profile, notes, history and file; fresh-process verification/restart; locked-vault cleanup warning; SQLite failure preserves profile/password; path/stack logs; retry and post-commit refresh failure. Isolated fixtures, no network.");
        }
        finally { Log.Logger=oldLogger; AppText.SetLanguage("pl"); }
    }
}
