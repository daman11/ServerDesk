using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Security.Cryptography;
using System.Text;

if(args.Length<2) throw new ArgumentException("Usage: probe host private-key [port] [username]");
var root=Path.GetFullPath("artifacts/network-probe"); Directory.CreateDirectory(root);
var runId=Guid.NewGuid().ToString("N");
var repository=new ProfileRepository(new AppPaths(Path.Combine(root,"data-"+runId))); await repository.InitializeAsync();
var profile=new ServerProfile { Name="Ubuntu integration test",Host=args[0],Port=args.Length>2?int.Parse(args[2]):22222,Username=args.Length>3?args[3]:SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(args[1]) };
const string passphrase="serverdesk-test-passphrase";
var factory=new SshConnectionFactory(repository); var prompts=0;
factory.ConfirmHostKey=(challenge,ct)=> { prompts++; Console.WriteLine("TEST host-key prompt: "+challenge.Fingerprint); return Task.FromResult(true); };
using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90)); var ct=timeout.Token;
using(var client=await factory.SshAsync(profile,passphrase,ct))
{ using var command=client.CreateCommand("printf 'serverdesk-ssh-ok'"); await command.ExecuteAsync(ct); if(command.Result!="serverdesk-ssh-ok")throw new Exception("SSH exec failed"); Console.WriteLine("PASS SSH key with passphrase and exec"); }
using(var terminal=new SshTerminalSession(await factory.SshAsync(profile,passphrase,ct)))
{
    terminal.Resize(100,35);
    await terminal.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("printf '\\033[31mSERVERDESK_PTY_OK\\033[0m\\n'\r"),ct); await terminal.WriterStream.FlushAsync(ct);
    var bytes=new byte[16384]; var text=new StringBuilder();
    while(!text.ToString().Contains("\u001b[31mSERVERDESK_PTY_OK\u001b[0m")) { var count=await terminal.ReaderStream.ReadAsync(bytes,ct); if(count==0)throw new Exception("PTY EOF"); text.Append(Encoding.UTF8.GetString(bytes,0,count)); }
    Console.WriteLine("PASS interactive PTY, ANSI output and resize");
}
using(var monitor=new LinuxMonitoring(await factory.SshAsync(profile,passphrase,ct)))
{
    var first=await monitor.SampleAsync(ct); await Task.Delay(1200,ct); var second=await monitor.SampleAsync(ct);
    if(first.Memory.Total<=0 || second.CpuPercent==null || !second.Disks.Contains("Filesystem") || !second.System.Contains("Ubuntu"))throw new Exception("Monitoring failed");
    if(second.DiskDetails.Count==0 || second.NetworkDetails.Interfaces.Count==0 || second.Topology==null)throw new Exception("Structured monitoring data missing on test Ubuntu");
    Console.WriteLine($"PASS real Ubuntu monitoring: CPU={second.CpuPercent:F1}% RAM={second.Memory.Total} bytes");
    Console.WriteLine($"PASS real disk/network rows and topology: disks={second.DiskDetails.Count}, interfaces={second.NetworkDetails.Interfaces.Count}, cores={second.Topology.Cores}, threads={second.Topology.Threads}");
}
using(var terminal=new SshTerminalSession(await factory.SshAsync(profile,passphrase,ct)))
{
    await terminal.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("exit\r"),ct); await terminal.WriterStream.FlushAsync(ct);
    var end=await terminal.Completion.WaitAsync(TimeSpan.FromSeconds(5),ct);
    if(end.Reason!=TerminalEndReason.RemoteClosed || terminal.ExitCode!=0)throw new Exception("Normal shell exit was not reported correctly");
    Console.WriteLine("PASS remote shell EOF and completion without polling the client");
}
using(var terminal=new SshTerminalSession(await factory.SshAsync(profile,passphrase,ct)))
{
    // Kill only this test user's per-connection sshd child, never the listener.
    await terminal.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("kill -KILL $PPID\r"),ct); await terminal.WriterStream.FlushAsync(ct);
    var end=await terminal.Completion.WaitAsync(TimeSpan.FromSeconds(5),ct);
    if(end.Reason!=TerminalEndReason.TransportError || terminal.ExitCode!=1)throw new Exception("Abrupt connection loss was not classified as a transport error: "+end.Reason);
    Console.WriteLine("PASS abrupt SSH connection loss classified and resources released");
}
using(var terminal=new SshTerminalSession(await factory.SshAsync(profile,passphrase,ct)))
{
    await terminal.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("yes serverdesk-disposal-test\r"),ct); await terminal.WriterStream.FlushAsync(ct); await Task.Delay(300,ct);
    terminal.Dispose(); var end=await terminal.Completion.WaitAsync(TimeSpan.FromSeconds(5),ct);
    if(end.Reason!=TerminalEndReason.Disposed)throw new Exception("Local disposal was not classified correctly"); Console.WriteLine("PASS disposal with unread output/backpressure terminates reader");
}
async Task ReadUntil(SshTerminalSession terminal,string marker)
{
    var bytes=new byte[16384]; var text=new StringBuilder();
    while(!text.ToString().Contains(marker)) { var count=await terminal.ReaderStream.ReadAsync(bytes,ct); if(count==0)throw new Exception("Unexpected session EOF"); text.Append(Encoding.UTF8.GetString(bytes,0,count)); }
}
using(var one=new SshTerminalSession(await factory.SshAsync(profile,passphrase,ct)))
using(var two=new SshTerminalSession(await factory.SshAsync(profile,passphrase,ct)))
{
    await one.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("printf '\\033[32mSESSION_ONE\\033[0m\\n'\r"),ct);
    await two.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("printf '\\033[32mSESSION_TWO\\033[0m\\n'\r"),ct);
    await one.WriterStream.FlushAsync(ct); await two.WriterStream.FlushAsync(ct);
    await Task.WhenAll(ReadUntil(one,"\u001b[32mSESSION_ONE"),ReadUntil(two,"\u001b[32mSESSION_TWO"));
    one.Dispose(); await two.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("printf '\\033[32mSTILL_CONNECTED\\033[0m\\n'\r"),ct); await two.WriterStream.FlushAsync(ct); await ReadUntil(two,"\u001b[32mSTILL_CONNECTED");
    using var reconnected=new SshTerminalSession(await factory.SshAsync(profile,passphrase,ct)); await reconnected.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("printf '\\033[32mRECONNECTED\\033[0m\\n'\r"),ct); await reconnected.WriterStream.FlushAsync(ct); await ReadUntil(reconnected,"\u001b[32mRECONNECTED");
    Console.WriteLine("PASS concurrent isolated SSH terminals, closing one keeps the other alive, reconnect");
}
using(var sftp=new SftpSession(await factory.SftpAsync(profile,passphrase,ct)))
{
    var remote="/tmp/serverdesk-probe-"+Guid.NewGuid().ToString("N"); await sftp.CreateDirectoryAsync(remote,ct);
    var local=Path.Combine(root,"upload.bin"); var downloaded=Path.Combine(root,"download.bin"); var data=RandomNumberGenerator.GetBytes(1024*1024); await File.WriteAllBytesAsync(local,data,ct);
    var remoteFile=RemotePath.Join(remote,"plik żółty.bin"); var progress=new Progress<TransferProgress>();
    await sftp.UploadAsync(local,remoteFile,false,progress,ct); var entries=await sftp.ListAsync(remote,ct); var entry=entries.Single(x=>x.Name=="plik żółty.bin");
    await sftp.ChmodAsync(remoteFile,Convert.ToInt16("640",8),ct); await sftp.DownloadAsync(remoteFile,downloaded,true,progress,ct);
    var downloadedBytes=await File.ReadAllBytesAsync(downloaded,ct); if(!data.SequenceEqual(downloadedBytes))throw new Exception("SFTP checksum mismatch");
    try { await sftp.UploadAsync(local,remoteFile,false,progress,ct); throw new Exception("Overwrite protection failed"); } catch(IOException) { }
    await sftp.UploadAsync(local,remoteFile,true,progress,ct);
    var renamed=RemotePath.Join(remote,"renamed.bin"); await sftp.RenameAsync(remoteFile,renamed,ct); await sftp.DeleteAsync(entry with { FullPath=renamed },ct); await sftp.DeleteAsync(new("test",remote,true,false,0,DateTime.UtcNow,""),ct);
    Console.WriteLine("PASS SFTP upload/download Unicode/checksum/chmod/rename/delete/overwrite refusal");
}
using(var sftp=new SftpSession(await factory.SftpAsync(profile,passphrase,ct)))
{
    var remote="/tmp/serverdesk-cancel-"+Guid.NewGuid().ToString("N")+".bin"; var local=Path.Combine(root,"cancel-large.bin"); await File.WriteAllBytesAsync(local,RandomNumberGenerator.GetBytes(8*1024*1024),ct);
    await sftp.UploadAsync(local,remote,false,new Progress<TransferProgress>(),ct);
    var destination=Path.Combine(root,"cancel-destination.bin"); var original=Encoding.UTF8.GetBytes("keep-original-on-cancel"); await File.WriteAllBytesAsync(destination,original,ct);
    using(var cancelled=new CancellationTokenSource())
    using(var transfer=new SftpSession(await factory.SftpAsync(profile,passphrase,ct)))
    {
        var report=new CallbackProgress(p=> { if(p.Bytes>0)cancelled.Cancel(); });
        try { await transfer.DownloadAsync(remote,destination,true,report,cancelled.Token); throw new Exception("Cancellation not observed"); } catch(OperationCanceledException) when(cancelled.IsCancellationRequested) { }
    }
    var after=await File.ReadAllBytesAsync(destination,ct); if(!original.SequenceEqual(after))throw new Exception("Cancelled download corrupted destination");
    if(Directory.EnumerateFiles(root,"cancel-destination.bin.serverdesk-*.part").Any())throw new Exception("Cancelled local partial file remained");
    await sftp.DeleteAsync(new("cancel",remote,false,false,0,DateTime.UtcNow,""),ct); Console.WriteLine("PASS cancelled SFTP download preserves original and cleans local partial file");
}
using(var sftp=new SftpSession(await factory.SftpAsync(profile,passphrase,ct)))
{
    var remote="/tmp/serverdesk-upload-cancel-"+Guid.NewGuid().ToString("N"); await sftp.CreateDirectoryAsync(remote,ct);
    var destination=RemotePath.Join(remote,"existing.bin"); var missing=RemotePath.Join(remote,"new.bin"); var original=Encoding.UTF8.GetBytes("keep-remote-original-on-cancel");
    var source=Path.Combine(root,"upload-original.bin"); await File.WriteAllBytesAsync(source,original,ct); await sftp.UploadAsync(source,destination,false,new Progress<TransferProgress>(),ct);
    var large=Path.Combine(root,"cancel-large.bin");
    foreach(var target in new[]{destination,missing})
    {
        using var cancelled=new CancellationTokenSource(); var observed=false;
        try { await sftp.UploadAsync(large,target,target==destination,new CallbackProgress(p=> { if(p.Bytes>0)cancelled.Cancel(); }),cancelled.Token); }
        catch(OperationCanceledException) when(cancelled.IsCancellationRequested) { observed=true; }
        if(!observed)throw new Exception("Upload did not report cancellation");
        if((await sftp.ListAsync(remote,ct)).Any(f=>f.Name.StartsWith(".serverdesk-",StringComparison.Ordinal)))throw new Exception("Cancelled upload left a remote temporary file");
    }
    var downloaded=Path.Combine(root,"upload-cancel-check.bin"); await sftp.DownloadAsync(destination,downloaded,true,new Progress<TransferProgress>(),ct);
    var afterUpload=await File.ReadAllBytesAsync(downloaded,ct);
    if(!original.SequenceEqual(afterUpload) || await sftp.ExistsAsync(missing,ct))throw new Exception("Cancelled upload published partial content");
    // A remote truncation after the initial size lookup must not replace the local destination.
    await sftp.UploadAsync(large,missing,false,new Progress<TransferProgress>(),ct);
    using var mutate=await factory.SftpAsync(profile,passphrase,ct); var changed=false;
    try
    {
        await sftp.DownloadAsync(missing,downloaded,true,new CallbackProgress(p=> { if(p.Bytes>0 && !changed) { changed=true; using var empty=mutate.Create(missing); } }),ct);
        throw new Exception("Changed remote size was published");
    }
    catch(IOException ex) when(ex.Message.Contains("Rozmiar pobranego pliku")) { }
    var afterTruncation=await File.ReadAllBytesAsync(downloaded,ct);
    if(!changed || !original.SequenceEqual(afterTruncation) || Directory.EnumerateFiles(root,"upload-cancel-check.bin.serverdesk-*.part").Any())throw new Exception("Truncated download corrupted destination or left partial file");
    await sftp.DeleteAsync(new("original",destination,false,false,0,DateTime.UtcNow,""),ct); await sftp.DeleteAsync(new("new",missing,false,false,0,DateTime.UtcNow,""),ct); await sftp.DeleteAsync(new("folder",remote,true,false,0,DateTime.UtcNow,""),ct);
    Console.WriteLine("PASS cancelled SFTP uploads preserve remote original, never publish a new partial file, clean owned temporary files and reject a truncated download without replacing the local original");
}
if(prompts>1)throw new Exception("Trusted host key was repeatedly prompted"); Console.WriteLine("PASS trusted key reused across separate SSH/SFTP sessions");
var altered=new ProfileRepository(new AppPaths(Path.Combine(root,"changed-key-data-"+runId))); await altered.InitializeAsync(); await altered.TrustHostAsync(profile.Host,profile.Port,"SHA256:deliberately-wrong-test-key");
var blocked=new SshConnectionFactory(altered) { ConfirmHostKey=(_,_)=>throw new Exception("Changed key must not prompt acceptance") };
try { using var client=await blocked.SshAsync(profile,passphrase,ct); throw new Exception("Changed key was accepted"); } catch(InvalidOperationException ex) when(ex.Message.Contains("ZMIANA KLUCZA")) { Console.WriteLine("PASS changed host key blocked"); }
Console.WriteLine("All network integration checks passed.");
sealed class CallbackProgress(Action<TransferProgress> action) : IProgress<TransferProgress> { public void Report(TransferProgress value)=>action(value); }
