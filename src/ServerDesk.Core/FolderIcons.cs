namespace ServerDesk.Core;

public static class FolderIcons
{
    public static IReadOnlyList<ProfileIconOption> Choices { get; }=Array.AsReadOnly<ProfileIconOption>([
        new("folder","Domyślna — folder"),..ProfileIcons.Choices.Where(c=>c.Id!="auto")
    ]);
    public static string Resolve(string? id)=>Choices.Any(c=>c.Id==id)?id!:"folder";
}
