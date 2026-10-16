using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.IO.Pipes;
using System.Text.Json;

namespace ServerDesk.App;

// An isolated, opt-in workbench for real clipboard/input tests. No terminal,
// server settings, user documents or credentials are included in its report.
internal static class RdpInteractionCheck
{
    private sealed record MatrixResult(string Name,bool Pass);
    internal const string Sample="ServerDesk RDP – żółć ąęćłńóśźż\r\nEmoji 😀 📁\r\nTab\t123\r\n";
    internal static int Run(string[] args)
    {
        if(!OperatingSystem.IsWindows() || args.Length is not (5 or 6))return 2;
        var report=Path.GetFullPath(args[1]); var root=Path.GetDirectoryName(report)!; Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("SERVERDESK_DATA_HOME",Path.Combine(root,"interaction-data-"+Guid.NewGuid().ToString("N")));
        var checks=new List<MatrixResult>(); Exception? error=null;
        Program.BuildAvaloniaApp().SetupWithoutStarting();
        var paths=new AppPaths(); var repo=new ProfileRepository(paths); repo.InitializeAsync().GetAwaiter().GetResult();
        var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()) { Title="ServerDesk — RDP matrix test" };
        using var proxy=args.Length==6 && args[5]=="--proxy"?new RdpFaultProxy(args[2]):null;
        var profile=new ServerProfile { Name="RDP matrix test",Protocol=Protocol.RDP,Host=proxy==null?args[2]:"127.0.0.1",Port=proxy?.Port??3389,Username=args[3],Rdp=new() { DisplayMode=RdpDisplayMode.Embedded,Clipboard=true,KeyboardMode=RdpKeyboardMode.Remote,AutoReconnect=true } };
        var pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
        using var done=new CancellationTokenSource(); using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(20));
        var clipboard=main.Clipboard!;
        var panel=new WrapPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(8),Name="RdpMatrixControls" };
        var note=DesignSystem.Label("Macierz RDP: dane syntetyczne; żadne treści schowka nie są zapisywane.",true,12);
        var frame=new Grid { RowDefinitions=new RowDefinitions("Auto,Auto,*") }; frame.Children.Add(panel); Grid.SetRow(note,1); frame.Children.Add(note);
        var normal=(Control)main.Content!; main.Content=null; Grid.SetRow(normal,2); frame.Children.Add(normal); main.Content=frame;
        var imageFixtures=new List<Bitmap>();
        string? expected=null; var started=false; var finished=false; var networkPending=false;
        var testFiles=Path.Combine(root,"clipboard-fixtures"); Directory.CreateDirectory(testFiles);
        var textFile=Path.Combine(testFiles,"ServerDesk żółć test.txt"); File.WriteAllText(textFile,Sample,new System.Text.UTF8Encoding(false));
        var folder=Directory.CreateDirectory(Path.Combine(testFiles,"ServerDesk folder")).FullName; File.WriteAllText(Path.Combine(folder,"nested.txt"),"Nested RDP fixture");
        void Record(string name,bool pass) { checks.Add(new(name,pass)); note.Text=$"{name}: {(pass?"PASS":"FAIL / brak potwierdzenia")}"; Save(); }
        void Save()
        {
            if(finished)return; var host=pane.RdpHost;
            File.WriteAllText(report,JsonSerializer.Serialize(new { Version=typeof(App).Assembly.GetName().Version?.ToString(),Authenticated=pane.IsRdpLoggedIn,Checks=checks,Screens=main.Screens.All.Select(s=>new { s.Scaling,Bounds=s.Bounds.ToString() }),CurrentScaling=TopLevel.GetTopLevel(pane)?.RenderScaling,RequestedDisplay=host?.LastDisplaySettings,DisplayError=host?.LastDisplayError,Viewport=host?.Bounds.ToString(),RemoteSize=host?.RemoteDesktopSize.ToString(),Error=error?.GetType().Name },new JsonSerializerOptions { WriteIndented=true }));
        }
        void Button(string label,Func<Task> action)=>panel.Children.Add(Dialogs.Button(label,()=>_ = main.Safe(action)));
        Button("Tekst → schowek",async()=> { expected=Sample; await clipboard.SetTextAsync(expected); note.Text="Wklej próbkę w nowym dokumencie zdalnego Notatnika."; });
        Button("Duży tekst → schowek",async()=> { expected=string.Concat(Enumerable.Repeat(Sample,4096)); await clipboard.SetTextAsync(expected); note.Text="Duży tekst syntetyczny gotowy do wklejenia."; });
        Button("Sentinel lokalny",async()=> { await clipboard.SetTextAsync("ServerDesk LOCAL SENTINEL"); note.Text="Teraz skopiuj tekst z zdalnego Notatnika i sprawdź wynik."; });
        Button("Sprawdź blokadę schowka",async()=> { Record("Clipboard OFF remote copy leaves local sentinel",await clipboard.TryGetTextAsync()=="ServerDesk LOCAL SENTINEL"); });
        Button("Sprawdź tekst ← RDP",async()=> { var value=await clipboard.TryGetTextAsync(); Record($"Text remote→local ({expected?.Length??0} UTF-16)",expected!=null && value?.Replace("\r\n","\n")==expected.Replace("\r\n","\n")); });
        Button("Obraz → schowek",async()=>
        {
            var bitmap=new WriteableBitmap(new PixelSize(64,32),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Opaque); imageFixtures.Add(bitmap);
            using(var data=bitmap.Lock()) { var pixels=new byte[data.RowBytes*32]; for(var y=0;y<32;y++)for(var x=0;x<64;x++) { var at=y*data.RowBytes+x*4; pixels[at]=(byte)(x<32?0:255); pixels[at+1]=64; pixels[at+2]=(byte)(x<32?255:0); pixels[at+3]=255; } System.Runtime.InteropServices.Marshal.Copy(pixels,0,data.Address,pixels.Length); }
            await clipboard.SetBitmapAsync(bitmap); note.Text="Obraz 64×32: czerwona/lewa i niebieska/prawa połowa, zielony=64.";
        });
        Button("Sprawdź obraz ← RDP",async()=>
        {
            using var bitmap=await clipboard.TryGetBitmapAsync(); var pass=bitmap?.PixelSize==new PixelSize(64,32);
            if(pass)
            {
                var buffer=new byte[64*32*4]; var pin=System.Runtime.InteropServices.GCHandle.Alloc(buffer,System.Runtime.InteropServices.GCHandleType.Pinned);
                try { bitmap!.CopyPixels(new PixelRect(0,0,64,32),pin.AddrOfPinnedObject(),buffer.Length,64*4); }
                finally { pin.Free(); }
                for(var y=0;y<32;y++)for(var x=0;x<64;x++) { var at=(y*64+x)*4; pass&=buffer[at]==(x<32?0:255) && buffer[at+1]==64 && buffer[at+2]==(x<32?255:0); }
            }
            Record("Image remote→local 64×32 pixels",pass);
        });
        Button("Pliki → schowek",()=>pane.PrepareRdpFilesAsync([textFile,folder]));
        void ObserveHost()
        {
            var host=pane.RdpHost!;
            host.FocusReleased+=()=>Record("Ctrl+Alt+arrow focus release event",true);
            host.Reconnecting+=attempt=>Record($"Network auto reconnect attempt {attempt}",true);
            host.Reconnected+=()=> { if(networkPending) { networkPending=false; Record("Network auto reconnected (OnAutoReconnected)",true); } };
        }
        Button("Schowek OFF + reconnect",async()=> { var check=pane.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Content as string=="Schowek RDP"); check.IsChecked=false; await pane.ReconnectAsync(); ObserveHost(); });
        Button("Schowek ON + reconnect",async()=> { var check=pane.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Content as string=="Schowek RDP"); check.IsChecked=true; await pane.ReconnectAsync(); ObserveHost(); });
        if(proxy!=null)Button("Zerwij sieć na 8s",()=> { if(!pane.IsRdpLoggedIn)throw new InvalidOperationException("Najpierw zaloguj sesję."); networkPending=true; proxy.Reset(TimeSpan.FromSeconds(8)); return Task.CompletedTask; });
        Button("Zapisz stan",()=> { Save(); return Task.CompletedTask; });
        Button("Zakończ test",()=> { Save(); finished=true; main.RemovePane(pane); main.Close(); done.Cancel(); return Task.CompletedTask; });
        main.Opened+=async (_,_)=>
        {
            try
            {
                using var pipe=new NamedPipeClientStream(".",args[4],PipeDirection.In,PipeOptions.Asynchronous);
                await pipe.ConnectAsync(limit.Token); using var input=new StreamReader(pipe);
                var password=await input.ReadLineAsync(limit.Token)??throw new IOException("Missing credential pipe.");
                await repo.InitializeAsync(); main.RegisterPane(pane); await pane.ConnectAsync(password,limit.Token); password="";
                ObserveHost(); started=true;
                foreach(var path in new[]{textFile,folder})
                {
                    IStorageItem item=Directory.Exists(path)?(await main.StorageProvider.TryGetFolderFromPathAsync(new Uri(path)))!:(await main.StorageProvider.TryGetFileFromPathAsync(new Uri(path)))!;
                    var source=new Border { Padding=new Thickness(10),Child=new TextBlock { Text=Directory.Exists(path)?"Przeciągnij folder":"Przeciągnij plik" } };
                    DragSupport.Source(source,DataFormat.File,()=>item); panel.Children.Add(source);
                }
            }
            catch(Exception ex) { error=ex; Save(); pane.Dispose(); done.Cancel(); }
        };
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };
        timer.Tick+=(_,_)=> { if(started)Save(); if(limit.IsCancellationRequested || !main.IsVisible) { timer.Stop(); done.Cancel(); } };
        try { main.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(done.Token); }
        finally { timer.Stop(); Save(); if(main.OpenSessions.Contains(pane))main.RemovePane(pane); main.Close(); foreach(var image in imageFixtures)image.Dispose(); }
        return error==null && checks.All(c=>c.Pass)?0:1;
    }
}

