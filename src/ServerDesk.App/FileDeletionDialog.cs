using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static class FileDeletionDialog
{
    internal static async Task<FileDeletionResult?> Show(Window owner,FileDeletionService service,IReadOnlyList<FileEntry> entries,CancellationToken ct,string? context=null)
    {
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(ct);
        var dialog=new Window { Name="FileDeletionDialog",Title=L.T("Usuń zaznaczone elementy"),Width=660,Height=540,MinWidth=460,MinHeight=440,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        Dialogs.FitToOwnerScreen(dialog,owner);
        var root=new Grid { RowDefinitions=new RowDefinitions("Auto,*,Auto"),RowSpacing=DesignTokens.Large,Margin=DesignTokens.PagePadding };
        root.Children.Add(DesignSystem.PageHeading("trash",L.T("Usuń zaznaczone elementy")));
        var content=new StackPanel { Spacing=DesignTokens.Medium };
        var location=Dialogs.Text((service.IsLocal?L.T("Na tym komputerze"):L.T("Na serwerze SFTP"))+(string.IsNullOrEmpty(context)?"":"\n"+context));
        location.Name="FileDeletionContext"; content.Children.Add(location);
        var summary=Dialogs.Text(L.T("Sprawdzanie zawartości…")); summary.Name="FileDeletionSummary"; content.Children.Add(summary);
        var paths=new ListBox { Name="FileDeletionTargets",ItemsSource=entries,MaxHeight=140,MinHeight=60,Focusable=true,
            ItemTemplate=new FuncDataTemplate<FileEntry>((entry,_)=>
            {
                var name=Dialogs.Text(entry.Name); name.FontWeight=FontWeight.SemiBold;
                var path=Dialogs.Text(entry.FullPath,DesignTokens.Caption); ToolTip.SetTip(path,entry.FullPath);
                return new StackPanel { Spacing=DesignTokens.Tight,Children={name,path} };
            }) };
        AutomationProperties.SetName(paths,L.T("Elementy wybrane do usunięcia")); content.Children.Add(paths);
        var warning=Dialogs.Text(L.T("Pliki i katalogi zostaną usunięte trwale, razem z ukrytą zawartością. Dowiązania są usuwane bez otwierania ich celu. Tej operacji nie można cofnąć."));
        DesignSystem.Resource(warning,TextBlock.ForegroundProperty,"Warning"); content.Children.Add(warning);
        var detail=Dialogs.Text(""); detail.Name="FileDeletionDetail"; content.Children.Add(detail);
        var progress=new ProgressBar { Name="FileDeletionProgress",IsIndeterminate=true,Height=6 }; content.Children.Add(progress);
        var scroll=new ScrollViewer { Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll,1); root.Children.Add(scroll);
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium };
        Grid.SetRow(buttons,2); root.Children.Add(buttons); dialog.Content=root;
        FileDeletionPlan? plan=null; FileDeletionResult? result=null; var working=false; var finished=false; var closeWhenStopped=false;
        var cancel=Dialogs.Button("Anuluj",()=> {}); cancel.Name="FileDeletionCancel";
        var confirm=Dialogs.Button("Usuń trwale",()=> {}); confirm.Name="FileDeletionConfirm"; confirm.IsEnabled=false; confirm.IsDefault=false; confirm.Classes.Add("danger");
        DesignSystem.Resource(confirm,Button.ForegroundProperty,"Danger"); DesignSystem.Resource(confirm,Button.BorderBrushProperty,"Danger");
        buttons.Children.Add(cancel); buttons.Children.Add(confirm);
        void ShowPath(string path) { detail.Text=path.Length>160?"…"+path[^160..]:path; ToolTip.SetTip(detail,path); }
        void Close() { finished=true; dialog.Close(result); }
        void RequestStop()
        {
            if(!working) { Close(); return; }
            closeWhenStopped=true; cancellation.Cancel(); cancel.IsEnabled=false;
            summary.Text=L.T("Zatrzymywanie operacji…"); detail.Text=L.T("Usuniętych elementów nie można przywrócić. Oczekiwanie na zakończenie bieżącego żądania.");
        }
        cancel.Click+=(_,_)=>RequestStop();
        dialog.Closing+=(_,e)=> { if(working) { e.Cancel=true; RequestStop(); } };
        using var registration=ct.Register(()=>Dispatcher.UIThread.Post(()=> { if(!finished)RequestStop(); }));
        dialog.Opened+=async (_,_)=>
        {
            working=true;
            var report=new Progress<FileDeletionProgress>(p=> { if(!working || plan!=null || cancellation.IsCancellationRequested)return; summary.Text=L.F($"Podgląd: znaleziono {p.Completed} elementów"); ShowPath(p.Path); });
            try
            {
                plan=await service.PrepareAsync(entries,report,cancellation.Token);
                summary.Text=L.F($"Pliki: {plan.Files} · katalogi: {plan.Directories} · dowiązania: {plan.Links}");
                detail.Text=L.T("Podgląd jest gotowy. Sprawdź wybrane ścieżki przed usunięciem."); confirm.IsEnabled=true; progress.IsVisible=false;
            }
            catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { closeWhenStopped=true; }
            catch(Exception ex) { summary.Text=L.T("Nie udało się przygotować usuwania. Żadne pliki nie zostały usunięte."); detail.Text=ex.Message; DesignSystem.Resource(summary,TextBlock.ForegroundProperty,"Danger"); progress.IsVisible=false; }
            finally { working=false; if(closeWhenStopped)Close(); }
        };
        confirm.Click+=async (_,_)=>
        {
            if(working || plan==null || result!=null)return;
            working=true; confirm.IsEnabled=false; cancel.IsEnabled=true;
            progress.IsVisible=true; progress.IsIndeterminate=true;
            summary.Text=L.T("Sprawdzanie zmian przed usunięciem…");
            var report=new Progress<FileDeletionProgress>(p=>
            {
                if(!working || result!=null || cancellation.IsCancellationRequested)return;
                progress.IsIndeterminate=false; progress.Maximum=p.Total; progress.Value=p.Completed;
                summary.Text=L.F($"Usunięto {p.Completed} z {p.Total} elementów"); ShowPath(p.Path);
            });
            try
            {
                result=await service.ExecuteAsync(plan,report,cancellation.Token);
                progress.IsIndeterminate=false; progress.Maximum=result.Total; progress.Value=result.Deleted;
                summary.Text=L.F($"Usunięto {result.Deleted} z {result.Total} elementów");
                detail.Text=result.Succeeded?L.T("Usuwanie zakończone."):result.Cancelled?L.T("Operacja została przerwana. Usuniętych elementów nie można przywrócić."):L.T("Operacja zatrzymała się po błędzie. Dalsze usuwanie zostało wstrzymane. Odśwież listę przed ponowieniem.")+"\n"+result.Error?.Message;
                if(!result.Succeeded)detail.Text+="\n"+L.T("Licznik obejmuje potwierdzone usunięcia. Serwer mógł dokończyć bieżące żądanie po przerwaniu połączenia. Odśwież listę.");
                DesignSystem.Resource(summary,TextBlock.ForegroundProperty,result.Succeeded?"Success":"Warning");
                cancel.Content=L.T("Zamknij"); AutomationProperties.SetName(cancel,L.T("Zamknij")); cancel.IsEnabled=true;
            }
            finally
            {
                working=false;
                // Show an explicit partial result after cancellation during deletion.
                // Disconnection/owner close may dismiss it once work has stopped.
                if(ct.IsCancellationRequested)Close(); else closeWhenStopped=false;
            }
        };
        if(ct.IsCancellationRequested)return null;
        try { return await dialog.ShowDialog<FileDeletionResult?>(owner); }
        finally { finished=true; }
    }
}
