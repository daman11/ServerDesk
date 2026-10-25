using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private sealed class BrowserConnectionState:AvaloniaObject
    {
        public static readonly StyledProperty<bool> ConnectedProperty=AvaloniaProperty.Register<BrowserConnectionState,bool>(nameof(Connected));
        public bool Connected { get=>GetValue(ConnectedProperty); set=>SetValue(ConnectedProperty,value); }
    }
    private readonly HashSet<Guid> connectedProfileIds=[];
    private Dictionary<Guid,BrowserConnectionState> browserConnectionStates=[];

    private void UpdateBrowserConnectionStates()=>ApplyBrowserConnectionStates(sessions.Where(s=>s.IsConnected || s.IsRdpLoggedIn).Select(s=>s.Profile.Id).ToHashSet());
    private void ApplyBrowserConnectionStates(HashSet<Guid> connected)
    {
        if(connectedProfileIds.SetEquals(connected))return;
        connectedProfileIds.Clear(); connectedProfileIds.UnionWith(connected);
        foreach(var (id,state) in browserConnectionStates)state.Connected=connectedProfileIds.Contains(id);
    }
    private void BindBrowserConnectionState(Grid row,Guid id,string? detail,Dictionary<Guid,BrowserConnectionState> states)
    {
        // Allocate state only for presented rows. Each tree generation owns its
        // map, so replacing/filtering the tree cannot retain old row controls.
        if(!states.TryGetValue(id,out var state))states[id]=state=new() { Connected=connectedProfileIds.Contains(id) };
        var marker=DeskIcons.Create("circle-check",12,"Success"); marker.Name="ConnectionPresenceMarker";
        marker.Bind(IsVisibleProperty,state.GetObservable(BrowserConnectionState.ConnectedProperty));
        Grid.SetRowSpan(marker,string.IsNullOrEmpty(detail)?1:2); row.Children.Add(marker);
        var label=L.T("Połączono"); ToolTip.SetTip(marker,label); AutomationProperties.SetName(marker,label);
    }
}
