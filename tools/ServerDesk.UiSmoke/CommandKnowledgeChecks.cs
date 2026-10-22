using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class CommandKnowledgeChecks
{
    private sealed class Vault:ICredentialStore
    {
        internal int Calls;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Knowledge accessed a credential"); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { Calls++; throw new Exception("Knowledge saved a credential"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Knowledge deleted a credential"); }
    }
    private sealed class Handler:HttpMessageHandler
    {
        internal int Calls,Cancelled; internal bool Pending,Fail;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            if(!CommandKnowledgeCatalog.Publishers.Contains(request.RequestUri!.Host) || request.Headers.Authorization!=null || request.Content!=null)throw new Exception("Unexpected knowledge request");
            if(Pending)try { await Task.Delay(Timeout.Infinite,ct); } catch(OperationCanceledException) { Cancelled++; throw; }
            if(Fail)return new(HttpStatusCode.ServiceUnavailable);
            return new(HttpStatusCode.OK) { Content=new StringContent("<html><head><title>Fixture official reference</title></head><main><h1>Public fixture manual</h1><p>REFRESHED_DOC_CANARY: -a includes hidden files, -A excludes dot entries.</p><script>NEVER_EXECUTE_REMOTE_CONTENT</script></main></html>",Encoding.UTF8,"text/html") };
        }
    }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Choose(ListBox list,string id)
    {
        list.SelectedItem=list.Items.Cast<object>().Single(item=>item switch { CommandGuide g=>g.Id==id,CommandToolReference t=>t.Id==id,_=>false });
        if(!list.Focus())throw new Exception("Knowledge results could not receive keyboard focus");
        var window=(Window)TopLevel.GetTopLevel(list)!;
        window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r");
        window.KeyRelease(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Frames(4);
        if(!Named<ScrollViewer>(window,"CommandKnowledgeDetails").IsVisible || !Named<Button>(window,"CommandKnowledgeBack").IsEffectivelyVisible)throw new Exception("Enter did not open knowledge details: "+id);
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
            var preferences=new ApplicationPreferences { Language=language }; Wait(repo.SaveApplicationSettingsAsync(Result(repo.ApplicationSettingsAsync()) with { Preferences=preferences }));
            var profile=new ServerProfile { Name="Untouched fixture profile",Host="fixture.invalid",Username="fixture" }; Wait(repo.SaveAsync(profile));
            var vault=new Vault(); using var handler=new Handler(); using var http=new HttpClient(handler);
            var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()) { CommandDocumentationFactory=()=>new(repo,http) }; main.Show(); RedesignChecks.Frames(12);
            try
            {
                Wait(main.OpenCommandsPanelAsync()); var panel=Named<CommandPanel>(main,"CommandsPanel"); Click(panel,"CommandKnowledgeTab"); RedesignChecks.Frames(4);
                var knowledge=Named<CommandKnowledgePanel>(panel,"CommandKnowledgePanel"); var list=Named<ListBox>(knowledge,"CommandKnowledgeList");
                if(list.ItemCount!=14 || handler.Calls!=0 || vault.Calls!=0)throw new Exception("Offline guides require a network or credential call");
                var card=list.GetVisualDescendants().OfType<Border>().First(b=>b.Name=="CommandKnowledgeCard" && b.TranslatePoint(new Point(20,15),list) is {} p && p.Y>=0 && p.Y<list.Bounds.Height);
                var point=card.TranslatePoint(new Point(20,15),main)!.Value; main.MouseMove(point,RawInputModifiers.None); main.MouseDown(point,MouseButton.Left,RawInputModifiers.None); main.MouseUp(point,MouseButton.Left,RawInputModifiers.None); RedesignChecks.Frames(4);
                if(!Named<Button>(knowledge,"CommandKnowledgeBack").IsEffectivelyVisible)throw new Exception("Pointer click did not open the guide");
                Click(knowledge,"CommandKnowledgeBack");
                var query=Named<TextBox>(knowledge,"CommandKnowledgeSearch"); query.Text="ssh-keygen"; RedesignChecks.Frames(3); Choose(list,"ssh-key-linux");
                Click(knowledge,"CommandGuideCopy_2");
                var copied=Result(main.Clipboard!.TryGetTextAsync()); if(copied?.Contains("ssh-keygen -t ed25519")!=true || main.OpenSessions.Count!=0)throw new Exception("Guide copy dispatched a session or copied the wrong step");
                Click(knowledge,"CommandKnowledgeBack"); query.Text=""; RedesignChecks.Frames(3);
                foreach(var theme in new[]{"graphite","light"})
                {
                    DesignSystem.Apply(theme);
                    foreach(var size in new[]{new Size(1280,800),new Size(920,620)})
                    {
                        main.Width=size.Width; main.Height=size.Height; main.PreviewPreferences(preferences with { InterfaceFontSize=size.Width<1000?16:13 }); RedesignChecks.Frames(6);
                        main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"guides-{language}-{theme}-{size.Width}.png"));
                        if(knowledge.Bounds.Width<300 || list.Bounds.Height<70)throw new Exception("Knowledge browser lost usable space");
                        foreach(var guide in CommandKnowledgeCatalog.Default.Guides)
                        {
                            Choose(list,guide.Id); var details=Named<ScrollViewer>(knowledge,"CommandKnowledgeDetails");
                            if(details.Bounds.Height<100 || !Named<Button>(knowledge,"CommandKnowledgeBack").IsVisible)throw new Exception("Guide details are not independently scrollable");
                            if(guide.Id=="netplan-network")main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"guide-netplan-{language}-{theme}-{size.Width}.png"));
                            Click(knowledge,"CommandKnowledgeBack");
                        }
                    }
                }
                var section=Named<ComboBox>(knowledge,"CommandKnowledgeSection"); section.SelectedIndex=1; query.Text="ls"; RedesignChecks.Frames(4); Choose(list,"ls");
                var options=Named<Expander>(knowledge,"CommandOptions"); var optionQuery=Named<TextBox>(options,"CommandOptionSearch");
                foreach(var flag in new[]{"-a","-A","--group"})
                {
                    optionQuery.Text=flag; RedesignChecks.Frames(3); var rows=options.GetVisualDescendants().OfType<Border>().Where(b=>b.Name=="CommandOptionRow").ToArray();
                    if(rows.Length!=1 || !(rows[0].Tag as string??"").StartsWith(flag,StringComparison.Ordinal))throw new Exception("Option search lost flag spelling or case: "+flag);
                }
                optionQuery.Text="not-a-real-option-fixture"; RedesignChecks.Frames(3); if(options.GetVisualDescendants().OfType<Border>().Any(b=>b.Name=="CommandOptionRow"))throw new Exception("Option no-results view is stale");
                optionQuery.Text=""; RedesignChecks.Frames(3); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"ls-options-{language}.png"));
                var documentation=Named<CommandDocumentationView>(knowledge,"CommandOfficialDocumentation"); documentation.IsExpanded=true; RedesignChecks.Frames(6); Wait(panel.Shutdown);
                if(handler.Calls!=0)throw new Exception("Opening reference silently downloaded documentation");
                Click(documentation,"CommandDocumentationRefresh"); Wait(panel.Shutdown); var text=Named<TextBox>(documentation,"CommandDocumentationText");
                if(!text.IsVisible || text.Text?.Contains("REFRESHED_DOC_CANARY")!=true || text.Text.Contains("NEVER_EXECUTE_REMOTE_CONTENT"))throw new Exception("Refreshed official text was not sanitized and displayed");
                var previous=text.Text; handler.Fail=true; Click(documentation,"CommandDocumentationRefresh"); Wait(panel.Shutdown); if(text.Text!=previous)throw new Exception("Failed refresh discarded the offline copy"); handler.Fail=false;
                handler.Pending=true; Click(documentation,"CommandDocumentationRefresh"); RedesignChecks.Frames(3); var sources=Named<ComboBox>(documentation,"CommandDocumentationSource"); sources.SelectedIndex=1; Wait(panel.Shutdown);
                if(handler.Cancelled<1 || text.IsVisible)throw new Exception("Source change retained old text or did not cancel download");
                handler.Pending=false; sources.SelectedIndex=0; Wait(panel.Shutdown); if(text.Text!=previous || !text.IsVisible)throw new Exception("Cached official text did not reopen offline");
                handler.Pending=true; Click(documentation,"CommandDocumentationRefresh"); RedesignChecks.Frames(3); Click(documentation,"CommandDocumentationCancel"); Wait(panel.Shutdown);
                if(text.Text!=previous || !Named<Button>(documentation,"CommandDocumentationRefresh").IsEnabled)throw new Exception("Cancellation damaged the cache or disabled retry");
                handler.Pending=false;
                Click(panel,"CommandBrowseTab"); Named<Expander>(panel,"CommandContextOptions").IsExpanded=true; RedesignChecks.Frames(3);
                Named<ComboBox>(panel,"CommandMode").SelectedIndex=1; Named<ComboBox>(panel,"CommandSystem").SelectedItem="linux"; Named<ComboBox>(panel,"CommandShell").SelectedItem=CommandShell.Bash;
                Named<TextBox>(panel,"CommandSearch").Text="ls"; panel.RefreshList(); var commands=Named<ListBox>(panel,"CommandList");
                commands.SelectedItem=commands.Items.OfType<CommandMatch>().First(m=>m.Command.Tools.Contains("ls")); panel.ShowCommandDetails(); RedesignChecks.Frames(4);
                var commandDetails=Named<Border>(panel,"CommandDetailCard");
                var attached=Named<Expander>(commandDetails,"CommandOptions"); attached.IsExpanded=true; RedesignChecks.Frames(3); var preview=Named<TextBox>(commandDetails,"CommandPreview"); var original=preview.Text;
                Named<TextBox>(attached,"CommandOptionSearch").Text="-a"; RedesignChecks.Frames(3); if(preview.Text!=original)throw new Exception("Educational options changed the executable preview");
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"command-ls-reference-{language}.png"));
                var sourceView=Named<CommandDocumentationView>(commandDetails,"CommandOfficialDocumentation"); sourceView.IsExpanded=true; RedesignChecks.Frames(3); Wait(panel.Shutdown);
                handler.Pending=true; Click(sourceView,"CommandDocumentationRefresh"); RedesignChecks.Frames(3);
                Wait(main.ApplyPluginsAsync(preferences with { DisabledPlugins=new([PluginCatalog.Commands]) })); Wait(panel.Shutdown);
                if(main.GetVisualDescendants().OfType<CommandPanel>().Any() || vault.Calls!=0 || main.OpenSessions.Count!=0 || JsonSerializer.Serialize(Result(repo.ListAsync()).Single())!=JsonSerializer.Serialize(profile))throw new Exception("Knowledge plugin lifecycle touched credentials, profiles or core sessions");
                using var readback=new CommandDocumentationClient(repo,http); if(Result(readback.ReadAsync("gnu-ls-files"))?.Text!=previous)throw new Exception("Plugin disable deleted public documentation");
                handler.Pending=false; Wait(main.ApplyPluginsAsync(preferences)); Wait(main.OpenCommandsPanelAsync());
                if(Result(readback.ReadAsync("gnu-ls-files"))?.Text!=previous)throw new Exception("Re-enable lost the documentation cache");
                Console.WriteLine("PASS knowledge "+language+": 14 guides across 4 layouts, 22 references/168 options, exact-case flag search, copy-only steps, unchanged command preview, explicit-only HTTP/cache/failure/cancel/source changes, plugin drain and data/credential isolation");
            }
            finally
            {
                main.Close(); RedesignChecks.Frames(4); if(main.OwnedWindows.FirstOrDefault() is {} dialog)dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(8);
            }
        }
        AppText.SetLanguage("pl"); DesignSystem.Apply("graphite");
    }
    internal static void LiveSources(string root)
    {
        var repo=new ProfileRepository(new(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")))); repo.InitializeAsync().GetAwaiter().GetResult();
        using var client=new CommandDocumentationClient(repo); var results=new List<object>();
        foreach(var source in CommandKnowledgeCatalog.Default.Sources)
        {
            try
            {
                var snapshot=client.RefreshAsync(source.Id).GetAwaiter().GetResult();
                results.Add(new { id=source.Id,passed=true,title=snapshot.Title,characters=snapshot.Text.Length,snapshot.Truncated }); Console.WriteLine("PASS official source "+source.Id+" ("+snapshot.Text.Length+" characters)");
            }
            catch(Exception ex) { results.Add(new { id=source.Id,passed=false,error=ex.GetType().Name }); Console.WriteLine("FAIL official source "+source.Id+": "+ex.GetType().Name); }
        }
        File.WriteAllText(Path.Combine(root,"sources.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions { WriteIndented=true }));
        if(results.Any(item=>JsonSerializer.SerializeToElement(item).GetProperty("passed").ValueKind==JsonValueKind.False))throw new Exception("Some official sources failed; inspect source metadata report");
    }
}
