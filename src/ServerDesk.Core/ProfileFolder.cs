namespace ServerDesk.Core;

public static class ProfileFolder
{
    public const int MaximumLength=1024;
    public const int MaximumDepth=32;
    public static void Validate(string path,bool allowEmpty=false)
    {
        if(path==null || path.Length>MaximumLength)throw Invalid();
        if(allowEmpty && path.Length==0)return;
        var parts=path.Split('/');
        if(parts.Length>MaximumDepth || parts.Any(x=>string.IsNullOrWhiteSpace(x) || x is "." or ".." || x.Any(char.IsControl)))throw Invalid();
    }
    public static IEnumerable<string> Ancestors(string path)
    {
        Validate(path);
        for(var index=0;index<path.Length;index++)if(path[index]=='/')yield return path[..index];
        yield return path;
    }
    private static ArgumentException Invalid()=>new(L.T("Folder może mieć do 1024 znaków i 32 poziomów. Użyj poprawnych, niepustych nazw."));
}
