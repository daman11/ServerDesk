using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SshSecurityChecks
{
    internal static void Run(string root,string host,string key)
    {
        var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"owned-ssh-security")));
        async Task Check()
        {
            await repo.InitializeAsync();
            var profile=new ServerProfile { Name="Owned SSH security fixture",Host=host,Port=22222,Username="serverdesk_fixture" };
            var factory=new SshConnectionFactory(repo); using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            async Task Denied(bool sftp)
            {
                try
                {
                    if(sftp)using(await factory.SftpAsync(profile,"disposable-must-not-authenticate-canary",timeout.Token)) { }
                    else using(await factory.SshAsync(profile,"disposable-must-not-authenticate-canary",timeout.Token)) { }
                }
                catch(Exception ex) when(ex is SshConnectionException { DisconnectReason:DisconnectReason.HostKeyNotVerifiable or DisconnectReason.KeyExchangeFailed } || ex is InvalidOperationException && ex.Message.StartsWith("ZMIANA KLUCZA HOSTA",StringComparison.Ordinal))
                { return; }
                throw new Exception("Untrusted server reached authentication or was accepted.");
            }
            await Denied(false); await Denied(true);
            if(await repo.HostKeyAsync(host,22222)!=null)throw new Exception("Unknown key was silently trusted.");
            Console.WriteLine("PASS SSH and SFTP deny unknown host keys with no approval callback before authentication");
            var prompts=0; factory.ConfirmHostKey=(_,_)=> { prompts++; return Task.FromResult(false); };
            await Denied(false); if(prompts!=1)throw new Exception("Host rejection was not requested.");
            Console.WriteLine("PASS explicit host-key rejection prevents authentication and does not persist trust");
            const string wrong="SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            await repo.TrustHostAsync(host,22222,wrong);
            factory.ConfirmHostKey=(_,_)=> { prompts++; return Task.FromResult(true); };
            await Denied(false); await Denied(true);
            if(prompts!=1 || await repo.HostKeyAsync(host,22222)!=wrong)throw new Exception("Changed host key was replaceable by a prompt.");
            Console.WriteLine("PASS simulated MITM key mismatch blocks SSH/SFTP before authentication, bypasses approval and preserves the pinned key");
            await repo.ForgetHostKeyAsync(new(host,22222,wrong));
            profile=profile with { Authentication=Authentication.PrivateKey,PrivateKeyPath=key };
            using(var client=await factory.SshAsync(profile,"serverdesk-test-passphrase",timeout.Token))
            {
                using var command=client.RunCommand("printf SERVERDESK_SECURITY_FIXTURE");
                if(command.ExitStatus!=0 || command.Result!="SERVERDESK_SECURITY_FIXTURE")throw new Exception("Approved test server failed.");
            }
            if(prompts!=2 || await repo.HostKeyAsync(host,22222)==null)throw new Exception("Approved identity was not persisted.");
            using(var client=await factory.SftpAsync(profile,"serverdesk-test-passphrase",timeout.Token)) { }
            if(prompts!=2)throw new Exception("Pinned matching host prompted again.");
            Console.WriteLine("PASS approved disposable host supports SSH/SFTP; the matching persisted pin is reused");
        }
        // No UI is involved; do not block an Avalonia synchronization context
        // while the asynchronous repository initialization needs to resume.
        Task.Run(Check).GetAwaiter().GetResult();
    }
}
