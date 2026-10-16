using Avalonia.Input;
using Avalonia.Interactivity;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private void ConfigureRemoteTerminalInput()
    {
        if(Profile.Protocol is not (Core.Protocol.SSH or Core.Protocol.Serial))return;
        var localReleases=new HashSet<Key>();
        terminal.AddHandler(InputElement.KeyDownEvent,(_,e)=>
        {
            if(!terminal.IsKeyboardFocusWithin)return;
            if(e.Key==Key.C && e.KeyModifiers==(KeyModifiers.Control|KeyModifiers.Shift))
            {
                // Copy with no selection is a no-op, never a remote Ctrl+C/SIGINT.
                e.Handled=true; localReleases.Add(e.Key); _=owner.Safe(async()=> { await terminal.CopyAsync(); }); return;
            }
            if(e.Key!=Key.Escape || !OperatingSystem.IsWindows() || !terminal.IsLive ||
               terminal.Terminal.Win32InputMode || terminal.Terminal.KittyKeyboardActive || e.KeyModifiers.HasFlag(KeyModifiers.Meta))return;
            // The host control's Windows Escape workaround targets ConPTY. SSH and
            // serial use VT streams; honor the emulator's legacy Escape encoding.
            var modifiers=XTerm.Input.KeyModifiers.None;
            if(e.KeyModifiers.HasFlag(KeyModifiers.Control))modifiers|=XTerm.Input.KeyModifiers.Control;
            if(e.KeyModifiers.HasFlag(KeyModifiers.Alt))modifiers|=XTerm.Input.KeyModifiers.Alt;
            if(e.KeyModifiers.HasFlag(KeyModifiers.Shift))modifiers|=XTerm.Input.KeyModifiers.Shift;
            var sequence=terminal.Terminal.GenerateKeyInput(XTerm.Input.Key.Escape,modifiers);
            e.Handled=true; localReleases.Add(e.Key); _=owner.Safe(()=>terminal.SendInputAsync(sequence,connectionLifetime.Token));
        },RoutingStrategies.Tunnel);
        terminal.AddHandler(InputElement.KeyUpEvent,(_,e)=> { if(localReleases.Remove(e.Key))e.Handled=true; },RoutingStrategies.Tunnel);
        terminal.LostFocus+=(_,_)=>localReleases.Clear();
    }
}
