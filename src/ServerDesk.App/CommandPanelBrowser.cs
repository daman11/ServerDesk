using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class CommandPanel
{
    private readonly ComboBox riskFilter=new() { Name="CommandRiskFilter",ItemsSource=new[]{L.T("Każdy poziom ryzyka"),Risk(CommandRisk.ReadOnly),Risk(CommandRisk.Modify),Risk(CommandRisk.Dangerous)},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly TextBlock dispatchHint=DesignSystem.Hint("");
    private readonly TextBlock emptyHeading=DesignSystem.Label("",false,16),emptyHint=DesignSystem.Hint("");
    private readonly DeskIcon commandEmptyIcon=DeskIcons.Create("search",26,"ForegroundSecondary");
    private readonly ScrollViewer emptyBrowser=new() { Name="CommandEmptyState",IsVisible=false,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private Button resetFilters=null!,emptyAction=null!;
    private bool HasCommandFilters=>!string.IsNullOrWhiteSpace(search.Text)||category.SelectedIndex>0||riskFilter.SelectedIndex>0;

    private void BuildCommandBrowser(Grid results)
    {
        AutomationProperties.SetName(riskFilter,L.T("Poziom ryzyka"));
        AutomationProperties.SetName(search,L.T("Szukaj komend"));
        ToolTip.SetTip(search,L.T("Enter: szczegóły komendy · ↓: lista wyników · Escape: wyczyść · Ctrl+F: szukaj"));
        var clear=DeskIcons.Button("close","Wyczyść wyszukiwanie komend",()=> { search.Text=""; search.Focus(); },26); clear.IsVisible=false; search.InnerRightContent=clear;
        search.TextChanged+=(_,_)=>clear.IsVisible=!string.IsNullOrEmpty(search.Text);
        search.KeyDown+=(_,e)=>
        {
            if(e.KeyModifiers!=KeyModifiers.None)return;
            if(e.Key==Key.Escape && !string.IsNullOrEmpty(search.Text)) { search.Text=""; queryDebounce.Cancel(); RefreshList(); e.Handled=true; }
            else if(e.Key is Key.Enter or Key.Down)
            {
                queryDebounce.Cancel(); RefreshList();
                list.SelectedItem??=list.Items.OfType<CommandMatch>().FirstOrDefault();
                if(list.SelectedItem!=null) { if(e.Key==Key.Enter)ShowCommandDetails(); else list.Focus(); }
                e.Handled=true;
            }
        };
        AddHandler(KeyDownEvent,(_,e)=>
        {
            if(e.Key==Key.F && e.KeyModifiers==KeyModifiers.Control) { FocusSearch(); search.SelectAll(); e.Handled=true; }
        },RoutingStrategies.Tunnel);
        count.Name="CommandResultCount"; count.TextTrimming=TextTrimming.CharacterEllipsis;
        resetFilters=Dialogs.Button("Wyczyść filtry",ResetCommandFilters); resetFilters.Name="CommandResetFilters"; resetFilters.Classes.Add("chrome"); resetFilters.Padding=new Thickness(5,2); resetFilters.MinHeight=26;
        var summary=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=6,Children={count,resetFilters} }; Grid.SetColumn(resetFilters,1); results.Children.Add(summary);
        emptyHeading.FontWeight=FontWeight.SemiBold; emptyHeading.TextWrapping=TextWrapping.Wrap;
        emptyAction=new Button { Name="CommandEmptyAction",HorizontalAlignment=HorizontalAlignment.Left };
        emptyAction.Click+=(_,_)=>
        {
            if(HasCommandFilters)ResetCommandFilters();
            else if(section.SelectedIndex==1 && !libraryLocked)Run(()=>EditAsync(null));
            else if(section.SelectedIndex==3 && !state.Preferences.RememberRecent)Run(ToggleHistoryAsync);
            else { section.SelectedIndex=0; RefreshList(); search.Focus(); }
        };
        emptyBrowser.Content=new StackPanel { Spacing=10,Margin=new Thickness(12),MaxWidth=310,VerticalAlignment=VerticalAlignment.Center,Children={commandEmptyIcon,emptyHeading,emptyHint,emptyAction} };
        Grid.SetRow(emptyBrowser,1); results.Children.Add(emptyBrowser);
        dispatchHint.Name="CommandDispatchHint";
    }

    private void ResetCommandFilters()
    {
        updating=true;
        try { search.Text=""; category.SelectedIndex=0; riskFilter.SelectedIndex=0; sort.SelectedIndex=0; }
        finally { updating=false; }
        queryDebounce.Cancel(); RefreshList(); search.Focus();
    }

    private void UpdateCommandBrowser(int matches)
    {
        list.IsVisible=matches>0; emptyBrowser.IsVisible=matches==0;
        resetFilters.IsVisible=HasCommandFilters||sort.SelectedIndex!=0; ToolTip.SetTip(count,count.Text);
        var filtered=HasCommandFilters;
        commandEmptyIcon.Glyph=DeskIcons.Resolve(filtered?"search":section.SelectedIndex switch { 2=>"star",3=>"history",_=>"square-terminal" });
        emptyHeading.Text=L.T(filtered?"Brak pasujących komend":section.SelectedIndex switch { 1=>"Brak własnych komend",2=>"Brak ulubionych komend",3=>state.Preferences.RememberRecent?"Brak ostatnich komend":"Historia komend jest wyłączona",_=>"Brak komend dla tego profilu" });
        emptyHint.Text=L.T(filtered?"Zmień zapytanie albo wyczyść filtry. Profil systemu pozostanie bez zmian.":section.SelectedIndex switch {
            1=>libraryLocked?"Własna biblioteka jest zablokowana lub uszkodzona. Wbudowana ściągawka pozostaje dostępna.":"Dodaj własny szablon lub zduplikuj komendę z wbudowanej ściągawki.",
            2=>"Otwórz szczegóły komendy i oznacz ją gwiazdką.",
            3=>state.Preferences.RememberRecent?"Tutaj pojawią się komendy po użyciu Kopiuj, Wstaw lub Wykonaj.":"Włącz historię, aby zapamiętywać identyfikatory i czas użycia. Wartości parametrów nie będą zapisywane.",
            _=>"Sprawdź system i powłokę w Profilu systemu lub wybierz inną sekcję." });
        var action=filtered?"Wyczyść filtry":section.SelectedIndex==1&&!libraryLocked?"Nowa komenda":section.SelectedIndex==3&&!state.Preferences.RememberRecent?"Włącz historię komend":"Ściągawka poleceń";
        emptyAction.Content=DeskIcons.Labelled(filtered?"close":section.SelectedIndex==1&&!libraryLocked?"plus":section.SelectedIndex==3&&!state.Preferences.RememberRecent?"history":"square-terminal",L.T(action));
        AutomationProperties.SetName(emptyAction,L.T(action));
    }

    private static Border RiskBadge(CommandRisk risk)
    {
        var badge=DesignSystem.Badge(Risk(risk));
        DesignSystem.Resource((TextBlock)badge.Child!,TextBlock.ForegroundProperty,risk switch { CommandRisk.Dangerous=>"Danger",CommandRisk.Modify=>"Warning",_=>"Success" });
        return badge;
    }
    private static string ShellLabel(CommandShell value)=>value==CommandShell.Any?L.T("Dowolna powłoka"):value.ToString();
}
