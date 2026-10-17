namespace ServerDesk.Core;

public static class ApplicationThemes
{
    public static IReadOnlyList<string> Ids { get; }=Array.AsReadOnly<string>(["dark","midnight","graphite","nord","light","ocean","forest","aubergine","dracula","solar-paper","sandstone","system"]);
    public static bool IsSupported(string id)=>Ids.Contains(id);
}
