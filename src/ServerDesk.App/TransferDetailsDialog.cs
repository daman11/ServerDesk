using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task TransferDetails(Window owner,string server,string endpoint,SessionPane.TransferSnapshot snapshot)
    {
        var report=string.Join("\n\n",server+" · "+endpoint,snapshot.Label,snapshot.Status,snapshot.Paths,snapshot.Detail);
        var text=new TextBox { Name="TransferDetailsText",Text=report,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top };
        ScrollViewer.SetHorizontalScrollBarVisibility(text,ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(text,ScrollBarVisibility.Auto);
        AutomationProperties.SetName(text,L.T("Szczegóły transferu"));
        var header=new StackPanel { Spacing=6,Children={DesignSystem.PageHeading("transfers",L.T("Szczegóły transferu")),DesignSystem.Hint(L.T("Stan z chwili otwarcia okna. Podgląd nie zmienia działania transferu."))} };
        var privacy=DesignSystem.Hint(L.T("Szczegóły zawierają adres serwera i ścieżki plików. Sprawdź je przed udostępnieniem."));
        var feedback=DesignSystem.Hint(""); feedback.Name="TransferDetailsFeedback";
        AutomationProperties.SetLiveSetting(feedback,AutomationLiveSetting.Polite);
        var buttons=new WrapPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,ItemSpacing=8,LineSpacing=6 };
        var footer=new StackPanel { Spacing=8,Children={privacy,feedback,buttons} };
        var body=new Grid { RowDefinitions=new("Auto,*,Auto"),RowSpacing=12,Children={header,text,footer} }; Grid.SetRow(text,1); Grid.SetRow(footer,2);
        var dialog=Window(L.T("Szczegóły transferu"),body); dialog.Name="TransferDetailsDialog"; dialog.Width=760; dialog.Height=540; dialog.MinHeight=380; dialog.SizeToContent=SizeToContent.Manual;
        var copy=Button("Kopiuj szczegóły",()=> { }); copy.Name="CopyTransferDetails"; copy.Click+=(_,_)=>_=CopyAsync();
        async Task CopyAsync()
        {
            copy.IsEnabled=false;
            try
            {
                if(dialog.Clipboard is not {} clipboard)throw new InvalidOperationException();
                await clipboard.SetTextAsync(report); feedback.Text=L.T("Szczegóły skopiowano do schowka.");
            }
            catch(Exception ex)
            {
                Serilog.Log.Warning("Copy transfer details failed: {ExceptionDetails}",ExceptionDiagnostics.Describe(ex));
                feedback.Text=L.T("Nie udało się skopiować szczegółów. Zaznacz tekst i skopiuj go ręcznie.");
            }
            finally { copy.IsEnabled=true; }
        }
        buttons.Children.Add(copy); buttons.Children.Add(Button("Zamknij",dialog.Close));
        dialog.Opened+=(_,_)=>text.Focus();
        await dialog.ShowDialog(owner);
    }
}

internal static class TransferHistoryActions
{
    internal static bool Successful(TransferState state,bool cleanup)=>state==TransferState.Completed && !cleanup;
    internal static Button Create(Func<IReadOnlyList<SessionPane.TransferSnapshot>> snapshot,Action<bool> clear,int size=26)
    {
        var menu=new ContextMenu();
        var success=new MenuItem { Name="ClearSuccessfulTransfers",Header=L.T("Usuń udane zadania"),Icon=DeskIcons.Create("success") };
        ToolTip.SetTip(success,L.T("Zachowuje zadania błędne, anulowane, pominięte i z ostrzeżeniami."));
        var all=new MenuItem { Name="ClearAllFinishedTransfers",Header=L.T("Usuń wszystkie zakończone zadania"),Icon=DeskIcons.Create("trash") };
        ToolTip.SetTip(all,L.T("Usuwa tylko historię. Pliki i aktywne transfery pozostają bez zmian."));
        success.Click+=(_,_)=>clear(true); all.Click+=(_,_)=>clear(false);
        menu.ItemsSource=new[]{success,all};
        menu.Opening+=(_,_)=>
        {
            var jobs=snapshot(); success.IsEnabled=jobs.Any(job=>Successful(job.State,job.HasCleanupWarning)); all.IsEnabled=jobs.Any(job=>!job.IsActive);
        };
        var button=DeskIcons.Button("trash","Wyczyść historię transferów",()=> { },size); button.ContextMenu=menu;
        button.Click+=(_,_)=>menu.Open(button); return button;
    }
}
