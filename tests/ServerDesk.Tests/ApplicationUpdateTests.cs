using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ApplicationUpdateTests
{
    private static readonly byte[] Bytes=Encoding.UTF8.GetBytes("synthetic installer — żółć");
    private static ApplicationUpdate Package(string version="0.46.0")=>new(version,$"https://github.com/daman11/ServerDesk/releases/download/v{version}/ServerDesk-{version}-win-x64-Setup.exe",Bytes.Length,Convert.ToHexStringLower(SHA256.HashData(Bytes)));
    private static object Release(string version="0.46.0",bool draft=false,bool prerelease=true,string? digest=null)=>new { draft,prerelease,tag_name="v"+version,assets=new[]{new { name=Package(version).FileName,state="uploaded",browser_download_url=Package(version).DownloadUrl,size=Bytes.Length,digest=digest??"sha256:"+Package(version).Sha256 }} };
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> send):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(send(request)); }
    }
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(value)) };
    private static string Temp()=>Path.Combine(Path.GetTempPath(),"ServerDesk-update-tests-"+Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("1.0.0")][InlineData("0.45.0")][InlineData("12.123.456")]
    public void Canonical_versions_are_accepted(string version)=>Assert.Equal(version,ApplicationUpdate.ParseVersion(version).ToString(3));
    [Theory]
    [InlineData("")][InlineData("1.0")][InlineData("v1.0.0")][InlineData("01.0.0")][InlineData("1.0.0.0")][InlineData("1.0.0-beta")][InlineData("1.0.0\n")][InlineData("2147483648.0.0")][InlineData("../1.0.0")]
    public void Ambiguous_versions_are_rejected(string version)=>Assert.Throws<InvalidDataException>(()=>ApplicationUpdate.ParseVersion(version));
    [Theory]
    [InlineData("http://github.com/daman11/ServerDesk/releases/download/v0.46.0/ServerDesk-0.46.0-win-x64-Setup.exe")]
    [InlineData("https://github.com/attacker/ServerDesk/releases/download/v0.46.0/ServerDesk-0.46.0-win-x64-Setup.exe")]
    [InlineData("https://github.com/daman11/ServerDesk/releases/download/v0.46.0/../../evil.exe")]
    [InlineData("https://github.com/daman11/ServerDesk/releases/download/v0.46.0/ServerDesk-0.46.0-win-x64-Setup.exe?x=1")]
    public void Only_exact_official_installer_is_accepted(string url)=>Assert.Throws<InvalidDataException>(()=>(Package() with { DownloadUrl=url }).Validate());
    [Fact]
    public async Task Digest_and_size_are_verified()
    {
        using var good=new MemoryStream(Bytes); await Package().VerifyAsync(good); Assert.Equal(0,good.Position);
        using var corrupt=new MemoryStream(Bytes.Select(b=>(byte)(b^1)).ToArray()); await Assert.ThrowsAsync<InvalidDataException>(()=>Package().VerifyAsync(corrupt));
        using var shortFile=new MemoryStream(Bytes[..^1]); await Assert.ThrowsAsync<InvalidDataException>(()=>Package().VerifyAsync(shortFile));
    }
    [Theory]
    [InlineData(0)][InlineData(-1)][InlineData(367001601)]
    public void Unsafe_sizes_are_rejected(long size)=>Assert.Throws<InvalidDataException>(()=>(Package() with { Size=size }).Validate());
    [Fact]
    public async Task Selects_highest_version_not_publication_order_and_ignores_drafts()
    {
        using var client=new GitHubUpdateClient(new Handler(request=>
        {
            Assert.Equal("api.github.com",request.RequestUri!.Host); Assert.Null(request.Headers.Authorization); Assert.Null(request.Headers.Referrer);
            return Json(new[]{Release("0.46.0"),Release("0.45.1"),Release("0.99.0",draft:true),Release("0.9.0")});
        }));
        Assert.Equal("0.46.0",(await client.CheckAsync(new(0,45,0)))!.Version);
        Assert.Null(await client.CheckAsync(new(0,46,0)));
    }
    [Fact]
    public async Task Stable_installation_does_not_offer_prereleases()
    {
        using var client=new GitHubUpdateClient(new Handler(_=>Json(new[]{Release("1.1.0",prerelease:true),Release("1.0.1",prerelease:false)})));
        Assert.Equal("1.0.1",(await client.CheckAsync(new(1,0,0)))!.Version);
    }
    [Fact]
    public async Task Missing_digest_and_duplicate_metadata_fail_closed()
    {
        using var missing=new GitHubUpdateClient(new Handler(_=>Json(new[]{Release(digest:"")})));
        await Assert.ThrowsAsync<InvalidDataException>(()=>missing.CheckAsync(new(0,45,0)));
        using var duplicate=new GitHubUpdateClient(new Handler(_=>new(HttpStatusCode.OK) { Content=new StringContent("[{\"draft\":true,\"draft\":false}]") }));
        await Assert.ThrowsAsync<InvalidDataException>(()=>duplicate.CheckAsync(new(0,45,0)));
    }
    [Theory]
    [InlineData("http://release-assets.githubusercontent.com/file")][InlineData("https://evil.example/file")][InlineData("file:///tmp/file")][InlineData("https://user@github.com/file")][InlineData("https://github.com:444/file")]
    public async Task Unsafe_redirect_is_rejected_before_second_request(string redirect)
    {
        var calls=0; var root=Temp();
        using var client=new GitHubUpdateClient(new Handler(_=> { calls++; return new(HttpStatusCode.Redirect) { Headers={Location=new Uri(redirect)} }; }));
        try { await Assert.ThrowsAsync<InvalidDataException>(()=>client.DownloadAsync(Package(),root)); Assert.Equal(1,calls); Assert.Empty(Directory.GetFiles(root)); }
        finally { Directory.Delete(root); }
    }
    [Fact]
    public async Task Successful_download_follows_trusted_redirect_and_is_verified()
    {
        var calls=0; var root=Temp();
        using var client=new GitHubUpdateClient(new Handler(_=>++calls==1?new(HttpStatusCode.Redirect) { Headers={Location=new Uri("https://release-assets.githubusercontent.com/file?token=synthetic")} }:new(HttpStatusCode.OK) { Content=new ByteArrayContent(Bytes) }));
        try { var path=await client.DownloadAsync(Package(),root); Assert.Equal(Bytes,await File.ReadAllBytesAsync(path)); Assert.Equal(2,calls); Assert.Single(Directory.GetFiles(root)); }
        finally { Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Corrupt_or_truncated_download_never_becomes_an_installer(bool corrupt)
    {
        var root=Temp(); var content=corrupt?Bytes.Select(b=>(byte)(b^1)).ToArray():Bytes[..^1];
        using var client=new GitHubUpdateClient(new Handler(_=>new(HttpStatusCode.OK) { Content=new ByteArrayContent(content) }));
        try { await Assert.ThrowsAsync<InvalidDataException>(()=>client.DownloadAsync(Package(),root)); Assert.Empty(Directory.GetFiles(root)); }
        finally { Directory.Delete(root); }
    }
    [Fact]
    public async Task Existing_partial_and_unrelated_files_are_never_deleted()
    {
        var root=Temp(); Directory.CreateDirectory(root); var partial=Path.Combine(root,Package().FileName+".part"); await File.WriteAllTextAsync(partial,"keep");
        using var client=new GitHubUpdateClient(new Handler(_=>new(HttpStatusCode.OK) { Content=new ByteArrayContent(Bytes) }));
        try { await Assert.ThrowsAsync<IOException>(()=>client.DownloadAsync(Package(),root)); Assert.Equal("keep",await File.ReadAllTextAsync(partial)); }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task Cancellation_does_not_leave_a_partial_installer()
    {
        var root=Temp(); using var ct=new CancellationTokenSource(); ct.Cancel();
        using var client=new GitHubUpdateClient(new Handler(_=>throw new InvalidOperationException()));
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.DownloadAsync(Package(),root,ct:ct.Token)); Assert.Empty(Directory.GetFiles(root)); }
        finally { Directory.Delete(root); }
    }
    [Fact]
    public void Installer_arguments_support_spaces_and_unicode_without_shell()
    {
        var root=Temp(); var install=Path.Combine(root,"aplikacja żółć"); var cache=Path.Combine(root,"cache"); Directory.CreateDirectory(install); Directory.CreateDirectory(cache);
        foreach(var name in new[]{"ServerDesk.App.exe","Uninstall.exe","install-state.ini"})File.WriteAllText(Path.Combine(install,name),"");
        File.WriteAllText(Path.Combine(install,"install-state.ini"),"[Setup]\nNoRegister=0\n");
        using var process=Process.GetCurrentProcess(); var plan=new UpdateInstallRequest(Package(),install,process.Id,process.StartTime.ToUniversalTime().Ticks,"0.45.0");
        try
        {
            var start=plan.InstallerStartInfo(cache); Assert.False(start.UseShellExecute); Assert.Equal("/S /D="+install,start.Arguments); Assert.Empty(start.ArgumentList);
            Assert.False(plan.WaitForParent(TimeSpan.Zero));
            Assert.True((plan with { ParentStarted=process.StartTime.ToUniversalTime().Ticks-1 }).WaitForParent(TimeSpan.Zero));
            Assert.Throws<InvalidDataException>(()=>(plan with { CurrentVersion="0.46.0" }).Validate(cache));
            Assert.Throws<InvalidDataException>(()=>plan.Validate(install));
            File.WriteAllText(Path.Combine(install,"install-state.ini"),"[Setup]\nNoRegister=1\n");
            Assert.Equal("/NOREGISTER /S /D="+install,plan.InstallerStartInfo(cache).Arguments);
            File.Delete(Path.Combine(install,"install-state.ini")); Assert.Throws<InvalidDataException>(()=>plan.Validate(cache));
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public void Startup_network_check_defaults_on_but_an_explicit_opt_out_survives()
    {
        Assert.True(new ApplicationPreferences().CheckUpdatesOnStartup);
        Assert.True(JsonSerializer.Deserialize<ApplicationPreferences>("{}")!.CheckUpdatesOnStartup);
        Assert.False(JsonSerializer.Deserialize<ApplicationPreferences>("{\"CheckUpdatesOnStartup\":false}")!.CheckUpdatesOnStartup);
    }

    [Fact]
    public async Task Skipped_update_survives_reopening_and_settings_save_without_changing_profiles()
    {
        var root=Temp();
        try
        {
            var paths=new AppPaths(root); var repo=new ProfileRepository(paths); await repo.InitializeAsync();
            var profile=new ServerProfile { Name="Update preservation",Host="fixture.invalid",Username="fixture" };
            await repo.SaveAsync(profile);
            await repo.SetSkippedApplicationUpdateAsync("0.99.0");
            await repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults);
            var reopened=new ProfileRepository(paths);
            Assert.Equal("0.99.0",await reopened.SettingAsync("skippedUpdateVersion"));
            Assert.Equal(profile.Id,Assert.Single(await reopened.ListAsync()).Id);
            await Assert.ThrowsAsync<InvalidDataException>(()=>reopened.SetSkippedApplicationUpdateAsync("../invalid"));
            Assert.Equal("0.99.0",await reopened.SettingAsync("skippedUpdateVersion"));
        }
        finally { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
    }
}
