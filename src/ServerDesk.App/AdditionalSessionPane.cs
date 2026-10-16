using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Diagnostics;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private SerialTerminalSession? serialSession;
    private VncSession? vncSession;
    private VncSurface? vncSurface;
    private Border? additionalNotice;
    private readonly List<Control> additionalActions=[];
    private void BuildAdditionalControls(WrapPanel actions)
    {
        if(Profile.Protocol==Protocol.VNC)
        {
            vncSurface=new() { FitToWindow=Profile.Vnc.FitToWindow,ViewOnly=Profile.Vnc.ViewOnly };
            vncSurface.EscapeFocus+=()=>connectButton.Focus();
            var viewport=new ScrollViewer { Content=vncSurface,HorizontalScrollBarVisibility=Profile.Vnc.FitToWindow?Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled:Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=Profile.Vnc.FitToWindow?Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled:Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
            Grid.SetRow(viewport,2); Children.Add(viewport);
            var fit=new CheckBox { Content=L.T("Dopasuj"),IsChecked=Profile.Vnc.FitToWindow }; fit.IsCheckedChanged+=(_,_)=> { vncSurface.FitToWindow=fit.IsChecked==true; viewport.HorizontalScrollBarVisibility=viewport.VerticalScrollBarVisibility=fit.IsChecked==true?Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled:Avalonia.Controls.Primitives.ScrollBarVisibility.Auto; }; actions.Children.Add(fit);
            var readOnly=new CheckBox { Content=L.T("Tylko podgląd"),IsChecked=Profile.Vnc.ViewOnly }; readOnly.IsCheckedChanged+=(_,_)=> { vncSurface.ViewOnly=readOnly.IsChecked==true; UpdateAdditionalActions(); }; actions.Children.Add(readOnly);
            var secureAttention=Dialogs.Button("Ctrl+Alt+Del",()=>vncSurface.SendCtrlAltDelete()); additionalActions.Add(secureAttention); actions.Children.Add(secureAttention);
        }
        var disconnect=Button("Rozłącz",DisconnectAdditionalAsync); additionalActions.Add(disconnect); actions.Children.Add(disconnect);
        var options=new Button { Content=DeskIcons.Create("more"),Classes={"chrome","icon"},Width=30 }; optionsButton=options;
        var choices=new List<MenuItem> { OperationMenu("Uruchom ponownie sesję",()=>owner.ReconnectAsync(this)) };
        if(Profile.Protocol==Protocol.VNC && Profile.Vnc.Authentication==VncAuthentication.Password)choices.Add(OperationMenu("Poświadczenia",()=>owner.ReauthenticateAsync(this)));
        if(Profile.Protocol==Protocol.Serial) { choices.Add(OperationMenu("Kopiuj zaznaczenie",()=>terminal.CopyAsync())); choices.Add(OperationMenu("Wklej",()=>terminal.PasteAsync())); }
        options.ContextMenu=new ContextMenu { ItemsSource=choices }; options.Click+=(_,_)=>options.ContextMenu.Open(options); Avalonia.Automation.AutomationProperties.SetName(options,L.T("Opcje sesji")); actions.Children.Add(options);
        var message=Profile.Protocol==Protocol.VNC?L.T("Pulpit VNC w tej zakładce\nMysz i klawiatura sterują serwerem. Ctrl+Alt+→ oddaje fokus do ServerDesk."):L.T("Terminal szeregowy\n")+Profile.Serial.Summary+L.T("\nPort zostanie otwarty po wybraniu Połącz.");
        var hint=DesignSystem.Label(message,true); hint.TextWrapping=Avalonia.Media.TextWrapping.Wrap;
        var title=Profile.Protocol==Protocol.VNC?L.T("Pulpit VNC"):L.T("Port szeregowy");
        additionalNotice=DesignSystem.Card(new StackPanel { Spacing=16,Children={DesignSystem.PageHeading(ProfileIcons.Resolve(Profile),title),hint} });
        additionalNotice.Margin=new Thickness(24); additionalNotice.MaxWidth=560; additionalNotice.VerticalAlignment=VerticalAlignment.Center;
        Grid.SetRow(additionalNotice,2); Children.Add(additionalNotice);
    }
    private void UpdateAdditionalActions()
    { foreach(var action in additionalActions)action.IsEnabled=!connecting && IsConnected && (action is not Button b || b.Tag as string!="Ctrl+Alt+Del" || vncSurface?.ViewOnly==false); }
    private async Task ConnectAdditionalAsync(CancellationToken ct)
    {
        Task<TerminalCompletion> completion; object observed;
        if(Profile.Protocol==Protocol.VNC)
        { terminal.IsVisible=false; vncSession=await VncSession.ConnectAsync(Profile,secret,ct); ct.ThrowIfCancellationRequested(); vncSurface!.Attach(vncSession); vncSurface.Focus(); completion=vncSession.Completion; observed=vncSession; }
        else
        {
            serialSession=await SerialTerminalSession.OpenAsync(Profile.Serial,ct); ct.ThrowIfCancellationRequested();
            terminal.IsVisible=true; terminal.ApplyTemplate(); terminal.Terminal.Reset(); terminal.AttachConnection(serialSession); terminal.Focus(); completion=serialSession.Completion; observed=serialSession;
        }
        if(additionalNotice!=null)additionalNotice.IsVisible=false;
        connectionStarted=Stopwatch.GetTimestamp(); SetStatus(L.F($"{Profile.Protocol} · {Profile.Endpoint} · połączono"),"●");
        // Register after the connected presentation so an already completed
        // transport cannot have its terminal state overwritten by Connected.
        _=ObserveAdditionalAsync(completion,observed);
    }
    private async Task ObserveAdditionalAsync(Task<TerminalCompletion> completion,object observed)
    {
        var result=await completion;
        await Dispatcher.UIThread.InvokeAsync(()=>
        {
            if(disposed!=0 || !ReferenceEquals(observed,(object?)vncSession??serialSession))return;
            if(additionalNotice!=null)additionalNotice.IsVisible=true;
            if(observed is VncSession) { vncSurface?.Attach(null); vncSession?.Dispose(); }
            SetStatus(L.F($"{(result.Reason==TerminalEndReason.TransportError?L.T("Utracono połączenie"):L.T("Rozłączono"))} · {Profile.Endpoint} · użyj Połącz ponownie"),"○",result.Reason==TerminalEndReason.TransportError?SessionVisualState.Failed:SessionVisualState.Closed);
        });
    }
    private async Task DisconnectAdditionalAsync()
    {
        await connectionGate.WaitAsync(lifetime.Token);
        try
        {
            var vnc=vncSession; var serial=serialSession; ResetConnections();
            if(vnc!=null)await vnc.Shutdown;
            if(serial!=null)await serial.Completion;
            if(additionalNotice!=null)additionalNotice.IsVisible=true; SetStatus(L.T("Rozłączono · ")+Profile.Endpoint,"○",SessionVisualState.Closed);
        }
        finally { connectionGate.Release(); }
    }
    private void ResetAdditional()
    { vncSurface?.Attach(null); vncSession?.Dispose(); vncSession=null; serialSession?.Dispose(); serialSession=null; }
}
