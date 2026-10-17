using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;
internal sealed partial class NetworkToolPane
{
    private readonly ComboBox historyChoice=new() { PlaceholderText=L.T("Ostatnie cele i parametry"),MinWidth=160,MaxWidth=350,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly ComboBox collectionChoice=new() { PlaceholderText=L.T("Zapisane kolekcje"),MinWidth=160,MaxWidth=350,HorizontalAlignment=HorizontalAlignment.Stretch };
    private ComboBox? rememberedResolvers,oidChoices,wakeFavorites;
    private Button clearHistory=null!;
    private sealed record HistoryChoice(NetworkHistory Entry) { public override string ToString()=>Entry.When.ToLocalTime().ToString("g")+" · "+Entry.Request.Target; }
    private sealed record CollectionChoice(NetworkCollection Entry) { public override string ToString()=>Entry.Name; }
    private sealed record WakeFavorite(string Name,string Mac,string Broadcast,int Port,string Local) { public override string ToString()=>Name+" · "+Mac; }
    private Control BuildSavedControls()
    {
        var items=new StackPanel { Spacing=6 };
        historyChoice.SelectionChanged+=(_,_)=> { if(historyChoice.SelectedItem is HistoryChoice choice) { ApplyRequest(choice.Entry.Request); status.Text=L.T("Wczytano parametry. Uzupełnij poświadczenia i wybierz Start. Historia HTTP zachowuje tylko origin, bez ścieżki, query, body i nagłówków."); } };
        items.Children.Add(historyChoice);
        var historyActions=new WrapPanel { Orientation=Orientation.Horizontal };
        var runHistory=Dialogs.Button("Uruchom z historii",()=> { if(historyChoice.SelectedItem is HistoryChoice choice) { ApplyRequest(choice.Entry.Request); _=StartAsync(); } }); runHistory.IsEnabled=false; historyActions.Children.Add(runHistory);
        var deleteHistory=Dialogs.Button("Usuń wpis historii",()=>_=GuardAsync(async()=> { if(historyChoice.SelectedItem is HistoryChoice choice)await store.DeleteHistoryAsync(choice.Entry.Id,lifetime.Token); await RefreshSavedAsync(); })); deleteHistory.IsEnabled=false; historyActions.Children.Add(deleteHistory);
        historyChoice.SelectionChanged+=(_,_)=>runHistory.IsEnabled=deleteHistory.IsEnabled=historyChoice.SelectedItem!=null;
        clearHistory=Dialogs.Button("Wyczyść historię narzędzi",()=>_=GuardAsync(async()=> { await store.ClearHistoryAsync(lifetime.Token); await RefreshSavedAsync(); })); clearHistory.IsEnabled=false; historyActions.Children.Add(clearHistory); items.Children.Add(historyActions);
        items.Children.Add(collectionChoice);
        var actions=new WrapPanel { Orientation=Orientation.Horizontal };
        actions.Children.Add(Dialogs.Button("Zapisz kolekcję",()=>_=GuardAsync(async()=>
        {
            var request=ReadRequest(); request.Validate(); var name=await Dialogs.Prompt(TopLevel.GetTopLevel(this) as Window??owner,L.T("Nazwa kolekcji"),ct:lifetime.Token); if(name==null)return;
            lifetime.Token.ThrowIfCancellationRequested(); await store.SaveCollectionAsync(name,request,ReadSecrets(),lifetime.Token); await RefreshSavedAsync(); if(!disposed)status.Text=L.T("Kolekcję zapisano w bezpiecznym magazynie systemowym.");
        })));
        var loadCollection=Dialogs.Button("Wczytaj kolekcję",()=>_=GuardAsync(async()=> { if(collectionChoice.SelectedItem is CollectionChoice choice) { var saved=await store.LoadCollectionAsync(choice.Entry.Id,lifetime.Token); lifetime.Token.ThrowIfCancellationRequested(); ApplyRequest(saved.Request,saved.Secrets); } })); loadCollection.IsEnabled=false; actions.Children.Add(loadCollection);
        var deleteCollection=Dialogs.Button("Usuń kolekcję",()=>_=GuardAsync(async()=> { if(collectionChoice.SelectedItem is CollectionChoice choice)await store.DeleteCollectionAsync(choice.Entry.Id,lifetime.Token); await RefreshSavedAsync(); })); deleteCollection.IsEnabled=false; actions.Children.Add(deleteCollection); items.Children.Add(actions);
        collectionChoice.SelectionChanged+=(_,_)=>loadCollection.IsEnabled=deleteCollection.IsEnabled=collectionChoice.SelectedItem!=null;
        if(Tool==NetworkToolKind.Wake)
        {
            wakeFavorites=new ComboBox { PlaceholderText=L.T("Ulubione urządzenia WoL"),HorizontalAlignment=HorizontalAlignment.Stretch };
            wakeFavorites.SelectionChanged+=(_,_)=> { if(wakeFavorites.SelectedItem is WakeFavorite favorite)ApplyRequest(Defaults() with { Target=favorite.Mac,Broadcast=favorite.Broadcast,Port=favorite.Port,LocalAddress=favorite.Local }); };
            items.Children.Add(wakeFavorites); items.Children.Add(Dialogs.Button("Zapisz urządzenie WoL",()=>_=GuardAsync(async()=> { var request=ReadRequest(); request.Validate(); var name=await Dialogs.Prompt(TopLevel.GetTopLevel(this) as Window??owner,L.T("Nazwa urządzenia"),ct:lifetime.Token); if(name==null)return; lifetime.Token.ThrowIfCancellationRequested(); if(name.Length>100)throw new ArgumentException(L.T("Nazwa jest zbyt długa.")); await store.RememberAsync("wolFavorites",JsonSerializer.Serialize(new WakeFavorite(name,request.Target,request.Broadcast,request.Port,request.LocalAddress)),lifetime.Token); await RefreshSavedAsync(); })));
        }
        var hint=DesignSystem.Label(L.T("Historia pomija sekrety. Kolekcje wymagają odblokowanego systemowego magazynu poświadczeń."),true,11); hint.TextWrapping=Avalonia.Media.TextWrapping.Wrap; items.Children.Add(hint);
        foreach(var panel in items.Children.OfType<WrapPanel>())foreach(var button in panel.Children)button.Margin=new Thickness(0,2,6,2);
        return new Expander { Header=L.T("Historia i kolekcje"),Content=new ScrollViewer { Content=items,MaxHeight=220 },HorizontalAlignment=HorizontalAlignment.Stretch,Classes={"network-options"} };
    }
    private async Task RefreshSavedAsync()
    {
        if(disposed)return;
        var history=await store.HistoryAsync(lifetime.Token); var collections=await store.CollectionsAsync(lifetime.Token);
        var resolvers=rememberedResolvers!=null?await store.ValuesAsync("resolvers",lifetime.Token):[];
        var oids=oidChoices!=null?new[]{"1.3.6.1.2.1.1.1.0 · sysDescr","1.3.6.1.2.1.1.5.0 · sysName","1.3.6.1.2.1.1.3.0 · sysUpTime"}.Concat(await store.ValuesAsync("oidFavorites",lifetime.Token)).Concat(await store.ValuesAsync("oids",lifetime.Token)).Distinct().ToArray():[];
        var favorites=new List<WakeFavorite>();
        if(wakeFavorites!=null)
        {
            foreach(var value in await store.ValuesAsync("wolFavorites",lifetime.Token))
            {
                try
                {
                    var favorite=JsonSerializer.Deserialize<WakeFavorite>(value)??throw new JsonException();
                    if(string.IsNullOrWhiteSpace(favorite.Name) || favorite.Name.Length>100 || favorite.Name.Any(char.IsControl))throw new JsonException();
                    (Defaults() with { Target=favorite.Mac,Broadcast=favorite.Broadcast,Port=favorite.Port,LocalAddress=favorite.Local }).Validate(); favorites.Add(favorite);
                }
                catch(Exception ex) when(ex is JsonException or ArgumentException) { throw new InvalidDataException(L.T("Nie można odczytać zapisanych danych narzędzi sieciowych. Oryginalne dane zachowano."),ex); }
            }
        }
        if(disposed)return;
        historyChoice.ItemsSource=history.Where(h=>h.Request.Tool==Tool).Take(30).Select(h=>new HistoryChoice(h)).ToArray(); clearHistory.IsEnabled=history.Count>0; collectionChoice.ItemsSource=collections.Where(c=>c.Tool==Tool).Select(c=>new CollectionChoice(c)).ToArray();
        if(rememberedResolvers!=null)rememberedResolvers.ItemsSource=resolvers;
        if(oidChoices!=null)oidChoices.ItemsSource=oids;
        if(wakeFavorites!=null)wakeFavorites.ItemsSource=favorites;
    }
}
