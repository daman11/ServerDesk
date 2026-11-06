using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    internal async Task PasteRemoteTerminalAsync()
    {
        if(Profile.Protocol!=Core.Protocol.SSH) { await terminal.PasteAsync(); return; }
        if(!terminal.IsLive || disposed!=0 || !terminal.IsEffectivelyVisible || connectionLifetime.IsCancellationRequested)return;
        if(terminal.Terminal.PasteNotificationMode) { await terminal.PasteAsync(); return; }
        await TerminalClipboardPaste.RunAsync(terminal.PasteAsync,connectionLifetime.Token);
    }
    private void FocusTerminalInput()
    {
        terminal.ApplyTemplate();
        // Focus the actual input surface directly. The outer control posts an
        // unguarded second focus request, which can outlive a fast tab switch.
        if(terminal.GetVisualDescendants().OfType<TerminalView>().FirstOrDefault() is {} input)input.Focus();
        else terminal.Focus();
    }
    private void ConfigureRemoteTerminalInput()
    {
        if(Profile.Protocol is not (Core.Protocol.SSH or Core.Protocol.Serial))return;
        var localReleases=new HashSet<Key>();
        terminal.AddHandler(InputElement.KeyDownEvent,(_,e)=>
        {
            if(!terminal.IsKeyboardFocusWithin)return;
            if(Profile.Protocol==Core.Protocol.SSH && (e.Key==Key.V && e.KeyModifiers==(KeyModifiers.Control|KeyModifiers.Shift) || e.Key==Key.Insert && e.KeyModifiers==KeyModifiers.Shift))
            { e.Handled=true; localReleases.Add(e.Key); _=owner.Safe(PasteRemoteTerminalAsync); return; }
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
        terminal.AddHandler(InputElement.PointerPressedEvent,(_,e)=>
        {
            if(Profile.Protocol!=Core.Protocol.SSH || !e.GetCurrentPoint(terminal).Properties.IsRightButtonPressed || terminal.Terminal.Selection.HasSelection)return;
            if(terminal.Terminal.MouseTrackingMode!=XTerm.Input.MouseTrackingMode.None && !(e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !terminal.Terminal.ShiftEscape))return;
            e.Handled=true; FocusTerminalInput(); _=owner.Safe(PasteRemoteTerminalAsync);
        },RoutingStrategies.Tunnel);
        terminal.LostFocus+=(_,_)=>localReleases.Clear();
    }
}
