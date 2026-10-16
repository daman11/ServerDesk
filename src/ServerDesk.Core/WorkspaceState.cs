namespace ServerDesk.Core;

public enum WorkspaceMode { Single,Columns,Rows }
public enum SessionDock { Primary,Secondary,Floating }

// Session IDs identify live instances, never server profiles (a profile can be opened twice).
public sealed class WorkspaceState
{
    private readonly List<Guid> primary=[],secondary=[],floating=[];
    public IReadOnlyList<Guid> Primary=>primary.AsReadOnly();
    public IReadOnlyList<Guid> Secondary=>secondary.AsReadOnly();
    public IReadOnlyList<Guid> Floating=>floating.AsReadOnly();
    public Guid? SelectedPrimary { get; private set; }
    public Guid? SelectedSecondary { get; private set; }
    public Guid? Focused { get; private set; }
    public WorkspaceMode Mode { get; private set; }
    public double Ratio { get; private set; }=.5;
    public void SetRatio(double value)=>Ratio=double.IsFinite(value)?Math.Clamp(value,.2,.8):.5;
    public SessionDock Location(Guid id)=>primary.Contains(id)?SessionDock.Primary:secondary.Contains(id)?SessionDock.Secondary:floating.Contains(id)?SessionDock.Floating:throw new InvalidOperationException(L.T("Sesja nie należy do obszaru pracy."));
    public void Add(Guid id,SessionDock target=SessionDock.Primary)
    {
        if(primary.Contains(id)||secondary.Contains(id)||floating.Contains(id))throw new InvalidOperationException(L.T("Sesja jest już otwarta."));
        if(target==SessionDock.Secondary && Mode==WorkspaceMode.Single)SetMode(WorkspaceMode.Columns);
        List(target).Add(id); Select(id);
    }
    public void Select(Guid id)
    {
        var location=Location(id); if(location==SessionDock.Primary)SelectedPrimary=id; if(location==SessionDock.Secondary)SelectedSecondary=id; Focused=id;
    }
    public void Move(Guid id,SessionDock target,Guid? before=null)
    {
        var old=Location(id); if(before==id) { if(old!=target)throw new InvalidOperationException(L.T("Niepoprawny cel dokowania.")); return; }
        if(before!=null && !List(target).Contains(before.Value))throw new InvalidOperationException(L.T("Niepoprawny cel dokowania."));
        var list=List(target); RemoveFrom(id,old); var index=before==null?list.Count:list.IndexOf(before.Value); list.Insert(index,id);
        if(target==SessionDock.Secondary && Mode==WorkspaceMode.Single)SetMode(WorkspaceMode.Columns); Select(id);
    }
    public void Remove(Guid id)
    {
        RemoveFrom(id,Location(id)); if(Focused==id)Focused=SelectedPrimary??SelectedSecondary??floating.Select(x=>(Guid?)x).FirstOrDefault();
    }
    public void SetMode(WorkspaceMode mode)
    {
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        if(mode==WorkspaceMode.Single)
        {
            var selected=Focused!=null && secondary.Contains(Focused.Value)?Focused:SelectedPrimary??SelectedSecondary;
            primary.AddRange(secondary); secondary.Clear(); SelectedPrimary=selected; SelectedSecondary=null;
        }
        Mode=mode;
    }
    private List<Guid> List(SessionDock dock)=>dock switch { SessionDock.Primary=>primary,SessionDock.Secondary=>secondary,SessionDock.Floating=>floating,_=>throw new ArgumentOutOfRangeException(nameof(dock)) };
    private void RemoveFrom(Guid id,SessionDock dock)
    {
        var list=List(dock); var index=list.IndexOf(id); list.RemoveAt(index);
        var next=list.Count==0?(Guid?)null:list[Math.Min(index,list.Count-1)];
        if(dock==SessionDock.Primary && SelectedPrimary==id)SelectedPrimary=next;
        if(dock==SessionDock.Secondary && SelectedSecondary==id)SelectedSecondary=next;
    }
}
