namespace ServerDesk.Core;

public sealed record PluginDefinition(string Id,string Name,string Description,string Icon);

/// <summary>Stable IDs for reviewed, built-in extensions. No external code is loaded.</summary>
public static class PluginCatalog
{
    public const string LinuxMonitoring="linux-monitoring";
    public const string Commands="commands";
    public const string Bitwarden="bitwarden";
    public static IReadOnlyList<PluginDefinition> BuiltIn { get; }=Array.AsReadOnly<PluginDefinition>([
        new(LinuxMonitoring,"Monitoring Linux","Parametry serwera i pasek metryk. Korzysta z osobnego połączenia SSH.","chart-no-axes-combined"),
        new(Commands,"Komendy","Ściągawka poleceń, własne szablony i wykrywanie systemu przez SSH.","square-terminal"),
        new(Bitwarden,"Bitwarden / Vaultwarden","Hasła z sejfu i przypisania wpisów do połączeń. Wymaga oficjalnego Bitwarden CLI.","key")
    ]);
    public static bool IsKnown(string id)=>BuiltIn.Any(p=>p.Id==id);
    public static bool IsEnabled(ApplicationPreferences preferences,string id)=>IsKnown(id)&&!preferences.DisabledPlugins.Contains(id,StringComparer.Ordinal);
    public static void Validate(IReadOnlyList<string>? disabled)
    {
        if(disabled==null || disabled.Count>128 || disabled.Distinct(StringComparer.Ordinal).Count()!=disabled.Count || disabled.Any(id=>string.IsNullOrEmpty(id)||id.Length>64||id[0] is <'a' or >'z'||id.Any(c=>c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-' and not '.')))
            throw new ArgumentException(L.T("Niepoprawne ustawienia pluginów."),nameof(ApplicationPreferences.DisabledPlugins));
    }
}
