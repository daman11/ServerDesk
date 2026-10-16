using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;

public sealed class CommandDiscoveryTests
{
    [Theory]
    [InlineData("ubuntu","debian","24.04","debian")][InlineData("debian","","13","debian")]
    [InlineData("rhel","fedora","9.6","rhel")][InlineData("rocky","rhel centos fedora","9.6","rhel")]
    [InlineData("almalinux","rhel centos fedora","10","rhel")][InlineData("centos","rhel fedora","10","rhel")]
    [InlineData("fedora","","42","rhel")][InlineData("arch","","","arch")][InlineData("alpine","","3.22","alpine")]
    [InlineData("vendor-linux","debian ubuntu","1.0","debian")][InlineData("unknown","",""," ")]
    [InlineData("rhel","debian","9.6","rhel")]
    public void Parses_os_release_as_data_without_evaluating_shell_content(string id,string like,string version,string expected)
    {
        var text=$"SDCMD_LINUX_V1\nID={id}\nID_LIKE=\"{like}\"\nVERSION_ID=\"{version}\"\nPRETTY_NAME=\"Example $(touch /tmp/never)\"\nSDCMD_SHELL=/bin/zsh\nSDCMD_TOOL=apt:A\nSDCMD_TOOL=docker:N\nSDCMD_TOOL=systemctl:U\nSDCMD_END\n";
        var env=CommandDetection.ParseLinux(new(0,text))!; Assert.NotNull(env); Assert.Equal(id,env.Distribution); Assert.Equal(expected.Trim(),env.Family); Assert.Equal(version,env.Version); Assert.Equal(CommandShell.Zsh,env.Shell); Assert.Contains("$(touch",env.PrettyName);
        Assert.Equal(ToolAvailability.Available,env.Tool("apt")); Assert.Equal(ToolAvailability.NotDetected,env.Tool("docker")); Assert.Equal(ToolAvailability.Unknown,env.Tool("systemctl")); Assert.Equal(ToolAvailability.Unknown,env.Tool("anything"));
    }
    [Theory]
    [InlineData("Windows 11 Pro","False","windows11")][InlineData("Windows 10 Enterprise","False","windows10")][InlineData("Windows Server 2025","True","windows-server")]
    public void Parses_Windows_supported_SSH_channel(string name,string server,string expected)
    {
        var env=CommandDetection.ParseWindows(new(0,$"SDCMD_WINDOWS_V1\r\nSDCMD_NAME={name}\r\nSDCMD_VERSION=10.0.26100\r\nSDCMD_SERVER={server}\r\nSDCMD_TOOL=Get-Service:A\r\nSDCMD_TOOL=winget:N\r\nSDCMD_END\r\n"))!;
        Assert.Equal(CommandPlatform.Windows,env.Platform); Assert.Equal(expected,env.Distribution); Assert.Equal(CommandShell.Any,env.Shell); Assert.Equal(ToolAvailability.NotDetected,env.Tool("winget"));
    }
    [Fact] public async Task Detection_uses_fixed_exec_probes_and_does_not_invent_absence_when_channels_fail()
    {
        var called=new List<string>(); var env=await CommandDetection.DetectAsync((text,_)=> { called.Add(text); throw new UnauthorizedAccessException(); },CancellationToken.None);
        Assert.False(env.Detected); Assert.Equal(ToolAvailability.Unknown,env.Tool("apt")); Assert.Equal(2,called.Count); Assert.DoesNotContain(called,c=>System.Text.RegularExpressions.Regex.IsMatch(c,@"(^|;)\s*sudo\s"));
        Assert.Null(CommandDetection.ParseLinux(new(1,"SDCMD_LINUX_V1\nID=debian\nSDCMD_END")));
        Assert.Null(CommandDetection.ParseLinux(new(0,"SDCMD_LINUX_V1\nID=debian")));
    }
    [Theory][InlineData(true,CommandShell.Cmd)][InlineData(false,CommandShell.PowerShell)]
    public async Task Windows_explicit_PowerShell_probe_does_not_mislabel_default_CMD_shell(bool cmd,CommandShell expected)
    {
        Task<CommandProbeResult> Run(string text,CancellationToken _)=>Task.FromResult(text==CommandDetection.LinuxProbe?new CommandProbeResult(1,""):text==CommandDetection.WindowsProbe?new(0,"SDCMD_WINDOWS_V1\nSDCMD_NAME=Windows 11\nSDCMD_END"):text=="echo %COMSPEC%"?new(0,cmd?"C:\\Windows\\System32\\cmd.exe":"%COMSPEC%"):new(0,"5"));
        Assert.Equal(expected,(await CommandDetection.DetectAsync(Run,CancellationToken.None)).Shell);
    }
    [Fact] public async Task Closing_session_cancels_detection_without_fallback_or_stale_success()
    {
        using var cts=new CancellationTokenSource(); var entered=new TaskCompletionSource();
        var task=CommandDetection.DetectAsync(async (_,ct)=> { entered.SetResult(); await Task.Delay(Timeout.Infinite,ct); return new(0,""); },cts.Token);
        await entered.Task; cts.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);
    }
    [Fact] public void Missing_os_release_keeps_proven_Linux_generic_and_probes_fit_Windows_command_line_limit()
    {
        var result=CommandDetection.ParseLinux(new(0,"SDCMD_LINUX_V1\nSDCMD_KERNEL=Linux\nSDCMD_SHELL=/bin/bash\nSDCMD_END"))!;
        Assert.Equal(CommandPlatform.Linux,result.Platform); Assert.Empty(result.Distribution); Assert.Equal(ToolAvailability.Unknown,result.Tool("apt"));
        Assert.True(CommandDetection.WindowsProbe.Length<8191);
    }
}
