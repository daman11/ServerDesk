using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;

namespace ServerDesk.App;

internal sealed class HelpPane : WorkspacePane
{
    private readonly HelpTopic[] topics=HelpContent.Read(L.Language);
    private readonly TextBox search=new() { Name="HelpSearch",MaxLength=200 };
    private readonly ListBox contents=new() { Name="HelpTopics",Classes={"help-topics"} };
    private readonly ComboBox compactContents=new() { Name="HelpCompactTopics",HorizontalAlignment=HorizontalAlignment.Stretch,IsVisible=false };
    private readonly StackPanel article=new() { Name="HelpArticle",Spacing=16,MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly ScrollViewer articleScroll=new() { Name="HelpArticleScroll",HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
    private readonly TextBlock count=DesignSystem.Hint("");
    private readonly TextBlock copyStatus=DesignSystem.Hint("");
    private readonly Button copy;
    private HelpTopic? selected;
    private bool updating,disposed;
    internal override string DisplayName=>L.T("Pomoc");

    internal HelpPane(Func<Task> about)
    {
        Name="HelpPane"; RowDefinitions=new("Auto,Auto,Auto,*,Auto"); RowSpacing=14; Margin=new Thickness(20); ClipToBounds=true;
        var header=DesignSystem.PageHeading("help",L.T("Pomoc"),L.T("Dokumentacja ServerDesk — dostępna bez internetu.")); header.Name="HelpHeading"; Children.Add(header);
        search.PlaceholderText=L.T("Szukaj w dokumentacji…"); AutomationProperties.SetName(search,L.T("Szukaj w dokumentacji"));
        search.InnerLeftContent=new Border { Child=DeskIcons.Create("search",15),Margin=new Thickness(8,0,0,0) };
        var clear=DeskIcons.Button("close","Wyczyść wyszukiwanie",()=> { search.Text=""; search.Focus(); },24); clear.IsVisible=false; search.InnerRightContent=clear;
        count.Name="HelpResultCount";
        var searchRow=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=12,Children={search,count} }; Grid.SetColumn(count,1); Grid.SetRow(searchRow,1); Children.Add(searchRow);
        compactContents.ItemTemplate=new FuncDataTemplate<HelpTopic>((topic,_)=>topic==null?new TextBlock():DeskIcons.Labelled(topic.Icon,topic.Title));
        AutomationProperties.SetName(compactContents,L.T("Tematy pomocy")); Grid.SetRow(compactContents,2); Children.Add(compactContents);
        contents.ItemTemplate=new FuncDataTemplate<HelpTopic>((topic,_)=>
        {
            if(topic==null)return new TextBlock();
            var label=DesignSystem.Label(topic.Title); label.TextWrapping=TextWrapping.Wrap;
            var row=new Grid { ColumnDefinitions=new("20,*"),ColumnSpacing=8,Margin=new Thickness(6,8),Children={DeskIcons.Create(topic.Icon,17),label} }; Grid.SetColumn(label,1); return row;
        });
        contents.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled); AutomationProperties.SetName(contents,L.T("Tematy pomocy"));
        var navigation=new Border { Name="HelpNavigation",Child=contents,Padding=new Thickness(0,0,12,0),BorderThickness=new Thickness(0,0,1,0) }; DesignSystem.Resource(navigation,Border.BorderBrushProperty,"BorderDefault");
        articleScroll.Content=article; var reading=new Border { Child=articleScroll,Padding=new Thickness(18),CornerRadius=new CornerRadius(8) }; DesignSystem.Resource(reading,Border.BackgroundProperty,"BackgroundSecondary");
        var body=new Grid { Name="HelpBody",ColumnDefinitions=new("230,*"),ColumnSpacing=18,Children={navigation,reading} }; Grid.SetColumn(reading,1); Grid.SetRow(body,3); Children.Add(body);
        copy=Dialogs.Button("Kopiuj temat",()=>_=CopyAsync()); copy.Name="HelpCopyTopic";
        var aboutButton=Dialogs.Button("O aplikacji",()=>_=about()); aboutButton.Name="HelpAbout";
        var aboutLabel=aboutButton.Content; var aboutIcon=DeskIcons.Create("info",18);
        var actions=new WrapPanel { Orientation=Orientation.Horizontal,Children={copy,aboutButton} }; foreach(var button in actions.Children)button.Margin=new Thickness(0,0,8,4);
        copyStatus.Name="HelpCopyStatus"; copyStatus.IsVisible=false;
        var footer=new StackPanel { Spacing=4,Children={copyStatus,actions} }; Grid.SetRow(footer,4); Children.Add(footer);
        contents.SelectionChanged+=(_,_)=> { if(!updating && contents.SelectedItem is HelpTopic topic)Select(topic); };
        compactContents.SelectionChanged+=(_,_)=> { if(!updating && compactContents.SelectedItem is HelpTopic topic)Select(topic); };
        search.TextChanged+=(_,_)=> { clear.IsVisible=!string.IsNullOrEmpty(search.Text); Filter(); };
        search.KeyDown+=(_,e)=>
        {
            if(e.KeyModifiers==KeyModifiers.None && (e.Key is Key.Enter or Key.Down) && contents.ItemCount>0)
            { if(contents.IsVisible)contents.Focus(); else compactContents.Focus(); e.Handled=true; }
        };
        KeyDown+=(_,e)=>
        {
            if(e.Key==Key.F && e.KeyModifiers==KeyModifiers.Control) { FocusContent(); e.Handled=true; }
            else if(e.Key==Key.Escape && e.KeyModifiers==KeyModifiers.None && !string.IsNullOrEmpty(search.Text)) { search.Text=""; search.Focus(); e.Handled=true; }
        };
        SizeChanged+=(_,_)=>
        {
            // Include our margin so changing it cannot oscillate at the breakpoint.
            var shortPanel=Bounds.Height+Margin.Top+Margin.Bottom<400; Margin=new Thickness(shortPanel?10:20);
            var narrow=Bounds.Width<760; navigation.IsVisible=!narrow; compactContents.IsVisible=narrow;
            body.ColumnDefinitions[0].Width=new GridLength(narrow?0:230); body.ColumnSpacing=narrow?0:18;
            count.IsVisible=Bounds.Width>=480;
            header.IsVisible=!shortPanel; RowSpacing=shortPanel?6:14; reading.Padding=new Thickness(shortPanel?12:18);
            aboutButton.Content=Bounds.Width<360?aboutIcon:aboutLabel;
        };
        Filter();
    }
    private void Filter()
    {
        var matches=topics.Where(topic=>topic.Matches(search.Text??"")).ToArray();
        updating=true;
        try { contents.ItemsSource=compactContents.ItemsSource=matches; }
        finally { updating=false; }
        count.Text=L.F($"Tematy: {matches.Length} / {topics.Length}");
        if(matches.Length>0)Select(selected is {} current && matches.Contains(current)?current:matches[0]);
        else
        {
            selected=null; copy.IsEnabled=false; copyStatus.IsVisible=false; article.Children.Clear();
            article.Children.Add(DesignSystem.PageHeading("search",L.T("Brak tematów pomocy"),L.T("Skróć zapytanie lub wyczyść wyszukiwanie, aby zobaczyć wszystkie tematy.")));
            var reset=Dialogs.Button("Wyczyść wyszukiwanie",()=> { search.Text=""; search.Focus(); }); reset.Name="HelpClearEmpty"; article.Children.Add(reset);
        }
    }
    private void Select(HelpTopic topic)
    {
        updating=true;
        try { contents.SelectedItem=compactContents.SelectedItem=topic; }
        finally { updating=false; }
        if(ReferenceEquals(selected,topic))return;
        selected=topic; copy.IsEnabled=true; copyStatus.IsVisible=false; article.Children.Clear(); articleScroll.Offset=default;
        var title=DesignSystem.Label(topic.Title,false,22); title.Name="HelpArticleTitle"; title.FontWeight=FontWeight.SemiBold; title.TextWrapping=TextWrapping.Wrap;
        article.Children.Add(title); article.Children.Add(DesignSystem.Hint(topic.Summary));
        foreach(var section in topic.Sections)
        {
            var heading=DesignSystem.Label(section.Title); heading.FontWeight=FontWeight.SemiBold; heading.TextWrapping=TextWrapping.Wrap; heading.Margin=new Thickness(0,8,0,0); article.Children.Add(heading);
            foreach(var paragraph in section.Paragraphs)
            {
                var text=new SelectableTextBlock { Text=paragraph,TextWrapping=TextWrapping.Wrap }; DesignSystem.Resource(text,TextBlock.FontSizeProperty,"FontBody"); DesignSystem.Resource(text,TextBlock.ForegroundProperty,"ForegroundPrimary"); article.Children.Add(text);
            }
        }
    }
    private async Task CopyAsync()
    {
        if(selected==null || disposed)return; var snapshot=selected.PlainText; copy.IsEnabled=false;
        try
        {
            var clipboard=TopLevel.GetTopLevel(this)?.Clipboard??throw new InvalidOperationException(); await clipboard.SetTextAsync(snapshot);
            copyStatus.Text=L.T("Skopiowano temat pomocy."); DesignSystem.Resource(copyStatus,TextBlock.ForegroundProperty,"ForegroundSecondary");
        }
        catch { copyStatus.Text=L.T("Nie udało się skopiować tematu. Zaznacz tekst i skopiuj go ręcznie."); DesignSystem.Resource(copyStatus,TextBlock.ForegroundProperty,"Danger"); }
        finally { if(!disposed) { copyStatus.IsVisible=true; copy.IsEnabled=selected!=null; } }
    }
    internal override void FocusContent() { search.Focus(); search.SelectAll(); }
    public override void Dispose()=>disposed=true;
}
