using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Iciclecreek.Terminal;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed class TerminalSearchBar : Border,IDisposable
{
    private readonly TerminalControl terminal;
    private readonly TextBox query=new() { Name="TerminalSearchQuery",MaxLength=256,PlaceholderText=L.T("Szukaj w historii terminala…") };
    private readonly TextBlock count=DesignSystem.Label("",true,DesignTokens.Caption);
    private readonly CheckBox matchCase=new() { Name="TerminalSearchCase",Content=L.T("Wielkość liter") };
    private readonly CheckBox wholeWord=new() { Name="TerminalSearchWord",Content=L.T("Całe słowa") };
    private readonly Button previous,next,refresh;
    private readonly DispatcherTimer debounce=new() { Interval=TimeSpan.FromMilliseconds(150) };
    private readonly CoalescingProgress<bool> output;
    private XTerm.Search.BufferSearch? index;
    private readonly List<int> matchStarts=[];
    private bool pending,stale,wanted;
    private int disposed;

    internal TerminalSearchBar(TerminalControl terminal)
    {
        this.terminal=terminal; Name="TerminalSearchBar"; IsVisible=false; Padding=new Thickness(8,6); BorderThickness=new Thickness(0,0,0,1); ClipToBounds=true;
        DesignSystem.Resource(this,BackgroundProperty,"BackgroundSecondary"); DesignSystem.Resource(this,BorderBrushProperty,"BorderDefault");
        AutomationProperties.SetName(query,L.T("Szukaj w historii terminala"));
        AutomationProperties.SetName(matchCase,L.T("Wielkość liter")); AutomationProperties.SetName(wholeWord,L.T("Całe słowa"));
        ToolTip.SetTip(query,L.T("Tekst dosłowny · Enter: następny · Shift+Enter: poprzedni · Escape: zamknij. Zapytanie pozostaje tylko w tej karcie."));
        count.Name="TerminalSearchCount"; count.TextTrimming=TextTrimming.CharacterEllipsis; count.MaxWidth=120;
        previous=DeskIcons.Button("arrow-up","Poprzedni wynik · Shift+Enter",()=>Navigate(false),28); previous.Name="TerminalSearchPrevious";
        next=DeskIcons.Button("arrow-up","Następny wynik · Enter",()=>Navigate(true),28); next.Name="TerminalSearchNext";
        if(next.Content is Control arrow) { arrow.RenderTransform=new RotateTransform(180); arrow.RenderTransformOrigin=RelativePoint.Center; }
        refresh=DeskIcons.Button("refresh","Odśwież wyniki wyszukiwania",()=>Search(true),28); refresh.Name="TerminalSearchRefresh";
        var close=DeskIcons.Button("close","Zamknij wyszukiwanie terminala · Escape",()=>Close(),28); close.Name="TerminalSearchClose";
        var tail=DeskIcons.Button("arrow-right","Przejdź do bieżącego tekstu terminala",()=> { terminal.FollowTail(); terminal.Focus(); },28); tail.Name="TerminalSearchTail";
        var top=new Grid { ColumnDefinitions=new("*,Auto,Auto"),ColumnSpacing=6,Children={query,count,close} }; Grid.SetColumn(count,1); Grid.SetColumn(close,2);
        var options=new WrapPanel { Orientation=Orientation.Horizontal,Children={previous,next,refresh,matchCase,wholeWord,tail} };
        foreach(var control in options.Children)control.Margin=new Thickness(0,2,8,0);
        Child=new StackPanel { Spacing=4,Children={top,options} };
        query.TextChanged+=(_,_)=>Schedule(); matchCase.IsCheckedChanged+=(_,_)=>Schedule(); wholeWord.IsCheckedChanged+=(_,_)=>Schedule();
        debounce.Tick+=(_,_)=> { debounce.Stop(); if(IsVisible && IsEffectivelyVisible)Search(true); };
        output=new(action=>Dispatcher.UIThread.Post(action,DispatcherPriority.Background),_=>
        {
            if(disposed!=0 || !wanted || string.IsNullOrEmpty(query.Text))return;
            stale=true; refresh.Classes.Add("active"); ToolTip.SetTip(refresh,L.T("W terminalu pojawił się nowy tekst. Odśwież wyniki.")); UpdateCount();
        });
        terminal.OutputReceived+=OnOutput; terminal.SizeChanged+=TerminalResized; terminal.PropertyChanged+=TerminalChanged; AttachedToVisualTree+=(_,_)=>Resume();
        AddHandler(KeyDownEvent,(_,e)=>
        {
            if(e.Key==Key.Escape) { Close(); e.Handled=true; }
            else if(e.Key==Key.Enter && query.IsKeyboardFocusWithin && (e.KeyModifiers & ~KeyModifiers.Shift)==KeyModifiers.None) { Navigate(!e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled=true; }
        },RoutingStrategies.Tunnel);
        UpdateCount();
    }
    private void OnOutput(object? sender,OutputReceivedEventArgs e) { if(Volatile.Read(ref wanted) && Volatile.Read(ref disposed)==0)output.Report(true); }
    private void TerminalResized(object? sender,SizeChangedEventArgs e) { if(wanted)Schedule(); }
    internal void Resume() { if(wanted && pending)Schedule(); }
    private void TerminalChanged(object? sender,AvaloniaPropertyChangedEventArgs e)
    {
        if(e.Property!=IsVisibleProperty)return;
        IsVisible=wanted && terminal.IsVisible;
        if(!IsVisible)debounce.Stop(); else Schedule();
    }
    internal bool IsOpen=>wanted;
    internal bool IsSearching=>pending;
    internal int MatchCount=>matchStarts.Count;
    internal void Open()
    {
        if(disposed!=0)return;
        wanted=true; IsVisible=terminal.IsVisible; if(!IsVisible)return;
        terminal.ApplyTemplate(); Search(true); query.Focus(); query.SelectAll();
    }
    internal void Close(bool focus=true)
    {
        wanted=false; IsVisible=false; debounce.Stop(); pending=stale=false; terminal.ClearSearch(); index?.Dispose(); index=null; matchStarts.Clear(); UpdateCount();
        if(focus && terminal.IsVisible)terminal.Focus();
    }
    internal void Reset()
    {
        Close(false); query.Text="";
    }
    private void Schedule()
    {
        if(disposed!=0 || !wanted)return;
        pending=true; previous.IsEnabled=next.IsEnabled=false; debounce.Stop();
        if(string.IsNullOrEmpty(query.Text)) { Search(false); return; }
        count.Text=L.T("Szukanie…"); if(IsVisible)debounce.Start();
    }
    private void Search(bool selectFirst)
    {
        if(disposed!=0)return;
        debounce.Stop(); pending=stale=false; refresh.Classes.Remove("active"); ToolTip.SetTip(refresh,L.T("Odśwież wyniki wyszukiwania"));
        matchStarts.Clear();
        if(string.IsNullOrEmpty(query.Text)) { terminal.ClearSearch(); index?.Clear(); }
        else
        {
            var options=new XTerm.Search.SearchOptions { CaseSensitive=matchCase.IsChecked==true,WholeWord=wholeWord.IsChecked==true };
            terminal.FindInBuffer(query.Text,options);
            index??=new(terminal.Terminal); index.Find(query.Text,options);
            // The renderer exposes physical highlight runs. A match across a
            // soft wrap has several runs with one MatchId: count and step it once.
            var run=0; var lastMatch=-1;
            for(var row=0;row<terminal.Terminal.Buffer.Lines.Length;row++)
            foreach(var hit in index.HitsOnRow(row))
            {
                if(hit.MatchId!=lastMatch) { matchStarts.Add(run); lastMatch=hit.MatchId; }
                run++;
            }
            if(selectFirst)terminal.FindNext();
        }
        UpdateCount();
    }
    internal void Navigate(bool forward)
    {
        if(disposed!=0 || !wanted)return;
        if(pending || stale)Search(false);
        if(matchStarts.Count>0)
        {
            var current=CurrentMatch(); var target=current<0?forward?0:matchStarts.Count-1:(current+(forward?1:matchStarts.Count-1))%matchStarts.Count;
            // Skip only the fragments of the adjacent logical match, not the
            // complete result list. The raw run cap also bounds this loop.
            for(var step=0;step<=terminal.SearchHitCount;step++)
            {
                if(forward)terminal.FindNext(); else terminal.FindPrevious();
                if(terminal.SearchCurrentIndex==matchStarts[target])break;
            }
        }
        UpdateCount();
    }
    private int CurrentMatch()
    {
        var current=terminal.SearchCurrentIndex; if(current<0 || matchStarts.Count==0)return -1;
        var position=matchStarts.BinarySearch(current); return position>=0?position:~position-1;
    }
    private void UpdateCount()
    {
        var total=matchStarts.Count; var current=CurrentMatch();
        count.Text=string.IsNullOrEmpty(query.Text)?"":stale?L.T("Nowe dane"):total==0?L.T("Brak wyników"):current<0?total.ToString():$"{current+1} / {total}";
        if(!stale && terminal.SearchTruncated)count.Text+="+";
        ToolTip.SetTip(count,stale?L.T("W terminalu pojawił się nowy tekst. Odśwież wyniki."):terminal.SearchTruncated?L.T("Limit wyników osiągnięty. Zawęź wyszukiwanie."):L.T("Wyniki w dostępnym buforze terminala. Starsze usunięte wiersze nie są przeszukiwane."));
        previous.IsEnabled=next.IsEnabled=!pending && (total>0 || stale); refresh.IsEnabled=!string.IsNullOrEmpty(query.Text);
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        debounce.Stop(); output.Dispose(); terminal.OutputReceived-=OnOutput; terminal.SizeChanged-=TerminalResized; terminal.PropertyChanged-=TerminalChanged; wanted=false; IsVisible=false; terminal.ClearSearch(); index?.Dispose(); index=null; matchStarts.Clear(); query.Text="";
    }
}
