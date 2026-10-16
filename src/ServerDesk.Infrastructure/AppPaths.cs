namespace ServerDesk.Infrastructure;

public sealed class AppPaths
{
    public string Data { get; }
    public string Config { get; }
    public AppPaths(string? root = null)
    {
        root ??= Environment.GetEnvironmentVariable("SERVERDESK_DATA_HOME");
        if (root != null) { Data = Config = root; }
        else if (OperatingSystem.IsWindows()) { Data = Config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServerDesk"); }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Data = Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share"), "serverdesk");
            Config = Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config"), "serverdesk");
        }
        Directory.CreateDirectory(Data); Directory.CreateDirectory(Config);
        if (!OperatingSystem.IsWindows()) { File.SetUnixFileMode(Data, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); File.SetUnixFileMode(Config, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    }
}
