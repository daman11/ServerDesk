namespace ServerDesk.Core;

public readonly record struct DirectoryNavigationTarget(int Index,string Path,long Revision);

// Only successfully loaded directories are committed. A failed read leaves the
// cursor and forward branch intact; navigation previews have no side effects.
public sealed class DirectoryNavigationHistory(StringComparer? comparer=null)
{
    public const int Capacity=100;
    private readonly StringComparer paths=comparer??StringComparer.Ordinal;
    private readonly List<string> entries=[];
    private int position=-1;
    private long revision;
    public int Count=>entries.Count;
    public string? Current=>position<0?null:entries[position];
    public DirectoryNavigationTarget? Back=>Target(position-1);
    public DirectoryNavigationTarget? Forward=>Target(position+1);
    private DirectoryNavigationTarget? Target(int index)=>index>=0 && index<entries.Count?new(index,entries[index],revision):null;
    public void Commit(string path,DirectoryNavigationTarget? target=null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if(target is {} move && move.Revision==revision && move.Index>=0 && move.Index<entries.Count && paths.Equals(entries[move.Index],move.Path) && paths.Equals(path,move.Path))
        { position=move.Index; revision++; return; }
        if(paths.Equals(Current,path))return;
        if(position+1<entries.Count)entries.RemoveRange(position+1,entries.Count-position-1);
        entries.Add(path);
        if(entries.Count>Capacity)entries.RemoveAt(0);
        position=entries.Count-1; revision++;
    }
    public void Clear() { entries.Clear(); position=-1; revision++; }
}
