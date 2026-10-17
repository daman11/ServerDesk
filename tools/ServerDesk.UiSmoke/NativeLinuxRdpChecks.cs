using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using SkiaSharp;

internal static class NativeLinuxRdpChecks
{
    private const string Password="serverdesk-disposable-rdp";
    private sealed class Vault:ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<string?>(null);
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new Exception("Native fixture must not save credentials");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>Task.CompletedTask;
    }
    private static LinuxRdpHost Host(SessionPane pane)=>(LinuxRdpHost)typeof(SessionPane).GetField("linuxRdp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static void Click(Window window,string tag)=>window.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task Until(Func<bool> ready,string message)
    { var deadline=DateTime.UtcNow.AddSeconds(20); while(!ready() && DateTime.UtcNow<deadline)await Task.Delay(80); if(!ready())throw new TimeoutException(message); }
    internal static void Run(string root,int port)
    {
        if(!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("SERVERDESK_DISPOSABLE_RDP_LAB")!="1")throw new InvalidOperationException("Requires the owned loopback RDP/Xvfb lab");
        var expected=RdpCertificateFingerprint.Normalize(Environment.GetEnvironmentVariable("SERVERDESK_RDP_LAB_SHA256")!);
        var serverDisplay=Environment.GetEnvironmentVariable("SERVERDESK_RDP_LAB_SERVER_DISPLAY")??throw new Exception("No private server display");
        var paths=new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Task.Run(()=>repo.InitializeAsync()).GetAwaiter().GetResult();
        using var surface=new Surface(serverDisplay); using var screen=new Screen();
        var main=new MainWindow(new MainViewModel(repo),repo,new Vault(),new SshConnectionFactory(repo),new ExternalRdpClient());
        using var done=new CancellationTokenSource(); Exception? failure=null;
        var pump=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(50) }; pump.Tick+=(_,_)=>surface.Pump();
        main.Show(); pump.Start();
        Dispatcher.UIThread.Post(async()=>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new AvaloniaSynchronizationContext(Dispatcher.UIThread,DispatcherPriority.Normal));
                await Until(()=>typeof(MainWindow).GetField("workspaceReady",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(main) is true,"Main initialization did not complete");
                var profile=new ServerProfile { Name="Owned RDP desktop",Protocol=Protocol.RDP,Host="127.0.0.1",Port=port,Username="LAB\\rdp_fixture",Rdp=new() { Width=1024,Height=768,DynamicResolution=true } };
                async Task<SessionPane> Connect(string password,bool cancel=false)
                {
                    await repo.SaveAsync(profile); var work=main.Safe(()=>main.ConnectAsync(profile));
                    await Until(()=>main.OwnedWindows.Count>0,"RDP GUI login did not open"); var login=main.OwnedWindows.Single();
                    var input=login.GetVisualDescendants().OfType<TextBox>().Single(); if(input.PasswordChar=='\0')throw new Exception("Native password is not masked"); input.Text=password;
                    screen.Save(main.TryGetPlatformHandle()!.Handle,Path.Combine(root,"native-login.png")); Click(login,"Połącz");
                    await Until(()=>main.OwnedWindows.Count>0 || work.IsCompleted,"Certificate GUI did not open");
                    var certificate=main.OwnedWindows.Single(); if(certificate.Name!="RdpCertificateDialog")throw new Exception("Expected certificate GUI: "+certificate.Title+" "+string.Join(" | ",certificate.GetVisualDescendants().OfType<TextBlock>().Select(t=>t.Text)));
                    if(!certificate.GetVisualDescendants().OfType<SelectableTextBlock>().Any(t=>t.Text==RdpCertificateFingerprint.Display(expected)))throw new Exception("Lab certificate does not match owned fixture");
                    screen.Save(certificate.TryGetPlatformHandle()!.Handle,Path.Combine(root,"native-certificate.png"));
                    if(cancel)Click(certificate,"Anuluj"); else { certificate.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=true; Click(certificate,"Zaufaj w tej karcie"); }
                    await work; return main.OpenSessions.Last();
                }
                var cancelled=await Connect(Password,true);
                if(cancelled.HasActiveClient || cancelled.EmbeddedRdpHandle!=0)throw new Exception("Certificate cancellation created a native child");
                main.RemovePane(cancelled); await cancelled.CloseAsync(); Console.WriteLine("PASS native RDP: GUI login and exact certificate; cancel starts no child");
                var wrong=await Connect("wrong-disposable-fixture"); await Until(()=>!wrong.HasActiveClient && wrong.VisualState==SessionVisualState.Failed,"Wrong password was not rejected");
                if(wrong.IsRdpLoggedIn || (await repo.HistoryAsync(profileId:profile.Id)).Count!=0)throw new Exception("Failed login recorded an authenticated desktop");
                main.RemovePane(wrong); await wrong.CloseAsync(); Console.WriteLine("PASS real NLA rejects wrong fixture password; GUI failure without CLI or successful history");
                var pane=await Connect(Password); await Until(()=>pane.IsRdpLoggedIn || pane.VisualState==SessionVisualState.Failed,"Desktop did not start");
                if(!pane.IsRdpLoggedIn)throw new Exception("Fixture login failed: "+pane.ConnectionStatus);
                var host=Host(pane); var pid=host.ProcessId; var handle=host.WindowHandle; var desktop=host.DesktopHandle;
                if(handle==0 || desktop==0 || !screen.Descendant(desktop,main.TryGetPlatformHandle()!.Handle))throw new Exception("RDP desktop is not inside ServerDesk");
                if(File.ReadAllText($"/proc/{pid}/cmdline").Contains(Password))throw new Exception("RDP password leaked into argv");
                await Task.Delay(700); screen.Save(main.TryGetPlatformHandle()!.Handle,Path.Combine(root,"native-embedded-desktop.png"));
                surface.Focus(); host.FocusDesktop(); var keys=surface.Keys; screen.Key("a"); await Until(()=>surface.Keys>keys,"RDP keyboard did not reach isolated server screen");
                var buttons=surface.Buttons; screen.Click(desktop); await Until(()=>surface.Buttons>buttons,"RDP mouse did not reach isolated server screen");
                Console.WriteLine("PASS actual FreeRDP/NLA desktop: child belongs to owned process and ServerDesk, no password in argv; keyboard and mouse reach separate private Xvfb server");
                void Same()
                { if(Host(pane).ProcessId!=pid || Host(pane).WindowHandle!=handle || Host(pane).DesktopHandle!=desktop || !pane.IsRdpLoggedIn)throw new Exception("Docking replaced the RDP session"); }
                using var idle=new SessionPane(main,new() { Name="Idle workspace fixture",Protocol=Protocol.Serial },new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(idle);
                main.FocusSession(idle); await Task.Delay(800); main.FocusSession(pane); await Task.Delay(500); Same();
                main.SetWorkspaceMode(WorkspaceMode.Columns); await Task.Delay(500); Same(); main.Width=1000; main.Height=680; await Task.Delay(600); Same();
                screen.Save(main.TryGetPlatformHandle()!.Handle,Path.Combine(root,"native-split-desktop.png"));
                main.DockSession(pane,SessionDock.Floating); await Task.Delay(800); Same(); var floating=main.OwnedWindows.Single();
                if(!screen.Descendant(desktop,floating.TryGetPlatformHandle()!.Handle))throw new Exception("Floating desktop did not move inside the new owner");
                main.DockSession(pane,SessionDock.Primary); await Task.Delay(600); Same();
                main.SetWorkspaceMode(WorkspaceMode.Grid); await Task.Delay(400); Same(); main.SetWorkspaceMode(WorkspaceMode.Single); await Task.Delay(400); Same();
                main.RemovePane(idle); await idle.CloseAsync(); main.FocusSession(pane); host.FocusDesktop(); screen.ReleaseShortcut();
                await Until(()=>!screen.Descendant(screen.Focused(),desktop),"Local release shortcut did not return focus");
                Console.WriteLine("PASS same authenticated PID/XIDs across inactive tab, split, resize, floating/redock and four panels; Ctrl+Alt+Right returns focus locally");
                var oldPid=pid; var reconnect=pane.ReconnectAsync(); await reconnect; await Until(()=>pane.IsRdpLoggedIn || pane.VisualState==SessionVisualState.Failed,"Reconnect did not finish");
                if(!pane.IsRdpLoggedIn || Host(pane).ProcessId==oldPid || Directory.Exists($"/proc/{oldPid}"))throw new Exception("Reconnect retained the old child or failed authentication");
                var lastPid=Host(pane).ProcessId; screen.Save(main.TryGetPlatformHandle()!.Handle,Path.Combine(root,"native-reconnected.png"));
                main.Close(); await Until(()=>main.OwnedWindows.Count>0,"Shutdown confirmation missing"); Click(main.OwnedWindows.Single(),"Potwierdź"); await Until(()=>!main.IsVisible,"App did not finish native RDP shutdown");
                if(Directory.Exists($"/proc/{lastPid}"))throw new Exception("RDP child survived application shutdown");
                Console.WriteLine("PASS reconnect reuses certificate consent in this tab, replaces owned child, and confirmed application shutdown drains native RDP process");
            }
            catch(Exception ex) { failure=ex; }
            finally
            {
                foreach(var pane in main.OpenSessions.ToArray()) { main.RemovePane(pane); await pane.CloseAsync(); }
                main.Close(); pump.Stop(); done.Cancel();
            }
        });
        Dispatcher.UIThread.MainLoop(done.Token); if(failure!=null)throw failure;
    }
    private sealed class Surface:IDisposable
    {
        private readonly nint display,window,gc;
        internal int Keys,Buttons;
        internal Surface(string name)
        {
            display=X.XOpenDisplay(name); if(display==0)throw new Exception("Private server Xvfb unavailable");
            window=X.XCreateSimpleWindow(display,X.XDefaultRootWindow(display),0,0,1024,768,0,0,0x164a58); gc=X.XCreateGC(display,window,0,0);
            X.XSelectInput(display,window,1|4|32768); X.XMapWindow(display,window); X.XSetForeground(display,gc,0xffffff); X.XFlush(display);
        }
        internal void Focus() { X.XSetInputFocus(display,window,2,0); X.XFlush(display); }
        internal void Pump()
        {
            while(X.XPending(display)>0) { X.XNextEvent(display,out var e); if(e.Type==2)Keys++; if(e.Type==4)Buttons++; }
            var text=$"ServerDesk disposable RDP fixture | keyboard {Keys} | mouse {Buttons}"; X.XClearWindow(display,window); X.XDrawString(display,window,gc,32,70,text,text.Length); X.XFlush(display);
        }
        public void Dispose() { X.XFreeGC(display,gc); X.XDestroyWindow(display,window); X.XCloseDisplay(display); }
    }
    private sealed class Screen:IDisposable
    {
        private readonly nint display=X.XOpenDisplay(null);
        internal Screen() { if(display==0)throw new Exception("Private client Xvfb unavailable"); }
        internal void Save(nint window,string path)
        {
            X.XSync(display,false); if(X.XGetGeometry(display,window,out _,out _,out _,out var width,out var height,out _,out _)==0 || width==0 || height==0 || width>4096 || height>4096)throw new Exception("Invalid own window capture dimensions");
            var image=X.XGetImage(display,window,0,0,width,height,nuint.MaxValue,2); if(image==0)throw new Exception("Own window capture failed");
            try
            {
                using var bitmap=new SKBitmap((int)width,(int)height,SKColorType.Bgra8888,SKAlphaType.Opaque); var pixels=new int[checked((int)(width*height))];
                for(var y=0;y<height;y++)for(var x=0;x<width;x++)pixels[checked((int)(y*width+x))]=unchecked((int)(0xff000000|X.XGetPixel(image,(int)x,(int)y)));
                Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length); using var file=File.Create(path); bitmap.Encode(file,SKEncodedImageFormat.Png,100);
            }
            finally { X.XDestroyImage(image); }
        }
        internal bool Descendant(nint window,nint ancestor)
        {
            for(var i=0;i<32 && window!=0;i++) { if(window==ancestor)return true; if(X.XQueryTree(display,window,out _,out var parent,out var children,out _)==0)return false; if(children!=0)X.XFree(children); window=parent; } return false;
        }
        internal nint Focused() { X.XGetInputFocus(display,out var focus,out _); return focus; }
        private void Press(string name,bool down) { var code=X.XKeysymToKeycode(display,X.XStringToKeysym(name)); X.XTestFakeKeyEvent(display,code,down,0); }
        internal void Key(string name) { Press(name,true); Press(name,false); X.XFlush(display); }
        internal void ReleaseShortcut() { Press("Control_L",true); Press("Alt_L",true); Press("Right",true); Press("Right",false); Press("Alt_L",false); Press("Control_L",false); X.XFlush(display); }
        internal void Click(nint window)
        { X.XTranslateCoordinates(display,window,X.XDefaultRootWindow(display),70,70,out var x,out var y,out _); X.XTestFakeMotionEvent(display,-1,x,y,0); X.XTestFakeButtonEvent(display,1,true,0); X.XTestFakeButtonEvent(display,1,false,0); X.XFlush(display); }
        public void Dispose()=>X.XCloseDisplay(display);
    }
    private static class X
    {
        private const string Library="libX11.so.6";
        [StructLayout(LayoutKind.Explicit,Size=192)] internal struct Event { [FieldOffset(0)] internal int Type; }
        [DllImport(Library)] internal static extern nint XOpenDisplay(string? name);
        [DllImport(Library)] internal static extern int XCloseDisplay(nint display);
        [DllImport(Library)] internal static extern nint XDefaultRootWindow(nint display);
        [DllImport(Library)] internal static extern nint XCreateSimpleWindow(nint display,nint parent,int x,int y,uint width,uint height,uint border,nuint borderPixel,nuint background);
        [DllImport(Library)] internal static extern nint XCreateGC(nint display,nint window,nuint mask,nint values);
        [DllImport(Library)] internal static extern int XSetForeground(nint display,nint gc,nuint pixel);
        [DllImport(Library)] internal static extern int XSelectInput(nint display,nint window,nint mask);
        [DllImport(Library)] internal static extern int XMapWindow(nint display,nint window);
        [DllImport(Library)] internal static extern int XClearWindow(nint display,nint window);
        [DllImport(Library)] internal static extern int XDrawString(nint display,nint window,nint gc,int x,int y,string text,int length);
        [DllImport(Library)] internal static extern int XSetInputFocus(nint display,nint window,int revert,nuint time);
        [DllImport(Library)] internal static extern int XGetInputFocus(nint display,out nint window,out int revert);
        [DllImport(Library)] internal static extern int XPending(nint display);
        [DllImport(Library)] internal static extern int XNextEvent(nint display,out Event input);
        [DllImport(Library)] internal static extern int XFreeGC(nint display,nint gc);
        [DllImport(Library)] internal static extern int XDestroyWindow(nint display,nint window);
        [DllImport(Library)] internal static extern int XGetGeometry(nint display,nint window,out nint root,out int x,out int y,out uint width,out uint height,out uint border,out uint depth);
        [DllImport(Library)] internal static extern nint XGetImage(nint display,nint window,int x,int y,uint width,uint height,nuint planes,int format);
        [DllImport(Library)] internal static extern nuint XGetPixel(nint image,int x,int y);
        [DllImport(Library)] internal static extern int XDestroyImage(nint image);
        [DllImport(Library)] internal static extern int XQueryTree(nint display,nint window,out nint root,out nint parent,out nint children,out uint count);
        [DllImport(Library)] internal static extern int XFree(nint pointer);
        [DllImport(Library)] internal static extern int XFlush(nint display);
        [DllImport(Library)] internal static extern int XSync(nint display,bool discard);
        [DllImport(Library)] internal static extern nint XStringToKeysym(string name);
        [DllImport(Library)] internal static extern byte XKeysymToKeycode(nint display,nint symbol);
        [DllImport(Library)] internal static extern int XTranslateCoordinates(nint display,nint source,nint destination,int x,int y,out int translatedX,out int translatedY,out nint child);
        [DllImport("libXtst.so.6")] internal static extern int XTestFakeKeyEvent(nint display,uint key,bool down,nuint delay);
        [DllImport("libXtst.so.6")] internal static extern int XTestFakeMotionEvent(nint display,int screen,int x,int y,nuint delay);
        [DllImport("libXtst.so.6")] internal static extern int XTestFakeButtonEvent(nint display,uint button,bool down,nuint delay);
    }
}
