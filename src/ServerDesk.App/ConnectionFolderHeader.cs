using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private static Control FolderHeader(TreeViewItem node,string path,string icon,int count)
    {
        var empty=node.ItemCount==0; var custom=icon!="folder";
        var body=new Grid { ColumnDefinitions=new(custom?"18,18,*,Auto":"18,0,*,Auto"),ColumnSpacing=4,MinHeight=26,Margin=new Thickness(5,2) };
        var folder=DeskIcons.Create("folder",16,"ForegroundSecondary"); folder.Name="ConnectionFolderGlyph";
        folder.Bind(DeskIcon.GlyphProperty,new Binding(nameof(TreeViewItem.IsExpanded)) { Source=node,Converter=new FuncValueConverter<bool,string>(expanded=>expanded && !empty?"folder-open":"folder") }); body.Children.Add(folder);
        if(custom) { var chosen=DeskIcons.Create(icon,16,"ForegroundSecondary"); chosen.Name="ConnectionFolderCustomGlyph"; Grid.SetColumn(chosen,1); body.Children.Add(chosen); }
        var label=DesignSystem.Label(path[(path.LastIndexOf('/')+1)..]); label.Name="ConnectionFolderName"; label.FontWeight=FontWeight.SemiBold; label.TextTrimming=TextTrimming.CharacterEllipsis;
        Grid.SetColumn(label,2); body.Children.Add(label);
        var total=DesignSystem.Label(empty?L.T("Pusty"):count.ToString(),true,11); total.Name="ConnectionFolderCount";
        var badge=new Border { Child=total,Padding=new Thickness(5,1),CornerRadius=new CornerRadius(4),VerticalAlignment=VerticalAlignment.Center };
        Grid.SetColumn(badge,3); body.Children.Add(badge);
        var surface=new Border { CornerRadius=new CornerRadius(4),Opacity=empty?.35:.18,IsHitTestVisible=false }; DesignSystem.Resource(surface,Border.BackgroundProperty,"BackgroundTertiary");
        var header=new Grid { Name="ConnectionFolderHeader",Margin=new Thickness(0,3,4,2),Children={surface,body} };
        node.Classes.Set("empty-folder",empty);
        AutomationProperties.SetName(node,L.F($"Folder: {path}"));
        ToolTip.SetTip(node,L.F($"Folder: {path}\nSerwery w gałęzi: {count}"));
        return header;
    }
}
