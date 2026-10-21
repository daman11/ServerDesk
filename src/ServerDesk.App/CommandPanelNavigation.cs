using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class CommandPanel
{
    private readonly Control filtersView,resultsView;
    private readonly Border detailCard,footerSurface;
    private readonly Grid detailActions;
    private readonly Button backToList;
    private readonly TextBlock detailContext=DesignSystem.Hint("");
    private readonly TextBlock detailPosition=DesignSystem.Hint("");
    private Button previousCommand=null!,nextCommand=null!;
    private bool showingDetails;
    private Grid BuildDetailNavigation()
    {
        previousCommand=DeskIcons.Button("arrow-left","Poprzednia komenda",()=>NavigateCommand(-1),28); previousCommand.Name="CommandPrevious";
        nextCommand=DeskIcons.Button("arrow-right","Następna komenda",()=>NavigateCommand(1),28); nextCommand.Name="CommandNext";
        ToolTip.SetTip(previousCommand,L.T("Poprzednia komenda · Alt+←")); ToolTip.SetTip(nextCommand,L.T("Następna komenda · Alt+→"));
        detailPosition.Name="CommandDetailPosition"; detailPosition.VerticalAlignment=VerticalAlignment.Center;
        var stepper=new StackPanel { Orientation=Orientation.Horizontal,Spacing=6,Children={detailPosition,previousCommand,nextCommand} };
        var navigation=new Grid { Name="CommandDetailNavigation",ColumnDefinitions=new("*,Auto"),RowDefinitions=new("Auto,Auto"),ColumnSpacing=6,RowSpacing=7,Children={backToList,stepper,detailContext} };
        Grid.SetColumn(stepper,1); Grid.SetRow(detailContext,1); Grid.SetColumnSpan(detailContext,2);
        AddHandler(KeyDownEvent,(_,e)=>
        {
            if(!showingDetails || e.KeyModifiers!=KeyModifiers.Alt || e.Key is not (Key.Left or Key.Right))return;
            NavigateCommand(e.Key==Key.Left?-1:1); e.Handled=true;
        },RoutingStrategies.Tunnel);
        return navigation;
    }
    private void NavigateCommand(int direction)
    {
        if(!showingDetails || working || lifetime.IsCancellationRequested || !owner.IsPluginEnabled(PluginCatalog.Commands))return;
        var current=CurrentCommandIndex(); var next=current+direction;
        if(current<0 || next<0 || next>=list.ItemCount)return;
        list.SelectedItem=list.Items[next]; ShowCommandDetails();
    }
    private int CurrentCommandIndex()=>list.SelectedItem is CommandMatch match && match.Command.Id==selected?.Id?list.SelectedIndex:-1;
    private void UpdateCommandNavigation()
    {
        var index=CurrentCommandIndex(); var total=list.ItemCount;
        previousCommand.IsEnabled=!working && index>0; nextCommand.IsEnabled=!working && index>=0 && index<total-1;
        detailPosition.Text=index<0?"":$"{index+1} / {total}";
        AutomationProperties.SetName(detailPosition,index<0?"":L.F($"Komenda {index+1} z {total}"));
    }
    internal void ShowCommandDetails()
    {
        if(selected==null || lifetime.IsCancellationRequested || !owner.IsPluginEnabled(PluginCatalog.Commands))return;
        showingDetails=true; UpdateCommandView(); backToList.Focus();
    }
    private void ShowCommandList(bool focus)
    {
        showingDetails=false; UpdateCommandView();
        if(focus) { list.Focus(); if(list.SelectedItem is {} item)list.ScrollIntoView(item); }
    }
    private void UpdateCommandView()
    {
        UpdateCommandNavigation();
        filtersView.IsVisible=resultsView.IsVisible=!showingDetails;
        detailCard.IsVisible=detailActions.IsVisible=showingDetails;
        dispatchHint.IsVisible=showingDetails;
        favorite.IsVisible=edit.IsVisible=duplicate.IsVisible=showingDetails;
        footerSurface.IsVisible=showingDetails||notice.IsVisible;
    }
}
