using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace ServerDesk.App;

internal static partial class DesignSystem
{
    internal static Border Card(Control content,Thickness? padding=null)
    {
        var card=new Border { Child=content,Padding=padding??new Thickness(18),CornerRadius=new CornerRadius(10),BorderThickness=new Thickness(1),ClipToBounds=true };
        Resource(card,Border.BackgroundProperty,"BackgroundSecondary"); Resource(card,Border.BorderBrushProperty,"BorderDefault"); return card;
    }
    internal static Control PageHeading(string glyph,string title,string? subtitle=null)
    {
        var heading=Label(title,false,20); heading.FontWeight=FontWeight.SemiBold; heading.TextWrapping=TextWrapping.Wrap;
        var text=new StackPanel { Spacing=5,Children={heading} };
        if(subtitle!=null) { var detail=Label(subtitle,true,12); detail.TextWrapping=TextWrapping.Wrap; text.Children.Add(detail); }
        var icon=new Border { Width=40,Height=40,CornerRadius=new CornerRadius(10),VerticalAlignment=VerticalAlignment.Top,Child=DeskIcons.Create(glyph,21,"AccentPrimary") };
        Resource(icon,Border.BackgroundProperty,"SurfaceSelected");
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*"),ColumnSpacing=14,Children={icon,text} }; Grid.SetColumn(text,1); return row;
    }
    private static void AddComponentStyles(Application app)
    {
        void Add(Func<Selector?,Selector> select,params Setter[] setters)=>app.Styles.Add(new Style(select).WithSetters(setters));
        static Setter R(AvaloniaProperty property,string key)=>new(property,new DynamicResourceExtension(key));
        Add(s=>s.OfType<Expander>().Class("network-options"),R(TemplatedControl.BackgroundProperty,"BackgroundSecondary"),R(TemplatedControl.ForegroundProperty,"ForegroundPrimary"),R(TemplatedControl.BorderBrushProperty,"BorderDefault"));
        Add(s=>s.OfType<Expander>().Class("network-options").Template().OfType<ToggleButton>(),R(TemplatedControl.BackgroundProperty,"BackgroundSecondary"),R(TemplatedControl.BorderBrushProperty,"BorderDefault"),R(TemplatedControl.ForegroundProperty,"ForegroundPrimary"));
        Add(s=>s.OfType<Expander>().Class("network-options").Template().OfType<ToggleButton>().Class(":pointerover"),R(TemplatedControl.BackgroundProperty,"SurfaceHover"));
        var pageTab=new FuncControlTemplate<TabItem>((tab,scope)=>
        {
            var presenter=new ContentPresenter { Name="PageNavigationContent",HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center };
            presenter.Bind(ContentPresenter.ContentProperty,new Binding(nameof(TabItem.Header)) { Source=tab });
            presenter.Bind(ContentPresenter.ContentTemplateProperty,new Binding(nameof(TabItem.HeaderTemplate)) { Source=tab });
            presenter.Bind(ContentPresenter.ForegroundProperty,new Binding(nameof(TabItem.Foreground)) { Source=tab });
            var surface=new Border { Child=presenter };
            surface.Bind(Border.BackgroundProperty,new Binding(nameof(TabItem.Background)) { Source=tab });
            surface.Bind(Border.BorderBrushProperty,new Binding(nameof(TabItem.BorderBrush)) { Source=tab });
            surface.Bind(Border.BorderThicknessProperty,new Binding(nameof(TabItem.BorderThickness)) { Source=tab });
            surface.Bind(Border.CornerRadiusProperty,new Binding(nameof(TabItem.CornerRadius)) { Source=tab });
            surface.Bind(Border.PaddingProperty,new Binding(nameof(TabItem.Padding)) { Source=tab }); return surface;
        });
        Add(s=>s.OfType<TabItem>().Class("page-navigation"),new Setter(TemplatedControl.TemplateProperty,pageTab),new Setter(TabItem.CornerRadiusProperty,new CornerRadius(6)),new Setter(TabItem.BorderThicknessProperty,new Thickness(1)),new Setter(TabItem.BorderBrushProperty,Brushes.Transparent),new Setter(TabItem.BackgroundProperty,Brushes.Transparent),new Setter(TabItem.PaddingProperty,new Thickness(12,9)),new Setter(TabItem.MarginProperty,new Thickness(3)),R(TabItem.ForegroundProperty,"ForegroundSecondary"));
        Add(s=>s.OfType<TabItem>().Class("page-navigation").Class(":pointerover"),R(TabItem.BackgroundProperty,"SurfaceHover"),R(TabItem.ForegroundProperty,"ForegroundPrimary"));
        Add(s=>s.OfType<TabItem>().Class("page-navigation").Class(":selected"),R(TabItem.BackgroundProperty,"SurfaceSelected"),R(TabItem.ForegroundProperty,"ForegroundPrimary"));
        Add(s=>s.OfType<TabItem>().Class("page-navigation").Class(":focus-visible"),R(TabItem.BorderBrushProperty,"AccentPrimary"));
        Add(s=>s.OfType<TabItem>().Class("settings-category"),new Setter(TabItem.WidthProperty,184d),new Setter(TabItem.MinHeightProperty,44d),new Setter(TabItem.MarginProperty,new Thickness(8,4)),new Setter(TabItem.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
        Add(s=>s.OfType<ListBoxItem>(),new Setter(ListBoxItem.CornerRadiusProperty,new CornerRadius(5)));
        Add(s=>s.OfType<TextBox>().Class("code-preview"),R(TextBox.BackgroundProperty,"BackgroundPrimary"),R(TextBox.FontSizeProperty,"FontCaption"),new Setter(TextBox.PaddingProperty,new Thickness(10)),new Setter(TextBox.FontWeightProperty,FontWeight.Normal));
        foreach(var listClass in new[]{"command-list","sftp-list","network-results"})
        {
            Add(s=>s.OfType<ListBox>().Class(listClass).Descendant().OfType<ListBoxItem>(),new Setter(ListBoxItem.MarginProperty,new Thickness(0,2)),R(ListBoxItem.ForegroundProperty,"ForegroundPrimary"));
            Add(s=>s.OfType<ListBox>().Class(listClass).Descendant().OfType<ListBoxItem>().Class(":selected").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"),R(ContentPresenter.BackgroundProperty,"SurfaceSelected"),R(ContentPresenter.ForegroundProperty,"ForegroundPrimary"));
            Add(s=>s.OfType<ListBox>().Class(listClass).Descendant().OfType<ListBoxItem>().Class(":selected").Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"),R(ContentPresenter.BackgroundProperty,"SurfaceSelected"));
        }
        Add(s=>s.OfType<TreeViewItem>(),new Setter(TreeViewItem.CornerRadiusProperty,new CornerRadius(5)));
        Add(s=>s.OfType<ContextMenu>(),new Setter(ContextMenu.CornerRadiusProperty,new CornerRadius(8)),new Setter(ContextMenu.BorderThicknessProperty,new Thickness(1)),R(ContextMenu.BorderBrushProperty,"BorderDefault"),new Setter(ContextMenu.PaddingProperty,new Thickness(5)));
        Add(s=>s.OfType<MenuItem>(),new Setter(MenuItem.CornerRadiusProperty,new CornerRadius(5)),new Setter(MenuItem.PaddingProperty,new Thickness(10,7)));
        Add(s=>s.OfType<ToolTip>(),new Setter(ToolTip.CornerRadiusProperty,new CornerRadius(6)));
    }
}
