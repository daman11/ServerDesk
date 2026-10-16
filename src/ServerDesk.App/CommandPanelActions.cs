using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Input.Platform;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Text;
using System.Text.Json;

namespace ServerDesk.App;

internal sealed partial class CommandPanel
{
    private void SetButtons(bool enabled)
    {
        foreach(var b in new[]{copy,insert,execute,favorite,edit,duplicate})b.IsEnabled=enabled;
    }
    private void Select(CommandMatch? match)
    {
        selected=match?.Command; ticket=null; values.Clear(); detail.Children.Clear(); preview.Text=""; validation.Text=""; codeApproval.IsChecked=false;
        if(selected==null) { detail.Children.Add(Dialogs.Text(L.T("Wybierz komendę, aby zobaczyć opis, parametry i podgląd."))); SetButtons(false); return; }
        var c=selected; if(target?.CanAcceptCommands==true)ticket=new(target.WorkspaceId,target.CommandConnectionId,c.Id);
        var title=DesignSystem.Label(c.Title,false,16); title.TextWrapping=Avalonia.Media.TextWrapping.Wrap; detail.Children.Add(title);
        detail.Children.Add(preview); detail.Children.Add(validation);
        detail.Children.Add(Dialogs.Text(c.Detail,12));
        var metadata=Dialogs.Text(c.Platform+" · "+c.Shell+" · "+Risk(c.Risk)+" · "+L.T("Uprawnienia")+": "+c.Privileges+"\n"+CategoryLabel(c.Category)+(c.Subcategory.Length>0?" / "+c.Subcategory:"")+" · "+(c.Origin==CommandOrigin.BuiltIn?L.T("Wbudowana"):L.T("Własna")),12);
        if(c.Risk==CommandRisk.Dangerous)DesignSystem.Resource(metadata,TextBlock.ForegroundProperty,"Danger"); detail.Children.Add(metadata);
        if(c.Tools.Length>0)detail.Children.Add(Dialogs.Text(string.Join(" · ",c.Tools.Select(t=>t+": "+L.T(environment.Tool(t) switch { ToolAvailability.Available=>"Dostępne",ToolAvailability.NotDetected=>"Niewykryte",_=>"Nieznane" }))),12));
        foreach(var p in c.Parameters)
        {
            detail.Children.Add(DesignSystem.Label(p.Label.Length>0?p.Label:p.Key,true,12));
            if(p.Kind==CommandParameterKind.Choice)
            {
                var combo=new ComboBox { ItemsSource=p.Choices,SelectedItem=p.Default.Length>0?p.Default:p.Choices.FirstOrDefault(),HorizontalAlignment=HorizontalAlignment.Stretch };
                values[p.Key]=()=>combo.SelectedItem as string??""; combo.SelectionChanged+=(_,_)=>UpdatePreview(); detail.Children.Add(combo);
            }
            else
            {
                var box=new TextBox { Name="CommandParameter_"+p.Key,Text=p.Default,MaxLength=4000,PasswordChar=p.Secret?'●':'\0',PlaceholderText=p.Kind+(p.Required?" *":"") };
                values[p.Key]=()=>box.Text??""; box.TextChanged+=(_,_)=> { if(p.Kind==CommandParameterKind.Code)codeApproval.IsChecked=false; UpdatePreview(); }; detail.Children.Add(box);
            }
        }
        if(c.Parameters.Any(p=>p.Kind==CommandParameterKind.Code))detail.Children.Add(codeApproval);
        if(c.Examples.Length>0)detail.Children.Add(Dialogs.Text(L.T("Przykłady")+"\n"+string.Join("\n",c.Examples),12));
        if(c.Origin==CommandOrigin.User)
        {
            var delete=Action("Usuń komendę",DeleteAsync); delete.HorizontalAlignment=HorizontalAlignment.Left; detail.Children.Add(delete);
        }
        UpdatePreview();
    }
    private RenderedCommand Render()
    {
        var c=selected??throw new InvalidOperationException(L.T("Wybierz komendę."));
        var selectedShell=environment.Shell!=CommandShell.Any?environment.Shell:c.Shell;
        return CommandRenderer.Render(c,selectedShell,values.ToDictionary(p=>p.Key,p=>p.Value()),codeApproval.IsChecked==true);
    }
    private void UpdatePreview()
    {
        if(selected==null) { SetButtons(false); return; }
        favorite.IsEnabled=!working; edit.IsEnabled=!working&&!libraryLocked&&selected.Origin==CommandOrigin.User; duplicate.IsEnabled=!working&&!libraryLocked;
        favorite.Content=DeskIcons.Labelled("star",L.T(state.Favorites.Contains(selected.Id)?"Usuń z ulubionych":"Ulubione"));
        try
        {
            var rendered=Render(); validation.Text=""; preview.Text=rendered.ContainsSecret?L.T("Podgląd ukryty: komenda zawiera sekret. Wartości nie są zapisywane."):rendered.Text;
            copy.IsEnabled=!working;
            var compatible=CommandCatalog.Scope(selected,target?.Profile.Id,target?.Profile.Folder??"")&&CommandCatalog.Supports(selected,target?.KnownCommandEnvironment??environment);
            compatible&=target?.KnownCommandEnvironment is not { Platform:not CommandPlatform.General } actual || CommandCatalog.Supports(selected,actual)&&selected.Tools.All(t=>actual.Tool(t)!=ToolAvailability.NotDetected)&&SessionPane.CompatibleShell(rendered.Shell,actual.Shell);
            insert.IsEnabled=execute.IsEnabled=!working&&ticket!=null&&target?.CanAcceptCommands==true&&!rendered.IsMultiline&&!rendered.ContainsSecret&&compatible&&(environment.Shell!=CommandShell.Any||selected.Shell==CommandShell.Any);
            ToolTip.SetTip(insert,L.T("Wstawia bez Enter. Sprawdź bieżący wiersz i program w terminalu."));
            ToolTip.SetTip(execute,L.T("Pokaż pełną komendę i potwierdź wysłanie Enter do wskazanej sesji."));
            if(rendered.IsMultiline)notice.Text=L.T("Skrypt wieloliniowy: podgląd i kopiowanie. Wstawianie bez wykonania nie jest gwarantowane.");
            else if(rendered.ContainsSecret)notice.Text=L.T("Sekretne parametry są dostępne tylko do kopiowania; nie wysyłamy ich do historii powłoki.");
        }
        catch(ArgumentException ex) { validation.Text=ex.Message; preview.Text=selected.Template; copy.IsEnabled=insert.IsEnabled=execute.IsEnabled=false; }
    }
    internal async Task DispatchAsync(string action)
    {
        var c=selected??throw new InvalidOperationException(L.T("Wybierz komendę.")); var rendered=Render(); var captured=ticket; var destination=target;
        var confirmed=false; var dangerous=false;
        if(action=="copy")
        {
            if(rendered.ContainsSecret && !await Dialogs.Confirm(owner,L.T("Skopiować sekret do schowka?"),L.T("Wartość będzie dostępna dla aplikacji czytających schowek. ServerDesk nie zapisze jej w historii."),lifetime.Token))return;
            var clipboard=owner.Clipboard??throw new InvalidOperationException(L.T("Schowek jest niedostępny.")); await clipboard.SetTextAsync(rendered.Text);
        }
        else
        {
            if(environment.Shell==CommandShell.Any&&c.Shell!=CommandShell.Any)throw new InvalidOperationException(L.T("Wybierz konkretną powłokę."));
            if(destination==null||captured==null||!ReferenceEquals(destination,owner.CommandTarget))throw new InvalidOperationException(L.T("Sesja zmieniła się. Wybierz komendę ponownie."));
            if(action=="execute")
            {
                var message=destination.Profile.Name+" · "+destination.Profile.Username+"@"+destination.Profile.Endpoint+"\n\n"+rendered.Text+"\n\n"+L.T("Zostanie wysłany Enter. Upewnij się, że terminal jest w powłoce, a bieżący wiersz jest pusty. Uprawnienia nie zostaną podniesione automatycznie.");
                if(c.Tools.Any(t=>environment.Tool(t)==ToolAvailability.Unknown)||!environment.Detected)message+="\n"+L.T("Zgodność lub narzędzia nie zostały w pełni potwierdzone. Zweryfikuj profil hosta.");
                confirmed=await Dialogs.Confirm(owner,L.T("Wykonać komendę w sesji SSH?"),message,lifetime.Token); if(!confirmed)return;
                if(!ReferenceEquals(destination,owner.CommandTarget)||destination.CommandConnectionId!=captured.ConnectionId)throw new InvalidOperationException(L.T("Sesja zmieniła się. Wybierz komendę ponownie."));
                if(c.Risk==CommandRisk.Dangerous) { dangerous=await Dialogs.Confirm(owner,L.T("Niebezpieczna komenda — potwierdź ponownie"),destination.Profile.Name+"\n\n"+rendered.Text+"\n\n"+L.T("Ta operacja może usunąć dane, zatrzymać usługę lub przerwać połączenie."),lifetime.Token); if(!dangerous)return; }
            }
            else if(action!="insert")throw new ArgumentException("Unknown command action.");
            await destination.SendCommandAsync(captured,rendered,c,action=="execute",confirmed||codeApproval.IsChecked==true,dangerous);
        }
        if(state.Preferences.RememberRecent && !rendered.ContainsSecret)
        {
            state=state with { Recent=new[]{new CommandRecent(c.Id,DateTimeOffset.UtcNow)}.Concat(state.Recent.Where(r=>r.CommandId!=c.Id)).Take(100).ToArray() }; await PersistStateAsync();
        }
        notice.Text=L.T(action switch { "copy"=>"Skopiowano komendę.","execute"=>"Wysłano komendę i Enter do wskazanej sesji.",_=>"Wstawiono komendę bez Enter." });
    }
    private async Task FavoriteAsync()
    {
        if(selected==null)return; var id=selected.Id;
        state=state with { Favorites=state.Favorites.Contains(id)?state.Favorites.Where(s=>s!=id).ToArray():state.Favorites.Append(id).ToArray() }; await PersistStateAsync(); RefreshList();
    }
    private async Task EditAsync(CommandDefinition? source)
    {
        if(libraryLocked)throw new InvalidOperationException(L.T("Odblokuj systemowy magazyn poświadczeń, aby otworzyć własne komendy."));
        if(source?.Origin==CommandOrigin.BuiltIn)return;
        var draft=source??new CommandDefinition { Id="user."+Guid.NewGuid().ToString("N"),Origin=CommandOrigin.User,Category="My commands",Template="echo {{text}}",Shell=CommandShell.Bash,Parameters=[new() { Key="text",Label="Text",Default="Hello" }] };
        var edited=await Dialogs.EditCommand(owner,draft,library.Categories,target?.Profile); if(edited==null)return;
        var next=library with { Commands=library.Commands.Where(c=>c.Id!=edited.Id).Append(edited).ToArray(),Categories=library.Categories.Append(edited.Category).Distinct().ToArray() };
        await store.SaveLibraryAsync(next,lifetime.Token); library=next; section.SelectedIndex=1; RefreshList(); list.SelectedItem=list.Items.OfType<CommandMatch>().FirstOrDefault(m=>m.Command.Id==edited.Id);
    }
    private async Task DuplicateAsync()
    {
        if(selected==null)return; var source=selected;
        await EditAsync(source with { Id="user."+Guid.NewGuid().ToString("N"),Origin=CommandOrigin.User,Operation="",Name=source.Name+L.T(" (kopia)"),NameEn=source.NameEn.Length>0?source.NameEn+" (copy)":"" });
    }
    private async Task DeleteAsync()
    {
        if(selected is not { Origin:CommandOrigin.User } c||!await Dialogs.Confirm(owner,L.T("Usunąć komendę?"),c.Title,lifetime.Token))return;
        var next=library with { Commands=library.Commands.Where(row=>row.Id!=c.Id).ToArray() }; await store.SaveLibraryAsync(next,lifetime.Token); library=next;
        state=state with { Favorites=state.Favorites.Where(id=>id!=c.Id).ToArray(),Recent=state.Recent.Where(r=>r.CommandId!=c.Id).ToArray() }; await PersistStateAsync(); RefreshList();
    }
    private async Task ToggleHistoryAsync()
    {
        var enabled=!state.Preferences.RememberRecent; state=state with { Preferences=state.Preferences with { RememberRecent=enabled },Recent=enabled?state.Recent:[] };
        await PersistStateAsync(); notice.Text=L.T(enabled?"Historia komend włączona: tylko identyfikatory i czas użycia.":"Historia komend wyłączona i wyczyszczona."); RefreshList();
    }
    private async Task ClearHistoryAsync() { state=state with { Recent=[] }; await PersistStateAsync(); RefreshList(); }
    private async Task ImportAsync()
    {
        if(libraryLocked)throw new InvalidOperationException(L.T("Najpierw odblokuj własną bibliotekę komend."));
        var files=await owner.StorageProvider.OpenFilePickerAsync(new() { Title=L.T("Import komend JSON"),AllowMultiple=false }); if(files.Count==0)return;
        await using var input=await files[0].OpenReadAsync(); var bytes=await StreamLimits.ReadAsync(input,CommandValidation.MaxDocumentBytes,lifetime.Token);
        var imported=CommandStore.Parse(new UTF8Encoding(false,true).GetString(bytes));
        if(!await Dialogs.Confirm(owner,L.T("Importować komendy?"),L.F($"Nowe komendy: {imported.Commands.Length}. Import nie uruchamia kodu i nie nadpisuje ustawień."),lifetime.Token))return;
        var next=CommandStore.Merge(library,imported); await store.SaveLibraryAsync(next,lifetime.Token); library=next; section.SelectedIndex=1; RefreshList();
    }
    private async Task ExportAsync()
    {
        if(libraryLocked)throw new InvalidOperationException(L.T("Najpierw odblokuj własną bibliotekę komend."));
        if(!await Dialogs.Confirm(owner,L.T("Eksportować własne komendy?"),L.T("Plik JSON zawiera jawne szablony, metadane i zapisane wartości domyślne. Nie zawiera wartości wpisanych podczas użycia komendy, historii ani ustawień aplikacji. Sprawdź cały plik przed udostępnieniem."),lifetime.Token))return;
        var file=await owner.StorageProvider.SaveFilePickerAsync(new() { Title=L.T("Eksport komend JSON"),SuggestedFileName="serverdesk-commands.json" }); if(file==null)return;
        var export=library with { Commands=library.Commands.Select(c=>c with { HostId=null,HostFolder="" }).ToArray() };
        await using var output=await file.OpenWriteAsync(); output.SetLength(0); await JsonSerializer.SerializeAsync(output,export,CommandValidation.Json,lifetime.Token);
    }
    private async Task CategoriesAsync()
    {
        if(libraryLocked)throw new InvalidOperationException(L.T("Najpierw odblokuj własną bibliotekę komend."));
        var edited=await Dialogs.EditCommandCategories(owner,library); if(edited==null)return;
        await store.SaveLibraryAsync(edited,lifetime.Token); library=edited; RefreshList();
    }
}
