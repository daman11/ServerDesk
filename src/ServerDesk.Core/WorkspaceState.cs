namespace ServerDesk.Core;

public enum WorkspaceMode { Single,Columns,Rows,Three,Grid }
// Floating keeps its original numeric value for existing snapshots.
public enum SessionDock { Primary,Secondary,Floating,Tertiary,Quaternary }

// Session IDs identify live instances, never server profiles (a profile can be opened twice).
public sealed class WorkspaceState
{
    public static IReadOnlyList<SessionDock> DockedGroups { get; }=Array.AsReadOnly<SessionDock>([SessionDock.Primary,SessionDock.Secondary,SessionDock.Tertiary,SessionDock.Quaternary]);
    private static readonly IReadOnlyList<SessionDock> SingleGroup=Array.AsReadOnly<SessionDock>([SessionDock.Primary]),TwoGroups=Array.AsReadOnly<SessionDock>([SessionDock.Primary,SessionDock.Secondary]),ThreeGroups=Array.AsReadOnly<SessionDock>([SessionDock.Primary,SessionDock.Secondary,SessionDock.Tertiary]);
    private readonly Dictionary<SessionDock,List<Guid>> groups=Enum.GetValues<SessionDock>().ToDictionary(d=>d,_=>new List<Guid>());
    private readonly Dictionary<SessionDock,Guid?> selected=DockedGroups.ToDictionary(d=>d,_=>(Guid?)null);
    public IReadOnlyList<Guid> Primary=>Items(SessionDock.Primary);
    public IReadOnlyList<Guid> Secondary=>Items(SessionDock.Secondary);
    public IReadOnlyList<Guid> Tertiary=>Items(SessionDock.Tertiary);
    public IReadOnlyList<Guid> Quaternary=>Items(SessionDock.Quaternary);
    public IReadOnlyList<Guid> Floating=>Items(SessionDock.Floating);
    public IEnumerable<Guid> All=>DockedGroups.Append(SessionDock.Floating).SelectMany(d=>groups[d]);
    public IReadOnlyList<SessionDock> VisibleDocks=>Mode switch { WorkspaceMode.Single=>SingleGroup,WorkspaceMode.Three=>ThreeGroups,WorkspaceMode.Grid=>DockedGroups,_=>TwoGroups };
    public Guid? SelectedPrimary=>Selected(SessionDock.Primary);
    public Guid? SelectedSecondary=>Selected(SessionDock.Secondary);
    public Guid? SelectedTertiary=>Selected(SessionDock.Tertiary);
    public Guid? SelectedQuaternary=>Selected(SessionDock.Quaternary);
    public Guid? Focused { get; private set; }
    public WorkspaceMode Mode { get; private set; }
    public double Ratio { get; private set; }=.5;
    public double RowRatio { get; private set; }=.5;
    public void SetRatio(double value)=>Ratio=Bound(value);
    public void SetRowRatio(double value)=>RowRatio=Bound(value);
    private static double Bound(double value)=>double.IsFinite(value)?Math.Clamp(value,.2,.8):.5;
    public IReadOnlyList<Guid> Items(SessionDock dock)=>List(dock).AsReadOnly();
    public Guid? Selected(SessionDock dock)=>selected.GetValueOrDefault(dock);
    public SessionDock Location(Guid id)=>groups.FirstOrDefault(g=>g.Value.Contains(id)) is { Value:not null } found?found.Key:throw new InvalidOperationException(L.T("Sesja nie należy do obszaru pracy."));
    public void Add(Guid id,SessionDock target=SessionDock.Primary)
    {
        var list=List(target);
        if(All.Contains(id))throw new InvalidOperationException(L.T("Sesja jest już otwarta."));
        Reveal(target); list.Add(id); Select(id);
    }
    public void Select(Guid id)
    {
        var location=Location(id); if(location!=SessionDock.Floating)selected[location]=id; Focused=id;
    }
    public void Move(Guid id,SessionDock target,Guid? before=null)
    {
        var old=Location(id); var list=List(target);
        if(before==id) { if(old!=target)throw new InvalidOperationException(L.T("Niepoprawny cel dokowania.")); return; }
        if(before!=null && !list.Contains(before.Value))throw new InvalidOperationException(L.T("Niepoprawny cel dokowania."));
        RemoveFrom(id,old); Reveal(target); var index=before==null?list.Count:list.IndexOf(before.Value); list.Insert(index,id); Select(id);
    }
    private void Reveal(SessionDock target)
    {
        if(target==SessionDock.Quaternary && Mode!=WorkspaceMode.Grid)SetMode(WorkspaceMode.Grid);
        else if(target==SessionDock.Tertiary && Mode is not (WorkspaceMode.Three or WorkspaceMode.Grid))SetMode(WorkspaceMode.Three);
        else if(target==SessionDock.Secondary && Mode==WorkspaceMode.Single)SetMode(WorkspaceMode.Columns);
    }
    public void Remove(Guid id)
    {
        RemoveFrom(id,Location(id)); if(Focused==id)Focused=DockedGroups.Select(Selected).FirstOrDefault(x=>x!=null)??Floating.Select(x=>(Guid?)x).FirstOrDefault();
    }
    public void SetMode(WorkspaceMode mode)
    {
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        Mode=mode;
        foreach(var dock in DockedGroups.Where(d=>!VisibleDocks.Contains(d)))
        { groups[SessionDock.Primary].AddRange(groups[dock]); groups[dock].Clear(); selected[dock]=null; }
        if(Focused is {} focus && groups[SessionDock.Primary].Contains(focus))selected[SessionDock.Primary]=focus;
        else if(SelectedPrimary==null && Primary.Count>0)selected[SessionDock.Primary]=Primary[0];
    }
    private List<Guid> List(SessionDock dock)=>groups.TryGetValue(dock,out var list)?list:throw new ArgumentOutOfRangeException(nameof(dock));
    private void RemoveFrom(Guid id,SessionDock dock)
    {
        var list=List(dock); var index=list.IndexOf(id); list.RemoveAt(index);
        if(dock!=SessionDock.Floating && selected[dock]==id)selected[dock]=list.Count==0?null:list[Math.Min(index,list.Count-1)];
    }
}
