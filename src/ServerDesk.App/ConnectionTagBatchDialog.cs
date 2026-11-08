using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private async Task EditConnectionTagsAsync(Guid? initial=null)
    {
        if(section is not ("connections" or "favorite" or "recent"))return;
        vm.Search=connectionSearch?.Text??vm.Search;
        var scope=ProfileOrdering.Sort(vm.AllServers.Where(IsBrowserProfileVisible),vm.SortMode,vm.SortDescending).ToArray();
        if(await Dialogs.BatchConnectionTags(this,scope,vm.TagColors,initial,
            changes=>Task.Run(()=>repository.ApplyTagChangesAsync(changes,lifetime.Token),lifetime.Token),lifetime.Token))
            await vm.LoadAsync();
    }
}

internal static partial class Dialogs
{
    internal static async Task<bool> BatchConnectionTags(Window owner,IReadOnlyList<ServerProfile> profiles,IReadOnlyDictionary<string,string> colors,
        Guid? initial,Func<IReadOnlyList<ProfileTagChange>,Task> apply,CancellationToken ct)
    {
        if(ct.IsCancellationRequested)return false;
        var view=new ConnectionTagBatchView(profiles,colors,initial,apply,ct);
        var dialog=Window(L.T("Edytuj tagi wielu połączeń"),view); dialog.Name="ConnectionTagBatchDialog";
        dialog.Width=780; dialog.Height=740; dialog.MinWidth=600; dialog.MinHeight=480; dialog.SizeToContent=SizeToContent.Manual;
        view.CloseRequested=result=>dialog.Close(result);
        dialog.Closing+=(_,e)=> { if(view.IsSaving)e.Cancel=true; };
        dialog.Closed+=(_,_)=>view.IsClosed=true;
        dialog.Opened+=(_,_)=>view.FocusSearch();
        using var cancellation=ct.Register(()=>Dispatcher.UIThread.Post(()=> { if(!view.IsSaving && !view.IsClosed)dialog.Close(false); }));
        return await dialog.ShowDialog<bool>(owner);
    }
}

internal sealed class ConnectionTagBatchView:Grid
{
    private sealed class Choice(ServerProfile profile):ObservableObject
    {
        internal ServerProfile Profile { get; }=profile;
        private bool chosen; private string preview="";
        public bool Chosen { get=>chosen; set=>SetProperty(ref chosen,value); }
        public string Preview { get=>preview; set=>SetProperty(ref preview,value); }
    }
    private readonly Choice[] choices;
    private readonly TextBox search=new() { Name="BatchTagSearch",PlaceholderText=L.T("Nazwa, adres, login lub tag"),MaxLength=200 };
    private readonly TextBox add=new() { Name="BatchTagAdd",PlaceholderText=L.T("np. produkcja, linux, baza"),MaxLength=4096 };
    private readonly TextBox remove=new() { Name="BatchTagRemove",PlaceholderText=L.T("np. produkcja, linux, baza"),MaxLength=4096 };
    private readonly ListBox list=new() { Name="BatchTagProfiles",Focusable=true };
    private readonly CheckBox selectedOnly=new() { Name="BatchTagSelectedOnly",Content=L.T("Tylko wybrane") };
    private readonly TextBlock count=DesignSystem.Hint("");
    private readonly SelectableTextBlock status=new() { Name="BatchTagStatus",TextWrapping=TextWrapping.Wrap };
    private readonly Button save;
    private readonly Func<IReadOnlyList<ProfileTagChange>,Task> apply;
    private readonly CancellationToken cancellation;
    private IReadOnlyList<ProfileTagChange> plan=[];
    private bool updating;
    internal bool IsSaving { get; private set; }
    internal bool IsClosed { get; set; }
    internal Action<bool> CloseRequested { get; set; }=_=> { };
    internal ConnectionTagBatchView(IReadOnlyList<ServerProfile> profiles,IReadOnlyDictionary<string,string> colors,Guid? initial,
        Func<IReadOnlyList<ProfileTagChange>,Task> apply,CancellationToken cancellation)
    {
        this.apply=apply; this.cancellation=cancellation;
        choices=profiles.Select(p=>new Choice(p) { Chosen=p.Id==initial }).ToArray();
        Name="ConnectionTagBatchView"; RowDefinitions=new("Auto,Auto,*,Auto,Auto,Auto"); RowSpacing=10;
        var heading=DesignSystem.PageHeading("tags",L.T("Edytuj tagi wielu połączeń"));
        var hint=DesignSystem.Hint(L.T("Wybierz połączenia i sprawdź podgląd przed zapisem. Lista uwzględnia bieżące filtry, także zwinięte foldery.")); hint.TextWrapping=TextWrapping.Wrap;
        Children.Add(new StackPanel { Spacing=5,Children={heading,hint} });
        SizeChanged+=(_,_)=>hint.IsVisible=Bounds.Height>=560;
        var clearSearch=DeskIcons.Button("close","Wyczyść wyszukiwanie",()=> { search.Text=""; search.Focus(); },24); search.InnerRightContent=clearSearch;
        var empty=DesignSystem.Hint(L.T("Brak wyników")); empty.Name="BatchTagEmpty"; empty.Margin=new Thickness(12);
        var selectVisible=Dialogs.Button("Zaznacz widoczne",()=>Choose(true)); selectVisible.Name="BatchTagSelectVisible"; selectVisible.Classes.Add("chrome");
        var clear=Dialogs.Button("Wyczyść wybór",()=>Choose(false)); clear.Name="BatchTagClearSelection"; clear.Classes.Add("chrome");
        var tools=new WrapPanel { Orientation=Orientation.Horizontal,Children={selectVisible,clear,selectedOnly} };
        foreach(var control in tools.Children)control.Margin=new Thickness(0,0,8,0);
        var filters=new StackPanel { Spacing=6,Children={search,tools} }; Grid.SetRow(filters,1); Children.Add(filters);
        list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        list.Styles.Add(new Style(s=>s.OfType<ListBoxItem>()) { Setters={new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch)} });
        list.ItemTemplate=new FuncDataTemplate<Choice>((choice,_)=>
        {
            if(choice==null)return null;
            var profile=choice.Profile;
            var name=DesignSystem.Label(profile.Name); name.TextWrapping=TextWrapping.Wrap; name.MaxLines=2;
            var address=DesignSystem.Hint(profile.Protocol+" · "+profile.Endpoint+" · "+profile.Folder); address.TextTrimming=TextTrimming.CharacterEllipsis; ToolTip.SetTip(address,address.Text);
            var tags=new WrapPanel { ItemSpacing=4,LineSpacing=3 };
            foreach(var tag in ProfileTags.Parse(profile.Tags).Take(5))tags.Children.Add(ConnectionTagVisuals.Chip(tag,colors,quiet:true));
            if(tags.Children.Count==0)tags.Children.Add(DesignSystem.Hint(L.T("Bez tagów")));
            ToolTip.SetTip(tags,profile.Tags);
            var preview=DesignSystem.Label(""); preview.Name="BatchTagPreview_"+profile.Id.ToString("N"); preview.TextWrapping=TextWrapping.Wrap;
            preview.Bind(TextBlock.TextProperty,new Binding(nameof(Choice.Preview)) { Source=choice });
            preview.Bind(IsVisibleProperty,new Binding(nameof(Choice.Chosen)) { Source=choice });
            DesignSystem.Resource(preview,TextBlock.ForegroundProperty,"Accent");
            var check=new CheckBox { Name="BatchTagSelect_"+profile.Id.ToString("N"),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,
                Content=new StackPanel { Spacing=4,Children={name,address,tags,preview} } };
            AutomationProperties.SetName(check,profile.Name);
            check.Bind(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty,new Binding(nameof(Choice.Chosen)) { Source=choice,Mode=BindingMode.TwoWay });
            return check;
        });
        var results=new Grid { Children={list,empty} }; Grid.SetRow(results,2); Children.Add(results);
        var fields=new Grid { ColumnDefinitions=new("*,*"),ColumnSpacing=12 };
        var adding=new StackPanel { Spacing=4,Children={DesignSystem.Label(L.T("Tagi do dodania")),add} };
        var removing=new StackPanel { Spacing=4,Children={DesignSystem.Label(L.T("Tagi do usunięcia")),remove} };
        fields.Children.Add(adding); Grid.SetColumn(removing,1); fields.Children.Add(removing); Grid.SetRow(fields,3); Children.Add(fields);
        AutomationProperties.SetName(search,L.T("Szukaj połączeń")); AutomationProperties.SetName(add,L.T("Tagi do dodania")); AutomationProperties.SetName(remove,L.T("Tagi do usunięcia"));
        count.Name="BatchTagCount"; count.TextWrapping=TextWrapping.Wrap;
        var feedback=new StackPanel { Spacing=4,Children={count,new ScrollViewer { Content=status,MaxHeight=96,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }} };
        Grid.SetRow(feedback,4); Children.Add(feedback);
        save=Dialogs.Button("Zapisz",()=>_=SaveAsync()); save.Name="BatchTagSave";
        var cancel=Dialogs.Button("Anuluj",()=>CloseRequested(false)); cancel.Name="BatchTagCancel";
        var footer=new WrapPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Children={cancel,save} };
        cancel.Margin=new Thickness(0,0,8,0); Grid.SetRow(footer,5); Children.Add(footer);
        void RefreshList()
        {
            list.ItemsSource=Visible().ToArray(); empty.IsVisible=list.ItemCount==0;
            clearSearch.IsVisible=!string.IsNullOrEmpty(search.Text); UpdateCount();
        }
        foreach(var choice in choices)choice.PropertyChanged+=(_,e)=>
        {
            if(e.PropertyName!=nameof(Choice.Chosen) || updating)return;
            Replan(); if(selectedOnly.IsChecked==true)RefreshList();
        };
        search.TextChanged+=(_,_)=>RefreshList(); selectedOnly.IsCheckedChanged+=(_,_)=>RefreshList();
        add.TextChanged+=(_,_)=>Replan(); remove.TextChanged+=(_,_)=>Replan();
        AddHandler(KeyDownEvent,(_,e)=>
        {
            if(e.Key==Key.F && e.KeyModifiers==KeyModifiers.Control) { FocusSearch(); e.Handled=true; }
            else if(e.Key==Key.Escape && search.IsKeyboardFocusWithin && !string.IsNullOrEmpty(search.Text)) { search.Text=""; e.Handled=true; }
        },Avalonia.Interactivity.RoutingStrategies.Tunnel);
        void Choose(bool chosen)
        {
            updating=true;
            try { foreach(var choice in chosen?Visible().ToArray():choices)choice.Chosen=chosen; }
            finally { updating=false; }
            Replan(); RefreshList();
        }
        Replan(); RefreshList();
    }
    private IEnumerable<Choice> Visible()
    {
        var matches=ProfileSearch.Compile(search.Text??"");
        return choices.Where(c=>(selectedOnly.IsChecked!=true || c.Chosen) && matches(c.Profile));
    }
    private void UpdateCount()=>count.Text=L.F($"Wybrane: {choices.Count(c=>c.Chosen)} · Zmiany: {plan.Count} · Wyniki: {list.ItemCount} / {choices.Length}");
    private void Replan()
    {
        if(IsSaving || IsClosed)return;
        try
        {
            plan=ProfileTags.PlanChanges(choices.Where(c=>c.Chosen).Select(c=>c.Profile),add.Text,remove.Text);
            var changes=plan.ToDictionary(c=>c.ProfileId,c=>c.UpdatedTags);
            foreach(var choice in choices)
            {
                var tags=changes.GetValueOrDefault(choice.Profile.Id,choice.Profile.Tags);
                choice.Preview=L.T("Po zmianie: ")+(ProfileTags.Parse(tags).Length==0?L.T("Bez tagów"):ProfileTags.Format(ProfileTags.Parse(tags)));
            }
            status.Text=plan.Count==0?L.T("Wybierz połączenia i wpisz tagi do dodania lub usunięcia."):L.T("Zapis zmieni tylko tagi zaznaczonych połączeń. Kolory i pozostałe dane zostaną zachowane.");
            DesignSystem.Resource(status,TextBlock.ForegroundProperty,"ForegroundSecondary");
        }
        catch(ArgumentException ex) { plan=[]; foreach(var choice in choices)choice.Preview=""; status.Text=ex.Message; DesignSystem.Resource(status,TextBlock.ForegroundProperty,"Danger"); }
        save.IsEnabled=plan.Count>0; UpdateCount();
    }
    private async Task SaveAsync()
    {
        if(IsSaving || IsClosed)return;
        // TextChanged can still be queued when Enter/click arrives. Never
        // submit an earlier preview after the user edits the operation fields.
        Replan(); if(plan.Count==0)return;
        var snapshot=plan.ToArray(); var saved=false;
        IsSaving=true; IsEnabled=false;
        status.Text=L.T("Zapisywanie zmian tagów…"); DesignSystem.Resource(status,TextBlock.ForegroundProperty,"ForegroundSecondary");
        try { cancellation.ThrowIfCancellationRequested(); await apply(snapshot); saved=true; }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
        catch(Exception ex)
        {
            var id=ApplicationDiagnostics.Report("Connections.Tags.Batch",ex,Serilog.Events.LogEventLevel.Warning);
            if(!IsClosed) { status.Text=(ex is ProfileTagConflictException?ex.Message:OperationErrorMessages.Describe(ex))+"\n"+L.F($"Identyfikator błędu: {id}"); DesignSystem.Resource(status,TextBlock.ForegroundProperty,"Danger"); }
        }
        finally { IsSaving=false; if(!IsClosed)IsEnabled=true; }
        if(!IsClosed && (saved || cancellation.IsCancellationRequested))CloseRequested(saved);
    }
    internal void FocusSearch() { search.Focus(); search.SelectAll(); }
}
