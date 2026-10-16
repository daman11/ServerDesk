using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;

namespace ServerDesk.App;
internal static partial class DesignSystem
{
    private static void AddControlStates(Application app)
    {
        void Add(Func<Selector?,Selector> select,params Setter[] setters)=>app.Styles.Add(new Style(select) { Setters= { } }.WithSetters(setters));
        static Setter Background(string key)=>new(TemplatedControl.BackgroundProperty,new DynamicResourceExtension(key));
        static Setter Border(string key)=>new(TemplatedControl.BorderBrushProperty,new DynamicResourceExtension(key));
        Add(s=>s.OfType<Button>().Class("activity"),Background("BackgroundPrimary"),new Setter(Button.BorderThicknessProperty,new Thickness(2,0,0,0)),Border("BackgroundPrimary"));
        Add(s=>s.OfType<Button>().Class("icon"),new Setter(Button.ForegroundProperty,new DynamicResourceExtension("ForegroundSecondary")));
        Add(s=>s.OfType<Button>().Class("icon").Class(":pointerover"),new Setter(Button.ForegroundProperty,new DynamicResourceExtension("ForegroundPrimary")));
        Add(s=>s.OfType<Button>().Class("activity").Class("active"),Background("SurfaceSelected"),Border("AccentPrimary"),new Setter(Button.ForegroundProperty,new DynamicResourceExtension("AccentPrimary")));

        Add(s=>s.OfType<Button>().Class("activity").Class(":pointerover"),Background("SurfaceHover"));
        Add(s=>s.OfType<Button>().Class("segment").Class("active"),Background("SurfaceSelected"));
        Add(s=>s.OfType<Button>().Class("segment").Class(":pointerover"),Background("SurfaceHover"));
        Add(s=>s.OfType<Button>().Class(":pressed"),Background("SurfaceSelected"));
        Add(s=>s.OfType<Button>().Class("primary").Class(":pointerover"),Background("AccentHover"));
        Add(s=>s.OfType<Button>().Class("primary").Class(":pressed"),Background("AccentHover"));
        foreach(var type in new[]{typeof(TextBox),typeof(ComboBox),typeof(NumericUpDown),typeof(CheckBox),typeof(ToggleSwitch),typeof(MenuItem)})
        {
            Add(s=>s.OfType(type),new Setter(TemplatedControl.ForegroundProperty,new DynamicResourceExtension("ForegroundPrimary")));
            Add(s=>s.OfType(type).Class(":disabled"),new Setter(Visual.OpacityProperty,.45d));
            Add(s=>s.OfType(type).Class(":focus-visible"),Border("AccentPrimary"),new Setter(TemplatedControl.BorderThicknessProperty,new DynamicResourceExtension("BorderWidth")));
        }
        foreach(var type in new[]{typeof(TextBox),typeof(ComboBox),typeof(NumericUpDown)})
        {
            Add(s=>s.OfType(type),Background("BackgroundTertiary"),Border("BorderDefault"),new Setter(TemplatedControl.CornerRadiusProperty,new DynamicResourceExtension("RadiusSmall")));
            Add(s=>s.OfType(type).Class(":pointerover"),Border("BorderStrong"));
            Add(s=>s.OfType(type).Class(":focus-within"),Border("AccentPrimary"));
            Add(s=>s.OfType(type).Class("error"),Border("Danger"));
        }
        Add(s=>s.OfType<MenuItem>().Class(":pointerover"),Background("SurfaceHover"));
        Add(s=>s.OfType<MenuItem>().Class(":selected"),Background("SurfaceSelected"));
        foreach(var type in new[]{typeof(CheckBox),typeof(ToggleSwitch)})
        {
            Add(s=>s.OfType(type),new Setter(TemplatedControl.MinHeightProperty,new DynamicResourceExtension("ControlHeight")));
            Add(s=>s.OfType(type).Class(":pointerover"),Background("SurfaceHover"));
            Add(s=>s.OfType(type).Class(":pressed"),Background("SurfaceSelected"));
        }
        Add(s=>s.OfType<SessionStatusIcon>().Class(":connected"),new Setter(SessionStatusIcon.ForegroundProperty,new DynamicResourceExtension("Success")));
        Add(s=>s.OfType<SessionStatusIcon>().Class(":closed"),new Setter(SessionStatusIcon.ForegroundProperty,new DynamicResourceExtension("ForegroundSecondary")));
        Add(s=>s.OfType<SessionStatusIcon>().Class(":connecting"),new Setter(SessionStatusIcon.ForegroundProperty,new DynamicResourceExtension("AccentPrimary")));
        Add(s=>s.OfType<SessionStatusIcon>().Class(":failed"),new Setter(SessionStatusIcon.ForegroundProperty,new DynamicResourceExtension("Danger")));
        Add(s=>s.OfType<SessionStatusIcon>().Class(":external"),new Setter(SessionStatusIcon.ForegroundProperty,new DynamicResourceExtension("Warning")));
        Add(s=>s.OfType<ScrollBar>().Class(":vertical"),new Setter(ScrollBar.WidthProperty,10d));
        Add(s=>s.OfType<ScrollBar>().Class(":horizontal"),new Setter(ScrollBar.HeightProperty,10d));
    }
    private static Style WithSetters(this Style style,IEnumerable<Setter> setters)
    { foreach(var setter in setters)style.Setters.Add(setter); return style; }
}




