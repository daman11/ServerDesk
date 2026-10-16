using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ProtocolChecks
{
    private static T Field<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static Button Action(Window window,string name)=>window.GetVisualDescendants().OfType<Button>().Single(c=>AutomationProperties.GetName(c)==name);
    private static void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(); }
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        var edit=Dialogs.Edit(main,null,[]); RedesignChecks.Frames(); var editor=main.OwnedWindows.Single();
        Field<ComboBox>(editor,"ProfileProtocol").SelectedItem=Protocol.VNC; RedesignChecks.Frames();
        Field<TextBox>(editor,"ProfileName").Text="VNC desktop"; Field<TextBox>(editor,"ProfileHost").Text="127.0.0.1";
        if(Field<TextBox>(editor,"ProfilePort").Text!="5900" || Field<TextBox>(editor,"ProfileUsername").IsEffectivelyVisible)throw new Exception("VNC editor default port/login visibility incorrect");
        Field<ComboBox>(editor,"VncAuthentication").SelectedIndex=(int)VncAuthentication.None;
        editor.CaptureRenderedFrame()!.Save(Path.Combine(root,"vnc-editor.png")); Click(Action(editor,"Zapisz")); RedesignChecks.Wait(edit); var vncProfile=edit.Result!;
        if(vncProfile.Vnc.Authentication!=VncAuthentication.None || vncProfile.Protocol!=Protocol.VNC)throw new Exception("VNC editor lost settings");
        edit=Dialogs.Edit(main,null,[]); RedesignChecks.Frames(); editor=main.OwnedWindows.Single(); Field<ComboBox>(editor,"ProfileProtocol").SelectedItem=Protocol.Serial; RedesignChecks.Frames(); Field<TextBox>(editor,"ProfileName").Text="Switch console";
        Click(Action(editor,"Zapisz")); if(edit.IsCompleted || !Field<TextBox>(editor,"SerialPortName").Classes.Contains("error"))throw new Exception("Serial editor accepted empty device");
        Field<TextBox>(editor,"SerialPortName").Text="COM987654"; Field<TextBox>(editor,"SerialBaudRate").Text="0"; Click(Action(editor,"Zapisz")); if(edit.IsCompleted || !Field<TextBox>(editor,"SerialBaudRate").Classes.Contains("error"))throw new Exception("Serial editor accepted invalid baud");
        Field<TextBox>(editor,"SerialBaudRate").Text="9600"; Field<ComboBox>(editor,"SerialDataBits").SelectedItem=7; Field<ComboBox>(editor,"SerialParity").SelectedIndex=2; Field<ComboBox>(editor,"SerialStopBits").SelectedIndex=1; Field<ComboBox>(editor,"SerialHandshake").SelectedIndex=2;
        foreach(var theme in new[]{"dark","light"}) { DesignSystem.Apply(theme); editor.Width=600; editor.Height=540; RedesignChecks.Frames(); editor.CaptureRenderedFrame()!.Save(Path.Combine(root,"serial-editor-"+theme+".png")); }
        DesignSystem.Apply("dark"); Click(Action(editor,"Zapisz")); RedesignChecks.Wait(edit); var serial=edit.Result!;
        if(serial.Protocol!=Protocol.Serial || serial.Serial.BaudRate!=9600 || serial.Serial.DataBits!=7 || serial.Serial.Parity!=SerialParity.Even || serial.Serial.StopBits!=SerialStopBits.Two || serial.Serial.Handshake!=SerialHandshake.RequestToSend)throw new Exception("Serial editor lost line parameters");
        RedesignChecks.Wait(repo.SaveAsync(vncProfile)); RedesignChecks.Wait(repo.SaveAsync(serial)); RedesignChecks.Wait(((MainViewModel)main.DataContext!).LoadAsync()); RedesignChecks.Frames();
        foreach(var protocol in new[]{Protocol.Serial,Protocol.VNC})
        {
            Click(Action(main,"Filtr protokołu: "+protocol)); var entries=main.GetVisualDescendants().OfType<TreeViewItem>().Where(t=>t.Tag is ServerProfile).ToArray(); if(entries.Length!=1 || ((ServerProfile)entries[0].Tag!).Protocol!=protocol)throw new Exception("New protocol filter inconsistent");
        }
        Click(Action(main,"Filtr protokołu: Wszystkie"));
        Console.WriteLine("PASS VNC/Serial editor: protocol defaults, hidden network/login fields, required device/baud errors, full line parameters, dark/light small windows, persistence and filters");

        using var server=new VncTestServer();
        using var pane=new SessionPane(main,vncProfile with { Port=server.Port },new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane); RedesignChecks.Frames();
        RedesignChecks.Wait(pane.ConnectAsync("",CancellationToken.None)); var surface=main.GetVisualDescendants().OfType<VncSurface>().Single();
        Until(()=>surface.RenderedFrames>0); if(!pane.IsConnected || pane.VisualState!=SessionVisualState.Connected)throw new Exception("VNC session did not authenticate");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"vnc-desktop.png"));
        surface.Focus(); main.KeyTextInput("ż"); main.KeyPress(Key.LeftCtrl,RawInputModifiers.Control,PhysicalKey.ControlLeft,null); main.KeyPress(Key.A,RawInputModifiers.Control,PhysicalKey.A,"a"); main.KeyRelease(Key.A,RawInputModifiers.Control,PhysicalKey.A,"a"); main.KeyRelease(Key.LeftCtrl,RawInputModifiers.None,PhysicalKey.ControlLeft,null);
        var center=surface.TranslatePoint(new Point(surface.Bounds.Width/2,surface.Bounds.Height/2),main)!.Value;
        main.MouseDown(center,MouseButton.Left); main.MouseUp(center,MouseButton.Left); RedesignChecks.Frames();
        Until(()=>server.Keys.Any(k=>k.Symbol==0x0100017c) && server.Keys.Any(k=>k.Symbol==97 && k.Down) && server.Pointers.Any(p=>p==(48,32,1)));
        var shortcuts=server.Keys.Count(k=>k.Symbol==97 && k.Down);
        main.KeyPress(Key.LeftCtrl,RawInputModifiers.Control,PhysicalKey.ControlLeft,null); main.KeyPress(Key.RightAlt,RawInputModifiers.Control|RawInputModifiers.Alt,PhysicalKey.AltRight,null); main.KeyPress(Key.A,RawInputModifiers.Control|RawInputModifiers.Alt,PhysicalKey.A,"ą"); main.KeyTextInput("ą"); main.KeyRelease(Key.A,RawInputModifiers.Control|RawInputModifiers.Alt,PhysicalKey.A,"ą");
        main.KeyPress(Key.LeftShift,RawInputModifiers.Control|RawInputModifiers.Alt|RawInputModifiers.Shift,PhysicalKey.ShiftLeft,null); main.KeyPress(Key.N,RawInputModifiers.Control|RawInputModifiers.Alt|RawInputModifiers.Shift,PhysicalKey.N,"Ń"); main.KeyTextInput("Ń"); main.KeyRelease(Key.N,RawInputModifiers.None,PhysicalKey.N,"Ń"); main.KeyRelease(Key.LeftShift,RawInputModifiers.None,PhysicalKey.ShiftLeft,null); main.KeyRelease(Key.RightAlt,RawInputModifiers.None,PhysicalKey.AltRight,null); main.KeyRelease(Key.LeftCtrl,RawInputModifiers.None,PhysicalKey.ControlLeft,null);
        Until(()=>server.Keys.Any(k=>k.Symbol==0x01000105) && server.Keys.Any(k=>k.Symbol==0x01000143)); if(server.Keys.Count(k=>k.Symbol==97 && k.Down)!=shortcuts || main.OwnedWindows.Count>0)throw new Exception("AltGr text became a Control or host-application shortcut");
        Click(Action(main,"Ctrl+Alt+Del")); Until(()=>server.Keys.Any(k=>k.Symbol==0xffff && k.Down));
        surface.ViewOnly=true; var before=server.Keys.Count; main.KeyTextInput("blocked"); RedesignChecks.Frames(); if(server.Keys.Count!=before)throw new Exception("View-only VNC sent keyboard input"); surface.ViewOnly=false;
        var second=new SessionPane(main,serial,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(second); main.SetWorkspaceMode(WorkspaceMode.Columns); RedesignChecks.Frames(); if(!pane.IsConnected)throw new Exception("Split disconnected VNC");
        main.DockSession(pane,SessionDock.Floating); RedesignChecks.Frames(); main.OwnedWindows.Single().CaptureRenderedFrame()!.Save(Path.Combine(root,"vnc-floating.png")); main.OwnedWindows.Single().Close(); RedesignChecks.Frames(); if(!pane.IsConnected)throw new Exception("Docking disconnected VNC");
        server.Drop(); Until(()=>!pane.IsConnected); RedesignChecks.Frames(); if(pane.VisualState is not (SessionVisualState.Closed or SessionVisualState.Failed))throw new Exception("VNC network loss retained connected state");
        using(var activeServer=new VncTestServer())
        using(var activePane=new SessionPane(main,vncProfile with { Port=activeServer.Port },new SshConnectionFactory(repo),new ExternalRdpClient(),repo))
        {
            var activeWindow=new Window { Width=800,Height=550,Content=activePane }; activeWindow.Show(); RedesignChecks.Frames();
            RedesignChecks.Wait(activePane.ConnectAsync("",CancellationToken.None)); Click(Action(activeWindow,"Rozłącz")); Until(()=>activePane.VisualState==SessionVisualState.Closed);
            RedesignChecks.Wait(activePane.CloseAsync()); activeWindow.Close();
        }
        var missing=serial with { Serial=serial.Serial with { PortName=OperatingSystem.IsWindows()?"COM987654":"/dev/serverdesk-test-nonexistent" } };
        using var unavailable=new SessionPane(main,missing,new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
        try { RedesignChecks.Wait(unavailable.ConnectAsync("",CancellationToken.None)); throw new Exception("Missing serial device unexpectedly opened"); } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
        if(unavailable.IsConnected || unavailable.IsBusy || unavailable.VisualState!=SessionVisualState.Failed)throw new Exception("Unavailable Serial left busy/connected state");
        RedesignChecks.Wait(pane.CloseAsync()); RedesignChecks.Wait(second.CloseAsync());
        main.Close(); RedesignChecks.Frames(); Click(Action(main.OwnedWindows.Single(),"Potwierdź")); RedesignChecks.Frames();
        Console.WriteLine("PASS actual loopback VNC pixels, Unicode/Control keyboard, pointer, view-only block, split/floating/redock continuity, network loss, live disconnect, missing serial device recovery and cleanup");
    }
    private static void Until(Func<bool> condition) { var until=DateTime.UtcNow.AddSeconds(12); while(!condition() && DateTime.UtcNow<until)RedesignChecks.Frames(1); if(!condition())throw new TimeoutException("Protocol UI check timed out"); }
}
