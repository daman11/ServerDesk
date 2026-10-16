using ServerDesk.Infrastructure;
using ServerDesk.Core;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

if(!OperatingSystem.IsLinux())throw new InvalidOperationException("Run this probe in the isolated Linux D-Bus session.");
var store=new SecretServiceCredentialStore(); var id=Guid.NewGuid(); var other=Guid.NewGuid();
using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(90));
var value=" test-ą-secret-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(12))+" \n\r\n";
try
{
    if(await store.GetAsync(id,ct.Token)!=null)throw new Exception("Unexpected existing test credential");
    await store.SaveAsync(id,value,ct.Token);
    if(await store.GetAsync(id,ct.Token)!=value)throw new Exception("Secret Service did not preserve Unicode/whitespace/newlines");
    var keyringPath=Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME")!,"keyrings");
    if(!Directory.Exists(keyringPath))throw new Exception("Keyring persistence missing");
    var keyringFiles=Directory.GetFiles(keyringPath,"*.keyring");
    if(keyringFiles.Length==0 || keyringFiles.All(file=>new FileInfo(file).Length==0))throw new Exception("No persisted keyring file with the credential present");
    foreach(var file in keyringFiles) { var text=Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file,ct.Token)); if(text.Contains(value))throw new Exception("Plaintext secret found in keyring file"); }
    Console.WriteLine("PASS keyring files with the credential present do not contain plaintext test secret");
    if(await store.GetAsync(other,ct.Token)!=null)throw new Exception("Credential identities are not isolated");
    var updated=value+"updated"; await store.SaveAsync(id,updated,ct.Token); if(await store.GetAsync(id,ct.Token)!=updated)throw new Exception("Credential update failed");
    await store.DeleteAsync(id,ct.Token); if(await store.GetAsync(id,ct.Token)!=null)throw new Exception("Credential delete failed");
    Console.WriteLine("PASS real Secret Service save/read/update/delete, GUID isolation, Unicode and trailing newlines");
    var absent=new SecretServiceCredentialStore("/does-not-exist-serverdesk/secret-tool");
    if(await absent.GetAsync(id,ct.Token)!=null)throw new Exception("Missing store lookup did not fall back");
    try { await absent.SaveAsync(id,value,ct.Token); throw new Exception("Missing store save unexpectedly succeeded"); } catch(InvalidOperationException) { }
    var bus=Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
    try
    {
        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS","unix:path=/tmp/serverdesk-nonexistent-bus-"+Guid.NewGuid().ToString("N"));
        if(await store.GetAsync(other,ct.Token)!=null)throw new Exception("Unavailable D-Bus lookup did not fall back");
        try { await store.SaveAsync(other,value,ct.Token); throw new Exception("Unavailable D-Bus save unexpectedly worked"); } catch(InvalidOperationException) { }
        Console.WriteLine("PASS unavailable real D-Bus service falls back on lookup and reports save failure");
    }
    finally { Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS",bus); }
    var wrapper=Path.Combine(Path.GetTempPath(),"serverdesk-slow-secret-"+Guid.NewGuid().ToString("N"));
    try
    {
        await File.WriteAllTextAsync(wrapper,"#!/bin/sh\nsleep 30\n",ct.Token); File.SetUnixFileMode(wrapper,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var slow=new SecretServiceCredentialStore(wrapper,TimeSpan.FromMilliseconds(250)); var clock=Stopwatch.StartNew();
        if(await slow.GetAsync(id,ct.Token)!=null || clock.Elapsed>TimeSpan.FromSeconds(4))throw new Exception("Lookup timeout did not fall back promptly");
        using var cancel=new CancellationTokenSource(100);
        try { await slow.GetAsync(id,cancel.Token); throw new Exception("User cancellation ignored"); } catch(OperationCanceledException) when(cancel.IsCancellationRequested) { }
        Console.WriteLine("PASS missing backend/lookup timeout fallback, explicit user cancellation, subprocess termination");
    }
    finally { File.Delete(wrapper); }

    var faultRoot=Path.Combine(Path.GetTempPath(),"serverdesk-secret-fault-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(faultRoot); File.SetUnixFileMode(faultRoot,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    try
    {
        var faultScript=Path.Combine(faultRoot,"backend"); var pidFile=Path.Combine(faultRoot,"pid");
        async Task Script(string body)
        {
            await File.WriteAllTextAsync(faultScript,"#!/bin/sh\necho $$ > '"+pidFile+"'\n"+body+"\n",ct.Token);
            File.SetUnixFileMode(faultScript,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        }
        void Stopped()
        {
            var pid=int.Parse(File.ReadAllText(pidFile).Trim());
            try { using var process=Process.GetProcessById(pid); if(!process.HasExited)throw new Exception("Faulting credential backend is still running"); } catch(ArgumentException) { }
        }
        foreach(var output in new[]{"head -c 1000000 /dev/zero","head -c 1000000 /dev/zero >&2"})
        {
            await Script(output+"\nsleep 30"); var bounded=new SecretServiceCredentialStore(faultScript,TimeSpan.FromSeconds(10)); var clock=Stopwatch.StartNew();
            try { await bounded.GetAsync(id,ct.Token); throw new Exception("Oversized backend output accepted"); } catch(CredentialReadException) { }
            if(clock.Elapsed>TimeSpan.FromSeconds(4))throw new Exception("Output limit waited for backend timeout"); Stopped();
            try { await bounded.SaveAsync(id,"fixture",ct.Token); throw new Exception("Oversized store output accepted"); } catch(InvalidOperationException) { }
            Stopped();
        }
        Console.WriteLine("PASS oversized stdout/stderr abort promptly, preserve secret confidentiality and terminate the owned credential backend");
        var unicodeFile=Path.Combine(faultRoot,"unicode"); await File.WriteAllTextAsync(unicodeFile,new string('界',22000),new UTF8Encoding(false),ct.Token); await Script("cat '"+unicodeFile+"'");
        try { await new SecretServiceCredentialStore(faultScript).GetAsync(id,ct.Token); throw new Exception("UTF-8 byte limit was bypassed"); } catch(CredentialReadException) { }
        Stopped();
        await Script("exec sleep 30"); var blocked=new SecretServiceCredentialStore(faultScript,TimeSpan.FromMilliseconds(250)); var blockedClock=Stopwatch.StartNew();
        try { await blocked.SaveAsync(id,new string('x',CredentialLimits.MaximumSecretBytes),ct.Token); throw new Exception("Blocked stdin unexpectedly saved"); } catch(InvalidOperationException) { }
        if(blockedClock.Elapsed>TimeSpan.FromSeconds(4))throw new Exception("Blocked stdin ignored timeout"); Stopped();
        Console.WriteLine("PASS UTF-8 byte bound, timeout while stdin is blocked and subprocess termination");
    }
    finally { Directory.Delete(faultRoot,true); }
}
finally { await store.DeleteAsync(id,CancellationToken.None); }
