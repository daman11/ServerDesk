namespace ServerDesk.Core;

public static class LocalFileName
{
    // Treat a remote entry as one component, never as a path. Reject Windows
    // aliases instead of silently trimming them into a different destination.
    public static void Validate(string name)
    {
        if(string.IsNullOrEmpty(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || name.Contains('/') || name.Contains('\\'))throw Invalid();
        if(!OperatingSystem.IsWindows())return;
        if(name.EndsWith('.') || name.EndsWith(' '))throw Invalid();
        var stem=name.Split('.')[0].TrimEnd(' ');
        if(stem.Equals("CON",StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN",StringComparison.OrdinalIgnoreCase) || stem.Equals("AUX",StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL",StringComparison.OrdinalIgnoreCase) || stem.Equals("CONIN$",StringComparison.OrdinalIgnoreCase) || stem.Equals("CONOUT$",StringComparison.OrdinalIgnoreCase))throw Invalid();
        if(stem.Length==4 && (stem.StartsWith("COM",StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT",StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3]))throw Invalid();
    }
    public static string InDirectory(string directory,string name)
    {
        Validate(name);
        var parent=Path.GetFullPath(directory);
        var target=Path.GetFullPath(Path.Combine(parent,name));
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(target)!),Path.TrimEndingDirectorySeparator(parent),comparison))throw Invalid();
        return target;
    }
    private static ArgumentException Invalid()=>new(L.T("Niepoprawna nazwa."));
}
