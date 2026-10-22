using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task<BitwardenChoice?> Bitwarden(Window owner,Func<BitwardenCli> create,ServerProfile? profile,BitwardenReference? reference,bool bindingOnly,CancellationToken ct)
    {
        if(ct.IsCancellationRequested)return null;
        using var scope=CancellationTokenSource.CreateLinkedTokenSource(ct);
        BitwardenCli? client=null; Task work=Task.CompletedTask; var busy=false; var closed=false;
        var stack=new StackPanel { Spacing=14 };
        stack.Children.Add(DesignSystem.PageHeading("key","Bitwarden / Vaultwarden",L.T(bindingOnly?"Przypisz wpis sejfu. Hasło nie zostanie zapisane w profilu.":"Wybierz hasło do tego połączenia. Sejf zostanie zamknięty w ServerDesk po zamknięciu okna.")));
        if(profile!=null)stack.Children.Add(DesignSystem.Card(Text(profile.Protocol+" · "+profile.Endpoint+"\n"+profile.Username,13)));
        var status=Text(L.T("Sprawdzanie stanu Bitwarden CLI…"),12); status.Name="BitwardenStatus"; stack.Children.Add(status);
        var master=new TextBox { Name="BitwardenMasterPassword",PasswordChar='●',MaxLength=4096,PlaceholderText=L.T("Hasło główne sejfu") };
        AutomationProperties.SetName(master,L.T("Hasło główne sejfu")); stack.Children.Add(master);
        var unlock=Button("Odblokuj sejf",()=>{}); unlock.Name="BitwardenUnlock"; unlock.Content=DeskIcons.Labelled("key",L.T("Odblokuj sejf")); unlock.HorizontalAlignment=HorizontalAlignment.Left; stack.Children.Add(unlock);
        var search=new TextBox { Name="BitwardenSearch",Text=profile?.Host??"",MaxLength=200,PlaceholderText=L.T("Nazwa wpisu lub adres — co najmniej 2 znaki") };
        AutomationProperties.SetName(search,L.T("Szukaj w sejfie"));
        var find=Button("Szukaj w sejfie",()=>{}); find.Name="BitwardenFind"; find.Content=DeskIcons.Labelled("search",L.T("Szukaj w sejfie"));
        var searchRow=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={search,find} }; Grid.SetColumn(find,1); searchRow.IsVisible=reference==null; stack.Children.Add(searchRow);
        var list=new ListBox { Name="BitwardenEntries",Height=190,IsVisible=reference==null,ItemTemplate=new FuncDataTemplate<BitwardenEntry>((entry,_)=>entry==null?null:new StackPanel { Margin=new Thickness(4),Spacing=3,Children={DesignSystem.Label(entry.Reference.Name),DesignSystem.Hint(entry.Username)} }) }; stack.Children.Add(list);
        if(reference!=null)stack.Children.Add(Text(L.T("Przypisany wpis")+": "+reference.Name+"\n"+reference.Server,12));
        var warning=Text("",12); warning.Name="BitwardenError"; warning.IsVisible=false; DesignSystem.Resource(warning,TextBlock.ForegroundProperty,"Warning"); stack.Children.Insert(stack.Children.IndexOf(status)+1,warning);
        stack.Children.Add(DesignSystem.Hint(L.T("Konto skonfiguruj i zaloguj wcześniej w oficjalnym CLI. Instrukcja: Pomoc → Bitwarden / Vaultwarden. Hasło główne i klucz sesji nie trafiają do schowka, bazy ani logów.")));
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8 }; stack.Children.Add(actions);
        var dialog=Window("Bitwarden / Vaultwarden",stack); dialog.Name="BitwardenPicker"; dialog.Width=640;
        dialog.SizeChanged+=(_,_)=> { var compact=dialog.ClientSize.Width<560; actions.Orientation=compact?Orientation.Vertical:Orientation.Horizontal; actions.HorizontalAlignment=compact?HorizontalAlignment.Stretch:HorizontalAlignment.Right; };
        var cancel=Button("Anuluj",()=>dialog.Close()); actions.Children.Add(cancel);
        if(!bindingOnly && reference!=null) { var manual=Button("Podaj hasło ręcznie",()=>dialog.Close(new BitwardenChoice(null,"",null,true))); manual.Name="BitwardenManual"; actions.Children.Add(manual); }
        var use=Button(bindingOnly?"Przypisz wpis":"Użyj hasła",()=>{}); use.Name="BitwardenUse"; use.Content=DeskIcons.Labelled("key",L.T(bindingOnly?"Przypisz wpis":"Użyj hasła")); use.Classes.Add("primary"); actions.Children.Add(use);
        void Refresh()
        {
            var ready=!busy && client?.IsUnlocked==true;
            master.IsEnabled=unlock.IsEnabled=!busy; find.IsEnabled=searchRow.IsEnabled=list.IsEnabled=ready; use.IsEnabled=ready&&(reference!=null||list.SelectedItem is BitwardenEntry);
        }
        void Run(Func<Task> action)
        {
            if(busy || closed || scope.IsCancellationRequested)return;
            busy=true; warning.IsVisible=false; Refresh(); work=Execute();
            async Task Execute()
            {
                try { client??=create(); await action(); }
                catch(OperationCanceledException) when(scope.IsCancellationRequested) { }
                catch(Exception ex)
                {
                    if(!closed && !scope.IsCancellationRequested)
                    {
                        warning.Text=ex is BitwardenException or ArgumentException?ex.Message:L.T("Nie udało się wykonać operacji sejfu."); warning.IsVisible=true;
                        // Never log CLI output, command data, master passwords or vault contents.
                        if(ex is BitwardenException or ArgumentException)Serilog.Log.Warning("Bitwarden operation failed: {Reason}",ex is BitwardenException failure?failure.Reason.ToString():ex.GetType().Name);
                        else ApplicationDiagnostics.Report("BitwardenPicker",ex);
                    }
                }
                finally { busy=false; if(!closed)Refresh(); }
            }
        }
        unlock.Click+=(_,_)=>Run(async()=>
        {
            var password=master.Text??""; master.Text="";
            try { await client!.UnlockAsync(password,scope.Token,reference); }
            finally { password=""; }
            if(closed || scope.IsCancellationRequested)return;
            status.Text=L.T("Sejf odblokowany na czas tego okna."); search.Focus();
        });
        find.Click+=(_,_)=>Run(async()=>
        {
            list.ItemsSource=null;
            var results=await client!.SearchAsync(search.Text??"",scope.Token);
            if(closed || scope.IsCancellationRequested)return;
            list.ItemsSource=results; list.SelectedIndex=results.Count>0?0:-1;
            status.Text=results.Count==0?L.T("Brak pasujących wpisów."):L.F($"Wpisy: {results.Count}. Maksymalnie 100 wyników — zawęź wyszukiwanie.");
        });
        use.Click+=(_,_)=>Run(async()=>
        {
            var selected=reference??(list.SelectedItem as BitwardenEntry)?.Reference;
            if(selected==null)return;
            if(bindingOnly) { dialog.Close(new BitwardenChoice(selected,(list.SelectedItem as BitwardenEntry)?.Username??"",null)); return; }
            var value=await client!.GetPasswordAsync(selected,scope.Token);
            if(closed || scope.IsCancellationRequested)return;
            if(profile is { Protocol:not Protocol.VNC } && profile.Username.Length>0 && value.Username.Length>0 && !string.Equals(profile.Username,value.Username,profile.Protocol==Protocol.RDP?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
            {
                if(!await Confirm(dialog,L.T("Inny login we wpisie sejfu"),L.T("Hasło zostanie użyte dla loginu zapisanego w profilu połączenia.")+"\n\n"+profile.Username+" ← "+value.Username,scope.Token))return;
            }
            if(!scope.IsCancellationRequested && !closed)dialog.Close(new BitwardenChoice(selected,value.Username,value.Password));
        });
        list.SelectionChanged+=(_,_)=>Refresh();
        search.KeyDown+=(_,e)=> { if(e.Key==Key.Enter) { e.Handled=true; find.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); } };
        master.KeyDown+=(_,e)=> { if(e.Key==Key.Enter) { e.Handled=true; unlock.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); } };
        dialog.Closed+=(_,_)=> { closed=true; scope.Cancel(); master.Text=""; list.ItemsSource=null; client?.Lock(); };
        dialog.Opened+=(_,_)=>Run(async()=> { var account=await client!.StatusAsync(scope.Token); if(!closed && !scope.IsCancellationRequested) { status.Text=account.Server+"\n"+L.T("Odblokuj sejf hasłem głównym."); master.Focus(); } });
        using var cancelled=ct.Register(()=>Avalonia.Threading.Dispatcher.UIThread.Post(()=> { if(!closed)dialog.Close(); }));
        Refresh();
        try { return await dialog.ShowDialog<BitwardenChoice?>(owner); }
        finally { scope.Cancel(); try { await work; } finally { master.Text=""; list.ItemsSource=null; client?.Dispose(); } }
    }
}
