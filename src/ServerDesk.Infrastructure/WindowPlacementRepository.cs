using System.Globalization;
using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class ProfileRepository
{
    public Task SaveMainWindowPlacementAsync(MainWindowPlacement placement,CancellationToken ct=default)=>SetSettingAsync("mainWindowPlacement",placement.Serialize(),ct);
    public async Task<MainWindowPlacement?> MainWindowPlacementAsync(CancellationToken ct=default)
    {
        if(await SettingAsync("mainWindowPlacement",ct) is { Length:>0 } json)
        {
            try { return MainWindowPlacement.Parse(json); }
            catch(Exception ex) when(ex is JsonException or InvalidDataException) { }
        }
        if(!int.TryParse(await SettingAsync("x",ct),NumberStyles.Integer,CultureInfo.InvariantCulture,out var x) || !int.TryParse(await SettingAsync("y",ct),NumberStyles.Integer,CultureInfo.InvariantCulture,out var y))return null;
        static double Size(string? text,double fallback,double min,double max)=>double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value) && double.IsFinite(value)?Math.Clamp(value,min,max):fallback;
        return new(1,new(x,y,Size(await SettingAsync("width",ct),1320,900,2400),Size(await SettingAsync("height",ct),840,600,1600)));
    }
}
