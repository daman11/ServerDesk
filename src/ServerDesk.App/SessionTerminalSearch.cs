using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private TerminalSearchBar? terminalSearch;
    private void BuildTerminalSearch(WrapPanel actions)
    {
        if(Profile.Protocol is not (Core.Protocol.SSH or Core.Protocol.Serial))return;
        terminalSearch=new(terminal);
        PropertyChanged+=(_,e)=> { if(e.Property==IsVisibleProperty && IsVisible)terminalSearch.Resume(); };
        var localReleases=new HashSet<Key>();
        var search=DeskIcons.Button("search","Szukaj w terminalu · Ctrl+Shift+F",()=>terminalSearch.Open()); search.Name="TerminalSearchOpen"; search.Classes.Add("chrome"); actions.Children.Add(search);
        AddHandler(KeyDownEvent,(_,e)=>
        {
            if(!terminal.IsVisible)return;
            if(terminalSearch.IsKeyboardFocusWithin && e.Key is Key.Enter or Key.Escape)localReleases.Add(e.Key);
            if(e.Key==Key.F && e.KeyModifiers==(KeyModifiers.Control|KeyModifiers.Shift)) { localReleases.Add(e.Key); terminalSearch.Open(); e.Handled=true; }
            else if(e.Key==Key.F3 && terminalSearch.IsOpen && (e.KeyModifiers & ~KeyModifiers.Shift)==KeyModifiers.None) { localReleases.Add(e.Key); terminalSearch.Navigate(!e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled=true; }
        },RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent,(_,e)=> { if(localReleases.Remove(e.Key))e.Handled=true; },RoutingStrategies.Tunnel);
        LostFocus+=(_,_)=> { if(!IsKeyboardFocusWithin)localReleases.Clear(); };
    }
}
