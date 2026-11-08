using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ConnectionTagBatchChecks
{
    private sealed class NoVault:ICredentialStore
    {
        internal int Calls;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected credential read"); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected credential write"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected credential delete"); }
    }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().FirstOrDefault(c=>c.Name==name)
        ??throw new Exception($"Missing batch control {name}; root={root.Name}, visible={root.IsVisible}");
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Verify(bool ok,string message) { if(!ok)throw new Exception(message); }
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static void Until(Func<bool> ready)
    {
        var timer=Stopwatch.StartNew(); while(!ready()) { if(timer.Elapsed.TotalSeconds>20)throw new TimeoutException("Batch tag fixture timed out"); RedesignChecks.Frames(1); }
    }
    private static async Task Sql(ProfileRepository repo,string sql)
    {
        await using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()); await db.OpenAsync();
        using var command=db.CreateCommand(); command.CommandText=sql; await command.ExecuteNonQueryAsync();
    }
    internal static void Run(string root)
    {
        var original=DesignSystem.Preferences; var theme=DesignSystem.CurrentId;
        try { foreach(var language in new[]{"pl","en"})Check(root,language); }
        finally { AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(original); DesignSystem.Apply(theme); }
        Console.WriteLine("PASS batch tags: PL/EN Graphite/Light/font16, live browser filter scope, row preselection, search/selected-only, before/after, cancel/no-op/overlap, atomic failure/retry, compact actions, busy close/cancel and zero credential/session access; disposable data only");
    }
    private static void Check(string root,string language)
    {
        AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        var settings=ApplicationSettings.Defaults with { Preferences=new() { Language=language,InterfaceFontSize=16 } }; Wait(repo.SaveApplicationSettingsAsync(settings));
        DesignSystem.Apply(language=="pl"?"graphite":"light");
        var first=new ServerProfile { Name="Database",Host="fixture.invalid",Username="fixture",Tags="prod, linux",Folder="Production",CredentialId=Guid.NewGuid() };
        var second=first with { Id=Guid.NewGuid(),Name="Windows",Protocol=Protocol.RDP,Port=3389,Tags="prod, windows" };
        var third=first with { Id=Guid.NewGuid(),Name="Lab",Tags="lab, linux" };
        var fourth=first with { Id=Guid.NewGuid(),Name="Untagged",Tags="" };
        foreach(var p in new[]{first,second,third,fourth})Wait(repo.SaveAsync(p));
        Wait(repo.SaveTagColorsAsync(new Dictionary<string,string> { ["prod"]="#C85CA7" }));
        var vm=new MainViewModel(repo); var vault=new NoVault(); var main=new MainWindow(vm,repo,vault,new(repo),new ExternalRdpClient()); main.Show();
        Until(()=>vm.AllServers.Count==4 && (bool)typeof(MainWindow).GetField("workspaceReady",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!); RedesignChecks.Frames(5);
        DesignSystem.Apply(language=="pl"?"graphite":"light");
        Task Open(Guid? initial=null)=>(Task)typeof(MainWindow).GetMethod("EditConnectionTagsAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[initial])!;
        Window Dialog() { Until(()=>main.OwnedWindows.Any()); RedesignChecks.Frames(5); return main.OwnedWindows.Single(); }
        ServerProfile Saved(Guid id)=>repo.ListAsync().GetAwaiter().GetResult().Single(p=>p.Id==id);
        try
        {
            var sidebar=Named<DockPanel>(main,"ConnectionSidebar"); var menu=sidebar.ContextMenu!; menu.Open(sidebar); RedesignChecks.Frames(3);
            var item=menu.Items.OfType<MenuItem>().Single(m=>m.Tag as string=="Edytuj tagi wielu połączeń");
            Verify(item.IsEnabled && item.Icon is DeskIcon && item.Header as string==AppText.T("Edytuj tagi wielu połączeń"),"Batch tag entry lacks availability, icon or localization"); menu.Close();
            vm.ProtocolFilter=Protocol.SSH; vm.SelectedTags=["prod"]; vm.Search="fixture"; RedesignChecks.Frames(5);
            var editing=Open(); var dialog=Dialog(); var list=Named<ListBox>(dialog,"BatchTagProfiles");
            Verify(list.ItemCount==1 && !Named<Button>(dialog,"BatchTagSave").IsEnabled,"Scope included filtered profiles or an empty plan was enabled");
            Click(dialog,"BatchTagSelectVisible"); Named<TextBox>(dialog,"BatchTagAdd").Text="backup"; Named<TextBox>(dialog,"BatchTagRemove").Text="PROD"; RedesignChecks.Frames(3);
            Verify(Named<TextBlock>(dialog,"BatchTagPreview_"+first.Id.ToString("N")).Text!.EndsWith("linux, backup") && Saved(first.Id).Tags==first.Tags,"Preview changed data or displayed another result");
            Click(dialog,"BatchTagCancel"); Wait(editing); Verify(Saved(first.Id)==first && Saved(second.Id)==second,"Cancel modified profiles");
            editing=Open(); dialog=Dialog(); Click(dialog,"BatchTagSelectVisible"); Named<TextBox>(dialog,"BatchTagAdd").Text="backup"; Named<TextBox>(dialog,"BatchTagRemove").Text="prod";
            RedesignChecks.Frames(3); Verify(Named<Button>(dialog,"BatchTagSave").IsEnabled,"A valid batch did not enable Save");
            Click(dialog,"BatchTagSave"); Wait(editing); RedesignChecks.Frames(3);
            Verify(Saved(first.Id).Tags=="linux, backup" && Saved(second.Id)==second && Saved(third.Id)==third && Saved(fourth.Id)==fourth,"Batch save changed hidden/unselected data");
            Verify(vm.SelectedTags.SequenceEqual(new[]{"prod"}) && vm.ProtocolFilter==Protocol.SSH && vm.Search=="fixture" && vm.AllServers.FirstOrDefault(p=>p.Id==first.Id)?.Tags=="linux, backup","Saving lost filters or did not reload the browser; profiles="+vm.AllServers.Count);
            menu.Open(sidebar); RedesignChecks.Frames(2); Verify(!item.IsEnabled,"An empty filtered scope still enabled the batch action"); menu.Close();
            vm.Search=""; vm.SelectedTags=[]; vm.ProtocolFilter=null; RedesignChecks.Frames(5);
            editing=Open(first.Id); dialog=Dialog();
            dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"batch-open-"+language+".png"));
            Verify(Named<CheckBox>(dialog,"BatchTagSelect_"+first.Id.ToString("N")).IsChecked==true,"Row action did not preselect its captured profile");
            Named<TextBox>(dialog,"BatchTagSearch").Text="Windows"; RedesignChecks.Frames(3); Verify(Named<ListBox>(dialog,"BatchTagProfiles").ItemCount==1,"Batch search missed the profile"); Click(dialog,"BatchTagSelectVisible");
            Named<TextBox>(dialog,"BatchTagSearch").Text=""; Named<CheckBox>(dialog,"BatchTagSelectedOnly").IsChecked=true; RedesignChecks.Frames(3);
            Verify(Named<ListBox>(dialog,"BatchTagProfiles").ItemCount==2,"Searching discarded earlier selections or selected-only scope is wrong");
            Named<TextBox>(dialog,"BatchTagAdd").Text="team"; Named<TextBox>(dialog,"BatchTagRemove").Text="TEAM"; RedesignChecks.Frames(2);
            Verify(!Named<Button>(dialog,"BatchTagSave").IsEnabled && Named<SelectableTextBlock>(dialog,"BatchTagStatus").Text==AppText.T("Ten sam tag nie może być jednocześnie dodawany i usuwany."),"Conflicting additions/removals were accepted or not localized");
            Named<TextBox>(dialog,"BatchTagRemove").Text=""; dialog.Width=620; dialog.Height=500; RedesignChecks.Frames(5);
            dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"batch-tags-"+language+".png"));
            foreach(var name in new[]{"BatchTagAdd","BatchTagRemove","BatchTagSave","BatchTagCancel"})
            {
                var control=dialog.GetVisualDescendants().OfType<Control>().FirstOrDefault(c=>c.Name==name)??throw new Exception("Missing compact batch control: "+name); var at=control.TranslatePoint(default,dialog)!.Value;
                Verify(at.X>=0 && at.Y>=0 && at.X+control.Bounds.Width<=dialog.ClientSize.Width+1 && at.Y+control.Bounds.Height<=dialog.ClientSize.Height+1,"Compact batch action escaped its window: "+name);
            }
            dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"batch-tags-"+language+".png"));
            Wait(Sql(repo,$"CREATE TRIGGER reject_tag_batch BEFORE UPDATE ON profiles WHEN NEW.id='{second.Id}' BEGIN SELECT RAISE(ABORT,'private fixture refusal'); END"));
            Click(dialog,"BatchTagSave"); var view=Named<ConnectionTagBatchView>(dialog,"ConnectionTagBatchView"); Until(()=>!view.IsSaving); RedesignChecks.Frames(3);
            Verify(dialog.IsVisible && view.IsEnabled && Saved(first.Id).Tags=="linux, backup" && Saved(second.Id).Tags==second.Tags,"Storage failure closed the dialog or partly saved the batch");
            var error=Named<SelectableTextBlock>(dialog,"BatchTagStatus").Text??"";
            Verify(error.Contains(AppText.T("Identyfikator błędu: {0}").Split('{')[0]) && !error.Contains("private fixture refusal"),"Batch error lacks a safe diagnostic ID");
            Wait(Sql(repo,"DROP TRIGGER reject_tag_batch")); Click(dialog,"BatchTagSave"); Wait(editing);
            Verify(Saved(first.Id).Tags=="linux, backup, team" && Saved(second.Id).Tags=="prod, windows, team" && Saved(third.Id)==third && Saved(fourth.Id)==fourth,"Explicit retry changed wrong profiles");
            Verify((repo.TagColorsAsync().GetAwaiter().GetResult())["prod"]=="#C85CA7","Batch editing changed shared colors");
            editing=Open(third.Id); dialog=Dialog(); Named<TextBox>(dialog,"BatchTagAdd").Text="review"; RedesignChecks.Frames(3);
            Wait(repo.SaveAsync(third with { Tags="external edit" })); Click(dialog,"BatchTagSave"); Until(()=>!Named<ConnectionTagBatchView>(dialog,"ConnectionTagBatchView").IsSaving);
            Verify(dialog.IsVisible && (Named<SelectableTextBlock>(dialog,"BatchTagStatus").Text??"").Contains(new ProfileTagConflictException().Message) && Saved(third.Id).Tags=="external edit","Stale preview overwrote external tags or lacked reopen guidance");
            Click(dialog,"BatchTagCancel"); Wait(editing); Wait(repo.SaveAsync(third));
            using var cancel=new CancellationTokenSource(); var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls=0;
            IReadOnlyList<ProfileTagChange> captured=[];
            var waiting=Dialogs.BatchConnectionTags(main,[Saved(first.Id)],vm.TagColors,first.Id,changes=> { captured=changes; calls++; return gate.Task; },cancel.Token); dialog=Dialog();
            Named<TextBox>(dialog,"BatchTagAdd").Text="waiting"; RedesignChecks.Frames(3);
            Named<TextBox>(dialog,"BatchTagAdd").Text="latest-field-value"; Click(dialog,"BatchTagSave"); var busy=Named<ConnectionTagBatchView>(dialog,"ConnectionTagBatchView");
            Verify(captured.Count==1 && captured[0].UpdatedTags.EndsWith("latest-field-value"),"Immediate Save submitted a stale operation field");
            Verify(busy.IsSaving && !busy.IsEnabled,"Saving did not disable duplicate edits"); dialog.Close(false); RedesignChecks.Frames(2); Verify(dialog.IsVisible,"Busy batch closed before the writer drained");
            Click(dialog,"BatchTagSave"); Verify(calls==1,"Repeated click started a second save"); cancel.Cancel(); gate.SetCanceled(cancel.Token); Wait(waiting);
            Verify(!waiting.Result && !dialog.IsVisible && Saved(first.Id).Tags=="linux, backup, team","Canceled writer reported success or changed data");
            Verify(vault.Calls==0 && main.OpenSessions.Count==0,"Tag editing touched credentials or protocol sessions");
            Console.WriteLine("PASS batch tags "+language+": filtered scope, preview/cancel/save, hidden selection, conflict validation, compact layout, rollback/retry and canceled writer");
        }
        finally { foreach(var window in main.OwnedWindows.ToArray())window.Close(); main.Close(); RedesignChecks.Frames(5); }
    }
}
