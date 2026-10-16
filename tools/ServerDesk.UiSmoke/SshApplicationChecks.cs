using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Infrastructure;
using static SshAcceptanceChecks;

internal static class SshApplicationChecks
{
    internal static void Run(string root,MainWindow main,SessionPane pane,TerminalControl terminal,SftpSession sftp,string remote)
    {
        // These are real packaged distro programs extracted into our tools directory,
        // not substitutes for their protocols or UI. No system installation is made.
        var repo=Path.GetFullPath(".").Replace('\\','/');
        if(OperatingSystem.IsWindows())repo="/mnt/"+char.ToLowerInvariant(repo[0])+repo[2..];
        var apps=repo+"/.tools/terminal-test-apps/root";
        Send(terminal,$"export LD_LIBRARY_PATH='{apps}/usr/lib/x86_64-linux-gnu'; cd '{remote}'; printf 'SD_APPS_READY\\n'\r"); Until(()=>Text(terminal).Contains("SD_APPS_READY\n"),"App fixture not ready");
        void Command(string command,string marker)
        { Send(terminal,command+"; printf '\\n"+marker+"\\n'\r"); Until(()=>Text(terminal).Contains(marker+"\n"),"Command failed: "+marker); }
        void Start(string command,string visible,string name)
        {
            Send(terminal,command+"; printf '\\nSD_DONE_"+name+"\\n'\r");
            try { Until(()=>(name=="top" || terminal.Terminal.IsAlternateBufferActive) && Text(terminal).Contains(visible),"Fullscreen app did not start: "+name); }
            catch { File.WriteAllText(Path.Combine(root,"failed-"+name+".txt"),Text(terminal)); throw; }
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,"ssh-"+name+".png"));
        }
        void Done(string name)
        { Until(()=>!terminal.Terminal.IsAlternateBufferActive && Text(terminal).Contains("SD_DONE_"+name+"\n"),"Fullscreen app did not exit: "+name); Console.WriteLine("PASS real SSH "+name+" screen, keyboard and return to shell"); }
        void FileEquals(string name,string expected)
        {
            var destination=Path.Combine(root,name); Wait(sftp.DownloadAsync(remote+"/"+name,destination,true,new Progress<ServerDesk.Core.TransferProgress>(),default));
            if(File.ReadAllText(destination)!=expected)throw new Exception("Remote editor saved different UTF-8 bytes: "+name);
        }
        foreach(var (width,height) in new[]{(900d,600d),(1600d,900d),(1200d,760d)})
        {
            main.Width=width; main.Height=height; RedesignChecks.Frames();
            var cols=terminal.Terminal.Cols; var rows=terminal.Terminal.Rows; var marker="SD_SIZE_"+(int)width+"=";
            Command("printf '"+marker+"'; stty size","SD_SIZE_DONE_"+(int)width);
            if(!Text(terminal).Contains(marker+rows+" "+cols))throw new Exception("Remote PTY does not match rendered grid: "+marker+rows+" "+cols+"\n"+Text(terminal));
            Console.WriteLine($"PASS actual SSH PTY resize {width}x{height} -> {cols}x{rows}");
        }
        Start("vim -Nu NONE -n -i NONE editor-vim.txt","~","vim");
        main.KeyTextInput("iżółć λ🙂"); RedesignChecks.Frames(); Press(main,Key.Escape,symbol:"\u001b"); Send(terminal,":wq\r"); Done("vim"); FileEquals("editor-vim.txt","żółć λ🙂\n");
        Start("nano --ignorercfiles editor-nano.txt","nano","nano"); main.KeyTextInput("Nano żółć λ🙂"); Press(main,Key.O,RawInputModifiers.Control,symbol:"o"); RedesignChecks.Frames(); Press(main,Key.Enter,symbol:"\r"); RedesignChecks.Frames(); Press(main,Key.X,RawInputModifiers.Control,symbol:"x"); Done("nano"); FileEquals("editor-nano.txt","Nano żółć λ🙂\n");
        Start("top -d 1","top -","top"); main.KeyTextInput("q"); Done("top");
        Start("'"+apps+"/usr/bin/htop'","CPU","htop"); Press(main,Key.F10); Done("htop");
        Start("MC_DATADIR='"+apps+"/usr/share/mc' MC_LIBDIR='"+apps+"/usr/lib/mc' '"+apps+"/usr/bin/mc' --nosubshell --nocolor","Name","mc"); Press(main,Key.F10); Done("mc");
        foreach(var (command,name) in new[]{("bash --noprofile --norc","bash"),("'"+apps+"/bin/zsh' -f","zsh"),("'"+apps+"/usr/bin/fish' --no-config","fish")})
        {
            Send(terminal,command+"\r"); RedesignChecks.Frames(30);
            main.KeyTextInput("printf 'SD_SHELL_"+name+"_żółć\\n'"); Press(main,Key.Enter,symbol:"\r"); Until(()=>Text(terminal).Contains("SD_SHELL_"+name+"_żółć\n"),"Shell Unicode failed: "+name);
            Send(terminal,"exit\r"); Command("true","SD_SHELL_EXIT_"+name); Console.WriteLine("PASS real interactive "+name+" UTF-8 and clean exit");
        }
        Send(terminal,"sleep 20; printf 'SD_INTERRUPT_DONE\\n'\r"); RedesignChecks.Frames(); Press(main,Key.C,RawInputModifiers.Control,symbol:"c");
        Command("true","SD_INTERRUPT_RECOVERED"); Console.WriteLine("PASS SSH Ctrl+C interrupts foreground process and shell remains usable");
        Press(main,Key.D,RawInputModifiers.Control,symbol:"d"); Until(()=>!pane.IsConnected,"Ctrl+D did not close SSH session");
        Wait(pane.ReconnectAsync()); Until(()=>pane.IsConnected,"SSH reconnect failed after EOF");
        Send(terminal,"printf 'SD_RECONNECTED\\n'\r"); Until(()=>Text(terminal).Contains("SD_RECONNECTED\n"),"SSH reconnect has no output");
        Console.WriteLine("PASS SSH EOF cleanup and reconnect with same password and trusted host key");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"ssh-reconnected.png"));
    }
}
