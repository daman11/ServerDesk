using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using Renci.SshNet.Common;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SshAcceptanceChecks
{
    private const string Password="ServerDesk-fixture-only-0280!";
    internal static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    internal static string Text(TerminalControl terminal)
    {
        var buffer=terminal.Terminal.Buffer;
        return string.Join("\n",Enumerable.Range(0,buffer.Lines.Length).Select(i=>buffer.Lines[i]?.TranslateToString(true)??""));
    }
    internal static void Until(Func<bool> ready,string description)
    {
        var limit=DateTime.UtcNow.AddSeconds(12);
        while(!ready() && DateTime.UtcNow<limit)RedesignChecks.Frames(1);
        if(!ready())throw new TimeoutException(description);
    }
    internal static void Send(TerminalControl terminal,string value)=>Wait(terminal.SendInputAsync(value,default));
    internal static void Press(Window window,Key key,RawInputModifiers modifiers=RawInputModifiers.None,PhysicalKey physical=PhysicalKey.None,string? symbol=null)
    { window.KeyPress(key,modifiers,physical,symbol); window.KeyRelease(key,modifiers,physical,symbol); }
    private const string CaptureScript="""
import os,sys,time,select,termios,tty
fd=sys.stdin.fileno()
saved=termios.tcgetattr(fd)
data=b''
try:
    tty.setraw(fd)
    mode=sys.argv[2] if len(sys.argv)>2 else ''
    os.write(1, ('\x1b[?1l'+({'paste':'\x1b[?2004h','kitty':'\x1b[>1u'}.get(mode,''))+'\r\nSD_RAW_READY_'+sys.argv[1]+'\r\n').encode())
    end=time.monotonic()+0.8
    while len(data)<4096:
        timeout=max(0,end-time.monotonic())
        if not timeout or not select.select([fd],[],[],timeout)[0]: break
        data+=os.read(fd,4096-len(data))
        end=time.monotonic()+0.16
finally:
    os.write(1,b'\x1b[?2004l')
    if mode=='kitty': os.write(1,b'\x1b[<u')
    termios.tcsetattr(fd,termios.TCSANOW,saved)
os.write(1, ('\r\nSD_RAW_'+sys.argv[1]+'='+data.hex()+';\r\n').encode())
""";
    internal static void Run(string root,string host)
    {
        var paths=new AppPaths(Path.Combine(root,"ssh-data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        var profile=new ServerProfile { Name="SSH password acceptance",Host=host,Port=22223,Username="serverdesk_fixture" };
        var trustCount=0; var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=> { trustCount++; return Task.FromResult(true); } };
        try { using var bad=Result(factory.SshAsync(profile,"deliberately-wrong-fixture-password",default)); throw new Exception("Wrong SSH password accepted"); }
        catch(SshAuthenticationException) { Console.WriteLine("PASS real password-only OpenSSH rejects wrong fixture password"); }
        using(var client=Result(factory.SshAsync(profile,Password,default)))
        { using var command=client.RunCommand("printf 'SERVERDESK_PASSWORD_OK'"); if(command.ExitStatus!=0 || command.Result!="SERVERDESK_PASSWORD_OK")throw new Exception("Password SSH exec failed"); }
        Console.WriteLine("PASS real password authentication and SSH exec, temporary NSS alias with no OS account changes");
        using var sftp=new SftpSession(Result(factory.SftpAsync(profile,Password,default)));
        var remote="/tmp/serverdesk-terminal-"+Guid.NewGuid().ToString("N"); Wait(sftp.CreateDirectoryAsync(remote,default));
        var capture=Path.Combine(root,"capture.py"); File.WriteAllText(capture,CaptureScript,new UTF8Encoding(false)); Wait(sftp.UploadAsync(capture,remote+"/capture.py",false,new Progress<TransferProgress>(),default));
        var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); main.RegisterPane(pane); Wait(pane.ConnectAsync(Password,default)); RedesignChecks.Frames();
        var terminal=pane.Children.OfType<TerminalControl>().Single(); terminal.Focus();
        try
        {
            if(!pane.IsConnected || trustCount!=2)throw new Exception("Password session failed or trusted key was prompted again");
            Send(terminal,"export PS1='SD_PROMPT> '; stty -echo\r"); RedesignChecks.Frames();
            var failures=new List<string>(); var number=0;
            void Capture(string name,Action input,params string[] expected)=>CaptureMode(name,input,expected,"");
            void CaptureMode(string name,Action input,string[] expected,string mode)
            {
                var id=(++number).ToString(); terminal.Terminal.Selection.ClearSelection();
                Send(terminal,"python3 "+remote+"/capture.py "+id+" "+mode+"\r"); Until(()=>Text(terminal).Contains("SD_RAW_READY_"+id+"\n"),"Raw capture did not start: "+name);
                input(); var prefix="SD_RAW_"+id+"="; Until(()=>Text(terminal).Contains(prefix),"Raw capture did not finish: "+name);
                var text=Text(terminal); var index=text.LastIndexOf(prefix,StringComparison.Ordinal)+prefix.Length; var actual=text[index..].Split(';')[0];
                var match=expected.Contains(actual,StringComparer.OrdinalIgnoreCase); Console.WriteLine((match?"PASS":"FAIL")+" SSH keyboard "+name+" -> "+(actual.Length==0?"(no bytes)":actual)); if(!match)failures.Add(name+": "+actual);
            }
            Capture("Escape",()=>Press(main,Key.Escape,symbol:"\u001b"),"1b");
            CaptureMode("Escape negotiated Kitty",()=>Press(main,Key.Escape,symbol:"\u001b"),["1b5b323775"],"kitty");
            Capture("Enter",()=>Press(main,Key.Enter,symbol:"\r"),"0d"); Capture("Tab",()=>Press(main,Key.Tab,symbol:"\t"),"09");
            foreach(var (key,expected) in new[]{(Key.A,"01"),(Key.B,"02"),(Key.C,"03"),(Key.D,"04"),(Key.V,"16"),(Key.W,"17"),(Key.Z,"1a")})Capture("Ctrl+"+key,()=>Press(main,key,RawInputModifiers.Control,symbol:key.ToString().ToLowerInvariant()),expected);
            Capture("Ctrl+Shift+C without selection",()=>Press(main,Key.C,RawInputModifiers.Control|RawInputModifiers.Shift,symbol:"C"),"");
            Capture("Alt+x",()=>Press(main,Key.X,RawInputModifiers.Alt,symbol:"x"),"1b78");
            foreach(var (key,expected) in new[]{(Key.Up,"1b5b41"),(Key.Down,"1b5b42"),(Key.Right,"1b5b43"),(Key.Left,"1b5b44"),(Key.PageUp,"1b5b357e"),(Key.PageDown,"1b5b367e"),(Key.F1,"1b4f50"),(Key.F5,"1b5b31357e"),(Key.F10,"1b5b32317e"),(Key.F12,"1b5b32347e")})Capture(key.ToString(),()=>Press(main,key),expected);
            Capture("Home",()=>Press(main,Key.Home),"1b5b48","1b5b317e"); Capture("End",()=>Press(main,Key.End),"1b5b46","1b5b347e"); Capture("Backspace",()=>Press(main,Key.Back),"7f","08");
            Capture("UTF-8 commit",()=>main.KeyTextInput("żółć λ🙂"),Convert.ToHexString(Encoding.UTF8.GetBytes("żółć λ🙂")));
            Wait(main.Clipboard!.SetTextAsync("żółć\nsecond line"));
            CaptureMode("Ctrl+Shift+V bracketed UTF-8 paste",()=>Press(main,Key.V,RawInputModifiers.Control|RawInputModifiers.Shift,symbol:"V"),[Convert.ToHexString(Encoding.UTF8.GetBytes("\x1b[200~żółć\nsecond line\x1b[201~"))],"paste");
            Send(terminal,"printf '\\033[2J\\033[HCLIPBOARD_żółć\\r\\n'\r"); Until(()=>Text(terminal).Contains("CLIPBOARD_żółć"),"Clipboard source missing");
            terminal.Terminal.Selection.StartSelection(0,0,XTerm.Selection.SelectionMode.Normal); terminal.Terminal.Selection.UpdateSelection(13,0); terminal.Terminal.Selection.EndSelection();
            var selected=terminal.Terminal.Selection.GetSelectionText(); if(selected!="CLIPBOARD_żółć")throw new Exception("Selection fixture incorrect: "+selected);
            Press(main,Key.C,RawInputModifiers.Control|RawInputModifiers.Shift,symbol:"C"); Until(()=>Result(main.Clipboard!.TryGetTextAsync())==selected,"Ctrl+Shift+C did not copy selection");
            terminal.Terminal.Selection.ClearSelection(); Console.WriteLine("PASS SSH selected Unicode copy to isolated headless clipboard");
            SshApplicationChecks.Run(root,main,pane,terminal,sftp,remote);
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,"ssh-keyboard.png"));
            File.WriteAllLines(Path.Combine(root,"keyboard-failures.txt"),failures);
            if(failures.Count>0)throw new Exception("SSH keyboard failures: "+string.Join("; ",failures));
        }
        finally
        {
            Wait(pane.CloseAsync());
            async Task Cleanup(string path)
            {
                if(!path.StartsWith(remote+"/",StringComparison.Ordinal) && path!=remote)throw new Exception("Unsafe SSH fixture cleanup");
                foreach(var entry in await sftp.ListAsync(path,default)) { if(entry.IsDirectory && !entry.IsSymbolicLink)await Cleanup(entry.FullPath); await sftp.DeleteAsync(entry,default); }
            }
            Wait(Cleanup(remote)); Wait(sftp.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default));
            main.Close(); RedesignChecks.Frames(); foreach(var dialog in main.OwnedWindows.ToArray())dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(20);
        }
    }
}
