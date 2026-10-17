using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ServerDesk.Core;

namespace ServerDesk.App;
internal sealed class ProfileVisual : Grid
{
    public static readonly StyledProperty<ServerProfile> ProfileProperty=AvaloniaProperty.Register<ProfileVisual,ServerProfile>(nameof(Profile),new ServerProfile());
    public ServerProfile Profile { get=>GetValue(ProfileProperty); set=>SetValue(ProfileProperty,value); }
    private Bitmap? bitmap;
    private string? loadedPng;
    private readonly DeskIcon icon=DeskIcons.Create("ssh");
    private readonly Image image=new() { Stretch=Stretch.Uniform,IsVisible=false };
    private bool attached;
    internal ProfileVisual(ServerProfile profile,double size=16)
    {
        Width=Height=size; VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center; IsHitTestVisible=false; icon.Width=icon.Height=size;
        Children.Add(icon); Children.Add(image); Profile=profile; Update();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); if(change.Property==ProfileProperty && icon!=null)Update(); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); attached=true; Update(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { attached=false; Release(); base.OnDetachedFromVisualTree(e); }
    private void Release() { image.Source=null; image.IsVisible=false; icon.IsVisible=true; bitmap?.Dispose(); bitmap=null; loadedPng=null; }
    private void Update()
    {
        icon.Glyph=DeskIcons.Resolve(ProfileIcons.Resolve(Profile));
        if(!attached)return;
        var png=Profile.CustomIconPng??"";
        // Metadata edits keep the owned bitmap. Detached rows release it, so
        // profiles never accumulate decoded images in a global cache.
        if(string.Equals(loadedPng,png,StringComparison.Ordinal))return;
        Release(); loadedPng=png;
        if(png.Length==0 || png.Length>(ProfileAppearance.MaximumIconBytes+2)/3*4)return;
        try
        {
            var bytes=Convert.FromBase64String(png); ProfileAppearance.ValidatePng(bytes);
            using var stream=new MemoryStream(bytes,writable:false); bitmap=new Bitmap(stream);
            image.Source=bitmap; image.IsVisible=true; icon.IsVisible=false;
        }
        catch(FormatException) { }
        catch(ArgumentException) { } // Invalid legacy profile: retain the protocol/vector fallback.
        catch(IOException) { }
    }
    internal static IBrush ColorBrush(string color)=>ProfileAppearance.IsColor(color) && color.Length>0?new SolidColorBrush(Color.Parse(color)):Brushes.Transparent;
}
