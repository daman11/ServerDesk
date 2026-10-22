using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    // Only explicit browsing actions may replace the browser connection.
    // Post-transfer refresh deliberately uses RefreshAsync without this retry.
    private async Task RefreshFilesAsync()
    {
        var ct=connectionLifetime.Token;
        await connectionGate.WaitAsync(ct);
        try { await RefreshFilesCoreAsync(ct); }
        finally { connectionGate.Release(); }
    }

    private async Task RefreshFilesCoreAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(disposed!=0 || !IsConnected)throw new InvalidOperationException(L.T("Najpierw połącz sesję SSH."));
        fileRefreshes++; UpdateFileNavigation();
        try
        {
            if(sftp?.IsConnected!=true)
            {
                using var busy=BeginBusy(L.T(remoteHome.Length==0?"Nawiązywanie połączenia SFTP…":"Przywracanie połączenia SFTP…"));
                // The same ordering as ShowAsync: connection gate, then refresh
                // gate. Close/reconnect waits for ownership of both to return.
                await refreshGate.WaitAsync(ct);
                try
                {
                    var previous=sftp; sftp=null;
                    if(previous!=null)await previous.DisposeAsync();
                    ct.ThrowIfCancellationRequested();
                    var defaults=await repository.PreferencesAsync(ct);
                    var startup=fileDefaultsApplied?null:await FileBrowserStartup.ResolveAsync(defaults,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),ct);
                    var client=await factory.SftpAsync(Profile,secret,sshKey,ct);
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        if(disposed!=0 || !IsConnected)throw new InvalidOperationException(L.T("Najpierw połącz sesję SSH."));
                        var home=client.WorkingDirectory;
                        var firstOpen=remoteHome.Length==0;
                        sftp=new SftpSession(client); remoteHome=home;
                        if(firstOpen || string.IsNullOrWhiteSpace(remotePath.Text))remotePath.Text=home;
                        if(startup!=null)ApplyFileDefaults(defaults,startup);
                    }
                    catch
                    {
                        sftp=null;
                        await Task.Run(()=>
                        {
                            client.ConnectionInfo.ChannelCloseTimeout=TimeSpan.FromMilliseconds(500);
                            try { client.OperationTimeout=TimeSpan.FromSeconds(1); } catch(ObjectDisposedException) { }
                            client.Dispose();
                        });
                        throw;
                    }
                }
                finally { refreshGate.Release(); }
            }
            await RefreshAsync();
        }
        finally { fileRefreshes--; UpdateFileNavigation(); }
    }
}
