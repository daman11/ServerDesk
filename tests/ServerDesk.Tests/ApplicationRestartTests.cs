using System.Diagnostics;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ApplicationRestartTests
{
    [Theory]
    [InlineData("dotnet",true)]
    [InlineData("dotnet.exe",true)]
    [InlineData("ServerDesk.App.exe",false)]
    [InlineData("ServerDesk.App",false)]
    public void Replacement_arguments_are_structured_and_do_not_replay_original_commands(string executable,bool managedHost)
    {
        var assembly=Path.GetFullPath(Path.Combine("folder żółć","ServerDesk.App.dll"));
        var start=ApplicationRestart.CreateStartInfo(executable,assembly,123,456);
        Assert.False(start.UseShellExecute); Assert.Equal(Path.GetDirectoryName(assembly),start.WorkingDirectory);
        Assert.Equal(managedHost?[assembly,"--restart-after","123","456"]:["--restart-after","123","456"],start.ArgumentList);
        Assert.Equal("",start.Arguments);
    }
    [Theory]
    [InlineData("--restart-after")]
    [InlineData("--restart-after|0|1")]
    [InlineData("--restart-after|-1|1")]
    [InlineData("--restart-after|1|-1")]
    [InlineData("--restart-after|1|9999999999999999999")]
    [InlineData("--restart-after|1|1|--check-rdp-input")]
    public void Invalid_handoffs_do_not_open_application_data(string arguments)=>Assert.False(ApplicationRestart.WaitForPreviousInstance(arguments.Split('|'),TimeSpan.Zero));
    [Fact] public void Self_handoff_is_rejected_and_normal_start_is_unchanged()
    {
        Assert.False(ApplicationRestart.WaitForPreviousInstance(["--restart-after",Environment.ProcessId.ToString(),"1"],TimeSpan.Zero));
        Assert.True(ApplicationRestart.WaitForPreviousInstance([]));
        Assert.True(ApplicationRestart.WaitForPreviousInstance(["--check-rdp-host"]));
    }
    [Fact] public void Handoff_waits_for_exact_parent_and_accepts_exited_or_reused_identity()
    {
        var start=new ProcessStartInfo(OperatingSystem.IsWindows()?"cmd.exe":"/bin/sh") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true };
        foreach(var arg in OperatingSystem.IsWindows()?new[]{"/d","/c","set /p serverdeskFixture="}:new[]{"-c","read value"})start.ArgumentList.Add(arg);
        using var parent=Process.Start(start)!;
        try
        {
            var args=new[]{"--restart-after",parent.Id.ToString(),parent.StartTime.ToUniversalTime().Ticks.ToString()};
            Assert.False(ApplicationRestart.WaitForPreviousInstance(args,TimeSpan.Zero));
            Assert.True(ApplicationRestart.WaitForPreviousInstance(["--restart-after",parent.Id.ToString(),"1"],TimeSpan.Zero));
            parent.StandardInput.Close(); Assert.True(parent.WaitForExit(5000));
            Assert.True(ApplicationRestart.WaitForPreviousInstance(args,TimeSpan.Zero));
        }
        finally { if(!parent.HasExited)parent.Kill(); }
    }
}
