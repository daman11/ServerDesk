using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed class CommandKnowledgePanel:Grid,IDisposable
{
    private readonly MainWindow owner;
    private readonly CommandDocumentationClient documentation;
    private readonly Action<Task> track;
    private readonly CancellationTokenSource lifetime;
    private readonly List<CommandDocumentationView> views=[];
    private readonly ComboBox section=new() { Name="CommandKnowledgeSection",ItemsSource=new[]{L.T("Mini poradniki"),L.T("Przełączniki narzędzi")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly ComboBox platform=new() { Name="CommandKnowledgePlatform",ItemsSource=new[]{L.T("Wszystkie systemy"),"Linux","Windows"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly TextBox search=new() { Name="CommandKnowledgeSearch",PlaceholderText=L.T("Szukaj poradnika, narzędzia lub przełącznika…"),MaxLength=160 };
    private readonly ListBox list=new() { Name="CommandKnowledgeList",Focusable=true };
    private readonly TextBlock count=DesignSystem.Hint(""),empty=DesignSystem.Hint("");
    private readonly StackPanel details=new() { Spacing=10,Margin=new Thickness(0,0,8,0) };
    private readonly Grid filters=new() { RowDefinitions=new("Auto,Auto"),RowSpacing=8 };
    private readonly Grid browser=new() { RowDefinitions=new("Auto,*"),RowSpacing=8 };
    private readonly Grid detailView=new() { RowDefinitions=new("Auto,*"),RowSpacing=9,IsVisible=false };
    private readonly TextBlock position=DesignSystem.Hint("");
    private readonly Button previous,next,back;
    private bool updating,detailsVisible,platformChosen,disposed;
    internal CommandKnowledgePanel(MainWindow owner,CommandDocumentationClient documentation,CancellationToken parent,Action<Task> track)
    {
        this.owner=owner; this.documentation=documentation; this.track=track; lifetime=CancellationTokenSource.CreateLinkedTokenSource(parent);
        Name="CommandKnowledgePanel"; RowDefinitions=new("Auto,*"); RowSpacing=10;
        var modes=new Grid { ColumnDefinitions=new("*,*"),ColumnSpacing=7,Children={section,platform} }; Grid.SetColumn(platform,1);
        filters.Children.Add(modes); Grid.SetRow(search,1); filters.Children.Add(search); Children.Add(filters);
        AutomationProperties.SetName(section,L.T("Rodzaj materiałów")); AutomationProperties.SetName(platform,L.T("System poradników")); AutomationProperties.SetName(search,L.T("Szukaj poradników i przełączników"));
        search.InnerLeftContent=new Border { Child=DeskIcons.Create("search",15,"ForegroundSecondary"),Margin=new Thickness(8,0,0,0) };
        var clear=DeskIcons.Button("close","Wyczyść wyszukiwanie",()=> { search.Text=""; search.Focus(); },26); search.InnerRightContent=clear;
        list.Classes.Add("command-list"); list.ItemTemplate=new FuncDataTemplate<object>((item,_)=>item==null?null:Row(item));
        browser.Children.Add(count); Grid.SetRow(list,1); browser.Children.Add(list);
        empty.Name="CommandKnowledgeEmpty"; empty.Text=L.T("Brak pasujących materiałów. Zmień system lub zapytanie."); empty.Margin=new Thickness(12); empty.IsVisible=false; Grid.SetRow(empty,1); browser.Children.Add(empty);
        back=Dialogs.Button("Lista materiałów",ShowList); back.Name="CommandKnowledgeBack"; back.Content=DeskIcons.Labelled("arrow-left",L.T("Lista materiałów"),14);
        previous=DeskIcons.Button("arrow-left","Poprzedni materiał",()=>Navigate(-1),28); previous.Name="CommandKnowledgePrevious";
        next=DeskIcons.Button("arrow-right","Następny materiał",()=>Navigate(1),28); next.Name="CommandKnowledgeNext";
        var stepper=new StackPanel { Orientation=Orientation.Horizontal,Spacing=5,Children={position,previous,next} };
        var navigation=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=5,Children={back,stepper} }; Grid.SetColumn(stepper,1); detailView.Children.Add(navigation);
        var scroll=new ScrollViewer { Name="CommandKnowledgeDetails",Content=details,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll,1); detailView.Children.Add(scroll); var content=new Grid { Children={browser,detailView} }; Grid.SetRow(content,1); Children.Add(content);
        search.TextChanged+=(_,_)=> { clear.IsVisible=!string.IsNullOrEmpty(search.Text); Refresh(); };
        section.SelectionChanged+=(_,_)=>Refresh(); platform.SelectionChanged+=(_,_)=> { if(!updating)platformChosen=true; Refresh(); };
        list.SelectionChanged+=(_,_)=>Navigation();
        list.AddHandler(KeyDownEvent,(_,e)=> { if(e.Key==Key.Enter && list.SelectedItem!=null) { ShowDetails(); e.Handled=true; } },RoutingStrategies.Tunnel);
        search.KeyDown+=(_,e)=>
        {
            if(e.KeyModifiers!=KeyModifiers.None)return;
            if(e.Key is Key.Enter or Key.Down) { list.SelectedIndex=Math.Max(0,list.SelectedIndex); if(list.SelectedItem!=null) { if(e.Key==Key.Enter)ShowDetails(); else list.Focus(); } e.Handled=true; }
            else if(e.Key==Key.Escape && !string.IsNullOrEmpty(search.Text)) { search.Text=""; e.Handled=true; }
        };
        KeyDown+=(_,e)=> { if(e.Key==Key.Escape && detailsVisible) { ShowList(); e.Handled=true; } else if(e.KeyModifiers==KeyModifiers.Alt && e.Key is Key.Left or Key.Right) { Navigate(e.Key==Key.Left?-1:1); e.Handled=true; } };
        Refresh();
    }
    internal void SetEnvironment(CommandEnvironment environment)
    {
        if(platformChosen)return; updating=true;
        try { platform.SelectedIndex=environment.Platform==CommandPlatform.Linux?1:environment.Platform==CommandPlatform.Windows?2:0; }
        finally { updating=false; }
        Refresh();
    }
    private void Refresh()
    {
        if(updating || lifetime.IsCancellationRequested)return;
        var environment=CommandEnvironment.Manual(platform.SelectedIndex switch { 1=>"linux",2=>"windows",_=>"general" });
        object[] items=section.SelectedIndex==0?CommandKnowledgeCatalog.Default.FindGuides(environment,search.Text??"").Cast<object>().ToArray():
            CommandKnowledgeCatalog.Default.FindTools(environment,search.Text??"").Cast<object>().ToArray();
        list.ItemsSource=items; list.SelectedIndex=items.Length>0?0:-1; list.IsVisible=items.Length>0; empty.IsVisible=items.Length==0;
        count.Text=section.SelectedIndex==0?L.F($"Poradniki: {items.Length}"):L.F($"Narzędzia: {items.Length}");
        ShowList(false);
    }
    private Control Row(object item)
    {
        var guide=item as CommandGuide; var tool=item as CommandToolReference;
        var title=guide?.Heading??tool!.Title; var summary=guide?.Description??tool!.Applicability;
        var heading=DesignSystem.Label(title); heading.FontWeight=Avalonia.Media.FontWeight.SemiBold; heading.TextWrapping=Avalonia.Media.TextWrapping.Wrap;
        var hint=DesignSystem.Hint(summary); hint.MaxLines=3; hint.TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis;
        var metadata=guide!=null?L.F($"Kroki: {guide.Steps.Length}")+" · "+guide.Platform:L.F($"Przełączniki: {tool!.Options.Length}");
        var body=new StackPanel { Spacing=7,Children={heading,hint,DesignSystem.Hint(metadata),DeskIcons.Labelled("arrow-right",L.T("Więcej informacji"),14)} };
        var row=DesignSystem.Card(body,new Thickness(11)); row.Name="CommandKnowledgeCard"; row.Tag=guide?.Id??tool!.Id; AutomationProperties.SetName(row,title);
        var open=new Button { Content=row,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Avalonia.Media.Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        AutomationProperties.SetName(open,title+" · "+L.T("Więcej informacji"));
        open.Click+=(_,e)=> { list.SelectedItem=item; ShowDetails(); e.Handled=true; }; return open;
    }
    private void ShowDetails()
    {
        if(list.SelectedItem==null || lifetime.IsCancellationRequested)return;
        DisposeViews(); details.Children.Clear();
        var item=list.SelectedItem; var guide=item as CommandGuide; var tool=item as CommandToolReference;
        var heading=DesignSystem.Label(guide?.Heading??tool!.Title,false,17); heading.FontWeight=Avalonia.Media.FontWeight.SemiBold; heading.TextWrapping=Avalonia.Media.TextWrapping.Wrap; details.Children.Add(heading);
        details.Children.Add(DesignSystem.Hint(guide?.Applicability??tool!.Applicability));
        details.Children.Add(DesignSystem.Hint(L.F($"Wersja biblioteki: {CommandKnowledgeCatalog.Default.Version}")+" · "+L.F($"Przegląd treści: {CommandKnowledgeCatalog.Default.ReviewedOn:yyyy-MM-dd}")));
        if(guide!=null)
        {
            details.Children.Add(CommandKnowledgeViews.Risk(guide.Risk));
            if(guide.Caution.Length>0)
            {
                var warning=DesignSystem.Hint(guide.Caution); DesignSystem.Resource(warning,TextBlock.ForegroundProperty,"Warning"); details.Children.Add(DesignSystem.Card(warning,new Thickness(10)));
            }
            for(var i=0;i<guide.Steps.Length;i++)
            {
                var step=guide.Steps[i]; var number=i+1;
                var title=DesignSystem.Label(number+". "+step.Heading); title.FontWeight=Avalonia.Media.FontWeight.SemiBold; title.TextWrapping=Avalonia.Media.TextWrapping.Wrap;
                var body=new StackPanel { Spacing=8,Children={title,DesignSystem.Hint(step.Body)} };
                if(step.Risk!=CommandRisk.ReadOnly)body.Children.Add(CommandKnowledgeViews.Risk(step.Risk));
                if(step.Code.Length>0)
                {
                    body.Children.Add(new TextBox { Name="CommandGuideExample",Text=step.Code,IsReadOnly=true,AcceptsReturn=true,TextWrapping=Avalonia.Media.TextWrapping.Wrap,MaxHeight=150,FontFamily=new("Cascadia Mono,Consolas,monospace") });
                    var copy=Dialogs.Button("Kopiuj przykład",()=>track(CopyAsync(step.Code))); copy.Name="CommandGuideCopy_"+number; copy.HorizontalAlignment=HorizontalAlignment.Left; copy.Content=DeskIcons.Labelled("copy",L.T("Kopiuj przykład"),14); body.Children.Add(copy);
                }
                details.Children.Add(DesignSystem.Card(body,new Thickness(11)));
            }
            details.Children.Add(DesignSystem.Hint(L.T("Przykłady służą do kopiowania i świadomego użycia. Otwarcie poradnika niczego nie wykonuje w sesji.")));
        }
        else details.Children.Add(CommandKnowledgeViews.Options(tool!,true));
        var docs=new CommandDocumentationView(owner,documentation,guide?.Sources??tool!.Sources,lifetime.Token,track); views.Add(docs); details.Children.Add(docs);
        detailsVisible=true; browser.IsVisible=filters.IsVisible=false; detailView.IsVisible=true; Navigation();
        back.Focus();
    }
    private async Task CopyAsync(string code)
    {
        if(lifetime.IsCancellationRequested || !owner.IsPluginEnabled(PluginCatalog.Commands))return;
        try { if(owner.Clipboard is {} clipboard)await clipboard.SetTextAsync(code); }
        catch(Exception ex) { ApplicationDiagnostics.Report("Commands.Guide.Copy",ex,Serilog.Events.LogEventLevel.Warning); }
    }
    internal void FocusSearch() { ShowList(false); search.Focus(); }
    private void ShowList()=>ShowList(true);
    private void ShowList(bool focus)
    {
        detailsVisible=false; browser.IsVisible=filters.IsVisible=true; detailView.IsVisible=false; DisposeViews(); details.Children.Clear();
        if(focus)list.Focus();
    }
    private void Navigate(int direction)
    {
        if(!detailsVisible || list.SelectedIndex+direction<0 || list.SelectedIndex+direction>=list.ItemCount)return;
        list.SelectedIndex+=direction; ShowDetails();
    }
    private void Navigation() { position.Text=list.SelectedIndex>=0?$"{list.SelectedIndex+1} / {list.ItemCount}":""; previous.IsEnabled=list.SelectedIndex>0; next.IsEnabled=list.SelectedIndex>=0&&list.SelectedIndex<list.ItemCount-1; }
    private void DisposeViews() { foreach(var view in views)view.Dispose(); views.Clear(); }
    internal void Pause() { foreach(var view in views)view.CancelPending(); }
    public void Dispose() { if(disposed)return; disposed=true; lifetime.Cancel(); DisposeViews(); lifetime.Dispose(); }
}
