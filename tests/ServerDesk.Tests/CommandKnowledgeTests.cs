using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class CommandKnowledgeTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-knowledge-"+Guid.NewGuid().ToString("N"));
    private async Task<ProfileRepository> Repository()
    { var repository=new ProfileRepository(new(root)); await repository.InitializeAsync(); return repository; }
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) { Calls++; return send(request,ct); }
    }
    private static HttpResponseMessage Page(string content="<html><head><title>Official ls help</title></head><body><main><h1>List directory contents</h1><p>-a includes dotfiles; -A excludes dot and dot-dot.</p></main></body></html>")
    { var response=new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(content,Encoding.UTF8,"text/html") }; response.Headers.ETag=new("\"fixture-v1\""); return response; }
    [Fact] public void Offline_catalog_has_bilingual_reviewed_guides_options_and_only_public_official_sources()
    {
        var catalog=CommandKnowledgeCatalog.Default;
        Assert.Equal(14,catalog.Guides.Count); Assert.Equal(22,catalog.Tools.Count); Assert.Equal(168,catalog.Tools.Sum(t=>t.Options.Length)); Assert.Equal(31,catalog.Sources.Count);
        Assert.All(catalog.Guides,g=> { Assert.NotEmpty(g.TitleEn); Assert.NotEmpty(g.SummaryEn); Assert.All(g.Steps,s=> { Assert.NotEmpty(s.TitleEn); Assert.NotEmpty(s.TextEn); }); Assert.All(g.Sources,id=>Assert.NotNull(catalog.Source(id))); });
        Assert.All(catalog.Sources,s=> { var uri=new Uri(s.Url); Assert.Equal("https",uri.Scheme); Assert.Contains(uri.Host,CommandKnowledgeCatalog.Publishers); Assert.Equal("",uri.UserInfo); });
        Assert.Contains(catalog.Guides,g=>g.Id=="netplan-network"&&g.Risk==CommandRisk.Dangerous);
        var key=catalog.Guides.Single(g=>g.Id=="ssh-key-linux");
        Assert.Contains(key.Steps,s=>s.Code.Contains("ssh-keygen -t ed25519")); Assert.DoesNotContain(key.Steps,s=>s.Code.Contains(" -N ")||s.Code.Contains("cat ~/.ssh/id_ed25519"));
    }
    [Fact] public void Guide_search_handles_Polish_terms_platforms_commands_and_configuration_paths()
    {
        var catalog=CommandKnowledgeCatalog.Default;
        Assert.Contains(catalog.FindGuides(CommandEnvironment.Manual("linux"),"sieć"),g=>g.Id=="linux-network-owner");
        Assert.Contains(catalog.FindGuides(CommandEnvironment.Manual("general"),"/etc/netplan"),g=>g.Id=="netplan-network");
        Assert.Contains(catalog.FindGuides(CommandEnvironment.Manual("windows"),"ssh-keygen"),g=>g.Id=="ssh-key-windows");
        Assert.DoesNotContain(catalog.FindGuides(CommandEnvironment.Manual("windows")),g=>g.Platform==CommandPlatform.Linux);
        Assert.DoesNotContain(catalog.FindGuides(CommandEnvironment.Manual("linux")),g=>g.Platform==CommandPlatform.Windows);
        Assert.Empty(catalog.FindGuides(CommandEnvironment.Manual("general"),"nonexistent-fixture-guide"));
    }
    [Fact] public void Option_references_distinguish_GNU_ls_from_PowerShell_and_do_not_attach_to_imported_templates()
    {
        var catalog=CommandKnowledgeCatalog.Default;
        Assert.Contains(catalog.FindTools(CommandEnvironment.Manual("linux"),"ls"),t=>t.Id=="ls");
        Assert.Equal("ls",catalog.FindTools(CommandEnvironment.Manual("general"),"LS").First().Id);
        Assert.DoesNotContain(catalog.FindTools(CommandEnvironment.Manual("windows"),"ls"),t=>t.Id=="ls");
        Assert.Contains(catalog.FindTools(CommandEnvironment.Manual("windows"),"ls"),t=>t.Id=="get-childitem");
        var ls=CommandCatalog.Default.BuiltIns.First(c=>c.Tools.Contains("ls"));
        var reference=Assert.Single(catalog.ForCommand(ls),t=>t.Id=="ls"); Assert.Contains(reference.Options,o=>o.Syntax=="-a, --all"); Assert.Contains(reference.Options,o=>o.Syntax=="-A, --almost-all");
        Assert.Empty(catalog.ForCommand(ls with { Origin=CommandOrigin.User,Id="user.fixture" }));
        Assert.Throws<ArgumentException>(()=>catalog.Source("https://127.0.0.1/private"));
    }
    [Fact] public void Html_extraction_removes_active_elements_navigation_and_controls_but_preserves_readable_lines()
    {
        var result=CommandDocumentationClient.Extract("<html><head><title>Manual &amp; options</title></head><nav>NAV_CANARY</nav><main><h1>ls reference</h1><p>-a &amp; -A</p><script>SCRIPT_CANARY</script><style>STYLE_CANARY</style><pre>line one\nline two</pre><p>safe\u202Etext\u0000</p></main><footer>FOOTER_CANARY</footer></html>",true);
        Assert.Equal("Manual & options",result.Title); Assert.Contains("-a & -A",result.Text); Assert.Contains("line one\nline two",result.Text); Assert.Contains("safetext",result.Text);
        foreach(var forbidden in new[]{"SCRIPT_CANARY","NAV_CANARY","STYLE_CANARY","FOOTER_CANARY"})Assert.DoesNotContain(forbidden,result.Text);
        Assert.DoesNotContain(result.Text,c=>c is '\u202E' or '\0');
        Assert.False(result.Truncated);
        Assert.Contains("<PATH>",CommandDocumentationClient.Extract("use <PATH> as an argument\nsecond line",false).Text);
    }
    [Fact] public void Large_and_malformed_html_is_bounded_without_splitting_Unicode_at_the_excerpt_boundary()
    {
        var value="<main>"+new string('x',CommandDocumentationClient.MaximumTextCharacters-1)+"😀"+new string('y',2000)+"</main>";
        var result=CommandDocumentationClient.Extract(value,true);
        Assert.True(result.Truncated); Assert.True(result.Text.Length<=CommandDocumentationClient.MaximumTextCharacters); Assert.False(char.IsHighSurrogate(result.Text[^1]));
        var malformed=CommandDocumentationClient.Extract("<main>"+new string('<',150000),true); Assert.True(malformed.Text.Length<=CommandDocumentationClient.MaximumTextCharacters);
    }
    [Fact] public async Task Explicit_refresh_uses_fixed_public_URL_and_conditional_cache_without_sending_profile_data()
    {
        var repo=await Repository(); var calls=0;
        using var handler=new Handler((request,_)=> {
            Assert.Equal(CommandKnowledgeCatalog.Default.Source("gnu-ls-files").Url,request.RequestUri!.AbsoluteUri); Assert.Null(request.Content); Assert.Null(request.Headers.Authorization); Assert.Null(request.Headers.Referrer); Assert.False(request.Headers.Contains("Cookie"));
            if(++calls==1)return Task.FromResult(Page()); Assert.Equal("\"fixture-v1\"",Assert.Single(request.Headers.IfNoneMatch).Tag); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        });
        using var http=new HttpClient(handler); using var client=new CommandDocumentationClient(repo,http);
        Assert.Null(await client.ReadAsync("gnu-ls-files")); Assert.Equal(0,handler.Calls);
        var first=await client.RefreshAsync("gnu-ls-files"); var second=await client.RefreshAsync("gnu-ls-files");
        Assert.Equal(first.Text,second.Text); Assert.Equal(first.ChangedAt,second.ChangedAt); Assert.True(second.CheckedAt>=first.CheckedAt);
        Assert.Equal(second,await new CommandDocumentationClient(repo,http).ReadAsync("gnu-ls-files"));
        Assert.DoesNotContain("SCRIPT_CANARY",second.Text); Assert.Equal(2,handler.Calls);
        await Assert.ThrowsAsync<ArgumentException>(()=>client.RefreshAsync("http://127.0.0.1/secret")); Assert.Equal(2,handler.Calls);
    }
    [Theory]
    [InlineData("http://www.gnu.org/manual")]
    [InlineData("https://evil.example/manual")]
    [InlineData("https://127.0.0.1/manual")]
    [InlineData("https://www.gnu.org:444/manual")]
    [InlineData("https://user:password@www.gnu.org/manual")]
    public async Task Redirects_outside_the_same_official_HTTPS_origin_are_rejected_before_a_second_request(string redirect)
    {
        var repo=await Repository();
        using var handler=new Handler((_,_)=> { var response=new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location=new(redirect); return Task.FromResult(response); });
        using var http=new HttpClient(handler); using var client=new CommandDocumentationClient(repo,http);
        await Assert.ThrowsAsync<InvalidDataException>(()=>client.RefreshAsync("gnu-ls-files")); Assert.Equal(1,handler.Calls); Assert.Null(await client.ReadAsync("gnu-ls-files"));
    }
    [Fact] public async Task Same_origin_redirects_work_and_redirect_loops_are_bounded()
    {
        var repo=await Repository(); var looping=false;
        using var handler=new Handler((request,_)=> {
            if(looping || request.RequestUri!.AbsolutePath!="/public-manual") { var response=new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location=new("/public-manual",UriKind.Relative); return Task.FromResult(response); }
            return Task.FromResult(Page());
        });
        using var http=new HttpClient(handler); using var client=new CommandDocumentationClient(repo,http);
        var snapshot=await client.RefreshAsync("gnu-ls-files"); Assert.Equal(2,handler.Calls); var before=handler.Calls; looping=true;
        await Assert.ThrowsAsync<InvalidDataException>(()=>client.RefreshAsync("gnu-ls-files")); Assert.Equal(4,handler.Calls-before); Assert.Equal(snapshot,await client.ReadAsync("gnu-ls-files"));
    }
    [Fact] public async Task Failure_cancel_and_timeout_preserve_the_previous_offline_copy()
    {
        var repo=await Repository(); var phase=0;
        using var handler=new Handler(async (_,ct)=> {
            if(phase==0)return Page();
            if(phase==1)return new(HttpStatusCode.ServiceUnavailable);
            if(phase==2)throw new OperationCanceledException();
            await Task.Delay(Timeout.Infinite,ct); return Page();
        });
        using var http=new HttpClient(handler); using var client=new CommandDocumentationClient(repo,http); var previous=await client.RefreshAsync("gnu-ls-files");
        phase=1; await Assert.ThrowsAsync<HttpRequestException>(()=>client.RefreshAsync("gnu-ls-files")); Assert.Equal(previous,await client.ReadAsync("gnu-ls-files"));
        phase=2; await Assert.ThrowsAsync<TimeoutException>(()=>client.RefreshAsync("gnu-ls-files")); Assert.Equal(previous,await client.ReadAsync("gnu-ls-files"));
        phase=3; using var cancel=new CancellationTokenSource(100); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.RefreshAsync("gnu-ls-files",cancel.Token)); Assert.Equal(previous,await client.ReadAsync("gnu-ls-files"));
    }
    [Theory][InlineData("application/json")][InlineData("application/octet-stream")]
    public async Task Non_document_content_cannot_replace_a_cached_manual(string media)
    {
        var repo=await Repository(); var bad=false;
        using var handler=new Handler((_,_)=> { var response=Page(); if(bad)response.Content.Headers.ContentType=new(media); return Task.FromResult(response); });
        using var http=new HttpClient(handler); using var client=new CommandDocumentationClient(repo,http); var previous=await client.RefreshAsync("gnu-ls-files"); bad=true;
        await Assert.ThrowsAsync<InvalidDataException>(()=>client.RefreshAsync("gnu-ls-files")); Assert.Equal(previous,await client.ReadAsync("gnu-ls-files"));
    }
    private sealed class UnknownLength(byte[] bytes):HttpContent
    {
        protected override bool TryComputeLength(out long length) { length=0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync()=>Task.FromResult<Stream>(new MemoryStream(bytes,false));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task Declared_and_streamed_oversized_documents_are_rejected(bool declared)
    {
        var repo=await Repository();
        using var handler=new Handler((_,_)=> {
            var content=declared?(HttpContent)new StringContent("tiny"):new UnknownLength(new byte[CommandDocumentationClient.MaximumResponseBytes+1]);
            content.Headers.ContentType=new("text/html"); if(declared)content.Headers.ContentLength=CommandDocumentationClient.MaximumResponseBytes+1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=content });
        });
        using var http=new HttpClient(handler); using var client=new CommandDocumentationClient(repo,http);
        await Assert.ThrowsAsync<InvalidDataException>(()=>client.RefreshAsync("gnu-ls-files")); Assert.Null(await client.ReadAsync("gnu-ls-files"));
    }
    [Fact] public async Task Corrupt_cache_cannot_change_the_source_and_explicit_refresh_repairs_it()
    {
        var repo=await Repository(); using var handler=new Handler((_,_)=>Task.FromResult(Page())); using var http=new HttpClient(handler); using var client=new CommandDocumentationClient(repo,http);
        var snapshot=await client.RefreshAsync("gnu-ls-files");
        await repo.SetSettingAsync("commands.documentation.v1.gnu-ls-files",JsonSerializer.Serialize(snapshot with { Url="https://evil.example/private" },CommandValidation.Json));
        await Assert.ThrowsAsync<InvalidDataException>(()=>client.ReadAsync("gnu-ls-files")); var repaired=await client.RefreshAsync("gnu-ls-files"); Assert.Equal(snapshot.Url,repaired.Url);
        await repo.SetSettingAsync("commands.documentation.v1.gnu-ls-files",JsonSerializer.Serialize(snapshot with { Text="tampered fixture text" },CommandValidation.Json));
        await Assert.ThrowsAsync<InvalidDataException>(()=>client.ReadAsync("gnu-ls-files"));
    }
    [Fact] public async Task Factory_reset_clears_public_document_cache_but_not_the_embedded_guides()
    {
        var repo=await Repository(); using var handler=new Handler((_,_)=>Task.FromResult(Page())); using var http=new HttpClient(handler); using var client=new CommandDocumentationClient(repo,http);
        await client.RefreshAsync("gnu-ls-files"); await repo.ScheduleFactorySettingsResetAsync(); Assert.True(await repo.ApplyPendingFactorySettingsResetAsync());
        Assert.Null(await client.ReadAsync("gnu-ls-files")); Assert.Equal(14,CommandKnowledgeCatalog.Default.Guides.Count);
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
