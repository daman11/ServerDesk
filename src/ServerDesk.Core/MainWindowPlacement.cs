using System.Text.Json;

namespace ServerDesk.Core;

public sealed record MainWindowPlacement(int Version,WindowGeometry NormalBounds,bool Maximized=false)
{
    public void Validate()
    { if(Version!=1 || NormalBounds==null)throw new InvalidDataException(L.T("Niepoprawny zapis pozycji okna.")); NormalBounds.Validate(); }
    public string Serialize() { Validate(); return JsonSerializer.Serialize(this); }
    public static MainWindowPlacement Parse(string json)
    {
        if(json.Length>4096)throw new InvalidDataException(L.T("Zapis pozycji okna jest zbyt duży."));
        var placement=JsonSerializer.Deserialize<MainWindowPlacement>(json)??throw new InvalidDataException(L.T("Pusty zapis pozycji okna.")); placement.Validate(); return placement;
    }
}
