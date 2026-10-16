using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;
internal sealed class ProfileIconPicker : Grid
{
    private readonly ComboBox choices=new() { HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly DeskIcon preview=DeskIcons.Create("ssh",24);
    private Protocol protocol;
    internal event Action? Changed;
    internal string SelectedId=>(choices.SelectedItem as ProfileIconOption)?.Id??"auto";
    internal ProfileIconPicker(ServerProfile original)
    {
        protocol=original.Protocol; ColumnDefinitions=new ColumnDefinitions("36,*"); ColumnSpacing=DesignTokens.Medium;
        var options=ProfileIcons.Choices.ToList(); if(!ProfileIcons.IsKnown(original.IconId))options.Add(new(original.IconId,L.T("Nierozpoznana — ikona protokołu")));
        choices.ItemsSource=options; choices.SelectedItem=options.First(c=>c.Id==original.IconId);
        UpdateTemplate();
        choices.SelectionChanged+=(_,_)=> { Refresh(); Changed?.Invoke(); }; AutomationProperties.SetName(choices,L.T("Ikona serwera"));
        Children.Add(preview); Grid.SetColumn(choices,1); Children.Add(choices); ToolTip.SetTip(preview,L.T("Podgląd ikony serwera")); Refresh();
    }
    internal void SetProtocol(Protocol value) { if(protocol==value)return; protocol=value; UpdateTemplate(); Refresh(); }
    private void UpdateTemplate()=>choices.ItemTemplate=new FuncDataTemplate<ProfileIconOption>((item,_)=>item==null?new TextBlock():DeskIcons.Labelled(ProfileIcons.Resolve(new ServerProfile { IconId=item.Id,Protocol=protocol }),L.T(item.Label)));
    private void Refresh()=>preview.Glyph=DeskIcons.Resolve(ProfileIcons.Resolve(new ServerProfile { IconId=SelectedId,Protocol=protocol }));
}

