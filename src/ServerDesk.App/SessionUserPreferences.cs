using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private (TerminalCursorShape Shape,bool Blink)? appliedCursor;
    private bool fileDefaultsApplied;
    private void ApplyTerminalCursor(ApplicationPreferences value,bool force=false)
    {
        if(Profile.Protocol is not (Protocol.SSH or Protocol.Serial))return;
        var cursor=(value.TerminalCursor,value.TerminalCursorBlink);
        if(!force && appliedCursor==cursor)return;
        terminal.CursorStyle=value.TerminalCursor switch {
            TerminalCursorShape.Block=>XTerm.Common.CursorStyle.Block,
            TerminalCursorShape.Underline=>XTerm.Common.CursorStyle.Underline,
            _=>XTerm.Common.CursorStyle.Bar
        };
        terminal.CursorBlink=value.TerminalCursorBlink;
        // DECSCUSR updates the emulator and realized renderer together, including
        // after a remote application has replaced the template's cursor binding.
        if(terminal.IsLoaded)
        {
            var code=(value.TerminalCursor switch { TerminalCursorShape.Block=>1,TerminalCursorShape.Underline=>3,_=>5 })+(value.TerminalCursorBlink?0:1);
            terminal.Terminal.Write($"\u001b[{code} q");
        }
        appliedCursor=cursor;
    }
    private void ApplyTerminalConnectionPreferences(ApplicationPreferences value)
    {
        // A smaller buffer discards history. Apply only after an established
        // connection's existing reset, never during preview/save/cancel.
        terminal.BufferSize=value.TerminalScrollbackLines;
        // The control's template binding alone does not update the already
        // initialized emulator in the pinned package. Set its public option too.
        terminal.Terminal.Options.Scrollback=value.TerminalScrollbackLines;
        ApplyTerminalCursor(value,true);
    }
    private void ApplyFileDefaults(ApplicationPreferences value,FileBrowserStartup startup)
    {
        if(fileDefaultsApplied)return;
        localPath.Text=startup.LocalDirectory; hidden.IsChecked=value.SftpShowHidden;
        localSort.SelectedIndex=(int)value.SftpLocalSort; remoteSort.SelectedIndex=(int)value.SftpRemoteSort;
        localDescending=value.SftpLocalDescending; remoteDescending=value.SftpRemoteDescending;
        fileTarget.SelectedIndex=value.SftpStartRemote?1:0; fileDefaultsApplied=true;
        if(startup.UsedFallback)
        {
            appearanceWarning=L.T("Katalog startowy SFTP jest niedostępny. Otwarto lokalny katalog domowy."); RenderOperationStatus();
        }
    }
}
