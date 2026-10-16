using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class TerminalSearchChecks
{
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(control=>control.Name==name);
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Until(Func<bool> done,string message)
    {
        var clock=Stopwatch.StartNew(); while(!done()) { if(clock.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException(message); RedesignChecks.Frames(1); }
    }
    private static void Query(Control root,string value)
    {
        Named<TextBox>(root,"TerminalSearchQuery").Text=value; RedesignChecks.Frames(1);
        Until(()=>!Named<TerminalSearchBar>(root,"TerminalSearchBar").IsSearching,"Terminal search did not settle");
    }
    private static void Press(Window window,Key key,RawInputModifiers modifiers=RawInputModifiers.None,PhysicalKey physical=PhysicalKey.None,string? symbol=null)=>SshAcceptanceChecks.Press(window,key,modifiers,physical,symbol);

    internal static void Run(string root)
    {
        var timings=new List<object>();
        foreach(var language in new[]{"pl","en"})foreach(var protocol in new[]{Protocol.SSH,Protocol.Serial})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"fixture-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            main.PreviewPreferences(main.Preferences with { InterfaceFontSize=16 }); DesignSystem.Apply(language=="pl"?"graphite":"light");
            using var pane=new SessionPane(main,new() { Name="Search fixture",Host="fixture.invalid",Username="fixture",Protocol=protocol,Serial=new() { PortName="fixture" } },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
            if(protocol==Protocol.Serial)Field<Border>(pane,"additionalNotice").IsVisible=false;
            var window=new Window { Width=1000,Height=680,Content=pane }; window.Show(); RedesignChecks.Frames();
            var terminal=pane.Children.OfType<TerminalControl>().Single(); terminal.ApplyTemplate(); terminal.Terminal.Reset();
            terminal.Terminal.Write("Needle α🙂 żółć\r\nneedle needleness\r\nNEEDLE\r\nliteral[a].*\r\n");
            for(var i=0;i<300;i++)terminal.Terminal.Write($"history line {i}\r\n");
            terminal.FollowTail(); var bottom=terminal.Terminal.Buffer.ViewportY; terminal.Focus();
            Press(window,Key.F,RawInputModifiers.Control|RawInputModifiers.Shift,PhysicalKey.F,"F"); RedesignChecks.Frames(2);
            var bar=Named<TerminalSearchBar>(window,"TerminalSearchBar"); if(!bar.IsVisible || !Named<TextBox>(bar,"TerminalSearchQuery").IsKeyboardFocusWithin)throw new Exception("Search shortcut did not focus its local field");
            Query(window,"needle"); if(terminal.SearchHitCount!=4 || terminal.SearchCurrentIndex!=0 || terminal.Terminal.Buffer.ViewportY>=bottom)throw new Exception("Search count/natural navigation into scrollback incorrect");
            Press(window,Key.Enter); if(terminal.SearchCurrentIndex!=1)throw new Exception("Enter did not navigate");
            Press(window,Key.Enter,RawInputModifiers.Shift); if(terminal.SearchCurrentIndex!=0)throw new Exception("Shift+Enter did not navigate backwards");
            Press(window,Key.F3,RawInputModifiers.Shift); if(terminal.SearchCurrentIndex!=3)throw new Exception("Previous search did not wrap");
            Named<CheckBox>(bar,"TerminalSearchCase").IsChecked=true; RedesignChecks.Frames(1); Until(()=>!bar.IsSearching,"Case-sensitive search pending"); if(terminal.SearchHitCount!=2)throw new Exception("Case-sensitive search incorrect");
            Named<CheckBox>(bar,"TerminalSearchWord").IsChecked=true; RedesignChecks.Frames(1); Until(()=>!bar.IsSearching,"Whole-word search pending"); if(terminal.SearchHitCount!=1)throw new Exception("Whole-word search incorrect");
            Named<CheckBox>(bar,"TerminalSearchCase").IsChecked=false; RedesignChecks.Frames(1); Until(()=>!bar.IsSearching,"Case reset pending"); if(terminal.SearchHitCount!=3)throw new Exception("Whole-word case-insensitive search incorrect");
            Named<CheckBox>(bar,"TerminalSearchWord").IsChecked=false; Query(window,"żółć"); if(terminal.SearchHitCount!=1)throw new Exception("Unicode search incorrect");
            Query(window,"🙂"); if(bar.MatchCount!=1)throw new Exception("Emoji codepoint search incorrect");
            Query(window,"[a].*"); if(terminal.SearchHitCount!=1)throw new Exception("Search interpreted literal text as a regex");
            Query(window,"not-in-buffer"); if(terminal.SearchHitCount!=0 || Named<Button>(bar,"TerminalSearchNext").IsEnabled)throw new Exception("No-match controls incorrect");
            Query(window,""); if(terminal.SearchHitCount!=0)throw new Exception("Empty query left highlights");
            Named<TextBox>(bar,"TerminalSearchQuery").Focus(); window.KeyTextInput(new string('x',600)); RedesignChecks.Frames();
            if(Named<TextBox>(bar,"TerminalSearchQuery").Text!.Length>256)throw new Exception("Terminal query exceeds input bound");
            Named<TextBox>(bar,"TerminalSearchQuery").Text="needle"; pane.IsVisible=false; RedesignChecks.Frames(10); pane.IsVisible=true; RedesignChecks.Frames(1); Until(()=>!bar.IsSearching,"Hidden pending search failed to resume");
            Field<Grid>(pane,"files").IsVisible=true; terminal.IsVisible=false; if(bar.IsVisible)throw new Exception("Terminal search overlaps file panel"); terminal.IsVisible=true; Field<Grid>(pane,"files").IsVisible=false; RedesignChecks.Frames();
            Query(window,"needle");
            using(var busy=pane.WaitForCredentials())
            foreach(var width in new[]{280,520,1000})
            {
                window.Width=width; RedesignChecks.Frames();
                var status=Named<Grid>(pane,"SessionOperationStatus"); var statusPoint=status.TranslatePoint(default,window)!.Value; var searchPoint=bar.TranslatePoint(default,window)!.Value; var terminalPoint=terminal.TranslatePoint(default,window)!.Value;
                if(statusPoint.Y+status.Bounds.Height>searchPoint.Y+1 || searchPoint.Y+bar.Bounds.Height>terminalPoint.Y+1)throw new Exception("Search/status overlap terminal content");
                foreach(var control in bar.GetVisualDescendants().Where(control=>control is Button or TextBox or CheckBox).Cast<Control>().Where(control=>control.IsEffectivelyVisible))
                {
                    var point=control.TranslatePoint(default,bar)!.Value;
                    if(point.X<0 || point.Y<0 || point.X+control.Bounds.Width>bar.Bounds.Width+1 || point.Y+control.Bounds.Height>bar.Bounds.Height+1)throw new Exception("Terminal search control escapes compact layout: "+control.Name);
                }
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"terminal-search-{protocol}-{language}-{width}.png"));
            }
            window.Width=1000; RedesignChecks.Frames();
            // The search API joins soft-wrapped rows, rather than treating them as newlines.
            terminal.Terminal.Write(new string('x',Math.Max(0,terminal.Terminal.Cols-4))+"wraptarget\r\n"); Query(window,"wraptarget");
            if(bar.MatchCount!=1)
            {
                File.WriteAllText(Path.Combine(root,"wrapped-buffer.txt"),SshAcceptanceChecks.Text(terminal));
                throw new Exception($"Soft-wrapped result incorrect: logical={bar.MatchCount}, runs={terminal.SearchHitCount}, cols={terminal.Terminal.Cols}");
            }
            Click(window,"TerminalSearchNext"); if(terminal.SearchCurrentIndex!=0)throw new Exception("Navigation visits wrapped fragments separately");
            terminal.Terminal.Write(string.Concat(Enumerable.Repeat("cap cap\r\n",11000))); Query(window,"cap");
            if(!terminal.SearchTruncated || !Named<TextBlock>(bar,"TerminalSearchCount").Text!.EndsWith('+'))throw new Exception("Result cap is not visible");
            var clock=Stopwatch.StartNew(); Click(window,"TerminalSearchRefresh"); clock.Stop(); timings.Add(new { Language=language,Protocol=protocol.ToString(),BoundedBufferSearchMs=clock.Elapsed.TotalMilliseconds });
            Query(window,"Needle"); if(terminal.SearchHitCount!=0)throw new Exception("Search claims to include discarded history");
            Named<TextBox>(bar,"TerminalSearchQuery").Focus(); Press(window,Key.Escape); if(bar.IsVisible || !terminal.IsKeyboardFocusWithin || terminal.SearchHitCount!=0)throw new Exception("Escape did not close/clear/focus terminal");
            Click(window,"TerminalSearchOpen"); Query(window,"cap"); Click(window,"TerminalSearchTail"); if(terminal.Terminal.Buffer.ViewportY<terminal.Terminal.Buffer.Lines.Length-terminal.Terminal.Rows)throw new Exception("Tail action failed");
            RedesignChecks.Wait(pane.CloseAsync()); if(bar.IsVisible || !string.IsNullOrEmpty(Named<TextBox>(bar,"TerminalSearchQuery").Text))throw new Exception("Close retained search UI/query");
            window.Close(); main.Close(); RedesignChecks.Frames();
        }
        File.WriteAllText(Path.Combine(root,"terminal-search-timing.json"),JsonSerializer.Serialize(timings,new JsonSerializerOptions { WriteIndented=true })); AppText.SetLanguage("pl");
        Console.WriteLine("PASS terminal search UI: SSH/Serial PL/EN, Unicode/literal/case/whole-word/wrapped text, history, navigation/wrap, input/result caps, hide/resume, tail, Escape/focus, disposal and 280/520/1000px font16 layouts with separate busy status");
    }

    internal static void Network(string root,string host,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"network-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
        var factory=new SshConnectionFactory(repo); var main=new MainWindow(new(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        main.PreviewPreferences(main.Preferences with { ShowMetrics=false }); factory.ConfirmHostKey=(_,_)=>Task.FromResult(true);
        var profile=new ServerProfile { Name="Owned search fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); main.RegisterPane(pane); RedesignChecks.Wait(pane.ConnectAsync("serverdesk-test-passphrase",default));
        var terminal=pane.Children.OfType<TerminalControl>().Single();
        RedesignChecks.Wait(terminal.SendInputAsync("stty -echo; PS1=''; printf '\\033[2J\\033[HSEARCH_NET_żółć\\n'\r",default)); Until(()=>SshAcceptanceChecks.Text(terminal).Contains("SEARCH_NET_żółć"),"SSH terminal marker missing");
        var sent=new ConcurrentQueue<string>(); terminal.InputSent+=(_,text)=>sent.Enqueue(text);
        terminal.Terminal.Write("\u001b[>3u"); terminal.Focus();
        Press(main,Key.F,RawInputModifiers.Control|RawInputModifiers.Shift,PhysicalKey.F,"F"); Query(main,"SEARCH_NET_żółć"); Press(main,Key.Enter); Press(main,Key.F3); Press(main,Key.Escape); RedesignChecks.Frames();
        if(!sent.IsEmpty)throw new Exception("Local search leaked input to SSH: "+string.Join(",",sent.Select(text=>Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(text)))));
        terminal.Terminal.Write("\u001b[<u"); sent.Clear(); Press(main,Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); RedesignChecks.Frames();
        if(!sent.Any(text=>text=="\u0006"))throw new Exception("Plain Ctrl+F was captured locally");
        RedesignChecks.Wait(terminal.SendInputAsync("\u0015",default));
        Click(main,"TerminalSearchOpen"); Query(main,"SEARCH_NEW_OUTPUT"); if(terminal.SearchHitCount!=0)throw new Exception("New-output marker already exists");
        RedesignChecks.Wait(terminal.SendInputAsync("printf 'SEARCH_NEW_OUTPUT\\n'\r",default)); Until(()=>Named<Button>(main,"TerminalSearchRefresh").Classes.Contains("active"),"New output did not mark results stale");
        Until(()=>SshAcceptanceChecks.Text(terminal).Contains("SEARCH_NEW_OUTPUT"),"Real output missing"); Click(main,"TerminalSearchRefresh"); if(terminal.SearchHitCount!=1)throw new Exception("Refreshed results did not include real SSH output");
        RedesignChecks.Wait(pane.ReconnectAsync()); if(Named<TerminalSearchBar>(main,"TerminalSearchBar").IsVisible || !string.IsNullOrEmpty(Named<TextBox>(main,"TerminalSearchQuery").Text))throw new Exception("Reconnect retained the previous search");
        main.RemovePane(pane); RedesignChecks.Wait(pane.CloseAsync()); main.Close(); RedesignChecks.Frames();
        Console.WriteLine("PASS real SSH terminal search: remote UTF-8 output, local shortcuts including Kitty releases send no bytes, plain Ctrl+F stays remote, new output/refresh and cleared search after reconnect");
    }
}
