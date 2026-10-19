using System.Security.Cryptography;
using System.Text;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class EmbeddedLinuxRdpTests
{
    [Fact] public void Embedded_arguments_bind_parent_and_certificate_without_exposing_credentials()
    {
        var profile=new ServerProfile { Name="Fixture",Protocol=Protocol.RDP,Host="2001:db8::2",Port=3390,Username="LAB\\test" };
        var hash=new string('A',64); var args=RdpConfiguration.EmbeddedFreeRdpArguments(profile,new() { Fullscreen=true,Clipboard=false },12345,hash);
        Assert.Contains("/parent-window:12345",args); Assert.Contains("/sec:nla",args); Assert.Contains("/from-stdin:force",args);
        Assert.Contains("/cert:deny,fingerprint:sha256:"+RdpCertificateFingerprint.Display(hash),args); Assert.Contains("/u:test",args); Assert.Contains("/d:LAB",args);
        Assert.Contains("-clipboard",args); Assert.Contains("-grab-keyboard",args); Assert.Contains("-toggle-fullscreen",args);
        Assert.Contains("/dynamic-resolution",args); Assert.DoesNotContain("/smart-sizing",args);
        var scaled=RdpConfiguration.EmbeddedFreeRdpArguments(profile,new() { DynamicResolution=false },12345,hash);
        Assert.Contains("/smart-sizing",scaled); Assert.DoesNotContain("/dynamic-resolution",scaled);
        Assert.DoesNotContain(args,x=>x.StartsWith("/p:") || x=="/f" || x.Contains("cert:ignore") || x.Contains("tofu") || x.StartsWith("/drive:"));
        Assert.Throws<ArgumentException>(()=>RdpConfiguration.EmbeddedFreeRdpArguments(profile,new(),0,hash));
        Assert.Throws<ArgumentException>(()=>RdpConfiguration.EmbeddedFreeRdpArguments(profile with { Username="" },new(),1,hash));
    }
    [Theory][InlineData("")][InlineData("sha256:AAAA")][InlineData("ignore")][InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAG")]
    public void Invalid_pins_cannot_become_extra_client_options(string fingerprint)=>Assert.Throws<ArgumentException>(()=>RdpCertificateFingerprint.Normalize(fingerprint));
    [Fact] public void Fingerprints_roundtrip_in_canonical_and_readable_forms()
    {
        var hash=string.Concat(Enumerable.Repeat("a1",32)); var display=RdpCertificateFingerprint.Display(hash);
        Assert.Equal(95,display.Length); Assert.Equal(hash.ToUpperInvariant(),RdpCertificateFingerprint.Normalize(display));
        Assert.Throws<ArgumentException>(()=>RdpCertificateFingerprint.Normalize(hash+",ignore"));
    }
    [Fact] public void Gui_errors_do_not_send_users_to_a_hidden_console()
    {
        foreach(var code in new[]{133,143,999}) { var status=RdpExitStatus.FromClient(code,true,interactiveConsole:false); Assert.True(status.IsError); Assert.DoesNotContain("konsoli",status.Message,StringComparison.OrdinalIgnoreCase); }
    }
    [Theory][InlineData("line\nbreak")][InlineData("line\rbreak")][InlineData("null\0byte")]
    public async Task Invalid_passwords_are_rejected_before_launch(string password)
    { await Assert.ThrowsAsync<ArgumentException>(()=>LinuxRdpProcessSession.StartAsync("missing-fixture-client",[],password,default)); }
    [Fact] public void Stdin_password_limit_is_utf8_bytes_not_characters()
    {
        LinuxRdpProcessSession.ValidatePassword(new string('x',511)); LinuxRdpProcessSession.ValidatePassword(new string('界',170));
        Assert.Throws<ArgumentException>(()=>LinuxRdpProcessSession.ValidatePassword(new string('x',512)));
        Assert.Throws<ArgumentException>(()=>LinuxRdpProcessSession.ValidatePassword(new string('界',171)));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Owned_child_receives_utf8_stdin_drains_output_and_cleans_its_private_config(bool stop)
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-rdp-pipe-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); var result=Path.Combine(root,"result.txt");
        const string secret="disposable-zażółć-界-fixture";
        string executable; string[] args;
        if(OperatingSystem.IsWindows())
        {
            executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
            var literal=result.Replace("'","''");
            var script="[Console]::InputEncoding=[Text.UTF8Encoding]::new($false); $value=[Console]::ReadLine(); $hash=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($value))).Replace('-','').ToLowerInvariant(); [IO.File]::WriteAllLines('"+literal+".tmp',@($hash,$env:XDG_CONFIG_HOME)); [IO.File]::Move('"+literal+".tmp','"+literal+"'); "+(stop?"Start-Sleep -Seconds 30":"[Console]::Out.Write(('x'*3145728)); [Console]::Error.Write(('y'*3145728)); exit 7");
            args=["-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(script))];
        }
        else
        {
            executable="/bin/bash";
            args=["-c","IFS= read -r value; printf '%s' \"$value\" | sha256sum | cut -d' ' -f1 > \"$1.tmp\"; printf '%s\\n' \"$XDG_CONFIG_HOME\" >> \"$1.tmp\"; mv -- \"$1.tmp\" \"$1\"; "+(stop?"sleep 30":"head -c 3145728 /dev/zero; head -c 3145728 /dev/zero >&2; exit 7"),"fixture",result];
        }
        try
        {
            await using var session=await LinuxRdpProcessSession.StartAsync(executable,args,secret,default);
            var deadline=DateTime.UtcNow.AddSeconds(10); while((!File.Exists(result) || (await File.ReadAllLinesAsync(result)).Length<2) && DateTime.UtcNow<deadline)await Task.Delay(20);
            var lines=await File.ReadAllLinesAsync(result); Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant(),lines[0]);
            if(stop)await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); else Assert.Equal(7,await session.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
            await session.StopAsync(); Assert.False(session.IsRunning); Assert.False(Directory.Exists(lines[1]));
            Assert.DoesNotContain(secret,await File.ReadAllTextAsync(result));
        }
        finally { Directory.Delete(root,true); }
    }
}
