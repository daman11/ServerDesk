using System.Diagnostics;
using System.Globalization;

namespace ServerDesk.Core;

public sealed record UpdateInstallRequest(ApplicationUpdate Package,string InstallDirectory,int ParentId,long ParentStarted,string CurrentVersion)
{
    public void Validate(string cache)
    {
        Package.Validate();
        if(ApplicationUpdate.ParseVersion(Package.Version)<=ApplicationUpdate.ParseVersion(CurrentVersion))throw new InvalidDataException("An update must be newer than the installed version.");
        if(ParentId<=0 || ParentStarted<=0 || ParentStarted>DateTime.MaxValue.Ticks)throw new InvalidDataException("Invalid update parent.");
        ValidateDirectory(InstallDirectory); ValidateDirectory(cache);
        if(!File.Exists(Path.Combine(InstallDirectory,"install-state.ini")) || !File.Exists(Path.Combine(InstallDirectory,"Uninstall.exe")) || !File.Exists(Path.Combine(InstallDirectory,"ServerDesk.App.exe")))throw new InvalidDataException("An installed Windows copy is required.");
        ReadRegistrationMode(InstallDirectory);
        if(Path.TrimEndingDirectorySeparator(Path.GetFullPath(cache)).Equals(Path.TrimEndingDirectorySeparator(InstallDirectory),StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(cache).StartsWith(Path.TrimEndingDirectorySeparator(InstallDirectory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update helper must run outside the install directory.");
    }
    private static void ValidateDirectory(string directory)
    {
        if(!Path.IsPathFullyQualified(directory) || directory.Any(c=>char.IsControl(c) || c=='"') || directory.StartsWith("\\\\",StringComparison.Ordinal))throw new InvalidDataException("Invalid local update directory.");
        for(var info=new DirectoryInfo(directory);info!=null;info=info.Parent)
            if(!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new InvalidDataException("Update directory is missing or contains a link.");
    }
    public ProcessStartInfo InstallerStartInfo(string cache)
    {
        Validate(cache);
        // NSIS /D consumes the rest of the command line, including spaces. It
        // must be last and unquoted. FileName is separate; no shell is involved.
        return new(Path.Combine(cache,Package.FileName)) { UseShellExecute=false,WorkingDirectory=cache,Arguments=(ReadRegistrationMode(InstallDirectory)?"/NOREGISTER ":"")+"/S /D="+Path.TrimEndingDirectorySeparator(InstallDirectory) };
    }
    public static bool ReadRegistrationMode(string installation)
    {
        var path=Path.Combine(installation,"install-state.ini");
        if(new FileInfo(path).Length>4096)throw new InvalidDataException("Invalid installer state.");
        var lines=File.ReadAllLines(path).Select(line=>line.Trim()).ToArray();
        if(!lines.Contains("[Setup]"))throw new InvalidDataException("Invalid installer state.");
        var mode=lines.Where(line=>line.StartsWith("NoRegister=",StringComparison.Ordinal)).ToArray();
        if(mode.Length!=1 || mode[0] is not ("NoRegister=0" or "NoRegister=1"))throw new InvalidDataException("Invalid installer registration mode.");
        return mode[0]=="NoRegister=1";
    }
    public bool WaitForParent(TimeSpan? timeout=null)
    {
        try
        {
            using var parent=Process.GetProcessById(ParentId);
            if(parent.StartTime.ToUniversalTime().Ticks!=ParentStarted)return true;
            if(parent.Id==Environment.ProcessId)return false;
            return parent.WaitForExit((int)Math.Clamp((timeout??TimeSpan.FromMinutes(2)).TotalMilliseconds,0,int.MaxValue));
        }
        catch(ArgumentException) { return true; }
        catch(InvalidOperationException) { return true; }
        catch(System.ComponentModel.Win32Exception) { return false; }
    }
}
