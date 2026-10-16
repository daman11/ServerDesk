using ServerDesk.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace ServerDesk.App;
internal enum SessionVisualState { Idle,Connecting,Connected,Closed,Failed,ExternalClient }

[PseudoClasses(":connected",":connecting",":closed",":failed",":external")]
internal sealed class SessionStatusIcon : DeskIcon
{
    public static readonly StyledProperty<SessionVisualState> StateProperty=AvaloniaProperty.Register<SessionStatusIcon,SessionVisualState>(nameof(State));
    public SessionVisualState State { get=>GetValue(StateProperty); set=>SetValue(StateProperty,value); }
    public SessionStatusIcon() { Width=14; Height=14; Update(); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if(change.Property==StateProperty)Update(); }
    private void Update()
    {
        Glyph=State switch { SessionVisualState.Connected=>"circle-check",SessionVisualState.Connecting=>"circle-dashed",SessionVisualState.Failed=>"circle-alert",SessionVisualState.ExternalClient=>"external-link",_=>"circle" };
        PseudoClasses.Set(":connected",State==SessionVisualState.Connected); PseudoClasses.Set(":connecting",State==SessionVisualState.Connecting); PseudoClasses.Set(":closed",State is SessionVisualState.Idle or SessionVisualState.Closed); PseudoClasses.Set(":failed",State==SessionVisualState.Failed); PseudoClasses.Set(":external",State==SessionVisualState.ExternalClient);
    }
}
internal sealed partial class SessionPane
{
    public static readonly StyledProperty<ServerProfile> PresentationProfileProperty=AvaloniaProperty.Register<SessionPane,ServerProfile>(nameof(PresentationProfile),new ServerProfile());
    public ServerProfile PresentationProfile { get=>GetValue(PresentationProfileProperty); private set=>SetValue(PresentationProfileProperty,value); }
    internal void UpdatePresentation(ServerProfile profile) { PresentationProfile=profile; UpdatePresentationIcon(profile.IconId); }
    internal override void BeginReparent()=>terminal.BeginReparent();
    internal override void EndReparent()=>terminal.EndReparent();
    public static readonly StyledProperty<string> ProfileGlyphProperty=AvaloniaProperty.Register<SessionPane,string>(nameof(ProfileGlyph),"square-terminal");
    public string ProfileGlyph { get=>GetValue(ProfileGlyphProperty); private set=>SetValue(ProfileGlyphProperty,value); }
    internal void UpdatePresentationIcon(string id)=>ProfileGlyph=DeskIcons.Resolve(ProfileIcons.Resolve(Profile with { IconId=id }));
    public static readonly StyledProperty<SessionVisualState> VisualStateProperty=AvaloniaProperty.Register<SessionPane,SessionVisualState>(nameof(VisualState));
    public SessionVisualState VisualState { get=>GetValue(VisualStateProperty); private set=>SetValue(VisualStateProperty,value); }
}
