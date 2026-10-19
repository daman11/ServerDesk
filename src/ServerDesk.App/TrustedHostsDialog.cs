using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task TrustedHosts(Window owner,ProfileRepository repository)
    {
        using var lifetime=new CancellationTokenSource(); var ct=lifetime.Token;
        var closed=false; var busy=false; Task active=Task.CompletedTask;
        IReadOnlyList<TrustedHostKey> entries=[];
        var query=new TextBox { Name="TrustedHostsSearch",PlaceholderText=L.T("Szukaj hosta, portu lub odcisku…"),MaxLength=512 };
        AutomationProperties.SetName(query,L.T("Szukaj zaufanych hostów SSH"));
        var list=new ListBox { Name="TrustedHostsList",SelectionMode=SelectionMode.Single };
        AutomationProperties.SetName(list,L.T("Zaufane klucze SSH"));
        list.Styles.Add(new Style(s=>s.OfType<ListBoxItem>()) { Setters={new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch)} });
        list.ItemTemplate=new FuncDataTemplate<TrustedHostKey>((entry,_)=>
        {
            if(entry==null)return new TextBlock();
            var endpoint=DesignSystem.Label(entry.Endpoint); endpoint.TextTrimming=TextTrimming.CharacterEllipsis;
            var fingerprint=DesignSystem.Label(entry.Fingerprint,true,DesignTokens.Caption); fingerprint.TextTrimming=TextTrimming.CharacterEllipsis;
            var row=new Grid { ColumnDefinitions=new("20,*"),ColumnSpacing=12,Margin=new Thickness(4,6),Children={DeskIcons.Create("key",17,"AccentPrimary")} };
            var text=new StackPanel { Spacing=4,Children={endpoint,fingerprint} }; Grid.SetColumn(text,1); row.Children.Add(text); ToolTip.SetTip(row,entry.Endpoint+"\n"+entry.Fingerprint); return row;
        });
        var count=DesignSystem.Label("",true,DesignTokens.Caption); count.Name="TrustedHostsCount"; count.TextWrapping=TextWrapping.Wrap;
        var notice=DesignSystem.Label(L.T("Wczytywanie zaufanych kluczy…"),true,DesignTokens.Caption); notice.Name="TrustedHostsNotice"; notice.TextWrapping=TextWrapping.Wrap;
        var details=new SelectableTextBlock { Name="TrustedHostsDetails",TextWrapping=TextWrapping.Wrap,MinHeight=40 };
        DesignSystem.Resource(details,SelectableTextBlock.ForegroundProperty,"ForegroundPrimary");
        var copy=Button("Kopiuj odcisk",()=>{}); copy.Name="TrustedHostsCopy";
        var forget=Button("Usuń zaufanie…",()=>{}); forget.Name="TrustedHostsForget";
        var refresh=DeskIcons.Button("refresh","Odśwież zaufane klucze SSH",()=>{}); refresh.Name="TrustedHostsRefresh";
        var filters=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={query,refresh} }; Grid.SetColumn(refresh,1);
        var actions=new WrapPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Children={copy,forget} };
        var footer=new StackPanel { Spacing=10,Children={details,notice,actions} };
        var surface=DesignSystem.Card(list,new Thickness(4));
        var body=new Grid { RowDefinitions=new("Auto,Auto,Auto,*,Auto"),RowSpacing=12,Children={
            DesignSystem.PageHeading("key",L.T("Zaufane klucze SSH"),L.T("Zapisane odciski identyfikują serwery SSH i SFTP. Sprawdź je niezależnym kanałem przed zmianą zaufania.")),filters,count,surface,footer} };
        Grid.SetRow(filters,1); Grid.SetRow(count,2); Grid.SetRow(surface,3); Grid.SetRow(footer,4);
        var dialog=Window(L.T("Zaufane klucze SSH"),body); dialog.Name="TrustedHostsDialog";
        dialog.SizeToContent=SizeToContent.Manual; dialog.Width=800; dialog.Height=640; dialog.MinWidth=500; dialog.MinHeight=460; dialog.MaxHeight=double.PositiveInfinity;
        actions.Children.Add(Button("Zamknij",()=>dialog.Close()));

        void Select()
        {
            var selected=list.SelectedItem as TrustedHostKey;
            copy.IsEnabled=!busy && selected!=null && dialog.Clipboard!=null; forget.IsEnabled=!busy && selected!=null;
            details.Text=selected==null?L.T("Wybierz hosta, aby zobaczyć pełny odcisk."):selected.Endpoint+"\n"+selected.Fingerprint;
        }
        void Filter()
        {
            var selected=list.SelectedItem as TrustedHostKey; var text=query.Text?.Trim()??"";
            var visible=entries.Where(entry=>entry.Endpoint.Contains(text,StringComparison.OrdinalIgnoreCase) || entry.Fingerprint.Contains(text,StringComparison.OrdinalIgnoreCase)).ToArray();
            list.ItemsSource=visible; list.SelectedItem=selected==null?null:visible.FirstOrDefault(entry=>entry==selected);
            count.Text=entries.Count==0?L.T("Brak zapisanych kluczy. Pojawią się po zaakceptowaniu pierwszego połączenia SSH."):L.F($"Widoczne klucze: {visible.Length} / {entries.Count}"); Select();
        }
        async Task Load()
        {
            var loaded=await Task.Run(()=>repository.TrustedHostKeysAsync(ct),ct);
            if(closed)return; entries=loaded; Filter();
        }
        async Task Run(Func<Task> action)
        {
            busy=true; query.IsEnabled=list.IsEnabled=refresh.IsEnabled=false; Select();
            try { await action(); }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
            catch(Exception ex)
            {
                ApplicationDiagnostics.Report("TrustedHosts.Manage",ex);
                if(!closed)notice.Text=L.T("Nie udało się wykonać operacji. Odśwież listę i spróbuj ponownie.");
            }
            finally { busy=false; if(!closed) { query.IsEnabled=list.IsEnabled=refresh.IsEnabled=true; Select(); } }
        }
        void Start(Func<Task> action) { if(!busy && !closed)active=Run(action); }
        list.SelectionChanged+=(_,_)=>Select(); query.TextChanged+=(_,_)=>Filter();
        refresh.Click+=(_,_)=>Start(async()=> { await Load(); if(!closed)notice.Text=""; });
        copy.Click+=(_,_)=>
        {
            if(list.SelectedItem is not TrustedHostKey entry)return;
            Start(async()=> { if(dialog.Clipboard is {} clipboard) { await clipboard.SetTextAsync(entry.Fingerprint); if(!closed)notice.Text=L.T("Odcisk skopiowany."); } });
        };
        forget.Click+=(_,_)=>
        {
            if(list.SelectedItem is not TrustedHostKey entry)return;
            Start(async()=>
            {
                if(!await Confirm(dialog,L.T("Usunąć zaufanie do tego hosta?"),L.F($"Host: {entry.Endpoint}\nZapisany odcisk: {entry.Fingerprint}\n\nPrzed usunięciem potwierdź zmianę klucza z administratorem serwera. Następne połączenie poprosi o ponowną weryfikację. Otwarte sesje pozostaną połączone."),ct))return;
                var removed=await Task.Run(()=>repository.ForgetHostKeyAsync(entry,ct),ct); await Load();
                if(!closed)notice.Text=L.T(removed?"Usunięto zaufanie. Przy następnym połączeniu porównaj nowy odcisk.":"Wpis zmienił się lub został już usunięty. Lista została odświeżona.");
            });
        };
        dialog.Closed+=(_,_)=> { closed=true; lifetime.Cancel(); };
        Select(); var showing=dialog.ShowDialog(owner); Start(async()=> { await Load(); if(!closed)notice.Text=""; });
        try { await showing; }
        finally { closed=true; lifetime.Cancel(); await active; }
    }
}
