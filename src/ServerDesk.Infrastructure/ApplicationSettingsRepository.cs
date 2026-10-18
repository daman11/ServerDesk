using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using System.Globalization;
using System.Text.Json;

namespace ServerDesk.Infrastructure;

public sealed record ApplicationSettings(string Theme,string TerminalPalette,int TerminalFont,int MonitorInterval,bool RememberWorkspace,ApplicationPreferences Preferences)
{
    public static ApplicationSettings Defaults=>new("dark","ServerDesk Default",14,5,true,new());
    public void Validate()
    {
        Preferences.Validate();
        if(!ApplicationThemes.IsSupported(Theme) || string.IsNullOrWhiteSpace(TerminalPalette) || TerminalFont is <9 or >32 || MonitorInterval is <2 or >300)throw new ArgumentException(L.T("Niepoprawne ustawienia aplikacji."));
    }
}
public sealed partial class ProfileRepository
{
    public async Task<ApplicationPreferences> PreferencesAsync(CancellationToken ct=default)
    {
        var json=await SettingAsync("applicationPreferences",ct);
        if(string.IsNullOrEmpty(json))return new();
        try { var result=JsonSerializer.Deserialize<ApplicationPreferences>(json)??new(); result.Validate(); return result; }
        catch(Exception ex) when(ex is JsonException or ArgumentException) { return new(); }
    }
    public async Task<ApplicationSettings> ApplicationSettingsAsync(CancellationToken ct=default)
    {
        var preferences=await PreferencesAsync(ct);
        if(double.TryParse(await SettingAsync("sidebar",ct),NumberStyles.Float,CultureInfo.InvariantCulture,out var width) && double.IsFinite(width))preferences=preferences with { SidebarWidth=(int)Math.Clamp(width,220,500) };
        return new(await SettingAsync("theme",ct)??"dark",await SettingAsync("terminalPalette",ct)??"ServerDesk Default",
            int.TryParse(await SettingAsync("terminalFont",ct),out var font)?Math.Clamp(font,9,32):14,
            int.TryParse(await SettingAsync("monitorInterval",ct),out var interval)?Math.Clamp(interval,2,300):5,
            await SettingAsync("rememberWorkspace",ct)!="False",preferences);
    }
    public async Task SaveApplicationSettingsAsync(ApplicationSettings value,CancellationToken ct=default)
    {
        value.Validate();
        var settings=new Dictionary<string,string> {
            ["theme"]=value.Theme,["terminalPalette"]=value.TerminalPalette,["terminalFont"]=value.TerminalFont.ToString(CultureInfo.InvariantCulture),
            ["monitorInterval"]=value.MonitorInterval.ToString(CultureInfo.InvariantCulture),["rememberWorkspace"]=value.RememberWorkspace.ToString(),
            ["applicationPreferences"]=JsonSerializer.Serialize(value.Preferences),["sidebar"]=value.Preferences.SidebarWidth.ToString(CultureInfo.InvariantCulture)
        };
        if(!value.RememberWorkspace)settings["lastWorkspace"]="";
        await using var db=await Open(ct); await using var transaction=await db.BeginTransactionAsync(ct);
        foreach(var (key,text) in settings)
        {
            using var cmd=db.CreateCommand(); cmd.Transaction=(SqliteTransaction)transaction;
            cmd.CommandText="INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            cmd.Parameters.AddWithValue("$key",key); cmd.Parameters.AddWithValue("$value",text); await cmd.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
