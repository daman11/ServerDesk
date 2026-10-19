using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Text.Json;
using Xunit;

namespace ServerDesk.Tests;
public sealed class RdpTests
{
    private static ServerProfile Profile()=>new() { Name="RDP test",Protocol=Protocol.RDP,Host="2001:db8::1",Port=3390,Username="ACME\\admin" };
    [Theory][InlineData(134,"Logowanie")][InlineData(135,"zablokowane")][InlineData(158,"dostępu")][InlineData(143,"TLS/NLA")][InlineData(148,"Hasło wygasło")]
    public void FreeRdpFailuresExplainTheActualProblem(int code,string hint)
    { var result=RdpExitStatus.FromClient(code,true); Assert.True(result.IsError); Assert.Contains(hint,result.Message); }
    [Fact] public void ClosedClientDoesNotClaimAuthenticatedDesktop()
    { var result=RdpExitStatus.FromClient(0,false); Assert.False(result.IsError); Assert.Contains("zamknięty",result.Message); Assert.DoesNotContain("zalogowano",result.Message); }
    [Theory][InlineData("user","","user","")][InlineData("ACME\\user","","user","ACME")][InlineData("ACME\\user","acme","user","ACME")][InlineData("user@example.test","","user@example.test","")]
    public void DomainIdentitiesAreNormalized(string user,string domain,string expectedUser,string expectedDomain)=>Assert.Equal(new RdpIdentity(expectedUser,expectedDomain),RdpIdentity.Parse(user,domain));
    [Theory][InlineData("ACME\\user","OTHER")][InlineData("A\\B\\C","")][InlineData("\\user","")][InlineData("user\n","")]
    public void InvalidIdentitiesAreRejected(string user,string domain)=>Assert.Throws<ArgumentException>(()=>RdpIdentity.Parse(user,domain));
    [Fact] public void WindowsConfigurationKeepsCertificateCheckingNlaAndNoDriveSharing()
    {
        var lines=RdpConfiguration.WindowsLines(Profile(),new() { Width=1920,Height=1080,Clipboard=false });
        Assert.Contains("full address:s:[2001:db8::1]:3390",lines); Assert.Contains("username:s:ACME\\admin",lines); Assert.Contains("desktopwidth:i:1920",lines); Assert.Contains("desktopheight:i:1080",lines); Assert.Contains("enablecredsspsupport:i:1",lines); Assert.Contains("authentication level:i:2",lines); Assert.Contains("redirectclipboard:i:0",lines); Assert.Contains("drivestoredirect:s:",lines);
    }
    [Fact] public void LinuxArgumentsEnableNlaWithoutCertificateBypassOrPassword()
    {
        var args=RdpConfiguration.FreeRdpArguments(Profile(),new() { Width=1920,Height=1080,Fullscreen=true,Clipboard=false });
        Assert.Contains("/v:[2001:db8::1]:3390",args); Assert.Contains("/u:admin",args); Assert.Contains("/d:ACME",args); Assert.Contains("/sec:nla",args); Assert.Contains("/size:1920x1080",args); Assert.Contains("-clipboard",args); Assert.Contains("/f",args);
        Assert.DoesNotContain(args,x=>x.StartsWith("/p:")||x.StartsWith("/cert:")||x.StartsWith("/from-stdin")||x.StartsWith("/drive:"));
    }
    [Theory][InlineData(0,900)][InlineData(8000,900)][InlineData(1600,0)][InlineData(1600,5000)]
    public void InvalidResolutionsAreRejected(int width,int height)=>Assert.Throws<ArgumentException>(()=>new RdpOptions { Width=width,Height=height }.Validate());
    [Fact] public void OldProfilesGetSafeRdpDefaults()
    { var profile=JsonSerializer.Deserialize<ServerProfile>("{\"Name\":\"old\",\"Host\":\"localhost\",\"Protocol\":1,\"Port\":3389}")!; profile.Validate(); Assert.Equal(new RdpOptions(),profile.Rdp); }
    [Fact] public void NewAndUnspecifiedRdpProfilesUseTabsWhileExplicitExternalChoiceSurvives()
    {
        Assert.Equal(RdpDisplayMode.Embedded,new RdpOptions().DisplayMode);
        Assert.Equal(RdpDisplayMode.Embedded,JsonSerializer.Deserialize<RdpOptions>("{}")!.DisplayMode);
        foreach(var mode in new[]{RdpDisplayMode.External,RdpDisplayMode.Embedded})
            Assert.Equal(mode,JsonSerializer.Deserialize<RdpOptions>(JsonSerializer.Serialize(new RdpOptions { DisplayMode=mode }))!.DisplayMode);
    }
    [Fact] public void EmbeddedModeSurvivesProfileExportWithoutCredentials()
    {
        var profile=Profile() with { Rdp=new() { DisplayMode=RdpDisplayMode.Embedded,DynamicResolution=false,Clipboard=false } };
        var json=JsonSerializer.Serialize(profile); var restored=JsonSerializer.Deserialize<ServerProfile>(json)!;
        restored.Validate(); Assert.Equal(profile.Rdp,restored.Rdp); Assert.DoesNotContain("Password",json); Assert.DoesNotContain("Secret",json);
    }
    [Fact] public void UnknownDisplayModeIsRejected()
    { Assert.Throws<ArgumentException>(()=>new RdpOptions { DisplayMode=(RdpDisplayMode)99 }.Validate()); }
    [Fact] public void KeyboardAndReconnectPersistWithSafeOldDefaults()
    { var original=new RdpOptions { KeyboardMode=RdpKeyboardMode.Remote,AutoReconnect=true }; Assert.Equal(original,JsonSerializer.Deserialize<RdpOptions>(JsonSerializer.Serialize(original))); Assert.Equal(RdpKeyboardMode.Fullscreen,new RdpOptions().KeyboardMode); Assert.False(new RdpOptions().AutoReconnect); Assert.Throws<ArgumentException>(()=>new RdpOptions { KeyboardMode=(RdpKeyboardMode)9 }.Validate()); }
    [Fact] public void FolderSharingRequiresExistingAbsolutePathAndRejectsWindowsMstsc()
    {
        var options=new RdpOptions { RedirectLocalFolder=true,LocalFolder=Path.GetTempPath() }; var args=RdpConfiguration.FreeRdpArguments(Profile(),options); Assert.Contains(args,x=>x.StartsWith("/drive:ServerDesk,")); Assert.Throws<NotSupportedException>(()=>RdpConfiguration.WindowsLines(Profile(),options));
        Assert.Throws<ArgumentException>(()=>RdpConfiguration.FreeRdpArguments(Profile(),options with { LocalFolder="relative" }));
    }
    [Fact] public async Task ProcessExitCleansConfigurationFile()
    {
        if(!OperatingSystem.IsWindows())return;
        var file=Path.Combine(Path.GetTempPath(),"serverdesk-rdp-process-"+Guid.NewGuid()+".rdp"); await File.WriteAllTextAsync(file,"test configuration without secrets");
        using var session=RdpProcessSession.Start(new(Environment.GetEnvironmentVariable("ComSpec")!,["/d","/c","exit 7"],false,file)); Assert.Equal(7,await session.Completion.WaitAsync(TimeSpan.FromSeconds(5))); Assert.False(File.Exists(file)); await session.StopAsync();
    }
    [Fact] public void FailedProcessStartCleansConfigurationFile()
    {
        var file=Path.Combine(Path.GetTempPath(),"serverdesk-rdp-process-"+Guid.NewGuid()+".rdp"); File.WriteAllText(file,"test"); Assert.Throws<System.ComponentModel.Win32Exception>(()=>RdpProcessSession.Start(new("missing-serverdesk-test-client-123456",[],false,file))); Assert.False(File.Exists(file));
    }
    [Fact] public async Task StopTerminatesOwnedProcessAndIsIdempotent()
    {
        if(!OperatingSystem.IsWindows())return;
        using var session=RdpProcessSession.Start(new(Environment.GetEnvironmentVariable("ComSpec")!,["/d","/c","ping -n 30 127.0.0.1 >nul"],false));
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); await session.StopAsync(); Assert.True(session.Completion.IsCompleted); Assert.False(session.IsRunning);
    }
    [Fact] public async Task MissingCredentialBackendFallsBackButSaveFails()
    {
        var store=new SecretServiceCredentialStore("missing-serverdesk-secret-tool-123456"); Assert.Null(await store.GetAsync(Guid.NewGuid())); await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(Guid.NewGuid(),"test-only"));
    }
}
