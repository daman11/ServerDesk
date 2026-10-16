using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private void BuildWindowCaptions(Grid titlebar)
    {
        var captions=new StackPanel { Name="WindowCaptionButtons",Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
        WindowDecorationProperties.SetElementRole(captions,WindowDecorationsElementRole.User);
        Button Caption(string name,string path,Action action)
        {
            var icon=new PathIcon { Data=Geometry.Parse(path),Width=12,Height=12 };
            var button=new Button { Content=icon,Width=46,Height=DesignTokens.HeaderHeight,Classes={"chrome","icon"} };
            AutomationProperties.SetName(button,L.T(name)); ToolTip.SetTip(button,L.T(name)); button.Click+=(_,_)=>action(); captions.Children.Add(button); return button;
        }
        Caption("Minimalizuj","M 0,5 L 12,5 L 12,6 L 0,6 Z",()=>WindowState=WindowState.Minimized);
        var maximize=Caption("Maksymalizuj / przywróć","M 0,0 L 12,0 L 12,12 L 0,12 Z M 1,1 L 1,11 L 11,11 L 11,1 Z",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized);
        PropertyChanged+=(_,e)=> { if(e.Property==WindowStateProperty)((PathIcon)maximize.Content!).Data=Geometry.Parse(WindowState==WindowState.Maximized?"M 0,3 L 9,3 L 9,12 L 0,12 Z M 1,4 L 1,11 L 8,11 L 8,4 Z M 3,0 L 12,0 L 12,9 L 10,9 L 10,8 L 11,8 L 11,1 L 4,1 L 4,2 L 3,2 Z":"M 0,0 L 12,0 L 12,12 L 0,12 Z M 1,1 L 1,11 L 11,11 L 11,1 Z"); };
        Caption("Zamknij okno","M 0,1 L 1,0 L 6,5 L 11,0 L 12,1 L 7,6 L 12,11 L 11,12 L 6,7 L 1,12 L 0,11 L 5,6 Z",Close).Classes.Add("window-close");
        Grid.SetColumn(captions,3); titlebar.Children.Add(captions);
        titlebar.PointerPressed+=(_,e)=>
        {
            if(IsExtendedIntoWindowDecorations || !e.GetCurrentPoint(titlebar).Properties.IsLeftButtonPressed)return;
            for(var source=e.Source as Visual;source!=null;source=source.GetVisualParent())if(source is Button)return;
            if(e.ClickCount==2)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
            else BeginMoveDrag(e);
            e.Handled=true;
        };
    }
}
