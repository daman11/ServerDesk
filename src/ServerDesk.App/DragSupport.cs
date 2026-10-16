using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ServerDesk.App;
internal static class DragSupport
{
    public static void Source<T>(Control control,DataFormat<T> format,Func<T?> getValue,DragDropEffects effects=DragDropEffects.Copy) where T:class
    {
        PointerPressedEventArgs? pressed=null; Point origin=default; bool dragging=false; object? clickSelection=null;
        control.AddHandler(InputElement.PointerPressedEvent,(_,e)=>
        {
            var source=e.Source as Visual;
            while(source!=null && source!=control) { if(source is Button or TextBox or Avalonia.Controls.Primitives.ScrollBar or GridSplitter)return; source=source.GetVisualParent(); }
            if(e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            {
                pressed=e; origin=e.GetPosition(control); clickSelection=null;
                // Keep a selected group intact until a drag starts. An ordinary click
                // still selects just this row when the pointer is released.
                if(control is ListBox list && e.KeyModifiers==KeyModifiers.None && list.SelectedItems?.Count>1 && e.Source is Control { DataContext:{} item } && list.SelectedItems.Contains(item))
                { clickSelection=item; e.Handled=true; }
            }
        },RoutingStrategies.Tunnel);
        control.PointerReleased+=(_,_)=> { pressed=null; if(!dragging && clickSelection!=null && control is ListBox list)list.SelectedItem=clickSelection; clickSelection=null; };
        control.PointerCaptureLost+=(_,_)=> { pressed=null; clickSelection=null; };
        control.DetachedFromVisualTree+=(_,_)=> { pressed=null; clickSelection=null; };
        control.PointerMoved+=async (_,e)=>
        {
            var delta=e.GetPosition(control)-origin;
            if(pressed==null || dragging || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed || Math.Abs(delta.X)+Math.Abs(delta.Y)<8)return;
            var value=getValue(); if(value==null)return; var start=pressed; pressed=null; clickSelection=null; dragging=true;
            try { var data=new DataTransfer(); data.Add(DataTransferItem.Create(format,value)); await DragDrop.DoDragDropAsync(start,data,effects); }
            catch(OperationCanceledException) { }
            finally { dragging=false; pressed=null; }
        };
    }
}
