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
using Avalonia.Animation;

namespace ServerDesk.App;
internal static partial class DesignSystem
{
    private static void AddChromeStyles(Application app)
    {
        void Add(Func<Selector?,Selector> selector,params Setter[] setters)=>app.Styles.Add(new Style(selector).WithSetters(setters));
        static Setter Resource(AvaloniaProperty property,string name)=>new(property,new DynamicResourceExtension(name));
        // Chrome has its own flat template; Fluent's disabled/hover surfaces must
        // not obscure the connection state or turn every icon into a boxed button.
        var template=new FuncControlTemplate<Button>((button,scope)=>
        {
            var content=new ContentPresenter { Name="PART_ContentPresenter",Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center };
            scope.Register("PART_ContentPresenter",content);
            content.Bind(ContentPresenter.ContentProperty,new Binding(nameof(Button.Content)) { Source=button });
            content.Bind(ContentPresenter.ContentTemplateProperty,new Binding(nameof(Button.ContentTemplate)) { Source=button });
            content.Bind(ContentPresenter.HorizontalContentAlignmentProperty,new Binding(nameof(Button.HorizontalContentAlignment)) { Source=button });
            content.Bind(ContentPresenter.ForegroundProperty,new Binding(nameof(Button.Foreground)) { Source=button });
            var border=new Border { Child=content };
            border.Bind(Border.BackgroundProperty,new Binding(nameof(Button.Background)) { Source=button });
            border.Bind(Border.BorderBrushProperty,new Binding(nameof(Button.BorderBrush)) { Source=button });
            border.Bind(Border.BorderThicknessProperty,new Binding(nameof(Button.BorderThickness)) { Source=button });
            border.Bind(Border.CornerRadiusProperty,new Binding(nameof(Button.CornerRadius)) { Source=button });
            border.Bind(Border.PaddingProperty,new Binding(nameof(Button.Padding)) { Source=button });
            return border;
        });
        Add(s=>s.OfType<Button>().Class("chrome"),new Setter(TemplatedControl.TemplateProperty,template),new Setter(Button.BackgroundProperty,Brushes.Transparent),new Setter(Button.BorderBrushProperty,Brushes.Transparent),new Setter(Button.BorderThicknessProperty,new Thickness(1)),Resource(Button.PaddingProperty,"ChromePadding"));
        Add(s=>s.OfType<Button>().Class("chrome").Class("icon"),new Setter(Button.PaddingProperty,new Thickness(4)),new Setter(Button.MinHeightProperty,0d),new Setter(Button.MinWidthProperty,0d));
        Add(s=>s.OfType<Button>().Class("chrome").Class(":pointerover"),Resource(Button.BackgroundProperty,"SurfaceHover"),Resource(Button.ForegroundProperty,"ForegroundPrimary"));
        Add(s=>s.OfType<Button>().Class("chrome").Class(":pressed"),Resource(Button.BackgroundProperty,"SurfaceSelected"));
        Add(s=>s.OfType<Button>().Class("chrome").Class(":focus-visible"),Resource(Button.BorderBrushProperty,"AccentPrimary"));
        Add(s=>s.OfType<Button>().Class("chrome").Class("primary"),Resource(Button.BackgroundProperty,"AccentPrimary"),Resource(Button.ForegroundProperty,"AccentForeground"));
        Add(s=>s.OfType<Button>().Class("chrome").Class("primary").Class(":pointerover"),Resource(Button.BackgroundProperty,"AccentHover"));
        Add(s=>s.OfType<Button>().Class("chrome").Class("primary").Class(":pressed"),Resource(Button.BackgroundProperty,"AccentHover"));
        Add(s=>s.OfType<Button>().Class("chrome").Class("activity"),new Setter(Button.BorderThicknessProperty,new Thickness(2,0,0,0)));
        Add(s=>s.OfType<Button>().Class("chrome").Class("activity").Class("active"),Resource(Button.BackgroundProperty,"SurfaceSelected"),Resource(Button.BorderBrushProperty,"AccentPrimary"),Resource(Button.ForegroundProperty,"AccentPrimary"));
        Add(s=>s.OfType<Button>().Class("chrome").Class("segment"),Resource(Button.ForegroundProperty,"ForegroundSecondary"));
        Add(s=>s.OfType<Button>().Class("chrome").Class("segment").Class("active"),Resource(Button.BackgroundProperty,"SurfaceSelected"),Resource(Button.ForegroundProperty,"ForegroundPrimary"),new Setter(Button.BorderBrushProperty,Brushes.Transparent),new Setter(Button.BorderThicknessProperty,new Thickness(1)));
        Add(s=>s.OfType<Button>().Class("chrome").Class("segment").Class(":focus-visible"),Resource(Button.BorderBrushProperty,"AccentPrimary"));
        Add(s=>s.OfType<Button>().Class("chrome").Class("live").Class(":disabled"),new Setter(Visual.OpacityProperty,1d),Resource(Button.ForegroundProperty,"ForegroundPrimary"));
        Add(s=>s.OfType<Button>().Class("welcome-action"),new Setter(TemplatedControl.TemplateProperty,template),Resource(Button.BackgroundProperty,"WelcomeSurface"),Resource(Button.BorderBrushProperty,"BorderDefault"),new Setter(Button.BorderThicknessProperty,new Thickness(1)),new Setter(Button.CornerRadiusProperty,new CornerRadius(7)),Resource(Button.PaddingProperty,"WelcomeActionPadding"),new Setter(Button.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch),new Setter(Button.HorizontalAlignmentProperty,HorizontalAlignment.Stretch));
        Add(s=>s.OfType<Button>().Class("welcome-action").Class(":pointerover"),Resource(Button.BackgroundProperty,"SurfaceHover"));
        Add(s=>s.OfType<Button>().Class("welcome-action").Class(":pressed"),Resource(Button.BackgroundProperty,"SurfaceSelected"));
        Add(s=>s.OfType<Button>().Class("welcome-action").Class(":focus-visible"),Resource(Button.BorderBrushProperty,"AccentPrimary"));
        Add(s=>s.OfType<ComboBox>().Class("quiet-select"),Resource(ComboBox.BackgroundProperty,"BackgroundSecondary"),Resource(ComboBox.ForegroundProperty,"ForegroundSecondary"),new Setter(ComboBox.BorderThicknessProperty,new Thickness(0)),new Setter(ComboBox.PaddingProperty,new Thickness(8,2)),new Setter(ComboBox.MinHeightProperty,26d));
        Add(s=>s.OfType<ComboBox>().Class("quiet-select").Class(":focus-visible"),Resource(ComboBox.BorderBrushProperty,"AccentPrimary"),new Setter(ComboBox.BorderThicknessProperty,new Thickness(1)));
        Add(s=>s.OfType<TabItem>().Class(":selected"),Resource(TabItem.BackgroundProperty,"TabActiveSurface"),Resource(TabItem.ForegroundProperty,"ForegroundPrimary"),new Setter(TabItem.FontWeightProperty,FontWeight.SemiBold));
        Add(s=>s.OfType<TabItem>().Class("session-tab"),Resource(TabItem.BackgroundProperty,"BackgroundPrimary"),Resource(TabItem.ForegroundProperty,"ForegroundSecondary"),new Setter(TabItem.BorderThicknessProperty,new Thickness(0)),new Setter(TabItem.FontWeightProperty,FontWeight.Normal));
        Add(s=>s.OfType<TabItem>().Class("session-tab").Class(":selected"),Resource(TabItem.BackgroundProperty,"BackgroundSecondary"));
        Add(s=>s.OfType<TabItem>().Class("session-tab").Class("current").Class(":selected"),Resource(TabItem.BackgroundProperty,"TabActiveSurface"),Resource(TabItem.ForegroundProperty,"ForegroundPrimary"),new Setter(TabItem.FontWeightProperty,FontWeight.SemiBold));
        Add(s=>s.OfType<TabItem>().Class("session-tab").Class(":focus-visible"),Resource(TabItem.BorderBrushProperty,"AccentPrimary"),new Setter(TabItem.BorderThicknessProperty,new Thickness(1)));
        Add(s=>s.OfType<TabItem>().Class("session-tab").Template().OfType<Border>().Name("PART_SelectedPipe"),new Setter(Visual.IsVisibleProperty,false));
        Add(s=>s.OfType<Button>().Class("window-close").Class(":pointerover"),Resource(Button.BackgroundProperty,"Danger"),new Setter(Button.ForegroundProperty,Brushes.White));
        app.Styles.Add(new Style(s=>s.OfType<BusyIndicator>().Class("busy"))
        {
            Animations={new Animation { Duration=TimeSpan.FromSeconds(1.2),IterationCount=IterationCount.Infinite,PlaybackBehavior=PlaybackBehavior.OnlyIfVisible,Children={
                new KeyFrame { Cue=new Cue(0),Setters={new Setter(BusyIndicator.AngleProperty,0d)} },
                new KeyFrame { Cue=new Cue(1),Setters={new Setter(BusyIndicator.AngleProperty,360d)} }
            } }}
        });
        Add(s=>s.OfType<TreeViewItem>(),Resource(TreeViewItem.PaddingProperty,"ConnectionRowPadding"));
        // Keep the same expander slot for populated folders, empty folders and servers.
        Add(s=>s.OfType<TreeViewItem>().Class("connection-entry").Template().OfType<Panel>().Name("PART_ExpandCollapseChevronContainer"),new Setter(Layoutable.WidthProperty,12d),new Setter(Layoutable.MarginProperty,new Thickness(8,0,4,0)));
    }
}
