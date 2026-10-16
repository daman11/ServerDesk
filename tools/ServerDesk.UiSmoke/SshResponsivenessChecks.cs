using System.Diagnostics;
using Avalonia.Threading;
using Renci.SshNet.Common;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SshResponsivenessChecks
{
    private static void Wait(Task task)
    {
        var deadline=DateTime.UtcNow.AddSeconds(40);
        while(!task.IsCompleted && DateTime.UtcNow<deadline)RedesignChecks.Frames(1);
        if(!task.IsCompleted)throw new TimeoutException("SSH responsiveness operation did not finish");
        task.GetAwaiter().GetResult();
    }
    internal static void Run(string root,string host,string key)
    {
        var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")))); Wait(repo.InitializeAsync());
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        var profile=new ServerProfile { Name="Owned responsiveness fixture",Host=host,Port=22222,Username="damian",Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var measurements=new List<double>();
        foreach(var sftp in new[]{false,true})
        {
            var heartbeat=false; Dispatcher.UIThread.Post(()=>heartbeat=true);
            var watch=Stopwatch.StartNew();
            Task work=sftp?factory.SftpAsync(profile,"incorrect-fixture-passphrase",default):factory.SshAsync(profile,"incorrect-fixture-passphrase",default);
            watch.Stop(); measurements.Add(watch.Elapsed.TotalMilliseconds);
            try { Wait(work); throw new Exception("Invalid fixture passphrase was accepted"); }
            catch(Exception ex) when(ex is SshPassPhraseNullOrEmptyException or SshException or System.Security.Cryptography.CryptographicException) { }
            RedesignChecks.Frames(1);
            if(!heartbeat)throw new Exception("UI heartbeat did not run while the key was prepared");
            Console.WriteLine($"INFO {(sftp?"SFTP":"SSH")} call returned in {watch.Elapsed.TotalMilliseconds:F1}ms while preparing an encrypted key");
        }
        File.WriteAllText(Path.Combine(root,"key-preparation-ms.txt"),string.Join("\n",measurements));
        if(measurements.Any(ms=>ms>100))throw new Exception("Encrypted key preparation blocks the calling UI thread");
        using(var ssh=Result(factory.SshAsync(profile,"serverdesk-test-passphrase",default)))
        { using var command=ssh.CreateCommand("printf 'responsive-ssh-ok'"); Wait(command.ExecuteAsync(default)); if(command.Result!="responsive-ssh-ok")throw new Exception("SSH after failed key preparation did not recover"); }
        using(var client=Result(factory.SftpAsync(profile,"serverdesk-test-passphrase",default)))
        { if(!client.IsConnected)throw new Exception("SFTP did not connect after failed preparation"); }
        Console.WriteLine("PASS SSH/SFTP encrypted-key preparation leaves UI responsive and later authentication still works");
    }
    private static T Result<T>(Task<T> work) { Wait(work); return work.Result; }
}
