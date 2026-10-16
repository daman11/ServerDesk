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
    private readonly DeskIcon icon=DeskIcons.Create("ssh");
    private readonly Image image=new() { Stretch=Stretch.Uniform };
    private bool attached;
    internal ProfileVisual(ServerProfile profile,double size=16)
    {
        Width=Height=size; VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center; IsHitTestVisible=false; icon.Width=icon.Height=size;
        Children.Add(icon); Children.Add(image); Profile=profile; Update();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); if(change.Property==ProfileProperty && icon!=null)Update(); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); attached=true; Update(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { attached=false; Release(); base.OnDetachedFromVisualTree(e); }
    private void Release() { image.Source=null; bitmap?.Dispose(); bitmap=null; }
    private void Update()
    {
        Release(); icon.Glyph=DeskIcons.Resolve(ProfileIcons.Resolve(Profile)); icon.IsVisible=true; image.IsVisible=false;
        if(!attached || Profile.CustomIconPng.Length==0)return;
        try
        {
            ProfileAppearance.Validate(Profile.Color,Profile.CustomIconPng);
            using var stream=new MemoryStream(Convert.FromBase64String(Profile.CustomIconPng)); bitmap=new Bitmap(stream);
            image.Source=bitmap; image.IsVisible=true; icon.IsVisible=false;
        }
        catch(ArgumentException) { } // Invalid legacy profile: retain the protocol/vector fallback.
        catch(IOException) { }
    }
    internal static IBrush ColorBrush(string color)=>ProfileAppearance.IsColor(color) && color.Length>0?new SolidColorBrush(Color.Parse(color)):Brushes.Transparent;
}
