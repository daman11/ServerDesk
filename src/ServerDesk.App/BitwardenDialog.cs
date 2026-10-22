using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task<BitwardenChoice?> Bitwarden(Window owner,Func<BitwardenCli> create,ServerProfile? profile,BitwardenReference? reference,bool bindingOnly,CancellationToken ct)
    {
        if(ct.IsCancellationRequested)return null;
        using var scope=CancellationTokenSource.CreateLinkedTokenSource(ct);
        BitwardenCli? client=null; Task work=Task.CompletedTask; var busy=false; var closed=false; var statusChecked=false; Control? focusAfter=null;
        // Reserve space for the overlaid dialog scrollbar, including in wide cards.
        var stack=new StackPanel { Spacing=14,Margin=new Thickness(0,0,12,0) };
        stack.Children.Add(DesignSystem.PageHeading("key","Bitwarden / Vaultwarden",L.T(bindingOnly?"Przypisz wpis sejfu. Hasło nie zostanie zapisane w profilu.":"Wybierz hasło do tego połączenia. Sejf zostanie zamknięty w ServerDesk po zamknięciu okna.")));
        if(profile!=null)stack.Children.Add(DesignSystem.Hint(profile.Protocol+" · "+profile.Endpoint+(profile.Username.Length>0?" · "+profile.Username:"")));

        var state=DesignSystem.Label(L.T("Dostęp do sejfu")); state.Name="BitwardenState";
        var server=DesignSystem.Hint(""); server.Name="BitwardenServer"; server.IsVisible=false;
        var status=DesignSystem.Hint(L.T("Sprawdzanie stanu Bitwarden CLI…")); status.Name="BitwardenStatus";
        var progress=new ProgressBar { Name="BitwardenProgress",IsIndeterminate=true,Height=3,IsVisible=false };
        var retry=DeskIcons.Button("refresh","Sprawdź ponownie",()=>{}); retry.Name="BitwardenRetry";
        var stateRow=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={state,retry} }; Grid.SetColumn(retry,1);
        var warning=DesignSystem.Hint(""); warning.Name="BitwardenError"; warning.IsVisible=false; DesignSystem.Resource(warning,TextBlock.ForegroundProperty,"Warning");
        var master=new TextBox { Name="BitwardenMasterPassword",PasswordChar='●',MaxLength=4096,PlaceholderText=L.T("Hasło główne sejfu") };
        AutomationProperties.SetName(master,L.T("Hasło główne sejfu"));
        var unlock=Button("Odblokuj sejf",()=>{}); unlock.Name="BitwardenUnlock"; unlock.Content=DeskIcons.Labelled("key",L.T("Odblokuj sejf"));
        var unlockRow=new Grid { Name="BitwardenUnlockRow",ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={master,unlock} }; Grid.SetColumn(unlock,1);
        var access=new StackPanel { Spacing=8,Children={stateRow,server,status,progress,warning,unlockRow} };
        stack.Children.Add(DesignSystem.Card(access,new Thickness(14)));

        var search=new TextBox { Name="BitwardenSearch",Text=profile?.Host??"",MaxLength=200,PlaceholderText=L.T("Nazwa wpisu lub adres — co najmniej 2 znaki") };
        AutomationProperties.SetName(search,L.T("Szukaj w sejfie"));
        search.InnerLeftContent=new Border { Child=DeskIcons.Create("search",15),Margin=new Thickness(8,0,0,0) };
        var clear=DeskIcons.Button("close","Wyczyść wyszukiwanie",()=> { search.Text=""; search.Focus(); },24); clear.Name="BitwardenClearSearch"; search.InnerRightContent=clear;
        var find=Button("Szukaj w sejfie",()=>{}); find.Name="BitwardenFind"; find.Content=DeskIcons.Labelled("search",L.T("Szukaj w sejfie"));
        var searchRow=new Grid { Name="BitwardenSearchRow",ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={search,find} }; Grid.SetColumn(find,1);
        var list=new ListBox { Name="BitwardenEntries",Height=160,Focusable=true,IsVisible=false,ItemTemplate=new FuncDataTemplate<BitwardenEntry>((entry,_) =>
        {
            if(entry==null)return null;
            var name=DesignSystem.Label(entry.Reference.Name); name.TextTrimming=TextTrimming.CharacterEllipsis;
            var username=DesignSystem.Hint(entry.Username.Length>0?entry.Username:L.T("Brak loginu we wpisie")); username.MaxLines=1; username.TextTrimming=TextTrimming.CharacterEllipsis;
            var details=new StackPanel { Spacing=3,Children={name,username} };
            var row=new Grid { ColumnDefinitions=new("22,*"),ColumnSpacing=8,Margin=new Thickness(4,6),Children={DeskIcons.Create("key",16,"AccentPrimary"),details} }; Grid.SetColumn(details,1);
            ToolTip.SetTip(row,entry.Reference.Name+"\n"+username.Text); return row;
        }) };
        var empty=DesignSystem.Hint(L.T("Odblokuj sejf, aby wyszukać wpisy.")); empty.Name="BitwardenEmpty"; empty.HorizontalAlignment=HorizontalAlignment.Center; empty.VerticalAlignment=VerticalAlignment.Center; empty.TextAlignment=TextAlignment.Center; empty.Margin=new Thickness(12);
        var results=new Grid { MinHeight=90,Children={list,empty} };
        var count=DesignSystem.Hint(""); count.Name="BitwardenResultCount"; count.IsVisible=false;
        var selection=DesignSystem.Label(""); selection.Name="BitwardenSelection"; selection.TextWrapping=TextWrapping.Wrap;
        var selectionUser=DesignSystem.Hint(""); selectionUser.Name="BitwardenSelectionUsername";
        var selectionHint=DesignSystem.Hint(L.T(bindingOnly?"Przypisanie zachowa metadane wpisu i sejfu, bez hasła.":"Hasło zostanie pobrane dopiero po wybraniu Użyj hasła."));
        var selected=new StackPanel { Spacing=5,Children={DesignSystem.SectionHeading("key",L.T(reference!=null?"Przypisany wpis":"Wybrany wpis")),selection,selectionUser,selectionHint} };
        if(reference!=null) { selection.Text=reference.Name; selectionUser.Text=reference.Server; }
        var selectedCard=DesignSystem.Card(selected,new Thickness(14)); selectedCard.Name="BitwardenSelectionDetails"; selectedCard.IsVisible=reference!=null;
        var resultsHeading=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={DesignSystem.SectionHeading("search",L.T("Wybór wpisu")),count} }; Grid.SetColumn(count,1);
        var entries=new StackPanel { Name="BitwardenSearchSection",Spacing=8,IsVisible=reference==null,Children={resultsHeading,searchRow,results} };
        stack.Children.Add(entries); stack.Children.Add(selectedCard);

        var setup=HelpContent.Read(L.Language).Single(topic=>topic.Id=="bitwarden").Sections[0];
        var guidance=new StackPanel { Spacing=10,Margin=new Thickness(0,8,0,0) };
        foreach(var paragraph in setup.Paragraphs)guidance.Children.Add(DesignSystem.Hint(paragraph));
        var help=new Expander { Name="BitwardenSetupHelp",Header=DeskIcons.Labelled("help",L.T("Instrukcja konfiguracji")),Content=guidance,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Classes={"network-options"} }; stack.Children.Add(help);
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8 }; stack.Children.Add(actions);
        var dialog=Window("Bitwarden / Vaultwarden",stack); dialog.Name="BitwardenPicker"; dialog.Width=720;
        dialog.SizeChanged+=(_,_) =>
        {
            var compact=dialog.ClientSize.Width<620;
            actions.Orientation=compact?Orientation.Vertical:Orientation.Horizontal; actions.HorizontalAlignment=compact?HorizontalAlignment.Stretch:HorizontalAlignment.Right;
            // Preserve a useful input width with large fonts and narrow windows.
            foreach(var row in new[]{unlockRow,searchRow,resultsHeading})
            {
                row.RowDefinitions=new(compact?"Auto,Auto":"Auto"); row.ColumnDefinitions=new(compact?"*":"*,Auto"); row.RowSpacing=8;
                Grid.SetColumn(row.Children[1],compact?0:1); Grid.SetRow(row.Children[1],compact?1:0);
            }
        };
        var cancel=Button("Anuluj",()=>dialog.Close()); actions.Children.Add(cancel);
        if(!bindingOnly && reference!=null) { var manual=Button("Podaj hasło ręcznie",()=>dialog.Close(new BitwardenChoice(null,"",null,true))); manual.Name="BitwardenManual"; actions.Children.Add(manual); }
        var use=Button(bindingOnly?"Przypisz wpis":"Użyj hasła",()=>{}); use.Name="BitwardenUse"; use.Content=DeskIcons.Labelled("key",L.T(bindingOnly?"Przypisz wpis":"Użyj hasła")); use.Classes.Add("primary"); actions.Children.Add(use);
        bool ValidSearch()=>search.Text is {} query && query.Trim().Length is >=2 and <=200 && !query.TrimStart().StartsWith('-') && !query.Any(char.IsControl);
        void Refresh()
        {
            var unlocked=client?.IsUnlocked==true; var ready=!busy && unlocked;
            state.Text=L.T(unlocked?"Sejf odblokowany":"Dostęp do sejfu");
            DesignSystem.Resource(state,TextBlock.ForegroundProperty,unlocked?"Success":"ForegroundPrimary");
            unlockRow.IsVisible=!unlocked; master.IsEnabled=unlock.IsEnabled=!busy && statusChecked && !unlocked;
            retry.IsVisible=!unlocked; retry.IsEnabled=!busy; progress.IsVisible=busy;
            status.IsVisible=busy || !unlocked || warning.IsVisible;
            searchRow.IsEnabled=list.IsEnabled=ready; find.IsEnabled=ready && ValidSearch(); clear.IsVisible=!string.IsNullOrEmpty(search.Text);
            use.IsEnabled=ready&&(reference!=null||list.SelectedItem is BitwardenEntry);
        }
        void ForgetResults()
        {
            list.ItemsSource=null; list.IsVisible=false; empty.IsVisible=true; count.IsVisible=false;
            if(reference==null)selectedCard.IsVisible=false;
        }
        void Run(string message,Func<Task> action)
        {
            if(busy || closed || scope.IsCancellationRequested)return;
            busy=true; warning.IsVisible=false; status.Text=L.T(message); Refresh(); work=Execute();
            async Task Execute()
            {
                try { client??=create(); await action(); }
                catch(OperationCanceledException) when(scope.IsCancellationRequested) { }
                catch(Exception ex)
                {
                    if(!closed && !scope.IsCancellationRequested)
                    {
                        warning.Text=ex is BitwardenException or ArgumentException?ex.Message:L.T("Nie udało się wykonać operacji sejfu."); warning.IsVisible=true;
                        status.Text=L.T("Operacja sejfu nie została ukończona. Możesz spróbować ponownie.");
                        if(client?.IsUnlocked!=true) { ForgetResults(); empty.Text=L.T("Odblokuj sejf, aby wyszukać wpisy."); }
                        else if(!list.IsVisible)empty.Text=L.T("Wyszukiwanie nie zostało ukończone. Spróbuj ponownie.");
                        focusAfter=client?.IsUnlocked==true?(reference==null?search:use):statusChecked?master:retry;
                        // Never log CLI output, command data, master passwords or vault contents.
                        if(ex is BitwardenException or ArgumentException)Serilog.Log.Warning("Bitwarden operation failed: {Reason}",ex is BitwardenException failure?failure.Reason.ToString():ex.GetType().Name);
                        else ApplicationDiagnostics.Report("BitwardenPicker",ex);
                    }
                }
                finally { busy=false; if(!closed) { Refresh(); focusAfter?.Focus(); focusAfter=null; } }
            }
        }
        async Task CheckStatus()
        {
            statusChecked=false;
            var account=await client!.StatusAsync(scope.Token);
            if(closed || scope.IsCancellationRequested)return;
            statusChecked=true; server.Text=account.Server; server.IsVisible=true;
            status.Text=L.T("Odblokuj sejf hasłem głównym."); focusAfter=master;
        }
        async Task Search()
        {
            ForgetResults(); status.Text=empty.Text=L.T("Wyszukiwanie wpisów…");
            var matches=await client!.SearchAsync(search.Text??"",scope.Token);
            if(closed || scope.IsCancellationRequested)return;
            list.ItemsSource=matches; list.IsVisible=matches.Count>0; empty.IsVisible=matches.Count==0; list.SelectedIndex=matches.Count>0?0:-1;
            list.Height=Math.Clamp(matches.Count*60,60,160);
            empty.Text=L.T("Brak pasujących wpisów. Zmień nazwę lub adres i wyszukaj ponownie.");
            count.Text=matches.Count==100?L.T("Pokazano 100 wpisów. Możesz zawęzić wyszukiwanie."):L.F($"Znalezione wpisy: {matches.Count}"); count.IsVisible=true;
            status.Text=L.T("Sejf odblokowany na czas tego okna."); focusAfter=search;
        }
        retry.Click+=(_,_)=>Run("Sprawdzanie stanu Bitwarden CLI…",CheckStatus);
        unlock.Click+=(_,_) =>
        {
            if(!unlock.IsEnabled)return;
            Run("Odblokowywanie sejfu…",async() =>
            {
                var password=master.Text??""; master.Text="";
                try { await client!.UnlockAsync(password,scope.Token,reference); }
                finally { password=""; }
                if(closed || scope.IsCancellationRequested)return;
                status.Text=L.T("Sejf odblokowany na czas tego okna."); focusAfter=reference==null?search:use;
                if(reference==null && ValidSearch())await Search();
                else empty.Text=L.T("Wpisz co najmniej 2 znaki nazwy wpisu lub adresu.");
            });
        };
        find.Click+=(_,_)=> { if(find.IsEnabled)Run("Wyszukiwanie wpisów…",Search); };
        use.Click+=(_,_) =>
        {
            if(!use.IsEnabled)return;
            Run(bindingOnly?"Przypisywanie wpisu…":"Pobieranie hasła wybranego wpisu…",async() =>
            {
                focusAfter=use;
                var chosen=reference??(list.SelectedItem as BitwardenEntry)?.Reference;
                if(chosen==null)return;
                if(bindingOnly) { dialog.Close(new BitwardenChoice(chosen,(list.SelectedItem as BitwardenEntry)?.Username??"",null)); return; }
                var value=await client!.GetPasswordAsync(chosen,scope.Token);
                if(closed || scope.IsCancellationRequested)return;
                if(profile is { Protocol:not Protocol.VNC } && profile.Username.Length>0 && value.Username.Length>0 && !string.Equals(profile.Username,value.Username,profile.Protocol==Protocol.RDP?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
                {
                    if(!await Confirm(dialog,L.T("Inny login we wpisie sejfu"),L.T("Hasło zostanie użyte dla loginu zapisanego w profilu połączenia.")+"\n\n"+profile.Username+" ← "+value.Username,scope.Token))return;
                }
                if(!scope.IsCancellationRequested && !closed)dialog.Close(new BitwardenChoice(chosen,value.Username,value.Password));
            });
        };
        list.SelectionChanged+=(_,_) =>
        {
            if(reference==null)
            {
                var entry=list.SelectedItem as BitwardenEntry; selectedCard.IsVisible=entry!=null;
                selection.Text=entry?.Reference.Name??"";
                selectionUser.Text=entry==null?"":entry.Username.Length>0?entry.Username:L.T("Brak loginu we wpisie");
            }
            Refresh();
        };
        // TextChanged is deferred by TextBox. Invalidate synchronously so an immediate
        // click/Enter cannot accept a result belonging to the previous query.
        search.PropertyChanged+=(_,e) =>
        {
            if(e.Property!=TextBox.TextProperty)return;
            ForgetResults(); empty.Text=L.T(client?.IsUnlocked!=true?"Odblokuj sejf, aby wyszukać wpisy.":ValidSearch()?"Naciśnij Enter lub Szukaj w sejfie.":"Wpisz co najmniej 2 znaki nazwy wpisu lub adresu."); Refresh();
        };
        search.AddHandler(InputElement.KeyDownEvent,(_,e) =>
        {
            if(e.KeyModifiers!=KeyModifiers.None)return;
            if(e.Key==Key.Enter && find.IsEnabled) { e.Handled=true; find.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); }
            else if(e.Key==Key.Down && list.IsVisible && list.IsEnabled) { e.Handled=true; list.Focus(); }
        },Avalonia.Interactivity.RoutingStrategies.Tunnel);
        list.KeyDown+=(_,e)=> { if(e.Key==Key.Enter && e.KeyModifiers==KeyModifiers.None && use.IsEnabled) { e.Handled=true; use.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); } };
        master.KeyDown+=(_,e)=> { if(e.Key==Key.Enter && unlock.IsEnabled) { e.Handled=true; unlock.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); } };
        dialog.KeyDown+=(_,e)=> { if(e.Key==Key.F && e.KeyModifiers==KeyModifiers.Control && search.IsEnabled && entries.IsVisible) { e.Handled=true; search.Focus(); } };
        dialog.Closed+=(_,_)=> { closed=true; scope.Cancel(); master.Text=""; list.ItemsSource=null; client?.Lock(); };
        dialog.Opened+=(_,_)=>Run("Sprawdzanie stanu Bitwarden CLI…",CheckStatus);
        using var cancelled=ct.Register(()=>Avalonia.Threading.Dispatcher.UIThread.Post(()=> { if(!closed)dialog.Close(); }));
        Refresh();
        try { return await dialog.ShowDialog<BitwardenChoice?>(owner); }
        finally { scope.Cancel(); try { await work; } finally { master.Text=""; list.ItemsSource=null; client?.Dispose(); } }
    }
}
