using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

internal static class ConnectionTagVisuals
{
    internal static Control Chip(string name,IReadOnlyDictionary<string,string> colors,bool selected=false)
    {
        var label=DesignSystem.Label(name,false,11); label.TextTrimming=TextTrimming.CharacterEllipsis; label.TextWrapping=TextWrapping.NoWrap; label.MaxWidth=120;
        var dot=new Border { Width=7,Height=7,CornerRadius=new CornerRadius(4),Background=Brush.Parse(ProfileTags.Color(name,colors)),VerticalAlignment=VerticalAlignment.Center };
        var body=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*"),ColumnSpacing=5,Children={dot,label} }; Grid.SetColumn(label,1);
        var chip=new Border { Child=body,Padding=new Thickness(6,2),CornerRadius=new CornerRadius(5),BorderThickness=new Thickness(1) };
        DesignSystem.Resource(chip,Border.BackgroundProperty,selected?"SurfaceSelected":"BackgroundTertiary");
        if(selected)chip.BorderBrush=dot.Background; else DesignSystem.Resource(chip,Border.BorderBrushProperty,"BorderDefault");
        ToolTip.SetTip(chip,name); return chip;
    }
}

internal sealed class ConnectionTagsEditor : StackPanel
{
    internal TextBox Input { get; }
    internal ConnectionTagsEditor(string text,IEnumerable<string> known,IReadOnlyDictionary<string,string> colors)
    {
        Spacing=8;
        Input=new TextBox { Text=text,PlaceholderText=L.T("np. produkcja, linux, baza"),Name="ProfileTags" }; AutomationProperties.SetName(Input,L.T("Tagi")); Children.Add(Input);
        var hint=DesignSystem.Hint(L.T("Oddziel tagi przecinkiem lub średnikiem. Kliknij podpowiedź, aby dodać tag; × usuwa go z profilu.")); hint.TextWrapping=TextWrapping.Wrap; Children.Add(hint);
        var chips=new WrapPanel { ItemSpacing=4,LineSpacing=4,Name="ProfileTagChips" }; Children.Add(chips);
        var suggestionTitle=DesignSystem.Label(L.T("Istniejące tagi"),true,11); Children.Add(suggestionTitle);
        var suggestions=new WrapPanel { ItemSpacing=4,LineSpacing=4,Name="ProfileTagSuggestions" }; Children.Add(suggestions);
        var available=known.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        void Refresh()
        {
            var selected=ProfileTags.Parse(Input.Text); chips.Children.Clear(); suggestions.Children.Clear();
            foreach(var name in selected.Take(32))
            {
                var button=new Button { Content=new StackPanel { Orientation=Orientation.Horizontal,Spacing=4,Children={ConnectionTagVisuals.Chip(name,colors),DeskIcons.Create("close",12)} },Padding=new Thickness(0),Classes={"chrome"},Tag=name };
                AutomationProperties.SetName(button,L.T("Usuń tag: ")+name); button.Click+=(_,_)=>Input.Text=ProfileTags.Format(ProfileTags.Parse(Input.Text).Where(t=>!string.Equals(t,name,StringComparison.OrdinalIgnoreCase))); chips.Children.Add(button);
            }
            if(selected.Length>32)chips.Children.Add(DesignSystem.Label("+"+(selected.Length-32),true,11));
            foreach(var name in available.Where(t=>!selected.Contains(t,StringComparer.OrdinalIgnoreCase)).Take(16))
            {
                var button=new Button { Content=ConnectionTagVisuals.Chip(name,colors),Padding=new Thickness(0),Classes={"chrome"},Tag=name };
                AutomationProperties.SetName(button,L.T("Dodaj tag: ")+name); button.Click+=(_,_)=>Input.Text=ProfileTags.Format(ProfileTags.Parse(Input.Text).Append(name)); suggestions.Children.Add(button);
            }
            suggestionTitle.IsVisible=suggestions.Children.Count>0;
        }
        Input.TextChanged+=(_,_)=>Refresh(); Refresh();
    }
}
