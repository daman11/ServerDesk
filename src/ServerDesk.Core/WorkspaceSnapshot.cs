using System.Text.Json;

namespace ServerDesk.Core;

// This document deliberately contains references and layout only, no connection
// configuration, terminal text, file paths or credentials.
public sealed record SavedSession(Guid Key,Guid ProfileId,SessionDock Dock,int Panel=0,WindowGeometry? Window=null);
public sealed record WorkspaceSnapshot(int Version,WorkspaceMode Mode,double Ratio,List<SavedSession> Sessions,Guid? SelectedPrimary=null,Guid? SelectedSecondary=null,Guid? Focused=null)
{
    public const int MaximumSessions=128;
    public const int MaximumJsonCharacters=128*1024;
    public void Validate()
    {
        if(Version!=1 || !Enum.IsDefined(Mode) || !double.IsFinite(Ratio) || Ratio is <.2 or >.8 || Sessions==null || Sessions.Count>MaximumSessions)throw new InvalidDataException(L.T("Niepoprawny zapis obszaru pracy."));
        if(Sessions.Any(s=>s==null || s.Key==Guid.Empty || s.ProfileId==Guid.Empty || !Enum.IsDefined(s.Dock) || s.Panel is <0 or >2) || Sessions.Select(s=>s.Key).Distinct().Count()!=Sessions.Count)throw new InvalidDataException(L.T("Niepoprawne karty obszaru pracy."));
        foreach(var session in Sessions)session.Window?.Validate();
        bool Selected(Guid? key,SessionDock? dock)=>key==null || Sessions.Any(s=>s.Key==key && (dock==null || s.Dock==dock));
        if(!Selected(SelectedPrimary,SessionDock.Primary) || !Selected(SelectedSecondary,SessionDock.Secondary) || !Selected(Focused,null) || Mode==WorkspaceMode.Single && Sessions.Any(s=>s.Dock==SessionDock.Secondary))throw new InvalidDataException(L.T("Niepoprawny wybór karty."));
    }
    public string Serialize() { Validate(); return JsonSerializer.Serialize(this); }
    public static WorkspaceSnapshot Parse(string json)
    {
        if(json.Length>MaximumJsonCharacters)throw new InvalidDataException(L.T("Zapis obszaru pracy jest zbyt duży."));
        var saved=JsonSerializer.Deserialize<WorkspaceSnapshot>(json)??throw new InvalidDataException(L.T("Pusty zapis obszaru pracy.")); saved.Validate(); return saved;
    }
    public WorkspaceSnapshot AvailableProfiles(IEnumerable<Guid> known)
    {
        var ids=known.ToHashSet(); var sessions=Sessions.Where(s=>ids.Contains(s.ProfileId)).ToList();
        Guid? Keep(Guid? key)=>sessions.Any(s=>s.Key==key)?key:null;
        return this with { Sessions=sessions,SelectedPrimary=Keep(SelectedPrimary),SelectedSecondary=Keep(SelectedSecondary),Focused=Keep(Focused) };
    }
}
public sealed record ScreenArea(int X,int Y,int Width,int Height,double Scale=1);
public sealed record WindowGeometry(int X,int Y,double Width,double Height)
{
    public void Validate()
    {
        if(!double.IsFinite(Width) || !double.IsFinite(Height) || Width is <1 or >4096 || Height is <1 or >4096)throw new InvalidDataException(L.T("Niepoprawny rozmiar okna."));
    }
    // Position/work areas use physical pixels; dimensions and frame use logical units.
    public WindowGeometry Fit(IReadOnlyList<ScreenArea> screens,double minWidth=520,double minHeight=380,double maxWidth=2000,double maxHeight=1400,double frameWidth=0,double frameHeight=32,bool center=false)
    {
        if(!double.IsFinite(minWidth) || !double.IsFinite(minHeight) || !double.IsFinite(maxWidth) || !double.IsFinite(maxHeight) || minWidth<1 || minHeight<1 || maxWidth<minWidth || maxHeight<minHeight || !double.IsFinite(frameWidth) || !double.IsFinite(frameHeight) || frameWidth<0 || frameHeight<0)throw new ArgumentOutOfRangeException(nameof(minWidth));
        Validate(); var valid=screens.Where(s=>s.Width>0 && s.Height>32 && double.IsFinite(s.Scale) && s.Scale is >=.5 and <=4).ToArray();
        if(valid.Length==0)return this with { Width=Math.Clamp(Width,minWidth,maxWidth),Height=Math.Clamp(Height,minHeight,maxHeight) };
        double Overlap(ScreenArea s)=>Math.Max(0,Math.Min((double)X+(Width+frameWidth)*s.Scale,(double)s.X+s.Width)-Math.Max((double)X,s.X))*Math.Max(0,Math.Min((double)Y+(Height+frameHeight)*s.Scale,(double)s.Y+s.Height)-Math.Max((double)Y,s.Y));
        var screen=center?valid[0]:valid.MaxBy(Overlap)!;
        center|=Overlap(screen)==0;
        var availableWidth=Math.Max(1,screen.Width/screen.Scale-frameWidth); var availableHeight=Math.Max(1,screen.Height/screen.Scale-frameHeight);
        var width=Math.Clamp(Width,Math.Min(minWidth,availableWidth),Math.Min(maxWidth,availableWidth));
        var height=Math.Clamp(Height,Math.Min(minHeight,availableHeight),Math.Min(maxHeight,availableHeight));
        var right=(long)screen.X+screen.Width-(long)Math.Ceiling((width+frameWidth)*screen.Scale); var bottom=(long)screen.Y+screen.Height-(long)Math.Ceiling((height+frameHeight)*screen.Scale);
        var x=center?(long)screen.X+(right-screen.X)/2:Math.Clamp((long)X,screen.X,Math.Max(screen.X,right));
        var y=center?(long)screen.Y+(bottom-screen.Y)/2:Math.Clamp((long)Y,screen.Y,Math.Max(screen.Y,bottom));
        return new((int)Math.Clamp(x,int.MinValue,int.MaxValue),(int)Math.Clamp(y,int.MinValue,int.MaxValue),width,height);
    }
}
