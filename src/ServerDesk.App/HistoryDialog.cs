using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Collections.ObjectModel;

namespace ServerDesk.App;
internal static partial class Dialogs
{
    internal static async Task<ServerProfile?> History(Window owner,ProfileRepository repository,Guid? profileId=null)
    {
        const int pageSize=100;
        using var lifetime=new CancellationTokenSource();
        CancellationTokenSource? request=null;
        var revision=0; var closed=false;
        var entries=new ObservableCollection<ConnectionHistoryEntry>();
        var list=new ListBox { Name="HistoryEntries",ItemsSource=entries };
        list.Styles.Add(new Style(s=>s.OfType<ListBoxItem>()) { Setters={new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch)} });
        list.ItemTemplate=new FuncDataTemplate<ConnectionHistoryEntry>((entry,_)=>
        {
            if(entry==null)return new TextBlock();
            var row=new Grid { ColumnDefinitions=new ColumnDefinitions("20,*,Auto"),ColumnSpacing=12,Margin=new Thickness(2,6) };
            row.Children.Add(DeskIcons.Create(entry.Protocol switch { Protocol.SSH=>"ssh",Protocol.RDP=>"rdp",Protocol.VNC=>"monitor",Protocol.Serial=>"serial",_=>"history" },16));
            var name=DesignSystem.Label(entry.Name); name.TextTrimming=TextTrimming.CharacterEllipsis;
            var detail=DesignSystem.Label(entry.Detail,true,DesignTokens.Caption); detail.TextTrimming=TextTrimming.CharacterEllipsis;
            var description=new StackPanel { Spacing=4,Children={name,detail} }; ToolTip.SetTip(description,entry.Name+"\n"+entry.Detail); Grid.SetColumn(description,1); row.Children.Add(description);
            var date=DesignSystem.Label(entry.Time.ToLocalTime().ToString("yyyy-MM-dd\nHH:mm:ss"),true,DesignTokens.Caption); date.TextAlignment=TextAlignment.Right; Grid.SetColumn(date,2); row.Children.Add(date); return row;
        });
        var query=new TextBox { Name="HistorySearch",PlaceholderText=L.T("Szukaj nazwy, adresu lub użytkownika…") };
        var protocol=new ComboBox { Name="HistoryProtocol",ItemsSource=new[]{L.T("Wszystkie"),"SSH","RDP","VNC","Serial"},SelectedIndex=0,MinWidth=110 };
        var filters=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=12,Children={query,protocol} }; Grid.SetColumn(protocol,1);
        var message=DesignSystem.Label(L.T("Wczytywanie historii…"),true,DesignTokens.Caption); message.Name="HistoryMessage";
        var selection=DesignSystem.Label(L.T("Wybierz wpis, aby połączyć się ponownie."),true,DesignTokens.Caption); selection.TextWrapping=TextWrapping.Wrap;
        var more=new Button { Name="HistoryMore",Content=L.T("Pokaż starsze"),IsEnabled=false };
        var connect=new Button { Name="HistoryConnect",Content=DeskIcons.Labelled("connect",L.T("Połącz")),Classes={"primary"},IsEnabled=false };
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=12,Children={more,connect} };
        var header=DesignSystem.PageHeading("history",L.T("Historia połączeń"),L.T("Potwierdzone sesje SSH, RDP, VNC i Serial. Zewnętrzny klient RDP nie potwierdza logowania."));
        var footer=new StackPanel { Spacing=12,Children={selection,actions} };
        var entriesSurface=DesignSystem.Card(list,new Thickness(4));
        var content=new Grid { RowDefinitions=new RowDefinitions("Auto,Auto,Auto,*,Auto"),RowSpacing=12,Children={header,filters,message,entriesSurface,footer} };
        Grid.SetRow(filters,1); Grid.SetRow(message,2); Grid.SetRow(entriesSurface,3); Grid.SetRow(footer,4);
        var dialog=Window(L.T("Historia połączeń"),content); dialog.Name="ConnectionHistory"; dialog.SizeToContent=SizeToContent.Manual; dialog.Width=780; dialog.Height=660; dialog.MinWidth=600; dialog.MinHeight=460; dialog.MaxHeight=double.PositiveInfinity; dialog.CanResize=true;
        actions.Children.Add(Button("Zamknij",()=>dialog.Close()));
        void Select()
        {
            var entry=list.SelectedItem as ConnectionHistoryEntry;
            connect.IsEnabled=entry?.CanReconnect==true;
            selection.Text=entry==null?L.T("Wybierz wpis, aby połączyć się ponownie."):entry.Profile==null?L.T("Profil usunięty lub połączenie jednorazowe. Utwórz profil, aby połączyć się ponownie."):!entry.CanReconnect?L.T("Profil wskazuje już inny cel. Otwórz go z listy połączeń."):entry.Snapshot==null?L.T("Starszy wpis: adres pochodzi z obecnego profilu."):L.T("Połącz ponownie z zapisanym profilem.");
        }
        async Task LoadAsync(bool reset,bool debounce=false)
        {
            if(closed)return;
            var current=++revision; request?.Cancel(); request?.Dispose(); request=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); var ct=request.Token;
            if(reset) { entries.Clear(); Select(); } more.IsEnabled=false;
            var before=entries.Count==0?(long?)null:entries.Last().Id;
            var chosen=protocol.SelectedIndex>0?(Protocol?)(protocol.SelectedIndex-1):null; var search=query.Text;
            message.Text=L.T("Wczytywanie historii…");
            try
            {
                if(debounce)await Task.Delay(200,ct);
                var page=await Task.Run(()=>repository.HistoryAsync(pageSize,before,profileId,ct,chosen,search),ct);
                if(ct.IsCancellationRequested || current!=revision)return;
                foreach(var entry in page)entries.Add(entry);
                more.IsEnabled=page.Count==pageSize; message.Text=entries.Count==0?L.T("Brak połączeń pasujących do filtrów."):L.F($"Wyświetlono {entries.Count} wpisów. Dwuklik łączy ponownie z profilem.");
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
            catch(Exception ex) { if(current!=revision || lifetime.IsCancellationRequested)return; Serilog.Log.Error("Błąd odczytu historii typu {Type}",ex.GetType().Name); message.Text=L.T("Nie udało się wczytać historii. Wybierz Pokaż starsze, aby ponowić."); more.IsEnabled=true; }
        }
        void Connect() { if(list.SelectedItem is ConnectionHistoryEntry { CanReconnect:true,Profile:{} profile })dialog.Close(profile); }
        list.SelectionChanged+=(_,_)=>Select(); connect.Click+=(_,_)=>Connect(); list.DoubleTapped+=(_,_)=>Connect();
        query.TextChanged+=async (_,_)=>await LoadAsync(true,true); protocol.SelectionChanged+=async (_,_)=>await LoadAsync(true);
        more.Click+=async (_,_)=>await LoadAsync(false); dialog.Closed+=(_,_)=> { closed=true; lifetime.Cancel(); request?.Cancel(); };
        var showing=dialog.ShowDialog<ServerProfile?>(owner); await LoadAsync(true); var result=await showing; request?.Dispose(); return result;
    }
}
