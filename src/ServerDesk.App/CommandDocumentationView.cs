using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

/// <summary>Public text only. Never passes downloaded content to command dispatch.</summary>
internal sealed class CommandDocumentationView:Expander,IDisposable
{
    private readonly MainWindow owner;
    private readonly CommandDocumentationClient client;
    private readonly Action<Task> track;
    private readonly CancellationTokenSource lifetime;
    private CancellationTokenSource? request;
    private long revision;
    private bool loaded,disposed;
    private readonly ComboBox sources=new() { Name="CommandDocumentationSource",HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly TextBlock status=DesignSystem.Hint(""),version=DesignSystem.Hint("");
    private readonly TextBox text=new() { Name="CommandDocumentationText",IsReadOnly=true,AcceptsReturn=true,TextWrapping=Avalonia.Media.TextWrapping.Wrap,MinHeight=100,MaxHeight=320,IsVisible=false };
    private readonly Button refresh,cancel;
    private bool active=>!disposed&&!lifetime.IsCancellationRequested;
    internal CommandDocumentationView(MainWindow owner,CommandDocumentationClient client,IEnumerable<string> ids,CancellationToken parent,Action<Task> track)
    {
        this.owner=owner; this.client=client; this.track=track; lifetime=CancellationTokenSource.CreateLinkedTokenSource(parent);
        Name="CommandOfficialDocumentation"; Header=L.T("Oficjalna dokumentacja"); HorizontalContentAlignment=HorizontalAlignment.Stretch;
        var body=new StackPanel { Spacing=9 };
        body.Children.Add(DesignSystem.Hint(L.T("Pobierz aktualny tekst ze strony wydawcy. Poradniki i podgląd komendy pozostają wbudowaną, sprawdzoną wersją.")));
        sources.ItemsSource=ids.Distinct().Select(CommandKnowledgeCatalog.Default.Source).ToArray();
        sources.ItemTemplate=new FuncDataTemplate<CommandDocumentationSource>((source,_)=>source==null?null:new TextBlock { Text=source.Title,TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis });
        AutomationProperties.SetName(sources,L.T("Źródło dokumentacji")); body.Children.Add(sources);
        var actions=new WrapPanel { Orientation=Orientation.Horizontal };
        refresh=Dialogs.Button("Odśwież dokumentację",()=>Start(true)); refresh.Name="CommandDocumentationRefresh"; refresh.Content=DeskIcons.Labelled("refresh",L.T("Odśwież dokumentację"),14);
        cancel=Dialogs.Button("Anuluj",CancelPending); cancel.Name="CommandDocumentationCancel"; cancel.IsVisible=false;
        var open=Dialogs.Button("Otwórz stronę",()=>track(OpenSourceAsync())); open.Name="CommandDocumentationOpen"; open.Content=DeskIcons.Labelled("external-link",L.T("Otwórz stronę"),14);
        foreach(var button in new[]{refresh,open,cancel}) { button.Margin=new Thickness(0,0,6,5); actions.Children.Add(button); }
        body.Children.Add(actions); body.Children.Add(status); body.Children.Add(version); body.Children.Add(text);
        body.Children.Add(DesignSystem.Hint(L.T("Dokumentacja wydawcy może opisywać nowszą wersję niż zainstalowana na serwerze. Oryginał jest zwykle po angielsku.")));
        Content=body;
        sources.SelectedIndex=0;
        sources.SelectionChanged+=(_,_)=> { if(loaded)Start(false); };
        PropertyChanged+=(_,e)=> { if(e.Property==IsExpandedProperty && IsExpanded && !loaded) { loaded=true; Start(false); } };
        status.Text=L.T("Dostępne offline poradniki nie wymagają pobierania dokumentacji.");
    }
    private async Task OpenSourceAsync()
    {
        if(!active || !owner.IsPluginEnabled(PluginCatalog.Commands) || sources.SelectedItem is not CommandDocumentationSource source)return;
        try { await owner.Launcher.LaunchUriAsync(new Uri(source.Url)); }
        catch(Exception ex) { ApplicationDiagnostics.Report("Commands.Documentation.Open",ex,Serilog.Events.LogEventLevel.Warning); if(active)status.Text=L.T("Nie udało się otworzyć przeglądarki. Spróbuj ponownie."); }
    }
    private void Start(bool download)
    {
        if(!active || !owner.IsPluginEnabled(PluginCatalog.Commands) || sources.SelectedItem is not CommandDocumentationSource source)return;
        request?.Cancel(); request?.Dispose(); request=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var stamp=++revision; var token=request.Token;
        if(!download) { text.Text=""; text.IsVisible=false; version.Text=""; }
        refresh.IsEnabled=!download; cancel.IsVisible=download;
        status.Text=L.T(download?"Pobieranie oficjalnej dokumentacji…":"Odczytywanie zapisanej dokumentacji…");
        track(LoadAsync(source,download,stamp,token));
    }
    private async Task LoadAsync(CommandDocumentationSource source,bool download,long stamp,CancellationToken token)
    {
        bool Current()=>active && stamp==revision;
        try
        {
            var snapshot=download?await client.RefreshAsync(source.Id,token):await client.ReadAsync(source.Id,token);
            if(!Current() || token.IsCancellationRequested)return;
            if(snapshot==null) { status.Text=L.T("Brak pobranej kopii. Odśwież, aby pobrać tekst z oficjalnej strony."); return; }
            text.Text=snapshot.Text; text.IsVisible=true;
            status.Text=L.F($"Sprawdzono: {snapshot.CheckedAt.ToLocalTime():g}")+" · "+source.Title;
            version.Text=snapshot.Title+(snapshot.Truncated?"\n"+L.T("Wyciąg został skrócony. Pełną treść otworzysz na stronie wydawcy."):"");
            ToolTip.SetTip(status,source.Url);
        }
        catch(OperationCanceledException) { if(Current()) { await RestoreCachedAsync(); if(Current())status.Text=L.T(text.IsVisible?"Anulowano pobieranie. Poprzednia kopia pozostaje dostępna.":"Anulowano pobieranie. Nie ma jeszcze zapisanej kopii."); } }
        catch(Exception ex)
        {
            ApplicationDiagnostics.Report("Commands.Documentation.Read",ex,Serilog.Events.LogEventLevel.Warning);
            if(Current()) { await RestoreCachedAsync(); if(Current())status.Text=L.T(!text.IsVisible?"Nie udało się pobrać dokumentacji. Spróbuj ponownie; poradniki nadal działają offline.":ex is TimeoutException?"Upłynął czas pobierania dokumentacji. Zachowano poprzednią kopię.":"Nie udało się odczytać oficjalnej dokumentacji. Zachowano poprzednią kopię."); }
        }
        finally { if(Current()) { refresh.IsEnabled=true; cancel.IsVisible=false; } }
        async Task RestoreCachedAsync()
        {
            if(!download || !Current() || text.IsVisible)return;
            try
            {
                var previous=await client.ReadAsync(source.Id,lifetime.Token);
                if(Current() && previous!=null) { text.Text=previous.Text; text.IsVisible=true; version.Text=previous.Title; }
            }
            catch(OperationCanceledException) { }
            catch(Exception error) { ApplicationDiagnostics.Report("Commands.Documentation.Cache",error,Serilog.Events.LogEventLevel.Warning); }
        }
    }
    internal void CancelPending()=>request?.Cancel();
    public void Dispose() { if(disposed)return; disposed=true; lifetime.Cancel(); request?.Cancel(); request?.Dispose(); lifetime.Dispose(); }
}

internal static class CommandKnowledgeViews
{
    internal static Border Risk(CommandRisk risk)
    {
        var badge=DesignSystem.Badge(L.T(risk switch { CommandRisk.ReadOnly=>"Tylko odczyt",CommandRisk.Modify=>"Zmienia stan",_=>"Niebezpieczna" }));
        DesignSystem.Resource((TextBlock)badge.Child!,TextBlock.ForegroundProperty,risk==CommandRisk.Dangerous?"Danger":risk==CommandRisk.Modify?"Warning":"Success"); return badge;
    }
    internal static Control Options(CommandToolReference tool,bool expand=false)
    {
        var options=new StackPanel { Spacing=8 };
        options.Children.Add(DesignSystem.Hint(tool.Applicability));
        var search=new TextBox { Name="CommandOptionSearch",PlaceholderText=L.T("Szukaj przełącznika lub znaczenia…"),MaxLength=120 };
        AutomationProperties.SetName(search,L.T("Szukaj przełączników")); options.Children.Add(search);
        var count=DesignSystem.Hint(""); count.Name="CommandOptionCount"; options.Children.Add(count);
        var rows=new StackPanel { Spacing=6 }; options.Children.Add(rows); var loaded=expand;
        void Filter()
        {
            if(!loaded)return;
            var query=(search.Text??"").Trim();
            var tokens=CommandCatalog.Normalize(query).Split(' ',StringSplitOptions.RemoveEmptyEntries);
            var matches=tool.Options.Where(o=>query.StartsWith('-')?
                o.Syntax.Split([' ',',','/'],StringSplitOptions.RemoveEmptyEntries).Any(s=>s.Equals(query,tool.Platform==CommandPlatform.Windows?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) || query.StartsWith("--",StringComparison.Ordinal)&&s.StartsWith(query,StringComparison.Ordinal)):
                tokens.All(t=>CommandCatalog.Normalize(o.Syntax+" "+o.Description+" "+o.DescriptionEn+" "+o.Example).Contains(t,StringComparison.Ordinal))).ToArray();
            rows.Children.Clear(); count.Text=L.F($"Przełączniki i składnia: {matches.Length} / {tool.Options.Length}");
            foreach(var option in matches)
            {
                var syntax=DesignSystem.Label(option.Syntax,false,12); syntax.Name="CommandOptionSyntax"; syntax.FontFamily=new("Cascadia Mono,Consolas,monospace"); syntax.TextWrapping=Avalonia.Media.TextWrapping.Wrap;
                var body=new StackPanel { Spacing=4,Children={syntax,DesignSystem.Hint(option.Text)} };
                if(option.Example.Length>0)body.Children.Add(new SelectableTextBlock { Text=option.Example,FontSize=12,TextWrapping=Avalonia.Media.TextWrapping.Wrap,FontFamily=syntax.FontFamily });
                var row=DesignSystem.Card(body,new Thickness(9,7)); row.Name="CommandOptionRow"; row.Tag=option.Syntax; rows.Children.Add(row);
            }
            if(matches.Length==0)rows.Children.Add(DesignSystem.Hint(L.T("Brak pasujących przełączników.")));
        }
        search.TextChanged+=(_,_)=>Filter(); Filter();
        var header=DesignSystem.Label(tool.Title+" · "+L.F($"Przełączniki: {tool.Options.Length}")); header.TextWrapping=Avalonia.Media.TextWrapping.Wrap;
        var expander=new Expander { Name="CommandOptions",Tag=tool.Id,Header=header,IsExpanded=expand,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Content=options };
        expander.PropertyChanged+=(_,e)=> { if(e.Property==Expander.IsExpandedProperty && expander.IsExpanded && !loaded) { loaded=true; Filter(); } };
        return expander;
    }
}
