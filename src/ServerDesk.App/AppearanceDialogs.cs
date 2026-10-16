using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Infrastructure;
using ServerDesk.Core;
using Iciclecreek.Terminal;

namespace ServerDesk.App;
internal static partial class Dialogs
{
    internal sealed record CredentialInput(string Secret,bool Remember);
    internal static async Task<CredentialInput?> Credentials(Window owner,bool key,bool rdp=false,bool vnc=false,string? notice=null)
    {
        var stack=new StackPanel { Spacing=DesignTokens.Section }; stack.Children.Add(DesignSystem.PageHeading("key",vnc?L.T("Uwierzytelnianie VNC"):rdp?L.T("Uwierzytelnianie RDP"):L.T("Uwierzytelnianie SSH")));
        if(notice!=null) { var warning=Text(notice,12); warning.Name="CredentialRecoveryNotice"; DesignSystem.Resource(warning,TextBlock.ForegroundProperty,"Warning"); stack.Children.Add(warning); }
        stack.Children.Add(DesignSystem.Label(key?L.T("Passphrase klucza prywatnego"):L.T("Hasło użytkownika"),true));
        var input=new TextBox { PasswordChar='●',MaxLength=CredentialLimits.MaximumSecretBytes }; stack.Children.Add(input);
        if(vnc)stack.Children.Add(Text(L.T("Standardowe uwierzytelnianie VNC używa pierwszych 8 bajtów hasła. Transmisja VNC nie jest szyfrowana."),12));
        stack.Children.Add(DesignSystem.Label(key?L.T("Pozostaw puste, jeśli klucz nie ma hasła."):L.T("Hasło służy wyłącznie do tej sesji, chyba że włączysz zapis."),true,12));
        var remember=new CheckBox { Content=L.T("Zapamiętaj w systemowym magazynie poświadczeń") }; stack.Children.Add(remember);
        stack.Children.Add(DesignSystem.Label(L.T("Windows: DPAPI · Linux: Secret Service.\nHasło nie jest zapisywane w bazie profili ani eksportowane."),true,12));
        var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium }; stack.Children.Add(row); var dialog=Window(vnc?L.T("Połączenie VNC"):rdp?L.T("Połączenie RDP"):L.T("Połączenie SSH"),stack);
        row.Children.Add(Button("Anuluj",()=>dialog.Close())); var connect=Button("Połącz",()=>dialog.Close(new CredentialInput(input.Text??"",remember.IsChecked==true))); connect.Classes.Add("primary"); row.Children.Add(connect);
        dialog.Opened+=(_,_)=>input.Focus(); dialog.KeyDown+=(_,e)=> { if(e.Key==Avalonia.Input.Key.Escape)dialog.Close(); if(e.Key==Avalonia.Input.Key.Enter)dialog.Close(new CredentialInput(input.Text??"",remember.IsChecked==true)); };
        return await dialog.ShowDialog<CredentialInput?>(owner);
    }
    internal static async Task Message(Window owner,string title,string text)
    {
        var message=new SelectableTextBlock { Text=text,TextWrapping=TextWrapping.Wrap }; DesignSystem.Resource(message,SelectableTextBlock.ForegroundProperty,"ForegroundPrimary"); DesignSystem.Resource(message,SelectableTextBlock.FontSizeProperty,"FontBody");
        var stack=new StackPanel { Spacing=18,Children={DesignSystem.PageHeading("info",title),message} };
        var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right }; stack.Children.Add(row); var dialog=Window(title,stack); row.Children.Add(Button("Zamknij",()=>dialog.Close())); await dialog.ShowDialog(owner);
    }
    internal static Task Settings(Window owner,ProfileRepository repository,Func<Task> changed)=>EditApplicationSettings(owner,repository,changed);
    internal static async Task<string?> Commands(Window owner,IEnumerable<string> commands,Func<string,string?>? icon=null)
    {
        var names=commands.ToArray(); var search=new TextBox { Name="CommandSearch",PlaceholderText=L.T("Wpisz nazwę akcji…") }; var list=new ListBox { Name="CommandResults",Height=240 };
        list.ItemTemplate=new FuncDataTemplate<string>((name,_)=>name==null?null:DeskIcons.Labelled(icon?.Invoke(name)??DeskIcons.ForAction(name)??"square-terminal",name));
        Avalonia.Automation.AutomationProperties.SetName(search,L.T("Szukaj akcji")); Avalonia.Automation.AutomationProperties.SetName(list,L.T("Dostępne akcje"));
        var empty=Text(L.T("Brak pasujących akcji. Zmień wyszukiwanie."),12); empty.Name="CommandsEmpty";
        var results=new Grid { Children={list,empty} }; empty.VerticalAlignment=VerticalAlignment.Center;
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8 };
        var stack=new StackPanel { Spacing=DesignTokens.Section,Children={DesignSystem.PageHeading("square-terminal",L.T("Paleta akcji")),search,results,actions} }; var dialog=Window(L.T("Paleta akcji"),stack);
        void Pick() { if(list.SelectedItem is string command)dialog.Close(command); }
        var run=Button("Uruchom",Pick); run.Classes.Add("primary"); actions.Children.Add(Button("Anuluj",()=>dialog.Close())); actions.Children.Add(run);
        void Filter()
        {
            var terms=(search.Text??"").Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            var matches=names.Where(name=>terms.All(term=>name.Contains(term,StringComparison.OrdinalIgnoreCase))).ToArray();
            list.ItemsSource=matches; list.SelectedIndex=matches.Length>0?0:-1; empty.IsVisible=matches.Length==0; run.IsEnabled=matches.Length>0;
        }
        search.TextChanged+=(_,_)=>Filter(); Filter(); list.DoubleTapped+=(_,_)=>Pick();
        dialog.AddHandler(Avalonia.Input.InputElement.KeyDownEvent,(_,e)=>
        {
            if(e.Key==Avalonia.Input.Key.Enter) { Pick(); e.Handled=true; }
            else if(e.Key is Avalonia.Input.Key.Down or Avalonia.Input.Key.Up)
            { if(list.ItemCount>0) { list.SelectedIndex=Math.Clamp(list.SelectedIndex+(e.Key==Avalonia.Input.Key.Down?1:-1),0,list.ItemCount-1); if(list.SelectedItem!=null)list.ScrollIntoView(list.SelectedItem); } e.Handled=true; }
        },Avalonia.Interactivity.RoutingStrategies.Tunnel);
        dialog.Opened+=(_,_)=>search.Focus(); return await dialog.ShowDialog<string?>(owner);
    }
}
internal static class TerminalAppearance
{
    internal static readonly string[] Names=["ServerDesk Default","VS Code Dark","Nord","One Dark","Solarized Dark"];
    private static readonly string[][] Palettes=[
        ["#141414","#E6E6E6","#202020","#E87979","#80C99B","#D7BA7D","#79B8E8","#C49AE0","#78C8CA","#D5D5D5","#707070","#F4978E","#A8DCB7","#EBD49B","#A0CEEF","#D6B6EA","#A2DDDF","#FFFFFF"],
        ["#181818","#CCCCCC","#000000","#CD3131","#0DBC79","#E5E510","#2472C8","#BC3FBC","#11A8CD","#E5E5E5","#666666","#F14C4C","#23D18B","#F5F543","#3B8EEA","#D670D6","#29B8DB","#FFFFFF"],
        ["#242A35","#ECEFF4","#3B4252","#BF616A","#A3BE8C","#EBCB8B","#81A1C1","#B48EAD","#88C0D0","#E5E9F0","#4C566A","#D08770","#B5CEA3","#F0D9A5","#A4BCD3","#C7A5C1","#8FBCBB","#ECEFF4"],
        ["#282C34","#ABB2BF","#282C34","#E06C75","#98C379","#E5C07B","#61AFEF","#C678DD","#56B6C2","#ABB2BF","#5C6370","#E99198","#B1D398","#EBD19D","#8AC4F2","#D5A0E5","#82CCD4","#FFFFFF"],
        ["#002B36","#A3B5B8","#073642","#DC322F","#859900","#B58900","#268BD2","#D33682","#2AA198","#EEE8D5","#657B83","#CB4B16","#A7BD35","#D4B24A","#63AFE1","#B49ADB","#6ABFB6","#FDF6E3"] ];
    internal static void Apply(TerminalControl terminal,string name,int font,string family="Domyślna")
    {
        var index=Array.IndexOf(Names,name); if(index<0)index=0; var p=Palettes[index];
        terminal.FontFamily=new FontFamily(family=="Domyślna"?"Cascadia Code,JetBrains Mono,Cascadia Mono,Consolas,DejaVu Sans Mono,monospace":family+",monospace"); terminal.FontSize=Math.Clamp(font,9,32);
        terminal.Background=new SolidColorBrush(Color.Parse(p[0])); terminal.Foreground=new SolidColorBrush(Color.Parse(p[1])); if(!terminal.IsLoaded || !terminal.IsEffectivelyVisible)return; terminal.ApplyTemplate();
        // OSC 4 changes ANSI entries in the existing emulator without replacing/resetting its buffer.
        for(var i=0;i<16;i++)terminal.Terminal.Write($"\u001b]4;{i};{p[i+2]}\u001b\\");
    }
}
