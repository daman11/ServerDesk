using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        if(args.Length!=3 || args[2]!="--disposable-desktop" || args[1] is not ("local" or "remote"))return 2;
        ApplicationConfiguration.Initialize();
        Application.Run(new Fixture(Path.GetFullPath(args[0]),args[1]));
        return 0;
    }
}

// A purpose-built fixture, never a general desktop automation service. Commands
// can only operate on fixed synthetic clipboard data and this window's controls.
internal sealed class Fixture:Form
{
    internal const string Sample="ServerDesk RDP – żółć ąęćłńóśźż\r\nEmoji 😀 📁\r\nTab\t123\r\n";
    private const string Polish="ąćęłńóśźż ĄĆĘŁŃÓŚŹŻ";
    private readonly string root,role;
    private readonly InputBox editor=new() { Dock=DockStyle.Fill,Multiline=true,AcceptsTab=true,Font=new Font("Segoe UI",18),Name="PhysicalInput",ScrollBars=RichTextBoxScrollBars.Both };
    private readonly Label status=new() { Dock=DockStyle.Bottom,Height=55,AutoEllipsis=true };
    private readonly System.Windows.Forms.Timer timer=new() { Interval=200 };
    private readonly Stopwatch lifetime=Stopwatch.StartNew();
    private readonly List<object> manual=[];
    private string lastRequest="";
    private int left,right,doubles,wheel,moves,keysDown;
    private long lastSnapshot;
    private bool awake;
    private readonly string? previousText;

    internal Fixture(string directory,string side)
    {
        root=directory; role=side; Directory.CreateDirectory(root);
        if(File.Exists(Path.Combine(root,".rdp-matrix-fixture")))throw new InvalidOperationException("Use a fresh fixture directory");
        File.WriteAllText(Path.Combine(root,".rdp-matrix-fixture"),Guid.NewGuid().ToString());
        Text="ServerDesk RDP fixture — "+role.ToUpperInvariant(); Width=1100; Height=700; StartPosition=FormStartPosition.CenterScreen;
        previousText=Clipboard.ContainsText()?Clipboard.GetText():null;
        var header=new Label { Dock=DockStyle.Top,Height=115,Text="TESTOWE OKNO RDP — wyłącznie dane syntetyczne\nMysz: lewy/prawy/dwuklik, kółko, przeciągnięcie zaznaczenia w polu.\nAltGr: wpisz ręcznie "+Polish+". IME: przełącz na japoński, wpisz nihongo, wybierz 日本語.\nNie wklejaj prywatnych danych. Wyniki zawierają liczniki i porównania, bez treści wpisanej z klawiatury.",Padding=new Padding(12),Font=new Font("Segoe UI",11) };
        var actions=new FlowLayoutPanel { Dock=DockStyle.Top,Height=76,AutoScroll=true };
        void Button(string name,Action action) { var b=new Button { Text=name,AutoSize=true }; b.Click+=(_,_)=> { try { action(); } catch(Exception ex) { status.Text=ex.GetType().Name; } }; actions.Controls.Add(b); }
        Button("Wyczyść pole",()=>editor.Clear());
        Button("Układ Polski programisty",()=> { if(LoadKeyboardLayout("00000415",1)==0)throw new InvalidOperationException("Polish keyboard unavailable"); editor.Focus(); });
        Button("Japoński Microsoft IME",()=> { var language=InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().FirstOrDefault(l=>l.Culture.Name=="ja-JP"); if(language==null)throw new InvalidOperationException("Japanese IME is not installed for this user"); InputLanguage.CurrentInputLanguage=language;editor.ImeMode=ImeMode.Hiragana; editor.Focus(); });
        Button("Katalog testowych plików",()=>Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute=true,ArgumentList={Files} }));
        Button("Sprawdź AltGr",()=> { var passed=editor.Text.Trim()==Polish;manual.Add(new { Name="AltGr",Passed=passed,KeysDown=keysDown });status.Text=passed?"PASS AltGr — treść zgodna":"Treść AltGr niezgodna; przygotuj próbkę i sprawdź ponownie";Snapshot(); });
        Button("Sprawdź IME",()=> { var passed=editor.Text.Trim()=="日本語";manual.Add(new { Name="IME text",Passed=passed,LegacyCompositionObserved=editor.ImeStarts>0,editor.ImeStarts,editor.ImeUpdates,editor.ImeEnds });status.Text=passed?"Treść 日本語 zgodna — kompozycję i wybór kandydata potwierdź ręcznie":"Treść IME niezgodna; przygotuj próbkę i sprawdź ponownie";Snapshot(); });
        Controls.Add(editor); Controls.Add(actions); Controls.Add(header); Controls.Add(status);
        editor.KeyDown+=(_,_)=>keysDown++;
        editor.MouseDown+=(_,e)=> { if(e.Button==MouseButtons.Left)left++;if(e.Button==MouseButtons.Right)right++; };
        editor.MouseDoubleClick+=(_,_)=>doubles++;
        editor.MouseWheel+=(_,e)=>wheel+=Math.Sign(e.Delta);
        editor.MouseMove+=(_,_)=>moves++;
        editor.ImeMode=ImeMode.NoControl;
        editor.PasteFiles=()=>
        {
            try
            {
                if(Clipboard.GetDataObject()?.GetDataPresent("FileGroupDescriptorW",false)!=true)return false;
                manual.Add(new {Name="Automatic file paste",Result=FileClipboard.Paste(root)});Snapshot();return true;
            }
            catch(Exception error) { manual.Add(new {Name="Automatic file paste",Passed=false,Error=error.GetType().Name});Snapshot();return true; }
        };
        PrepareFiles();
        timer.Tick+=(_,_)=>Tick(); timer.Start();
        FormClosing+=(_,_)=> { Snapshot(); timer.Stop();if(awake)SetThreadExecutionState(0x80000000); if(role=="local")try { if(previousText!=null)Clipboard.SetText(previousText);else Clipboard.Clear(); } catch(ExternalException) { } };
        Shown+=(_,_)=> { WindowState=role=="remote"?FormWindowState.Maximized:FormWindowState.Minimized; Snapshot(); };
    }
    private string Files=>Path.Combine(root,"fixture-files");
    private void PrepareFiles()
    {
        Directory.CreateDirectory(Path.Combine(Files,"ServerDesk folder"));
        File.WriteAllText(Path.Combine(Files,"ServerDesk żółć test.txt"),Sample,new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(Files,"ServerDesk folder","nested.txt"),"Nested RDP fixture");
        var binary=new byte[1024*1024+17]; for(var i=0;i<binary.Length;i++)binary[i]=(byte)(i*37+11);
        File.WriteAllBytes(Path.Combine(Files,"ServerDesk binary.bin"),binary);
    }
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Normalize(string text)=>text.Replace("\r\n","\n");
    private void Json(string file,object value)
    { var path=Path.Combine(root,file);File.WriteAllText(path+".part",JsonSerializer.Serialize(value,new JsonSerializerOptions { WriteIndented=true }));File.Move(path+".part",path,true); }
    private void Snapshot()=>Json("state.json",new { Role=role,Session=Process.GetCurrentProcess().SessionId,Pid=Environment.ProcessId,Mouse=new { Left=left,Right=right,DoubleClick=doubles,Wheel=wheel,Moves=moves,Selection=editor.SelectionLength },KeysDown=keysDown,TextLength=editor.Text.Length,PolishMatches=editor.Text.Trim()==Polish,ImeMatches=editor.Text.Trim()=="日本語",editor.ImeStarts,editor.ImeUpdates,editor.ImeEnds,ManualChecks=manual,InputLanguage=InputLanguage.CurrentInputLanguage.Culture.Name,InstalledInputs=InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().Select(l=>new { Language=l.Culture.Name,l.LayoutName }),UptimeSeconds=(int)lifetime.Elapsed.TotalSeconds });
    private void Tick()
    {
        if(role=="remote" && !awake)awake=SetThreadExecutionState(0x80000003)!=0;
        if(lifetime.Elapsed.TotalMinutes>90){Close();return;}
        if(lifetime.ElapsedMilliseconds-lastSnapshot>1000){Snapshot();lastSnapshot=lifetime.ElapsedMilliseconds;}
        var path=Path.Combine(root,"request.json"); if(!File.Exists(path) || new FileInfo(path).Length>4096)return;
        try
        {
            using var json=JsonDocument.Parse(File.ReadAllText(path));
            if(json.RootElement.ValueKind!=JsonValueKind.Object || !json.RootElement.TryGetProperty("Id",out var identity) || identity.ValueKind!=JsonValueKind.String || !json.RootElement.TryGetProperty("Operation",out var action) || action.ValueKind!=JsonValueKind.String)return;
            var id=identity.GetString()!;
            if(!Guid.TryParse(id,out _) || id==lastRequest)return;
            var operation=action.GetString()!; lastRequest=id;
            object value;
            try { value=Execute(operation); } catch(Exception ex) { value=new { Passed=false,Error=ex.GetType().Name }; }
            Json("response-"+id+".json",new { Id=id,Operation=operation,Result=value }); Snapshot();
        }
        catch(IOException) { } catch(JsonException) { }
    }
    private object Execute(string operation)
    {
        switch(operation)
        {
            case "text:set":Clipboard.SetText(Sample);return new { Passed=true };
            case "text:verify":return TextResult(Sample);
            case "large:set":Clipboard.SetText(string.Concat(Enumerable.Repeat(Sample,4096)));return new { Passed=true };
            case "large:verify":return TextResult(string.Concat(Enumerable.Repeat(Sample,4096)));
            case "sentinel:set":Clipboard.SetText("ServerDesk LOCAL SENTINEL");return new { Passed=true };
            case "sentinel:verify":return TextResult("ServerDesk LOCAL SENTINEL");
            case "empty:set":Clipboard.Clear();return new { Passed=true };
            case "empty:verify":return new { Passed=!Clipboard.ContainsText() && !Clipboard.ContainsImage() && !Clipboard.ContainsFileDropList(),Formats=Clipboard.GetDataObject()?.GetFormats(false)??[] };
            case "formats":return new { Passed=true,Formats=Clipboard.GetDataObject()?.GetFormats(false)??[] };
            case "rich:set":ClipboardFixtures.SetRich(Handle);return new { Passed=true };
            case "rich:verify":return ClipboardFixtures.VerifyRich();
            case "dib:set":ClipboardFixtures.SetImage("dib",64,32);return new { Passed=true };
            case "dib:verify":return ClipboardFixtures.VerifyImage("dib",64,32);
            case "dibv5:set":ClipboardFixtures.SetImage("dibv5",64,32);return new { Passed=true };
            case "dibv5:verify":return ClipboardFixtures.VerifyImage("dibv5",64,32);
            case "png:set":ClipboardFixtures.SetImage("png",64,32);return new { Passed=true };
            case "png:verify":return ClipboardFixtures.VerifyImage("png",64,32);
            case "image-large:set":ClipboardFixtures.SetImage("png",1024,768);return new { Passed=true };
            case "image-large:verify":return ClipboardFixtures.VerifyImage("png",1024,768);
            case "files:set":var files=new System.Collections.Specialized.StringCollection(); files.AddRange([Path.Combine(Files,"ServerDesk żółć test.txt"),Path.Combine(Files,"ServerDesk folder"),Path.Combine(Files,"ServerDesk binary.bin")]);Clipboard.SetFileDropList(files);return new { Passed=true };
            case "files:paste":return FileClipboard.Paste(root);
            case "editor:clear":editor.Clear();return new { Passed=true };
            case "editor:verify-polish":return new { Passed=editor.Text.Trim()==Polish,Keys=keysDown };
            case "editor:verify-ime":return new { Passed=editor.Text.Trim()=="日本語",LegacyCompositionObserved=editor.ImeStarts>0,editor.ImeStarts,editor.ImeUpdates,editor.ImeEnds };
            case "snapshot":Snapshot();return new { Passed=true };
            case "close":BeginInvoke((Action)Close);return new { Passed=true };
            default:throw new ArgumentException("Unknown fixed fixture operation");
        }
    }
    private object TextResult(string expected)
    { var value=Clipboard.GetText();return new { Passed=Normalize(value)==Normalize(expected),Length=value.Length,Sha256=Hash(value) }; }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern nint LoadKeyboardLayout(string id,uint flags);
    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
}

internal sealed class InputBox:RichTextBox
{
    internal int ImeStarts,ImeUpdates,ImeEnds;
    internal Func<bool>? PasteFiles;
    protected override bool ProcessCmdKey(ref Message message,Keys keyData)
    {
        if(keyData==(Keys.Control|Keys.V) && PasteFiles?.Invoke()==true)return true;
        return base.ProcessCmdKey(ref message,keyData);
    }
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x302)
        { try { if(PasteFiles?.Invoke()==true)return; } catch(ExternalException) { } }
        if(message.Msg==0x10d)ImeStarts++;if(message.Msg==0x10f)ImeUpdates++;if(message.Msg==0x10e)ImeEnds++;base.WndProc(ref message);
    }
}
