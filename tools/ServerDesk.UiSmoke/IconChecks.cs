using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Automation;
using Avalonia.Interactivity;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class IconChecks
{
    internal static void Run(MainWindow main,ProfileRepository repository,string root)
    {
        using var manifest=JsonDocument.Parse(File.ReadAllText("third-party/lucide/manifest.json"));
        foreach(var asset in manifest.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name=asset.GetProperty("name").GetString()!; using var file=typeof(App).Assembly.GetManifestResourceStream("ServerDesk.App.Assets.Icons."+name+".svg")!;
            var hash=Convert.ToHexStringLower(SHA256.HashData(file)); if(hash!=asset.GetProperty("sha256").GetString())throw new Exception("Bundled icon differs from pinned upstream: "+name);
            var bounds=DeskIcons.Geometry(name).Bounds; if(bounds.Width<=0 || bounds.Height<=0 || bounds.X<-.1 || bounds.Y<-.1 || bounds.Right>24.1 || bounds.Bottom>24.1)throw new Exception("Unexpected icon bounds: "+name);
        }
        foreach(var theme in new[]{"dark","light"})
        {
            DesignSystem.Apply(theme); var sheet=new WrapPanel { ItemWidth=138,ItemHeight=82,Margin=new Thickness(12) };
            foreach(var name in DeskIcons.Available.Concat(DeskIcons.ApplicationPaths.Keys).Order())
            {
                var sizes=new StackPanel { Orientation=Orientation.Horizontal,Spacing=12,HorizontalAlignment=HorizontalAlignment.Center,Children={DeskIcons.Create(name,16),DeskIcons.Create(name,24)} };
                var text=DesignSystem.Label(name,true,DesignTokens.Micro); text.HorizontalAlignment=HorizontalAlignment.Center;
                sheet.Children.Add(new StackPanel { Spacing=8,Children={sizes,text} });
            }
            var gallery=new Window { Width=1140,Height=760,Content=new ScrollViewer { Content=sheet } }; gallery.Show(); RedesignChecks.Frames(); gallery.CaptureRenderedFrame()!.Save(Path.Combine(root,"icons-"+theme+".png")); gallery.Close();
        }
        DesignSystem.Apply("dark");
        var icon=DeskIcons.Create("save"); var foreground=new SolidColorBrush(Color.Parse("#D7BA7D")); var iconButton=new Button { Content=icon,Foreground=foreground };
        var states=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16),Spacing=16,Children={iconButton} };
        foreach(var state in Enum.GetValues<SessionVisualState>())states.Children.Add(new StackPanel { Spacing=8,Children={new SessionStatusIcon { State=state },DesignSystem.Label(state.ToString(),true,12)} });
        var stateWindow=new Window { Width=900,Height=160,Content=states }; stateWindow.Show(); RedesignChecks.Frames();
        if(!Equals(icon.Foreground,foreground))throw new Exception("Icon does not inherit its action foreground"); stateWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"icon-states.png")); stateWindow.Close();
        var profile=new ServerProfile { Name="Database icon test",Host="localhost",Username="admin",IconId="auto" };
        var edit=Dialogs.Edit(main,profile,[]); RedesignChecks.Frames(); var editor=main.OwnedWindows.Single();
        var picker=editor.GetLogicalDescendants().OfType<ProfileIconPicker>().Single();
        var combo=editor.GetLogicalDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Ikona serwera");
        var protocol=editor.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.SelectedItem is Protocol); protocol.SelectedItem=Protocol.RDP; RedesignChecks.Frames();
        if(picker.Children.OfType<DeskIcon>().Single().Glyph!="monitor")throw new Exception("Automatic icon did not follow protocol change");
        combo.SelectedItem=ProfileIcons.Choices.Single(c=>c.Id=="database"); PolishChecks.Page(editor,1); RedesignChecks.Frames(); editor.CaptureRenderedFrame()!.Save(Path.Combine(root,"icon-picker.png"));
        editor.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(edit);
        var selected=edit.GetAwaiter().GetResult()??throw new Exception("Icon picker did not return a profile"); if(selected.IconId!="database")throw new Exception("Selected icon lost on save");
        using var session=new SessionPane(main,profile,new SshConnectionFactory(repository),new ExternalRdpClient(),repository); var liveIcon=DeskIcons.Create(session.ProfileGlyph); liveIcon.Bind(DeskIcon.GlyphProperty,new Avalonia.Data.Binding(nameof(SessionPane.ProfileGlyph)) { Source=session }); var liveWindow=new Window { Width=300,Height=100,Content=liveIcon }; liveWindow.Show(); RedesignChecks.Frames(); session.UpdatePresentationIcon("database"); RedesignChecks.Frames(); if(liveIcon.Glyph!="database")throw new Exception("Open-session icon did not update"); liveWindow.Close();
        session.Dispose(); session.Dispose();
        using var unopenedRdp=new SessionPane(main,profile with { Protocol=Protocol.RDP,Port=3389 },new SshConnectionFactory(repository),new ExternalRdpClient(),repository); unopenedRdp.Dispose(); unopenedRdp.Dispose();
        Console.WriteLine("PASS all bundled SVG hashes/primitives/bounds, 16/24px dark/light rendering, inherited foreground, session-state icons, live icon update, icon picker save and disposal before terminal realization");
    }
}
