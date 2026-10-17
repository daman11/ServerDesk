using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using ServerDesk.Core;

namespace ServerDesk.App;

internal static class ConnectionTagVisuals
{
    internal static Border Chip(string name,IReadOnlyDictionary<string,string> colors,bool selected=false,double maxTextWidth=150,bool removable=false,bool quiet=false)
    {
        var subdued=quiet && !selected;
        var label=DesignSystem.Label(name,subdued,11); label.TextTrimming=TextTrimming.CharacterEllipsis; label.TextWrapping=TextWrapping.NoWrap; label.MaxWidth=maxTextWidth;
        var dot=new Border { Width=subdued?5:7,Height=subdued?5:7,CornerRadius=new CornerRadius(4),Background=Brush.Parse(ProfileTags.Color(name,colors)),VerticalAlignment=VerticalAlignment.Center };
        var body=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*,Auto"),ColumnSpacing=5,Children={dot,label} }; Grid.SetColumn(label,1);
        if(removable) { var close=DeskIcons.Create("close",12,"ForegroundSecondary"); Grid.SetColumn(close,2); body.Children.Add(close); }
        var color=Color.Parse(ProfileTags.Color(name,colors));
        var chip=new Border { Child=body,Padding=subdued?new Thickness(2,1):new Thickness(7,3),CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(subdued?0:1),
            Background=subdued?Brushes.Transparent:new SolidColorBrush(Color.FromArgb(selected?(byte)40:(byte)18,color.R,color.G,color.B)),
            BorderBrush=new SolidColorBrush(Color.FromArgb(selected?(byte)170:(byte)75,color.R,color.G,color.B)) };
        ToolTip.SetTip(chip,name); return chip;
    }
    internal static Button Remove(string name,IReadOnlyDictionary<string,string> colors,Action action,string accessiblePrefix)
    {
        var button=new Button { Content=Chip(name,colors,true,150,true),Tag=name,Padding=new Thickness(0),MinHeight=24,Classes={"chrome"} };
        AutomationProperties.SetName(button,L.T(accessiblePrefix)+name); ToolTip.SetTip(button,L.T(accessiblePrefix)+name); button.Click+=(_,_)=>action(); return button;
    }
    internal static string ColorName(string color)=>L.T(color switch
    {
        "#7C8CFF"=>"Indygo","#4C9CE5"=>"Niebieski","#69CFB6"=>"Miętowy","#E5B65C"=>"Bursztynowy",
        "#EB8181"=>"Koralowy","#C792EA"=>"Fioletowy","#66C5D6"=>"Turkusowy","#A8B56A"=>"Oliwkowy",_=>color
    });
}

internal sealed class ConnectionTagsEditor : StackPanel
{
    internal TextBox Input { get; }
    private readonly TextBox entry;
    internal void CommitPending()
    {
        if(ProfileTags.Parse(entry.Text).Length==0)return;
        Input.Text=ProfileTags.Format(ProfileTags.Parse(Input.Text).Concat(ProfileTags.Parse(entry.Text))); entry.Text="";
    }
    internal ConnectionTagsEditor(string text,IEnumerable<string> known,IReadOnlyDictionary<string,string> colors)
    {
        Spacing=8;
        Input=new TextBox { Text=text,PlaceholderText=L.T("np. produkcja, linux, baza"),Name="ProfileTags" }; AutomationProperties.SetName(Input,L.T("Tagi"));
        var chips=new WrapPanel { ItemSpacing=5,LineSpacing=5,Name="ProfileTagChips" };
        var empty=DesignSystem.Hint(L.T("To połączenie nie ma jeszcze tagów."));
        var assigned=new StackPanel { Spacing=6,Children={chips,empty} }; Children.Add(assigned);
        entry=new TextBox { Name="ProfileTagEntry",PlaceholderText=L.T("Dodaj lub wyszukaj tag…") }; AutomationProperties.SetName(entry,L.T("Dodaj lub wyszukaj tag…"));
        var add=DeskIcons.Button("plus","Dodaj tag",()=> { CommitPending(); entry.Focus(); }); add.Name="AddProfileTag"; add.Width=32; add.Height=32;
        entry.InnerRightContent=add; Children.Add(entry);
        var hint=DesignSystem.Hint(L.T("Enter dodaje tag. Możesz wkleić kilka nazw oddzielonych przecinkiem. × usuwa przypisanie.")); Children.Add(hint);
        var suggestionTitle=DesignSystem.Label(L.T("Istniejące tagi"),true,11); Children.Add(suggestionTitle);
        var suggestions=new WrapPanel { ItemSpacing=4,LineSpacing=4,Name="ProfileTagSuggestions" }; Children.Add(suggestions);
        var suggestionHint=DesignSystem.Hint(""); suggestionHint.Name="ProfileTagSuggestionHint"; Children.Add(suggestionHint);
        var raw=new Expander { Name="ProfileTagRawEditor",Header=L.T("Edytuj listę tagów"),Content=Input,HorizontalAlignment=HorizontalAlignment.Stretch,Classes={"network-options"} }; Children.Add(raw);
        var available=known.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        void Refresh()
        {
            var selected=ProfileTags.Parse(Input.Text); chips.Children.Clear(); suggestions.Children.Clear();
            empty.IsVisible=selected.Length==0;
            foreach(var name in selected.Take(32))
            {
                chips.Children.Add(ConnectionTagVisuals.Remove(name,colors,()=>Input.Text=ProfileTags.Format(ProfileTags.Parse(Input.Text).Where(t=>!string.Equals(t,name,StringComparison.OrdinalIgnoreCase))),"Usuń tag: "));
            }
            if(selected.Length>32)chips.Children.Add(DesignSystem.Label("+"+(selected.Length-32),true,11));
            var query=(entry.Text??"").Trim();
            var matches=available.Where(t=>!selected.Contains(t,StringComparer.OrdinalIgnoreCase) && t.Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach(var name in matches.Take(12))
            {
                var button=new Button { Content=ConnectionTagVisuals.Chip(name,colors),Padding=new Thickness(0),Classes={"chrome"},Tag=name };
                AutomationProperties.SetName(button,L.T("Dodaj tag: ")+name); button.Click+=(_,_)=> { Input.Text=ProfileTags.Format(ProfileTags.Parse(Input.Text).Append(name)); entry.Text=""; entry.Focus(); }; suggestions.Children.Add(button);
            }
            suggestionTitle.IsVisible=suggestions.Children.Count>0;
            suggestionHint.Text=matches.Length>12?L.F($"Podpowiedzi: {12} z {matches.Length}. Wpisz nazwę, aby zawęzić listę."):query.Length>0 && matches.Length==0?L.T("Nowy tag zostanie dodany klawiszem Enter lub przy zapisie profilu."):"";
            suggestionHint.IsVisible=suggestionHint.Text.Length>0; add.IsEnabled=ProfileTags.Parse(entry.Text).Length>0;
        }
        entry.AddHandler(KeyDownEvent,(_,e)=>
        {
            if(e.Key==Key.Enter) { CommitPending(); e.Handled=true; }
            else if(e.Key==Key.Escape && !string.IsNullOrEmpty(entry.Text)) { entry.Text=""; e.Handled=true; }
        },RoutingStrategies.Tunnel);
        Input.TextChanged+=(_,_)=>Refresh(); entry.TextChanged+=(_,_)=>Refresh(); Refresh();
    }
}
