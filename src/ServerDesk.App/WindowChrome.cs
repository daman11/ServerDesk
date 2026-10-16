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
    private void BuildResizeEdges()
    {
        // With the custom titlebar X11/XWayland has no WM-drawn frame. Keep
        // explicit client-side hit areas instead of relying on that missing frame.
        var edges=new List<Border>();
        void Edge(WindowEdge edge,WindowDecorationsElementRole role,HorizontalAlignment horizontal,VerticalAlignment vertical,double width,double height,StandardCursorType cursor,Thickness margin)
        {
            var grip=new Border { Name="WindowResize"+edge,Width=width,Height=height,HorizontalAlignment=horizontal,VerticalAlignment=vertical,Margin=margin,Background=Brushes.Transparent,Cursor=new Cursor(cursor),ZIndex=100 };
            WindowDecorationProperties.SetElementRole(grip,role);
            grip.PointerPressed+=(_,e)=> { if(CanResize && WindowState==WindowState.Normal && e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) { BeginResizeDrag(edge,e); e.Handled=true; } };
            Grid.SetColumnSpan(grip,4); Grid.SetRowSpan(grip,3); layout.Children.Add(grip); edges.Add(grip);
        }
        Edge(WindowEdge.North,WindowDecorationsElementRole.ResizeN,HorizontalAlignment.Stretch,VerticalAlignment.Top,double.NaN,6,StandardCursorType.SizeNorthSouth,new(14,0,14,0));
        Edge(WindowEdge.South,WindowDecorationsElementRole.ResizeS,HorizontalAlignment.Stretch,VerticalAlignment.Bottom,double.NaN,6,StandardCursorType.SizeNorthSouth,new(14,0,14,0));
        Edge(WindowEdge.West,WindowDecorationsElementRole.ResizeW,HorizontalAlignment.Left,VerticalAlignment.Stretch,6,double.NaN,StandardCursorType.SizeWestEast,new(0,14,0,14));
        Edge(WindowEdge.East,WindowDecorationsElementRole.ResizeE,HorizontalAlignment.Right,VerticalAlignment.Stretch,6,double.NaN,StandardCursorType.SizeWestEast,new(0,14,0,14));
        Edge(WindowEdge.NorthWest,WindowDecorationsElementRole.ResizeNW,HorizontalAlignment.Left,VerticalAlignment.Top,14,14,StandardCursorType.TopLeftCorner,default);
        Edge(WindowEdge.NorthEast,WindowDecorationsElementRole.ResizeNE,HorizontalAlignment.Right,VerticalAlignment.Top,14,14,StandardCursorType.TopRightCorner,default);
        Edge(WindowEdge.SouthWest,WindowDecorationsElementRole.ResizeSW,HorizontalAlignment.Left,VerticalAlignment.Bottom,14,14,StandardCursorType.BottomLeftCorner,default);
        Edge(WindowEdge.SouthEast,WindowDecorationsElementRole.ResizeSE,HorizontalAlignment.Right,VerticalAlignment.Bottom,14,14,StandardCursorType.BottomRightCorner,default);
        var mark=new PathIcon { Data=Geometry.Parse("M 2,10 L 10,2 L 11,3 L 3,11 Z M 6,10 L 10,6 L 11,7 L 7,11 Z"),Width=10,Height=10,Margin=new Thickness(0,0,2,2),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false };
        DesignSystem.Resource(mark,PathIcon.ForegroundProperty,"ForegroundSecondary"); edges[^1].Child=mark;
        ToolTip.SetTip(edges[^1],L.T("Przeciągnij, aby zmienić rozmiar okna"));
        void Visibility() { foreach(var edge in edges)edge.IsVisible=CanResize && WindowState==WindowState.Normal; }
        PropertyChanged+=(_,e)=> { if(e.Property==CanResizeProperty || e.Property==WindowStateProperty)Visibility(); }; Visibility();
    }
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
