using System.Security.Cryptography;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

// Opt-in only: the disposable OpenSSH fixture on port 22222, never saved profiles.
internal static class SftpSourceIntegrityChecks
{
    private sealed class InlineProgress(Action<TransferProgress> action):IProgress<TransferProgress>
    { public void Report(TransferProgress progress)=>action(progress); }
    internal static async Task Run(string root,string host,string key)
    {
        var previous=AppText.Language; var results=new List<object>();
        var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")))); await repo.InitializeAsync();
        var profile=new ServerProfile { Name="Disposable source integrity",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(3)); var ct=deadline.Token;
        await using var transfer=new SftpSession(await factory.SftpAsync(profile,"serverdesk-test-passphrase",ct));
        using var inspection=await factory.SftpAsync(profile,"serverdesk-test-passphrase",ct);
        var remote="/tmp/serverdesk-source-integrity-"+Guid.NewGuid().ToString("N"); await transfer.CreateDirectoryAsync(remote,ct);
        var local=Path.Combine(Path.GetFullPath(root),"files-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(local);
        var payload=RandomNumberGenerator.GetBytes(2<<20); var source=Path.Combine(local,"source.bin"); await File.WriteAllBytesAsync(source,payload,ct);
        var original=new byte[]{1,3,5,7}; var remoteTarget=remote+"/target.bin"; var target=Path.Combine(local,"target.bin");
        var quiet=new InlineProgress(_=>{});
        try
        {
            foreach(var language in new[]{"pl","en"})
            {
                AppText.SetLanguage(language);
                var longName=new string('x',244)+".txt"; var longTarget=Path.Combine(local,longName);
                await transfer.UploadAsync(source,remote+"/"+longName,false,quiet,ct);
                await transfer.DownloadAsync(remote+"/"+longName,longTarget,true,quiet,ct);
                if(!File.ReadAllBytes(longTarget).SequenceEqual(payload))throw new Exception("Long filename download differs from source");
                var unicode=Path.Combine(local,"żółć 日本語.txt"); await transfer.DownloadAsync(remote+"/"+longName,unicode,true,quiet,ct);
                if(!File.ReadAllBytes(unicode).SequenceEqual(payload))throw new Exception("Unicode download differs from source");
                inspection.WriteAllBytes(remoteTarget,original);
                var changed=false;
                var uploadProgress=new InlineProgress(p=>
                {
                    if(changed || p.Bytes==0)return; changed=true;
                    File.SetLastWriteTimeUtc(source,File.GetLastWriteTimeUtc(source).AddMinutes(2));
                });
                await Reject(()=>transfer.UploadAsync(source,remoteTarget,true,uploadProgress,ct),AppText.T("Źródło wysyłanego pliku zmieniło się podczas transferu. Plik docelowy pozostał bez zmian. Poczekaj na zakończenie zapisu źródła i ponów zadanie."));
                if(!changed || !inspection.ReadAllBytes(remoteTarget).SequenceEqual(original))throw new Exception("Changed upload published over the existing target");
                await transfer.UploadAsync(source,remoteTarget,true,quiet,ct);
                if(!SHA256.HashData(inspection.ReadAllBytes(remoteTarget)).SequenceEqual(SHA256.HashData(payload)))throw new Exception("Upload retry did not fetch the stable source");
                await File.WriteAllBytesAsync(target,original,ct); changed=false; var replacement=RandomNumberGenerator.GetBytes(payload.Length);
                var downloadProgress=new InlineProgress(p=>
                {
                    if(changed || p.Bytes==0)return; changed=true;
                    inspection.WriteAllBytes(remoteTarget,replacement);
                    var attrs=inspection.GetAttributes(remoteTarget); attrs.LastWriteTimeUtc=attrs.LastWriteTimeUtc.AddMinutes(2); inspection.SetAttributes(remoteTarget,attrs);
                });
                await Reject(()=>transfer.DownloadAsync(remoteTarget,target,true,downloadProgress,ct),AppText.T("Źródło pobieranego pliku zmieniło się podczas transferu. Plik docelowy pozostał bez zmian. Poczekaj na zakończenie zapisu źródła i ponów zadanie."));
                if(!changed || !File.ReadAllBytes(target).SequenceEqual(original))throw new Exception("Changed download published over the existing target");
                await transfer.DownloadAsync(remoteTarget,target,true,quiet,ct);
                if(!SHA256.HashData(File.ReadAllBytes(target)).SequenceEqual(SHA256.HashData(replacement)))throw new Exception("Download retry differs from the latest stable source");
                if(Directory.EnumerateFiles(local,"*.part").Any() || inspection.ListDirectory(remote).Any(f=>f.Name.StartsWith(".serverdesk-",StringComparison.Ordinal)))throw new Exception("Source change leaked a partial file");
                inspection.DeleteFile(remote+"/"+longName);
                results.Add(new { language,longFilenameCharacters=longName.Length,unicode=true,uploadChangedSourceRejected=true,downloadChangedSourceRejected=true,preservedTargets=true,retrySha256=true,partialsCleaned=true });
                Console.WriteLine("PASS SFTP source integrity "+language+": 248-character/Unicode filenames, same-size remote mutation, local timestamp change, original targets preserved, SHA-256-equivalent retries and partial cleanup on disposable OpenSSH");
            }
            await File.WriteAllTextAsync(Path.Combine(root,"source-integrity.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions { WriteIndented=true }),ct);
        }
        finally
        {
            AppText.SetLanguage(previous);
            foreach(var file in inspection.ListDirectory(remote).Where(f=>f.Name is not ("." or "..")))
            {
                if(file.IsDirectory || !file.FullName.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unsafe fixture cleanup target");
                inspection.DeleteFile(file.FullName);
            }
            inspection.DeleteDirectory(remote);
        }
    }
    private static async Task Reject(Func<Task> transfer,string message)
    {
        try { await transfer(); throw new Exception("A changed source was published"); }
        catch(IOException ex) { if(ex.Message!=message)throw new Exception("Unexpected source-change classification",ex); }
    }
}
