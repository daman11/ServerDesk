using System.Text;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using Porta.Pty;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class RemoteTerminalPasteChecks
{
    private sealed class ParkedReader:Stream
    {
        private readonly TaskCompletionSource done=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead=>true; public override bool CanWrite=>false; public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException(); public override long Position { get=>throw new NotSupportedException(); set=>throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default) { await done.Task.WaitAsync(ct); return 0; }
        public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken ct)=>ReadAsync(buffer.AsMemory(offset,count),ct).AsTask();
        public override int Read(byte[] buffer,int offset,int count) { done.Task.GetAwaiter().GetResult(); return 0; }
        protected override void Dispose(bool disposing) { done.TrySetResult(); base.Dispose(disposing); }
        public override void Flush() { } public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException(); public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException(); public override void SetLength(long length)=>throw new NotSupportedException();
    }
    private sealed class RecordingConnection:IPtyConnection
    {
        public Stream ReaderStream { get; }=new ParkedReader();
        public Stream WriterStream { get; }=new MemoryStream();
        public bool SupportsCancellableRead=>true;
        public int Pid=>0; public int ExitCode=>0;
        public event EventHandler<PtyExitedEventArgs>? ProcessExited { add { } remove { } }
        public bool WaitForExit(int milliseconds)=>false;
        public void Resize(int columns,int rows) { } public void Kill()=>Dispose();
        public void Dispose() { ReaderStream.Dispose(); WriterStream.Dispose(); }
        internal string Written=>Encoding.UTF8.GetString(((MemoryStream)WriterStream).ToArray());
        internal void Clear()=>((MemoryStream)WriterStream).SetLength(0);
    }
    private static void Wait(Task work)=>RedesignChecks.Wait(work);
    internal static void Run(string root)
    {
        var paths=new AppPaths(Path.Combine(root,"paste-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { CheckUpdatesOnStartup=false,Bitwarden=new(UnlockOnStartup:false) } }));
        var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        using var pane=new SessionPane(main,new() { Name="Synthetic paste",Host="fixture.invalid" },new(repo),new ExternalRdpClient(),repo);
        var window=new Window { Width=900,Height=540,Content=pane }; window.Show(); RedesignChecks.Frames(5);
        var terminal=pane.Children.OfType<TerminalControl>().Single(); terminal.ApplyTemplate();
        using var connection=new RecordingConnection(); terminal.AttachConnection(connection); RedesignChecks.Frames(4);
        var clipboard=window.Clipboard!;
        var canonical="printf '%s\\n' \\\n"+string.Join("\n",Enumerable.Range(0,2400).Select(i=>$"  'wiersz {i} żółć 🙂' \\"))+"\n  'koniec'";
        foreach(var bracketed in new[]{true,false})foreach(var newline in new[]{"\r\n","\n","\r"})
        {
            terminal.Terminal.Write(bracketed?"\x1b[?2004h":"\x1b[?2004l");
            var source=canonical.Replace("\n",newline,StringComparison.Ordinal); Wait(clipboard.SetTextAsync(source)); connection.Clear();
            Wait(pane.PasteRemoteTerminalAsync());
            var expected=bracketed?"\x1b[200~"+canonical+"\x1b[201~":canonical.Replace('\n','\r');
            if(connection.Written!=expected || clipboard.TryGetTextAsync().GetAwaiter().GetResult()!=source)throw new Exception("Long SSH paste changed content, backslashes, newline count, framing or the clipboard");
        }
        terminal.Terminal.Write("\x1b[?2004h");
        var small="printf '%s\\n' \\\n  'żółć'"; Wait(clipboard.SetTextAsync(small.Replace("\n","\r\n")));
        var input=terminal.GetVisualDescendants().OfType<TerminalView>().Single(); input.Focus(); RedesignChecks.Frames(2);
        foreach(var shiftInsert in new[]{false,true})
        {
            connection.Clear(); var key=shiftInsert?Key.Insert:Key.V; var modifiers=shiftInsert?RawInputModifiers.Shift:RawInputModifiers.Control|RawInputModifiers.Shift;
            window.KeyPress(key,modifiers,shiftInsert?PhysicalKey.Insert:PhysicalKey.V,""); window.KeyRelease(key,modifiers,shiftInsert?PhysicalKey.Insert:PhysicalKey.V,"");
            SshAcceptanceChecks.Until(()=>connection.Written=="\x1b[200~"+small+"\x1b[201~","Paste shortcut was duplicated, truncated or not normalized");
        }
        connection.Clear();
        var point=input.TranslatePoint(new Point(35,35),window)!.Value; window.MouseDown(point,MouseButton.Right); window.MouseUp(point,MouseButton.Right);
        SshAcceptanceChecks.Until(()=>connection.Written=="\x1b[200~"+small+"\x1b[201~","Right-click paste bypassed SSH normalization or pasted twice");
        terminal.Terminal.Write("\x1b[?1000h"); connection.Clear();
        window.MouseDown(point,MouseButton.Right); window.MouseUp(point,MouseButton.Right); RedesignChecks.Frames(2);
        if(connection.Written.Contains("żółć"))throw new Exception("A TUI mouse event was turned into a clipboard paste");
        terminal.Terminal.Write("\x1b[?1000l");
        Wait(clipboard.SetTextAsync("C:\\Temp\\żółć")); connection.Clear(); Wait(pane.PasteRemoteTerminalAsync());
        if(connection.Written!="\x1b[200~C:\\Temp\\żółć\x1b[201~")throw new Exception("Single-line paste changed literal slashes");
        terminal.DetachConnection(); connection.Clear(); Wait(pane.PasteRemoteTerminalAsync());
        if(connection.Written.Length!=0)throw new Exception("Disconnected terminal accepted a paste");
        Wait(pane.CloseAsync()); window.Close(); main.Close(); RedesignChecks.Frames(6);
        Console.WriteLine("PASS SSH clipboard: 2400 continuation lines, CRLF/LF/CR, Unicode, literal backslashes, bracketed/legacy framing, Ctrl+Shift+V, Shift+Insert, right click, unchanged clipboard and disconnected no-op; synthetic PTY only");
    }
    internal static void Network(string root,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"network-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { CheckUpdatesOnStartup=false,DisabledPlugins=new([PluginCatalog.Bitwarden,PluginCatalog.Commands,PluginCatalog.LinuxMonitoring]) } }));
        var factory=new SshConnectionFactory(repo);
        var main=new MainWindow(new(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        factory.ConfirmHostKey=(_,_)=>Task.FromResult(true);
        using var pane=new SessionPane(main,new() { Name="Disposable SSH paste",Host="127.0.0.1",Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) },factory,new ExternalRdpClient(),repo);
        var window=new Window { Width=1100,Height=650,Content=pane }; window.Show(); RedesignChecks.Frames(5);
        try
        {
            var encrypted=SshConnectionFactory.PrivateKeyNeedsPassphraseAsync(Path.GetFullPath(key)); Wait(encrypted);
            if(!encrypted.Result)throw new Exception("Disposable encrypted OpenSSH key did not request a passphrase");
            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); var terminal=pane.Children.OfType<TerminalControl>().Single();
            var lines=Enumerable.Range(0,1200).Select(i=>$"wiersz {i} żółć 🙂").Append("koniec").ToArray();
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',lines)+"\n"))).ToLowerInvariant();
            var command="printf '%s\\n' \\\n"+string.Join("\n",lines.Select((line,i)=>"  '"+line+"'"+(i<lines.Length-1?" \\":" | sha256sum")));
            foreach(var bracketed in new[]{true,false})
            {
                terminal.Terminal.Write("\x1b[2J\x1b[H");
                SshAcceptanceChecks.Send(terminal,"bind 'set enable-bracketed-paste "+(bracketed?"on":"off")+"'; printf 'SD_PASTE_READY\\n'\r");
                SshAcceptanceChecks.Until(()=>SshAcceptanceChecks.Text(terminal).Contains("SD_PASTE_READY\n") && terminal.Terminal.BracketedPasteMode==bracketed,"Disposable Bash did not configure paste mode");
                Wait(window.Clipboard!.SetTextAsync(command.Replace("\n","\r\n",StringComparison.Ordinal))); Wait(pane.PasteRemoteTerminalAsync());
                if(SshAcceptanceChecks.Text(terminal).Contains(hash))throw new Exception("Pasting unexpectedly executed a command without Enter");
                SshAcceptanceChecks.Send(terminal,"\r");
                SshAcceptanceChecks.Until(()=>SshAcceptanceChecks.Text(terminal).Contains(hash),"Long SSH continuation command produced missing or changed arguments");
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,bracketed?"ssh-bracketed.png":"ssh-legacy.png"));
                Console.WriteLine("PASS actual OpenSSH/Bash "+(bracketed?"bracketed":"legacy")+": 1200 UTF-8 continuation lines pasted as CRLF, exact SHA-256 and execution only after Enter");
            }
        }
        finally { Wait(pane.CloseAsync()); window.Close(); main.Close(); RedesignChecks.Frames(6); }
    }
}
