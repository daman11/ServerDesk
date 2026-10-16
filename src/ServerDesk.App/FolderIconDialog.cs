using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task<string?> FolderIcon(Window owner,string path,string original)
    {
        var options=FolderIcons.Choices.ToList();
        if(!options.Any(c=>c.Id==original))options.Add(new(original,L.T("Nierozpoznana — folder")));
        var preview=DeskIcons.Create(FolderIcons.Resolve(original),32);
        var choices=new ComboBox { ItemsSource=options,SelectedItem=options.First(c=>c.Id==original),HorizontalAlignment=HorizontalAlignment.Stretch,
            ItemTemplate=new FuncDataTemplate<ProfileIconOption>((item,_)=>item==null?new TextBlock():DeskIcons.Labelled(FolderIcons.Resolve(item.Id),L.T(item.Label))) };
        AutomationProperties.SetName(choices,L.T("Ikona folderu"));
        choices.SelectionChanged+=(_,_)=>preview.Glyph=FolderIcons.Resolve((choices.SelectedItem as ProfileIconOption)?.Id);
        var stack=new StackPanel { Spacing=16,Children={DesignSystem.PageHeading("folder",L.T("Ikona folderu"),path),preview,choices} };
        var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium }; stack.Children.Add(row);
        var dialog=Window(L.T("Ikona folderu"),stack);
        row.Children.Add(Button("Domyślna ikona",()=>choices.SelectedItem=options[0]));
        row.Children.Add(Button("Anuluj",()=>dialog.Close()));
        var save=Button("Zapisz",()=>dialog.Close((choices.SelectedItem as ProfileIconOption)?.Id??"folder")); save.Classes.Add("primary"); row.Children.Add(save);
        return await dialog.ShowDialog<string?>(owner);
    }
}
