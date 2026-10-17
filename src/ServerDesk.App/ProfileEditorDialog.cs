using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static class ProfileEditorDialog
{
    internal static async Task<ServerProfile?> Show(Window owner,ServerProfile? profile,IEnumerable<string> folders)
    {
        var original=profile??new ServerProfile();
        var fresh=string.IsNullOrEmpty(original.Name) && string.IsNullOrEmpty(original.Host);
        var dialog=new Window { Title=fresh?L.T("Nowe połączenie"):L.T("Edytuj połączenie"),Width=780,Height=740,MinWidth=600,MinHeight=460,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        Dialogs.FitToOwnerScreen(dialog,owner);
        var root=new Grid { RowDefinitions=new RowDefinitions("Auto,Auto,*,Auto"),Margin=new Thickness(20) }; dialog.Content=root;
        var pages=new Grid(); Grid.SetRow(pages,2); root.Children.Add(pages);
        var navigation=new TabControl { Name="ProfileEditorNavigation",Height=42,Margin=new Thickness(0,14,0,0) };
        AutomationProperties.SetName(navigation,L.T("Sekcje edytora serwera")); Grid.SetRow(navigation,1); root.Children.Add(navigation);
        var pageControls=new List<ScrollViewer>(); var navItems=new List<TabItem>();
        var fields=new Dictionary<Control,(TextBlock Error,int Page)>();
        var submitted=false;
        var summary=DesignSystem.Label("",false,12); summary.Name="ProfileValidationSummary"; summary.TextWrapping=TextWrapping.Wrap; summary.IsVisible=false; DesignSystem.Resource(summary,TextBlock.ForegroundProperty,"Danger");
        var errors=new Dictionary<Control,string>();

        StackPanel Page(string title,string subtitle,string icon)
        {
            title=L.T(title); subtitle=L.T(subtitle);
            var index=pageControls.Count;
            var body=new StackPanel { Spacing=16,Margin=new Thickness(0,16,8,12) };
            var intro=new StackPanel { Spacing=4,Children={DesignSystem.Label(title,false,18)} };
            var hint=DesignSystem.Label(subtitle,true,12); hint.TextWrapping=TextWrapping.Wrap; intro.Children.Add(hint); body.Children.Add(intro);
            var scroll=new ScrollViewer { Content=body,IsVisible=index==0,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            pages.Children.Add(scroll); pageControls.Add(scroll);
            navItems.Add(new TabItem { Classes={"page-navigation"},Header=DeskIcons.Labelled(icon,title),Name="ProfilePage"+index });
            return body;
        }
        StackPanel Card(StackPanel page,string title)
        {
            title=L.T(title);
            var body=new StackPanel { Spacing=12 }; var heading=DesignSystem.Label(title); heading.FontWeight=FontWeight.SemiBold; body.Children.Add(heading);
            page.Children.Add(DesignSystem.Card(body,new Thickness(16))); return body;
        }
        Control Field(string label,Control control,int page,string? hint=null)
        {
            label=L.T(label); if(hint!=null)hint=L.T(hint);
            var body=new StackPanel { Spacing=5 }; body.Children.Add(DesignSystem.Label(label,true,12)); body.Children.Add(control);
            AutomationProperties.SetName(control,label); control.Name??="Profile"+fields.Count;
            if(hint!=null) { var help=DesignSystem.Label(hint,true,12); help.TextWrapping=TextWrapping.Wrap; body.Children.Add(help); }
            var error=DesignSystem.Label("",false,12); error.IsVisible=false; error.TextWrapping=TextWrapping.Wrap; DesignSystem.Resource(error,TextBlock.ForegroundProperty,"Danger"); body.Children.Add(error); fields[control]=(error,page); return body;
        }
        Grid Pair(Control left,Control right,double rightWidth=0)
        {
            var row=new Grid { ColumnDefinitions=new ColumnDefinitions(rightWidth>0?$"*,{rightWidth}":"*,*"),RowDefinitions=new RowDefinitions("Auto,Auto"),ColumnSpacing=12,RowSpacing=12 };
            row.Children.Add(left); Grid.SetColumn(right,1); row.Children.Add(right);
            row.SizeChanged+=(_,_)=> { var narrow=row.Bounds.Width<500; Grid.SetColumn(right,narrow?0:1); Grid.SetRow(right,narrow?1:0); row.RowSpacing=narrow?12:0; row.ColumnDefinitions[1].Width=narrow?new GridLength(0):rightWidth>0?new GridLength(rightWidth):GridLength.Star; };
            return row;
        }
        void Select(int index) { navigation.SelectedIndex=index; for(var i=0;i<pageControls.Count;i++)pageControls[i].IsVisible=i==index; }

        var connection=Page("Połączenie","Adres serwera i sposób logowania. Hasło podasz podczas łączenia.","connect");
        var appearancePage=Page("Wygląd","Rozpoznawaj serwer po ikonie, kolorze lub własnej grafice.","image");
        var organization=Page("Organizacja","Foldery i tagi pomagają odnaleźć serwer na liście.","folder");
        var desktop=Page("Pulpit RDP","Ustawienia klienta i przekazywania danych do pulpitu zdalnego.","monitor");

        var endpoint=Card(connection,"Serwer");
        var name=new TextBox { Text=original.Name,PlaceholderText=L.T("np. Baza produkcyjna"),Name="ProfileName" };
        var host=new TextBox { Text=original.Host,PlaceholderText=L.T("np. 10.0.0.20 lub server.example"),Name="ProfileHost" };
        var port=new TextBox { Text=original.Port.ToString(),Name="ProfilePort" };
        var protocol=new ComboBox { ItemsSource=Enum.GetValues<Protocol>(),SelectedItem=original.Protocol,HorizontalAlignment=HorizontalAlignment.Stretch,Name="ProfileProtocol" };
        endpoint.Children.Add(Pair(Field("Nazwa",name,0),Field("Protokół",protocol,0),160));
        var addressRow=Pair(Field("Hostname / IP",host,0),Field("Port",port,0),130); endpoint.Children.Add(addressRow);
        var authentication=Card(connection,"Logowanie");
        var user=new TextBox { Text=original.Username,PlaceholderText=L.T("Login użytkownika"),Name="ProfileUsername" };
        var domain=new TextBox { Text=original.Domain,PlaceholderText=L.T("Opcjonalnie"),Name="ProfileDomain" };
        var loginField=Field("Login",user,0); var domainField=Field("Domena Windows",domain,0);
        var loginRow=Pair(loginField,domainField); authentication.Children.Add(loginRow);
        var auth=new ComboBox { ItemsSource=Enum.GetValues<Authentication>(),SelectedItem=original.Authentication,HorizontalAlignment=HorizontalAlignment.Stretch,Name="ProfileAuthentication",ItemTemplate=new FuncDataTemplate<Authentication>((a,_)=>DesignSystem.Label(a==Authentication.PrivateKey?L.T("Klucz prywatny"):L.T("Hasło"))) };
        var authField=Field("Uwierzytelnianie SSH",auth,0); authentication.Children.Add(authField);
        var key=new TextBox { Text=original.PrivateKeyPath,PlaceholderText=L.T("Ścieżka do pliku klucza"),Name="ProfilePrivateKey" };
        var chooseKey=Dialogs.Button("Wybierz plik",()=>_ = PickKeyAsync());
        var keyRow=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=8,Children={key} }; Grid.SetColumn(chooseKey,1); keyRow.Children.Add(chooseKey);
        var keyField=Field("Klucz prywatny",keyRow,0,"Klucz pozostaje w wybranym pliku na Twoim komputerze."); fields[key]=fields[keyRow]; authentication.Children.Add(keyField);
        var rdpShortcut=Dialogs.Button("Ustawienia pulpitu RDP",()=>Select(3)); rdpShortcut.HorizontalAlignment=HorizontalAlignment.Left; connection.Children.Add(rdpShortcut);

        var vncCard=Card(connection,"Pulpit VNC"); var vncFrame=(Border)vncCard.Parent!;
        var vncAuth=new ComboBox { Name="VncAuthentication",ItemsSource=new[]{L.T("Hasło VNC"),L.T("Bez uwierzytelniania")},SelectedIndex=(int)original.Vnc.Authentication,HorizontalAlignment=HorizontalAlignment.Stretch };
        vncCard.Children.Add(Field("Uwierzytelnianie VNC",vncAuth,0));
        var vncShared=new CheckBox { Content=L.T("Współdziel sesję z innymi klientami"),IsChecked=original.Vnc.Shared };
        var vncViewOnly=new CheckBox { Content=L.T("Tylko podgląd — bez myszy i klawiatury"),IsChecked=original.Vnc.ViewOnly };
        var vncFit=new CheckBox { Content=L.T("Dopasuj pulpit do rozmiaru panelu"),IsChecked=original.Vnc.FitToWindow };
        vncCard.Children.Add(vncShared); vncCard.Children.Add(vncViewOnly); vncCard.Children.Add(vncFit);
        var vncHelp=DesignSystem.Label(L.T("Obsługiwany jest standardowy RFB z hasłem VNC lub bez uwierzytelniania. Transmisja nie jest szyfrowana — używaj zaufanej sieci, VPN lub istniejącego tunelu SSH. TLS/VeNCrypt i logowanie kontem systemowym nie są obsługiwane."),true,12); vncHelp.TextWrapping=TextWrapping.Wrap; vncCard.Children.Add(vncHelp);

        var serialCard=Card(connection,"Port szeregowy"); var serialFrame=(Border)serialCard.Parent!;
        var serialPort=new TextBox { Name="SerialPortName",Text=original.Serial.PortName,PlaceholderText=OperatingSystem.IsWindows()?L.T("np. COM3"):L.T("np. /dev/ttyUSB0") };
        var ports=DeskIcons.Button("refresh","Wykryj porty szeregowe",()=> { }); ports.Click+=(_,_)=>_ = DetectPortsAsync(); serialPort.InnerRightContent=ports;
        var baud=new TextBox { Name="SerialBaudRate",Text=original.Serial.BaudRate.ToString() };
        serialCard.Children.Add(Pair(Field("Port / urządzenie",serialPort,0),Field("Prędkość (baud)",baud,0),160));
        var dataBits=new ComboBox { Name="SerialDataBits",ItemsSource=new[]{5,6,7,8},SelectedItem=original.Serial.DataBits,HorizontalAlignment=HorizontalAlignment.Stretch };
        var parity=new ComboBox { Name="SerialParity",ItemsSource=new[]{L.T("Brak (N)"),L.T("Nieparzysta (O)"),L.T("Parzysta (E)"),"Mark (M)","Space (S)"},SelectedIndex=(int)original.Serial.Parity,HorizontalAlignment=HorizontalAlignment.Stretch };
        serialCard.Children.Add(Pair(Field("Bity danych",dataBits,0),Field("Parzystość",parity,0)));
        var stopBits=new ComboBox { Name="SerialStopBits",ItemsSource=new[]{"1","2","1.5"},SelectedIndex=(int)original.Serial.StopBits-1,HorizontalAlignment=HorizontalAlignment.Stretch };
        var handshake=new ComboBox { Name="SerialHandshake",ItemsSource=new[]{L.T("Brak"),"XON/XOFF","RTS/CTS","RTS/CTS + XON/XOFF"},SelectedIndex=(int)original.Serial.Handshake,HorizontalAlignment=HorizontalAlignment.Stretch };
        serialCard.Children.Add(Pair(Field("Bity stopu",stopBits,0),Field("Sterowanie przepływem",handshake,0)));
        var dtr=new CheckBox { Content="DTR",IsChecked=original.Serial.DtrEnable }; var rts=new CheckBox { Content=L.T("RTS (ręczne)"),IsChecked=original.Serial.RtsEnable }; serialCard.Children.Add(new WrapPanel { Children={dtr,rts},ItemSpacing=16 });
        var serialHelp=DesignSystem.Label(L.T("Port otwieramy dopiero po kliknięciu Połącz. Przy sterowaniu RTS/CTS linią RTS zarządza sterownik. Możesz wpisać port ręcznie, także gdy nie ma go na liście."),true,12); serialHelp.TextWrapping=TextWrapping.Wrap; serialCard.Children.Add(serialHelp);

        var iconPicker=new ProfileIconPicker(original); var visualCard=Card(appearancePage,"Ikona i kolor"); visualCard.Children.Add(Field("Ikona serwera",iconPicker,1));
        var appearance=new ProfileAppearancePicker(dialog,original); visualCard.Children.Add(appearance);
        var colorInput=appearance.ColorInput; fields[colorInput]=(appearance.ColorError,1);

        var group=Card(organization,"Miejsce na liście");
        var folder=new TextBox { Text=original.Folder,PlaceholderText=L.T("np. Produkcja/Linux"),Name="ProfileFolder" };
        var folderChoice=DeskIcons.Button("folder","Wybierz istniejący folder",()=> { });
        var folderMenu=new ContextMenu { ItemsSource=new[]{""}.Concat(folders.Where(f=>f is not ("Wszystkie" or "★ Ulubione" or "◷ Ostatnie") && f.Length>0).Distinct().Order(StringComparer.CurrentCultureIgnoreCase)).Select(path=> { var item=new MenuItem { Header=path.Length==0?L.T("Bez folderu"):path,Icon=DeskIcons.Create("folder",16) }; item.Click+=(_,_)=>folder.Text=path; return item; }).ToArray() };
        folderChoice.ContextMenu=folderMenu; folderChoice.Click+=(_,_)=>folderMenu.Open(folderChoice); folder.InnerRightContent=folderChoice;
        group.Children.Add(Field("Folder / podfolder",folder,2,"Użyj /, aby utworzyć podfolder. Puste pole oznacza listę główną."));
        var tags=new TextBox { Text=original.Tags,PlaceholderText=L.T("np. produkcja, linux, baza"),Name="ProfileTags" }; group.Children.Add(Field("Tagi",tags,2));
        var description=new TextBox { Text=original.Description,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=90,MaxHeight=150,Name="ProfileDescription" }; group.Children.Add(Field("Opis",description,2));
        var favorite=new CheckBox { Content=L.T("Ulubiony"),IsChecked=original.Favorite }; group.Children.Add(favorite);

        var display=Card(desktop,"Obraz i okno");
        var rdpMode=new ComboBox { Name="RdpDisplayMode",ItemsSource=new[]{L.T("Osobne okno klienta"),L.T("W zakładce ServerDesk")},SelectedIndex=OperatingSystem.IsLinux()?1:(int)original.Rdp.DisplayMode,HorizontalAlignment=HorizontalAlignment.Stretch,IsEnabled=OperatingSystem.IsWindows() };
        display.Children.Add(Field("Wyświetlanie pulpitu",rdpMode,3,OperatingSystem.IsWindows()?"Tryb w zakładce obsługuje podział widoku i dokowanie.":"Linux osadza klienta X11 FreeRDP w zakładce przez X11/XWayland."));
        var width=new TextBox { Text=original.Rdp.Width.ToString(),Name="RdpWidth" }; var height=new TextBox { Text=original.Rdp.Height.ToString(),Name="RdpHeight" };
        display.Children.Add(Pair(Field("Szerokość (640–7680)",width,3),Field("Wysokość (480–4320)",height,3)));
        var fullscreen=new CheckBox { Name="RdpExternalFullscreen",Content=L.T("Pełny ekran klienta w osobnym oknie"),IsChecked=original.Rdp.Fullscreen }; var dynamic=new CheckBox { Content=L.T("Dynamiczna rozdzielczość / skalowanie"),IsChecked=original.Rdp.DynamicResolution }; display.Children.Add(fullscreen); display.Children.Add(dynamic);
        var fullscreenHelp=DesignSystem.Hint(L.T("Pełny ekran dotyczy klienta w osobnym oknie. Pulpit w zakładce korzysta z rozmiaru panelu ServerDesk; to ustawienie go nie przełącza.")); fullscreenHelp.Name="RdpFullscreenHelp"; display.Children.Add(fullscreenHelp);
        var input=Card(desktop,"Klawiatura i schowek");
        var keyboard=new ComboBox { Name="RdpKeyboardMode",ItemsSource=new[]{L.T("Skróty Windows na tym komputerze"),L.T("Skróty Windows na serwerze"),L.T("Skróty na serwerze tylko w pełnym ekranie")},SelectedIndex=(int)original.Rdp.KeyboardMode,HorizontalAlignment=HorizontalAlignment.Stretch,IsEnabled=OperatingSystem.IsWindows() };
        input.Children.Add(Field("Klawiatura RDP",keyboard,3,"Ctrl+Alt+← / → oddaje fokus do ServerDesk."));
        var clipboard=new CheckBox { Content=L.T("Schowek"),IsChecked=original.Rdp.Clipboard }; input.Children.Add(clipboard);
        var reconnect=new CheckBox { Content=L.T("Ponawiaj po utracie sieci (do 5 prób, Windows)"),IsChecked=original.Rdp.AutoReconnect,IsEnabled=OperatingSystem.IsWindows() }; input.Children.Add(reconnect);
        var sharing=Card(desktop,"Folder lokalny");
        var redirect=new CheckBox { Content=L.T("Przekaż wybrany folder lokalny (FreeRDP / Linux)"),IsChecked=original.Rdp.RedirectLocalFolder,IsEnabled=!OperatingSystem.IsWindows()||original.Rdp.RedirectLocalFolder }; sharing.Children.Add(redirect);
        var localFolder=new TextBox { Text=original.Rdp.LocalFolder,Name="RdpLocalFolder",IsEnabled=!OperatingSystem.IsWindows() && redirect.IsChecked==true }; sharing.Children.Add(Field("Ścieżka folderu lokalnego",localFolder,3));
        var redirectHelp=DesignSystem.Label(OperatingSystem.IsWindows()?L.T("Przekazywanie folderu jest dostępne w FreeRDP na Linux."):L.T("Tylko wskazany folder będzie dostępny na zdalnym komputerze."),true,12); redirectHelp.TextWrapping=TextWrapping.Wrap; sharing.Children.Add(redirectHelp);

        var header=new Grid { ColumnDefinitions=new ColumnDefinitions("44,*"),ColumnSpacing=12 };
        var preview=new ProfileVisual(original,32); var previewFrame=new Border { Child=preview,Width=44,Height=44,CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1) }; DesignSystem.Resource(previewFrame,Border.BackgroundProperty,"BackgroundSecondary"); DesignSystem.Resource(previewFrame,Border.BorderBrushProperty,"BorderDefault"); header.Children.Add(previewFrame);
        var title=DesignSystem.Label(fresh?L.T("Nowe połączenie"):original.Name,false,20); title.Name="ProfilePreviewName"; title.TextTrimming=TextTrimming.CharacterEllipsis;
        var address=DesignSystem.Label("",true,12); address.Name="ProfilePreviewAddress"; address.TextTrimming=TextTrimming.CharacterEllipsis;
        var headingStack=new StackPanel { Spacing=4,Children={title,address} }; Grid.SetColumn(headingStack,1); header.Children.Add(headingStack); root.Children.Add(header);
        var footer=new StackPanel { Spacing=8,Margin=new Thickness(0,12,0,0) }; footer.Children.Add(summary);
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8 };
        var footerRow=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=12 };
        var note=DesignSystem.Label(L.T("Zapis nie uruchamia połączenia."),true,12); note.TextWrapping=TextWrapping.Wrap; footerRow.Children.Add(note); Grid.SetColumn(actions,1); footerRow.Children.Add(actions); footer.Children.Add(footerRow);
        actions.Children.Add(Dialogs.Button("Anuluj",()=>dialog.Close())); var save=Dialogs.Button("Zapisz",Save); save.Classes.Add("primary"); actions.Children.Add(save); Grid.SetRow(footer,3); root.Children.Add(footer);

        void Refresh()
        {
            var selected=protocol.SelectedItem is Protocol p?p:Protocol.SSH;
            var isRdp=selected==Protocol.RDP; var isSerial=selected==Protocol.Serial; domainField.IsVisible=isRdp; Grid.SetColumnSpan(loginField,isRdp?1:2);
            ((StackPanel)connection.Children[0]).Children.OfType<TextBlock>().Last().Text=isSerial?L.T("Wybierz urządzenie i parametry transmisji szeregowej."):L.T("Adres serwera i sposób logowania. Hasło podasz podczas łączenia.");
            ((TextBlock)endpoint.Children[0]).Text=isSerial?L.T("Połączenie"):L.T("Serwer");
            ((Border)authentication.Parent!).IsVisible=selected is Protocol.SSH or Protocol.RDP;
            addressRow.IsVisible=!isSerial; vncFrame.IsVisible=selected==Protocol.VNC; serialFrame.IsVisible=isSerial;
            authField.IsVisible=selected==Protocol.SSH; keyField.IsVisible=selected==Protocol.SSH && auth.SelectedItem is Authentication.PrivateKey;
            rts.IsEnabled=handshake.SelectedIndex<2;
            rdpShortcut.IsVisible=isRdp; navItems[3].IsVisible=isRdp;
            if(!isRdp && navigation.SelectedIndex==3)Select(0);
            iconPicker.SetProtocol(selected); fullscreen.IsEnabled=rdpMode.SelectedIndex==0;
            title.Text=string.IsNullOrWhiteSpace(name.Text)?L.T("Nowe połączenie"):name.Text.Trim();
            address.Text=isSerial?L.F($"Serial · {serialPort.Text} · {baud.Text} baud"):$"{selected} · {(string.IsNullOrWhiteSpace(host.Text)?L.T("Adres serwera"):host.Text.Trim())}:{port.Text}";
            preview.Profile=original with { Protocol=selected,IconId=iconPicker.SelectedId,CustomIconPng=appearance.CustomIconPng,Color=ProfileAppearance.IsColor(appearance.SelectedColor)?appearance.SelectedColor:"" };
            if(submitted)Validate();
        }
        ServerProfile? Validate()
        {
            errors.Clear();
            void Error(Control field,string message)=>errors.TryAdd(field,message);
            var selected=(Protocol)protocol.SelectedItem!;
            if(string.IsNullOrWhiteSpace(name.Text))Error(name,L.T("Wpisz nazwę serwera."));
            var hostname=selected==Protocol.Serial?original.Host:(host.Text??"").Trim();
            var number=original.Port;
            if(selected!=Protocol.Serial)
            {
                if(hostname.Length==0)Error(host,L.T("Wpisz adres IP lub nazwę hosta."));
                else if(hostname.Any(char.IsWhiteSpace) || hostname.Any(c=>c is '/' or '\\' || char.IsControl(c)) || hostname.StartsWith('-'))Error(host,L.T("Podaj sam host, bez protokołu, ścieżki i spacji."));
                if(!int.TryParse(port.Text,out number) || number is <1 or >65535)Error(port,L.T("Port musi być liczbą od 1 do 65535."));
            }
            var username=(user.Text??"").Trim();
            if(selected==Protocol.SSH && username.Length==0)Error(user,L.T("Wpisz login SSH."));
            if(username.Any(char.IsControl))Error(user,L.T("Login nie może zawierać znaków sterujących."));
            if((domain.Text??"").Any(char.IsControl))Error(domain,L.T("Domena nie może zawierać znaków sterujących."));
            if(selected==Protocol.SSH && auth.SelectedItem is Authentication.PrivateKey && string.IsNullOrWhiteSpace(key.Text))Error(key,L.T("Wybierz plik klucza prywatnego."));
            var path=(folder.Text??"").Trim().Trim('/');
            if(path.Length>0 && path.Split('/').Any(s=>string.IsNullOrWhiteSpace(s) || s is "." or ".."))Error(folder,L.T("Użyj nazw folderów rozdzielonych /, bez pustych segmentów ani .."));
            if(!ProfileAppearance.IsColor(appearance.SelectedColor))Error(colorInput,L.T("Kolor musi mieć postać #RRGGBB lub pozostać pusty."));
            var options=original.Rdp;
            var serialOptions=original.Serial;
            if(selected==Protocol.Serial)
            {
                if(string.IsNullOrWhiteSpace(serialPort.Text) || serialPort.Text.Any(char.IsControl))Error(serialPort,L.T("Wpisz port, np. COM3 albo /dev/ttyUSB0."));
                if(!int.TryParse(baud.Text,out var speed) || speed is <50 or >4000000)Error(baud,L.T("Prędkość musi być liczbą od 50 do 4000000."));
                serialOptions=serialOptions with { PortName=serialPort.Text?.Trim()??"",BaudRate=speed,DataBits=(int)(dataBits.SelectedItem??8),Parity=(SerialParity)parity.SelectedIndex,StopBits=(SerialStopBits)(stopBits.SelectedIndex+1),Handshake=(SerialHandshake)handshake.SelectedIndex,DtrEnable=dtr.IsChecked==true,RtsEnable=rts.IsChecked==true };
            }
            if(selected==Protocol.RDP)
            {
                if(!int.TryParse(width.Text,out var w) || w is <640 or >7680)Error(width,L.T("Szerokość: liczba od 640 do 7680."));
                if(!int.TryParse(height.Text,out var h) || h is <480 or >4320)Error(height,L.T("Wysokość: liczba od 480 do 4320."));
                try { RdpIdentity.Parse(username,domain.Text?.Trim()??""); } catch(ArgumentException e) { Error(user,e.Message); }
                if(redirect.IsChecked==true && string.IsNullOrWhiteSpace(localFolder.Text))Error(localFolder,L.T("Wskaż folder do przekazania."));
                options=options with { Width=w,Height=h,DisplayMode=(RdpDisplayMode)rdpMode.SelectedIndex,KeyboardMode=(RdpKeyboardMode)keyboard.SelectedIndex,Fullscreen=fullscreen.IsChecked==true,Clipboard=clipboard.IsChecked==true,DynamicResolution=dynamic.IsChecked==true,AutoReconnect=reconnect.IsChecked==true,RedirectLocalFolder=redirect.IsChecked==true,LocalFolder=localFolder.Text??"" };
            }
            var result=original with { Name=(name.Text??"").Trim(),Host=hostname,Port=number,Protocol=selected,Username=username,Domain=domain.Text?.Trim()??"",Authentication=(Authentication)auth.SelectedItem!,PrivateKeyPath=(key.Text??"").Trim(),Folder=path,Tags=tags.Text??"",Description=description.Text??"",Favorite=favorite.IsChecked==true,IconId=iconPicker.SelectedId,Color=appearance.SelectedColor,CustomIconPng=appearance.CustomIconPng,Rdp=options,Serial=serialOptions,Vnc=original.Vnc with { Authentication=(VncAuthentication)vncAuth.SelectedIndex,Shared=vncShared.IsChecked==true,ViewOnly=vncViewOnly.IsChecked==true,FitToWindow=vncFit.IsChecked==true } };
            string? general=null;
            if(errors.Count==0) { try { result.Validate(); } catch(ArgumentException e) { general=e.Message; } }
            foreach(var (control,field) in fields) { var invalid=errors.TryGetValue(control,out var message); control.Classes.Set("error",invalid); field.Error.Text=message??""; field.Error.IsVisible=invalid; }
            summary.Text=general??(errors.Count>0?L.F($"Popraw zaznaczone pola ({errors.Count})."):""); summary.IsVisible=summary.Text.Length>0;
            return errors.Count==0 && general==null?result:null;
        }
        void Save()
        {
            submitted=true; var result=Validate();
            if(result!=null) { dialog.Close(result); return; }
            if(errors.FirstOrDefault().Key is {} control)
            {
                Select(fields[control].Page);
                Dispatcher.UIThread.Post(()=> { control.BringIntoView(); control.Focus(); });
            }
        }
        async Task PickKeyAsync()
        {
            try
            {
                var files=await dialog.StorageProvider.OpenFilePickerAsync(new() { Title=L.T("Klucz prywatny SSH"),AllowMultiple=false });
                if(files.FirstOrDefault()?.TryGetLocalPath() is {} path)key.Text=path;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
            { summary.Text=L.T("Nie udało się wybrać pliku. Wpisz ścieżkę do klucza ręcznie."); summary.IsVisible=true; }
        }
        async Task DetectPortsAsync()
        {
            ports.IsEnabled=false;
            try
            {
                var detected=await Task.Run(SerialTerminalSession.PortNames);
                var menu=new ContextMenu { ItemsSource=(detected.Length==0?new[]{new MenuItem { Header=L.T("Nie wykryto portów — wpisz nazwę ręcznie"),IsEnabled=false }}:detected.Select(p=> { var item=new MenuItem { Header=p }; item.Click+=(_,_)=>serialPort.Text=p; return item; }).ToArray()) };
                ports.ContextMenu=menu; menu.Open(ports);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException) { summary.Text=L.T("Nie udało się wykryć portów. Wpisz nazwę ręcznie."); summary.IsVisible=true; }
            finally { ports.IsEnabled=true; }
        }
        navigation.ItemsSource=navItems; navigation.SelectionChanged+=(_,_)=> { for(var i=0;i<pageControls.Count;i++)pageControls[i].IsVisible=i==navigation.SelectedIndex; }; navigation.SelectedIndex=0;
        foreach(var box in new[]{name,host,port,user,domain,key,folder,tags,description,width,height,localFolder,colorInput,serialPort,baud})box.TextChanged+=(_,_)=>Refresh();
        auth.SelectionChanged+=(_,_)=>Refresh(); handshake.SelectionChanged+=(_,_)=>Refresh(); protocol.SelectionChanged+=(_,_)=> { if(port.Text is "22" or "3389" or "5900")port.Text=QuickConnect.DefaultPort((Protocol)protocol.SelectedItem!).ToString(); Refresh(); };
        iconPicker.Changed+=Refresh; appearance.Changed+=Refresh; rdpMode.SelectionChanged+=(_,_)=>Refresh();
        redirect.IsCheckedChanged+=(_,_)=> { localFolder.IsEnabled=!OperatingSystem.IsWindows() && redirect.IsChecked==true; if(OperatingSystem.IsWindows() && redirect.IsChecked!=true)redirect.IsEnabled=false; Refresh(); };
        dialog.Opened+=(_,_)=> { Refresh(); name.Focus(); };
        Refresh();
        return await dialog.ShowDialog<ServerProfile?>(owner);
    }
}
