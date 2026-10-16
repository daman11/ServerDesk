using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed record FileBrowserStartup(string LocalDirectory,bool UsedFallback)
{
    // Probe only on first opening of SFTP, off the dispatcher. Editing settings
    // performs syntax validation without touching disks or network shares.
    public static Task<FileBrowserStartup> ResolveAsync(ApplicationPreferences preferences,string home,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested(); preferences.Validate();
        return Task.Run(()=>
        {
            ct.ThrowIfCancellationRequested();
            var requested=preferences.SftpLocalDirectory.Length==0?home:preferences.SftpLocalDirectory;
            var exists=Directory.Exists(requested); ct.ThrowIfCancellationRequested();
            var directory=exists?requested:home;
            if(!Directory.Exists(directory))throw new DirectoryNotFoundException(L.T("Nie można otworzyć lokalnego katalogu startowego SFTP."));
            ct.ThrowIfCancellationRequested();
            return new FileBrowserStartup(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)),!exists);
        },ct);
    }
}
