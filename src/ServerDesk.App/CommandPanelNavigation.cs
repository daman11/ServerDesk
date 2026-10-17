using Avalonia.Controls;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class CommandPanel
{
    private readonly Control filtersView,resultsView;
    private readonly Border detailCard,footerSurface;
    private readonly Grid detailActions;
    private readonly Button backToList;
    private readonly TextBlock detailContext=DesignSystem.Hint("");
    private bool showingDetails;
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
        filtersView.IsVisible=resultsView.IsVisible=!showingDetails;
        detailCard.IsVisible=detailActions.IsVisible=showingDetails;
        favorite.IsVisible=edit.IsVisible=duplicate.IsVisible=showingDetails;
        footerSurface.IsVisible=showingDetails||notice.IsVisible;
    }
}
