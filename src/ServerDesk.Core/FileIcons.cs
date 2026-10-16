namespace ServerDesk.Core;
public static class FileIcons
{
    public static string Resolve(FileEntry entry)
    {
        if(entry.IsSymbolicLink)return "link";
        if(entry.IsDirectory)return "folder";
        var name=entry.Name.ToLowerInvariant();
        if(name.StartsWith(".env",StringComparison.Ordinal))return "file-key";
        if(name is "dockerfile" or "containerfile")return "container";
        return Path.GetExtension(name) switch {
            ".key" or ".pem" or ".p12" or ".pfx"=>"file-key",
            ".json"=>"file-braces",
            ".yaml" or ".yml" or ".toml" or ".ini" or ".conf" or ".config"=>"file-cog",
            ".sh" or ".ps1" or ".py" or ".js" or ".ts" or ".cs" or ".go" or ".rs" or ".html" or ".css"=>"file-code",
            ".zip" or ".tar" or ".gz" or ".tgz" or ".7z" or ".bz2" or ".xz"=>"file-archive",
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".svg" or ".gif"=>"image",
            ".log" or ".txt" or ".md" or ".csv"=>"file-text",
            _=>"file"
        };
    }
}
