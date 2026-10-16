namespace ServerDesk.Core;

public sealed record PathCrumb(string Label,string Path);

public static class PathNavigation
{
    // Editing a path includes incomplete/empty values. Navigation validates it when submitted.
    public static IReadOnlyList<PathCrumb> Breadcrumbs(string? path,bool remote)
    {
        if(string.IsNullOrEmpty(path) || path.Contains('\0'))return [];
        string root;
        try { root=remote?(path.StartsWith('/')?"/":""):Path.GetPathRoot(path)??""; }
        catch(ArgumentException) { return []; }
        var separators=remote?new[]{'/'}:new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar};
        var parts=path[root.Length..].Split(separators,StringSplitOptions.RemoveEmptyEntries);
        var result=new List<PathCrumb>(); var current=root;
        if(root.Length>0)result.Add(new(root,root));
        foreach(var part in parts)
        {
            current=remote?(current.Length==0?part:current.TrimEnd('/')+"/"+part):Path.Combine(current,part);
            result.Add(new(part,current));
        }
        return result;
    }
}
