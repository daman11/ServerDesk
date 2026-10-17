using System.Diagnostics;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public static class TextEditorLauncher
{
    public static void EnsureAvailable(ApplicationPreferences preferences) { preferences.Validate(); _=ResolveExecutable(preferences); }
    public static void Open(ApplicationPreferences preferences,string file)
    {
        using var process=Process.Start(CreateStartInfo(preferences,file))??throw new IOException(L.T("Nie udało się uruchomić edytora. Sprawdź wybór programu w ustawieniach SFTP."));
    }

    internal static ProcessStartInfo CreateStartInfo(ApplicationPreferences preferences,string file)
    {
        preferences.Validate();
        if(!Path.IsPathFullyQualified(file))throw new ArgumentException(nameof(file));
        var executable=ResolveExecutable(preferences);
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true };
        if(preferences.SftpTextEditor==TextEditorKind.VisualStudioCode)start.ArgumentList.Add("--reuse-window");
        // Always pass a full path as a single argument. Never use file associations,
        // shell commands or user-supplied argument templates for remote content.
        start.ArgumentList.Add(Path.GetFullPath(file));
        return start;
    }

    private static string ResolveExecutable(ApplicationPreferences preferences)
    {
        IEnumerable<string> candidates=preferences.SftpTextEditor switch {
            TextEditorKind.Automatic=>OperatingSystem.IsWindows()?[Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe")]:FindOnPath("kate","gnome-text-editor","gedit","mousepad","xed","kwrite"),
            TextEditorKind.VisualStudioCode=>OperatingSystem.IsWindows()?WindowsCode():FindOnPath("code"),
            TextEditorKind.NotepadPlusPlus=>OperatingSystem.IsWindows()?WindowsNotepadPlusPlus():[],
            TextEditorKind.Kate=>FindOnPath(OperatingSystem.IsWindows()?"kate.exe":"kate"),
            TextEditorKind.GnomeTextEditor=>OperatingSystem.IsWindows()?[]:FindOnPath("gnome-text-editor"),
            TextEditorKind.Custom=>[preferences.SftpTextEditorPath],
            _=>[]
        };
        return candidates.FirstOrDefault(path=>File.Exists(path) && (!OperatingSystem.IsWindows() || Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".com",StringComparison.OrdinalIgnoreCase)))
            ??throw new FileNotFoundException(L.T("Nie znaleziono edytora tekstu. Wybierz zainstalowany program w Ustawienia → SFTP → Edycja plików."));
    }

    private static IEnumerable<string> WindowsCode()=>FindOnPath("Code.exe").Concat(new[]{
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Microsoft VS Code","Code.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft VS Code","Code.exe") });
    private static IEnumerable<string> WindowsNotepadPlusPlus()=>FindOnPath("notepad++.exe").Concat(new[]{
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Notepad++","notepad++.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Notepad++","notepad++.exe") });
    private static IEnumerable<string> FindOnPath(params string[] names)
    {
        foreach(var name in names)foreach(var directory in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
        {
            var full=directory.Trim('"');
            if(Path.IsPathFullyQualified(full))yield return Path.Combine(full,name);
        }
    }
}
