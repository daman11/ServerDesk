using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SerialPaneChecks
{
    internal static void Run(MainWindow main,ProfileRepository repo,string root,string device)
    {
        var original=repo.ApplicationSettingsAsync().GetAwaiter().GetResult();
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(original with { Preferences=original.Preferences with { TerminalScrollbackLines=200,TerminalCursor=TerminalCursorShape.Underline,TerminalCursorBlink=false } }));
        var profile=new ServerProfile { Name="Serial PTY integration",Protocol=Protocol.Serial,Serial=new() { PortName=device } };
        using var pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane); RedesignChecks.Frames();
        RedesignChecks.Wait(pane.ConnectAsync("",CancellationToken.None));
        var history=repo.HistoryAsync(profileId:profile.Id).GetAwaiter().GetResult();
        if(history.Count!=1 || history[0].Snapshot?.Endpoint!=device || history[0].Protocol!=Protocol.Serial)throw new Exception("Opened Serial device did not enter history exactly once");
        if(!pane.IsConnected || pane.VisualState!=SessionVisualState.Connected)throw new Exception("Serial pane did not open device");
        var terminal=pane.GetVisualDescendants().OfType<TerminalControl>().Single(); var emulator=terminal.Terminal;
        if(terminal.BufferSize!=200 || terminal.Terminal.Options.Scrollback!=200 || terminal.Terminal.Options.CursorStyle!=XTerm.Common.CursorStyle.Underline || terminal.Terminal.Options.CursorBlink)throw new Exception("Serial connection ignored saved cursor/history preferences");
        RedesignChecks.Wait(terminal.SendInputAsync("SERIAL_UI_PTY: żółć ANSI\r\n",CancellationToken.None)); RedesignChecks.Frames(40);
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"serial-terminal.png"));
        main.DockSession(pane,SessionDock.Floating); RedesignChecks.Frames(); RedesignChecks.Wait(terminal.SendInputAsync("SERIAL_UI_FLOATING\r\n",CancellationToken.None)); RedesignChecks.Frames(25);
        main.OwnedWindows.Single().Close(); RedesignChecks.Frames(); RedesignChecks.Wait(pane.ApplyAppearanceAsync());
        if(!pane.IsConnected || !ReferenceEquals(emulator,terminal.Terminal))throw new Exception("Serial docking/preferences replaced or disconnected terminal");
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(original with { Preferences=original.Preferences with { TerminalScrollbackLines=0 } }));
        RedesignChecks.Wait(pane.ApplyAppearanceAsync());
        if(terminal.Terminal.Options.Scrollback!=200)throw new Exception("Serial preference save trimmed current history");
        RedesignChecks.Wait(pane.ReconnectAsync()); RedesignChecks.Frames(10); terminal.Terminal.Write(string.Concat(Enumerable.Repeat("SERIAL_NO_HISTORY\r\n",500))); RedesignChecks.Frames(3);
        if(terminal.Terminal.Options.Scrollback!=0 || terminal.Terminal.Buffer.Lines.Length>terminal.Terminal.Rows)throw new Exception("Serial reconnect did not apply zero history");
        RedesignChecks.Wait(pane.CloseAsync()); if(pane.IsConnected)throw new Exception("Serial pane remained connected after close");
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(original));
        Console.WriteLine("PASS Serial terminal attached to actual PTY: saved cursor/history, input/output, floating/redock continuity, live settings preserve history, reconnect applies zero history, close waits for port shutdown");
    }
}
