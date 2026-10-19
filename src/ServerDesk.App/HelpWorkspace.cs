using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private HelpPane? helpPane;
    internal HelpPane OpenHelp()
    {
        if(helpPane==null)RegisterPane(new HelpPane(()=>Safe(AboutAsync)));
        var pane=helpPane!; FocusSession(pane);
        if(floatingDocks.TryGetValue(pane.WorkspaceId,out var floating))floating.Window.Activate(); else Activate();
        pane.FocusContent(); return pane;
    }
    private Task HelpAsync() { if(!closing && !closePending)OpenHelp(); return Task.CompletedTask; }
    private Control HelpTabHeader(HelpPane pane)
    {
        var icon=DeskIcons.Create("help"); icon.Name="WorkspaceTabIcon"; DesignSystem.Resource(icon,Visual.IsVisibleProperty,"ShowTabIcons");
        var title=DesignSystem.Label(pane.DisplayName);
        var close=DeskIcons.Button("close",L.T("Zamknij ")+pane.DisplayName,()=>_=Safe(()=>CloseWorkspacePaneAsync(pane)),24); close.Name="WorkspaceTabClose"; DesignSystem.Resource(close,Visual.IsVisibleProperty,"ShowTabCloseButtons");
        return new StackPanel { Orientation=Orientation.Horizontal,Spacing=6,Children={icon,title,close} };
    }
}
