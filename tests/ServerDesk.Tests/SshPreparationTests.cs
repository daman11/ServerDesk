using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class SshPreparationTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task AlreadyCancelledRequestDoesNotReadAMissingKey(bool sftp)
    {
        var paths=new AppPaths(Path.Combine(Path.GetTempPath(),"serverdesk-preparation-"+Guid.NewGuid().ToString("N")));
        var factory=new SshConnectionFactory(new ProfileRepository(paths));
        var profile=new ServerProfile { Name="Cancelled",Host="127.0.0.1",Username="fixture",Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.Combine(paths.Data,"does-not-exist") };
        using var stop=new CancellationTokenSource(); stop.Cancel();
        Task request=sftp?factory.SftpAsync(profile,"",stop.Token):factory.SshAsync(profile,"",stop.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>request);
        Assert.False(File.Exists(profile.PrivateKeyPath));
    }
}
