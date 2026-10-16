using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Threading;

namespace ServerDesk.App;

internal static class SettingsNavigation
{
    internal static void Configure(TabControl categories)
    {
        // Keep a single scrollable column when category height exceeds the space
        // available above the footer, including with a language reminder open.
        categories.Template=new FuncControlTemplate<TabControl>((control,scope)=>
        {
            var items=new ItemsPresenter { Name="PART_ItemsPresenter",ItemsPanel=new FuncTemplate<Panel?>(()=>new StackPanel { Orientation=Orientation.Vertical }) };
            scope.Register(items.Name,items);
            var navigation=new ScrollViewer { Name="SettingsNavigationScroll",Content=items,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            var surface=new Border { Child=navigation,BorderThickness=new Thickness(0,0,1,0) }; DesignSystem.Resource(surface,Border.BorderBrushProperty,"BorderDefault");
            var content=new ContentPresenter { Name="PART_SelectedContentHost",HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch };
            scope.Register(content.Name,content);
            content.Bind(ContentPresenter.ContentProperty,new Binding(nameof(TabControl.SelectedContent)) { Source=control });
            content.Bind(ContentPresenter.ContentTemplateProperty,new Binding(nameof(TabControl.SelectedContentTemplate)) { Source=control });
            var layout=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*"),Children={surface,content},ClipToBounds=true }; Grid.SetColumn(content,1); return layout;
        });
        void Reveal()=>Dispatcher.UIThread.Post(()=> { if(categories.SelectedItem is Control item)item.BringIntoView(); });
        categories.SelectionChanged+=(_,_)=>Reveal(); categories.SizeChanged+=(_,_)=>Reveal();
    }
}
