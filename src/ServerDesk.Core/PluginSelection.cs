using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerDesk.Core;

// Immutable, structurally comparable settings; copies cannot mutate live plugin
// state and JSON roundtrips retain ApplicationPreferences value semantics.
[JsonConverter(typeof(PluginSelectionJsonConverter))]
public sealed class PluginSelection:IReadOnlyList<string>,IEquatable<PluginSelection>
{
    private readonly string[] ids;
    public static PluginSelection Empty { get; }=new([]);
    public PluginSelection(IEnumerable<string> ids)=>this.ids=ids.Order(StringComparer.Ordinal).ToArray();
    public int Count=>ids.Length;
    public string this[int index]=>ids[index];
    public IEnumerator<string> GetEnumerator()=>((IEnumerable<string>)ids).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    public bool Equals(PluginSelection? other)=>other!=null&&ids.SequenceEqual(other.ids,StringComparer.Ordinal);
    public override bool Equals(object? other)=>other is PluginSelection selection&&Equals(selection);
    public override int GetHashCode() { var hash=new HashCode(); foreach(var id in ids)hash.Add(id,StringComparer.Ordinal); return hash.ToHashCode(); }
}
public sealed class PluginSelectionJsonConverter:JsonConverter<PluginSelection>
{
    public override PluginSelection Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>new(JsonSerializer.Deserialize<string[]>(ref reader,options)??throw new JsonException("Invalid plugin selection."));
    public override void Write(Utf8JsonWriter writer,PluginSelection value,JsonSerializerOptions options)=>JsonSerializer.Serialize(writer,value.ToArray(),options);
}
