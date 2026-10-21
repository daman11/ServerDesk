using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    public Task SetSkippedApplicationUpdateAsync(string version,CancellationToken ct=default)
    {
        ApplicationUpdate.ParseVersion(version);
        return SetSettingAsync("skippedUpdateVersion",version,ct);
    }
}
