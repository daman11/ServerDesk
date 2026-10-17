using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private Button tagFilterButton=null!;
    private readonly WrapPanel activeTagFilters=new() { ItemSpacing=4,LineSpacing=4,Name="ActiveConnectionTags" };
    private readonly TextBlock activeTagMode=DesignSystem.Label("",true,11);
    private void BuildTagFilters(StackPanel body)
    {
        tagFilterButton=Dialogs.Button("Tagi i kolory",()=>_=Safe(OpenTagFiltersAsync)); tagFilterButton.Name="ConnectionTagFilter"; tagFilterButton.Classes.Add("chrome");
        tagFilterButton.HorizontalAlignment=HorizontalAlignment.Stretch; tagFilterButton.HorizontalContentAlignment=HorizontalAlignment.Stretch; tagFilterButton.Padding=new Thickness(8,6);
        body.Children.Add(tagFilterButton); body.Children.Add(activeTagMode); body.Children.Add(activeTagFilters);
    }
    private void UpdateTagFilters()
    {
        var heading=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*,Auto"),ColumnSpacing=7,Children={DeskIcons.Create("tags",15)} };
        var title=DesignSystem.Label(L.T("Tagi i kolory")); Grid.SetColumn(title,1); heading.Children.Add(title);
        var count=DesignSystem.Badge(HasTagFilter?(vm.TagMode==TagFilterMode.Untagged?L.T("Bez tagów"):vm.SelectedTags.Length.ToString()):vm.AllServers.SelectMany(p=>ProfileTags.Parse(p.Tags)).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString());
        ToolTip.SetTip(count,HasTagFilter?L.T("Aktywne filtry tagów"):L.T("Liczba dostępnych tagów"));
        Grid.SetColumn(count,2); heading.Children.Add(count); tagFilterButton.Content=heading;
        activeTagMode.Text=L.T(vm.TagMode==TagFilterMode.All?"Wszystkie wybrane tagi":"Dowolny z wybranych tagów"); activeTagMode.IsVisible=vm.TagMode!=TagFilterMode.Untagged && vm.SelectedTags.Length>1;
        tagFilterButton.Classes.Set("active",HasTagFilter); activeTagFilters.Children.Clear();
        if(vm.TagMode==TagFilterMode.Untagged)
        {
            var clear=Dialogs.Button("Bez tagów",()=> { vm.TagMode=TagFilterMode.Any; vm.SelectedTags=[]; QueueTree(); }); clear.Content=DeskIcons.Labelled("close",L.T("Bez tagów"),12); clear.Padding=new Thickness(5,2); activeTagFilters.Children.Add(clear);
        }
        else
        {
            foreach(var name in vm.SelectedTags.Take(3))
            {
                activeTagFilters.Children.Add(ConnectionTagVisuals.Remove(name,vm.TagColors,()=> { vm.SelectedTags=vm.SelectedTags.Where(t=>!string.Equals(t,name,StringComparison.OrdinalIgnoreCase)).ToArray(); QueueTree(); },"Usuń filtr tagu: "));
            }
            if(vm.SelectedTags.Length>3) { var more=Dialogs.Button("Tagi i kolory",()=>_=Safe(OpenTagFiltersAsync)); more.Content="+"+(vm.SelectedTags.Length-3); more.Padding=new Thickness(5,2); activeTagFilters.Children.Add(more); }
        }
        activeTagFilters.IsVisible=HasTagFilter;
        ToolTip.SetTip(activeTagFilters,L.T(vm.TagMode==TagFilterMode.All?"Wszystkie wybrane tagi":vm.TagMode==TagFilterMode.Untagged?"Tylko połączenia bez tagów":"Dowolny z wybranych tagów"));
        ToolTip.SetTip(tagFilterButton,L.T("Filtruj po tagach i ustaw ich kolory. Kolor jest wspólny dla wszystkich połączeń z tym tagiem."));
    }
    private Control WithConnectionTags(Control row,ServerProfile profile)
    {
        var tags=ProfileTags.Parse(profile.Tags); if(tags.Length==0)return row;
        var stack=new StackPanel { Spacing=3,ClipToBounds=true,Children={row} };
        var chips=new Grid { Name="ConnectionRowTags",ColumnDefinitions=new ColumnDefinitions(tags.Length==1?"*,0,0":"Auto,*,Auto"),ColumnSpacing=4,Margin=new Thickness(3+20+DesignTokens.IconGap*2,0,0,2),ClipToBounds=true };
        for(var i=0;i<Math.Min(2,tags.Length);i++)
        {
            var name=tags[i]; var chip=ConnectionTagVisuals.Chip(name,vm.TagColors,vm.SelectedTags.Contains(name,StringComparer.OrdinalIgnoreCase)); chip.MaxWidth=i==0 && tags.Length>1?90:130; chip.ClipToBounds=true; chip.HorizontalAlignment=HorizontalAlignment.Left; Grid.SetColumn(chip,i); chips.Children.Add(chip);
        }
        if(tags.Length>2) { var more=DesignSystem.Label("+"+(tags.Length-2),true,11); Grid.SetColumn(more,2); chips.Children.Add(more); }
        ToolTip.SetTip(chips,string.Join(", ",tags)); stack.Children.Add(chips); return stack;
    }
    private sealed record TagChoice(string Name,int Count);
    private async Task OpenTagFiltersAsync()
    {
        var selected=vm.SelectedTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var colors=new Dictionary<string,string>(vm.TagColors,StringComparer.OrdinalIgnoreCase);
        var counts=vm.AllServers.SelectMany(p=>ProfileTags.Parse(p.Tags)).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.OrdinalIgnoreCase);
        var options=counts.Select(g=>new TagChoice(g.Key,g.Value)).Concat(selected.Where(t=>!counts.ContainsKey(t)).Select(t=>new TagChoice(t,0)))
            .OrderBy(t=>t.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
        var dialog=new Window { Title=L.T("Tagi i kolory"),Name="ConnectionTagDialog",Width=620,Height=680,MinWidth=420,MinHeight=460,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        Dialogs.FitToOwnerScreen(dialog,this);
        var root=new Grid { Margin=new Thickness(20),RowDefinitions=new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto"),RowSpacing=10 }; dialog.Content=root;
        root.Children.Add(DesignSystem.PageHeading("tags",L.T("Tagi połączeń"),L.T("Wybierz etykiety do filtrowania. Kolory obowiązują we wszystkich połączeniach.")));
        var mode=new ComboBox { Name="ConnectionTagMode",ItemsSource=new[]{L.T("Dowolny z wybranych tagów"),L.T("Wszystkie wybrane tagi"),L.T("Bez tagów")},SelectedIndex=(int)vm.TagMode,HorizontalAlignment=HorizontalAlignment.Stretch };
        AutomationProperties.SetName(mode,L.T("Sposób dopasowania tagów")); Grid.SetRow(mode,1); root.Children.Add(mode);
        var search=new TextBox { Name="ConnectionTagSearch",PlaceholderText=L.T("Szukaj tagu") }; AutomationProperties.SetName(search,L.T("Szukaj tagu")); Grid.SetRow(search,2); root.Children.Add(search);
        var clearSearch=DeskIcons.Button("close","Wyczyść wyszukiwanie",()=>search.Text=""); clearSearch.Classes.Add("chrome"); search.InnerRightContent=clearSearch;
        var tools=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=8 }; Grid.SetRow(tools,3); root.Children.Add(tools);
        var selectedOnly=new CheckBox { Name="SelectedConnectionTagsOnly",Content=L.T("Tylko wybrane"),IsChecked=false }; tools.Children.Add(selectedOnly);
        var selectVisible=Dialogs.Button("Zaznacz widoczne",()=> { }); selectVisible.Name="SelectVisibleConnectionTags"; selectVisible.Classes.Add("chrome"); Grid.SetColumn(selectVisible,1); tools.Children.Add(selectVisible);
        var list=new ListBox { Name="ConnectionTagList" };
        list.Styles.Add(new Style(s=>s.OfType<ListBoxItem>()) { Setters={new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch)} });
        var empty=DesignSystem.Hint(L.T("Brak tagów. Dodaj je w edytorze połączenia, w sekcji Organizacja.")); empty.TextWrapping=TextWrapping.Wrap; empty.Margin=new Thickness(12); empty.VerticalAlignment=VerticalAlignment.Center;
        var listHost=new Grid { Children={list,empty} }; Grid.SetRow(listHost,4); root.Children.Add(listHost);
        var status=DesignSystem.Hint(""); status.Name="ConnectionTagStatus"; status.TextWrapping=TextWrapping.Wrap;
        var preview=DesignSystem.Label("",false,13); preview.Name="ConnectionTagPreview"; preview.TextWrapping=TextWrapping.Wrap;
        var summary=DesignSystem.Card(new StackPanel { Spacing=4,Children={preview,status} },new Thickness(10,8)); Grid.SetRow(summary,5); root.Children.Add(summary);
        var searchMatch=ProfileSearch.Compile(vm.Search);
        var scope=vm.AllServers.Where(p=>searchMatch(p) && (vm.ProtocolFilter==null || p.Protocol==vm.ProtocolFilter) &&
            (section=="connections" || section=="favorite" && p.Favorite || section=="recent" && p.LastConnected!=null)).ToArray();
        void UpdateSummary()
        {
            var predicate=ProfileTags.CompileFilter(selected,(TagFilterMode)mode.SelectedIndex);
            preview.Text=L.F($"Pasujące połączenia: {scope.Count(predicate)}");
            status.Text=mode.SelectedIndex==2?L.T("Tylko połączenia bez tagów"):L.F($"Wybrane tagi: {selected.Count}");
        }
        TagChoice[] Visible()=>options.Where(t=>t.Name.Contains((search.Text??"").Trim(),StringComparison.OrdinalIgnoreCase) && (selectedOnly.IsChecked!=true || selected.Contains(t.Name))).ToArray();
        void Refresh()
        {
            var matches=Visible(); list.ItemsSource=matches;
            empty.IsVisible=matches.Length==0; empty.Text=options.Length==0?L.T("Brak tagów. Dodaj je w edytorze połączenia, w sekcji Organizacja."):selectedOnly.IsChecked==true && selected.Count==0?L.T("Nie wybrano tagów. Wyłącz „Tylko wybrane”, aby zobaczyć całą listę."):L.T("Brak wyników");
            clearSearch.IsVisible=!string.IsNullOrEmpty(search.Text); selectVisible.IsEnabled=mode.SelectedIndex!=2 && matches.Any(t=>!selected.Contains(t.Name)); selectedOnly.IsEnabled=mode.SelectedIndex!=2;
            UpdateSummary();
        }
        list.ItemTemplate=new FuncDataTemplate<TagChoice>((item,_)=>
        {
            if(item==null)return null;
            var row=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),ColumnSpacing=10,Margin=new Thickness(2,4) };
            var check=new CheckBox { Content=ConnectionTagVisuals.Chip(item.Name,colors,selected.Contains(item.Name),double.PositiveInfinity),IsChecked=selected.Contains(item.Name),IsEnabled=mode.SelectedIndex!=2,Name="ConnectionTagChoice",Tag=item.Name,VerticalAlignment=VerticalAlignment.Center,HorizontalContentAlignment=HorizontalAlignment.Stretch };
            AutomationProperties.SetName(check,item.Name); check.IsCheckedChanged+=(_,_)=> { if(check.IsChecked==true)selected.Add(item.Name); else selected.Remove(item.Name); check.Content=ConnectionTagVisuals.Chip(item.Name,colors,selected.Contains(item.Name),double.PositiveInfinity); UpdateSummary(); selectVisible.IsEnabled=mode.SelectedIndex!=2 && Visible().Any(t=>!selected.Contains(t.Name)); if(selectedOnly.IsChecked==true)Dispatcher.UIThread.Post(Refresh,DispatcherPriority.Background); }; row.Children.Add(check);
            var count=DesignSystem.Badge(item.Count.ToString()); count.VerticalAlignment=VerticalAlignment.Center; Grid.SetColumn(count,1); row.Children.Add(count); ToolTip.SetTip(count,L.T("Liczba połączeń z tagiem"));
            var color=new Button { Name="ConnectionTagColor",Tag=item.Name,Width=30,Height=30,MinHeight=30,Padding=new Thickness(5),IsEnabled=item.Name.Length<=256 && !item.Name.Any(char.IsControl) };
            void UpdateColor() { color.Content=new Border { Background=Brush.Parse(ProfileTags.Color(item.Name,colors)),CornerRadius=new CornerRadius(8),Width=16,Height=16 }; check.Content=ConnectionTagVisuals.Chip(item.Name,colors,selected.Contains(item.Name),double.PositiveInfinity); }
            UpdateColor(); AutomationProperties.SetName(color,L.T("Kolor tagu: ")+item.Name); ToolTip.SetTip(color,L.T("Kolor tagu: ")+item.Name);
            var automatic=new MenuItem { Header=L.T("Kolor automatyczny"),Icon=DeskIcons.Create("refresh",16) }; automatic.Click+=(_,_)=> { colors.Remove(item.Name); UpdateColor(); };
            var palette=new List<MenuItem> { automatic };
            foreach(var value in ProfileAppearance.Colors.Where(c=>c.Length>0))
            {
                var swatch=new MenuItem { Header=ConnectionTagVisuals.ColorName(value),Icon=new Border { Width=14,Height=14,Background=Brush.Parse(value),CornerRadius=new CornerRadius(7) },Tag=value }; ToolTip.SetTip(swatch,value);
                swatch.Click+=(_,_)=> { colors[item.Name]=value; UpdateColor(); }; palette.Add(swatch);
            }
            color.ContextMenu=new ContextMenu { ItemsSource=palette }; color.Click+=(_,_)=>color.ContextMenu.Open(color); Grid.SetColumn(color,2); row.Children.Add(color); return row;
        });
        var actions=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),ColumnSpacing=8 }; Grid.SetRow(actions,6); root.Children.Add(actions);
        var clear=Dialogs.Button("Wyczyść wybór",()=> { selected.Clear(); mode.SelectedIndex=0; Refresh(); }); clear.HorizontalAlignment=HorizontalAlignment.Left; actions.Children.Add(clear);
        var cancel=Dialogs.Button("Anuluj",dialog.Close); Grid.SetColumn(cancel,1); actions.Children.Add(cancel);
        var apply=Dialogs.Button("Zastosuj",()=> { }); apply.Name="ApplyConnectionTags"; apply.IsDefault=true; apply.Classes.Add("primary"); Grid.SetColumn(apply,2); actions.Children.Add(apply);
        var applying=false; dialog.Closing+=(_,e)=> { if(applying)e.Cancel=true; };
        apply.Click+=async (_,_)=>
        {
            applying=true; root.IsEnabled=false;
            try
            {
                await repository.SaveTagColorsAsync(colors,lifetime.Token);
                vm.UpdateTagColors(colors); vm.TagMode=(TagFilterMode)mode.SelectedIndex; vm.SelectedTags=mode.SelectedIndex==2?[]:selected.ToArray(); QueueTree(); applying=false; dialog.Close();
            }
            catch(Exception ex) { status.Text=ex.Message; DesignSystem.Resource(status,TextBlock.ForegroundProperty,"Danger"); }
            finally { applying=false; root.IsEnabled=true; }
        };
        selectVisible.Click+=(_,_)=> { foreach(var option in Visible())selected.Add(option.Name); Refresh(); };
        selectedOnly.IsCheckedChanged+=(_,_)=>Refresh(); mode.SelectionChanged+=(_,_)=> { if(mode.SelectedIndex==2)selectedOnly.IsChecked=false; Refresh(); }; search.TextChanged+=(_,_)=>Refresh();
        dialog.KeyDown+=(_,e)=>
        {
            if(e.Key==Key.F && e.KeyModifiers==KeyModifiers.Control) { search.Focus(); search.SelectAll(); e.Handled=true; }
            else if(e.Key==Key.Escape) { if(!string.IsNullOrEmpty(search.Text)) { search.Text=""; search.Focus(); } else dialog.Close(); e.Handled=true; }
        };
        dialog.Opened+=(_,_)=>search.Focus();
        Refresh(); await dialog.ShowDialog(this);
    }
}
