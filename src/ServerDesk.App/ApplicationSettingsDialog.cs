using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task EditApplicationSettings(Window owner,ProfileRepository repository,Func<Task> changed)
    {
        var original=await repository.ApplicationSettingsAsync(); var originalTheme=DesignSystem.CurrentId;
        var main=owner as MainWindow; var runtime=main?.CurrentPreferences??DesignSystem.Preferences;
        var p=original.Preferences with { SidebarWidth=runtime.SidebarWidth }; var saved=false; var saving=false; var resetting=false; string? restartAfterSave=null;
        Action preview=()=>{};
        var searchIndex=new SettingsSearchIndex();
        var flags=new Dictionary<string,CheckBox>();
        var flagDefaults=new Dictionary<string,bool>();
        var error=DesignSystem.Label("",true,12); DesignSystem.Resource(error,TextBlock.ForegroundProperty,"Danger"); error.TextWrapping=TextWrapping.Wrap; error.IsVisible=false;
        error.PropertyChanged+=(_,e)=> { if(e.Property==TextBlock.TextProperty)error.IsVisible=!string.IsNullOrEmpty(error.Text); };
        StackPanel Page(string icon,string title,string detail)
        {
            var page=new StackPanel { Spacing=16,Margin=new Thickness(20) };
            page.Children.Add(DesignSystem.PageHeading(icon,L.T(title),L.T(detail))); searchIndex.Page(page,title+" "+L.T(title)); return page;
        }
        StackPanel Section(StackPanel page,string glyph,string title,string? badge=null)
        {
            var fields=new StackPanel { Spacing=12 };
            Control heading=DesignSystem.SectionHeading(glyph,L.T(title));
            if(badge!=null)
            {
                var tag=DesignSystem.Badge(L.T(badge)); ToolTip.SetTip(tag,L.T("Plugin wbudowany · dostarczany z ServerDesk"));
                var row=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=12,Children={heading,tag} }; Grid.SetColumn(tag,1); heading=row;
            }
            var rule=new Border { Height=1 }; DesignSystem.Resource(rule,Border.BackgroundProperty,"BorderDefault");
            var content=new StackPanel { Spacing=14,Children={heading,rule,fields} };
            var card=DesignSystem.Card(content); card.Classes.Add("settings-card");
            page.Children.Add(card); searchIndex.Section(page,card,fields,title+" "+L.T(title)); return fields;
        }
        void Label(StackPanel panel,string label,Control control)
        {
            var originalLabel=label; label=L.T(label);
            var caption=DesignSystem.Label(label,true); caption.TextWrapping=TextWrapping.Wrap;
            var row=new Grid { Classes={"setting-field"},ColumnDefinitions=new ColumnDefinitions("*,240"),RowDefinitions=new RowDefinitions("Auto,Auto"),ColumnSpacing=20,RowSpacing=6,Children={caption,control} };
            Grid.SetColumn(control,1);
            row.SizeChanged+=(_,_)=>
            {
                var narrow=row.Bounds.Width<560; Grid.SetColumn(control,narrow?0:1); Grid.SetRow(control,narrow?1:0);
                row.ColumnDefinitions[1].Width=narrow?new GridLength(0):new GridLength(240); row.ColumnSpacing=narrow?0:20; row.RowSpacing=narrow?6:0;
            };
            panel.Children.Add(row);
            searchIndex.Field(panel,row,originalLabel+" "+label);
            AutomationProperties.SetName(control,label);
        }
        NumericUpDown Number(StackPanel panel,string label,int value,int min,int max)
        {
            var input=new NumericUpDown { Minimum=min,Maximum=max,Value=value,FormatString="0",HorizontalAlignment=HorizontalAlignment.Stretch };
            Label(panel,label,input); input.ValueChanged+=(_,_)=>preview(); return input;
        }
        CheckBox Flag(StackPanel panel,string key,string label,bool value,bool defaultValue=true)
        {
            var originalLabel=label; label=L.T(label);
            var input=new CheckBox { Content=new TextBlock { Text=label,TextWrapping=TextWrapping.Wrap },IsChecked=value,HorizontalAlignment=HorizontalAlignment.Stretch }; AutomationProperties.SetName(input,label);
            flags[key]=input; flagDefaults[key]=defaultValue; panel.Children.Add(input); searchIndex.Field(panel,input,originalLabel+" "+label); input.IsCheckedChanged+=(_,_)=>preview(); return input;
        }
        var appearancePage=Page("settings","Wygląd","Motyw, język i czytelność interfejsu.");
        var terminalPage=Page("ssh","Terminal","Kolory, czcionka, kursor i historia sesji SSH oraz Serial.");
        var sftpPage=Page("folder","SFTP","Domyślny widok plików i lokalny katalog startowy.");
        var workspacePage=Page("layers","Obszar pracy","Panele, karty i zachowanie przy uruchamianiu.");
        var monitoringPage=Page("chart-no-axes-combined","Monitoring","Parametry serwera i informacje na pasku statusu.");
        var securityPage=Page("key","Bezpieczeństwo","Tożsamość serwerów i zapisane zaufanie SSH.");
        var pluginsPage=Page("plugins","Pluginy","Włączaj dodatki i dostosuj zakres działania aplikacji.");
        foreach(var plugin in PluginCatalog.BuiltIn)
        {
            var section=Section(pluginsPage,plugin.Icon,plugin.Name,"Wbudowany");
            var description=DesignSystem.Hint(L.T(plugin.Description)); section.Children.Add(description);
            searchIndex.Field(section,description,plugin.Description+" "+L.T(plugin.Description));
            var toggle=Flag(section,"plugin:"+plugin.Id,"Włącz plugin",PluginCatalog.IsEnabled(p,plugin.Id));
            toggle.Name="PluginEnabled_"+plugin.Id.Replace('-','_'); AutomationProperties.SetName(toggle,L.T(plugin.Name));
        }
        pluginsPage.Children.Add(DesignSystem.Hint(L.T("Zmiany pluginów działają po zapisaniu, bez restartu. Wyłączenie zatrzymuje ich pracę i ukrywa akcje; ustawienia oraz własne komendy pozostają zachowane.")));
        var trustSection=Section(securityPage,"key","Weryfikacja serwerów SSH");
        Window? settingsOwner=null;
        var manageTrust=Button("Zarządzaj kluczami…",async()=> { if(settingsOwner!=null)await TrustedHosts(settingsOwner,repository); }); manageTrust.Name="ManageTrustedHosts";
        Label(trustSection,"Zaufane klucze SSH",manageTrust);
        trustSection.Children.Add(DesignSystem.Hint(L.T("Usunięcie zaufania wymaga osobnego potwierdzenia i działa od razu, niezależnie od zapisu ustawień. Następne połączenie wymaga ponownego porównania odcisku.")));
        var backupSection=Section(securityPage,"database","Kopie bazy danych");
        var backupContent=new StackPanel { Spacing=10 };
        backupContent.Children.Add(DesignSystem.Hint(L.T("Kopia obejmuje profile, foldery, ustawienia, historię i zaufane klucze SSH. Nie obejmuje haseł, prywatnych kluczy ani zaszyfrowanej treści komend i kolekcji z magazynu systemowego. Zawiera poufne dane infrastruktury — przechowuj ją prywatnie.")));
        var backupStatus=DesignSystem.Hint(L.T("Przed zmianą schematu istniejącej bazy ServerDesk automatycznie tworzy sprawdzoną kopię. Ręczna kopia działa od razu, niezależnie od zapisu ustawień."));
        backupStatus.Name="DatabaseBackupStatus";
        var backupBrowser=new DatabaseBackupBrowser(repository.DatabaseBackupDirectory);
        Button? createBackup=null;
        createBackup=Button("Utwórz kopię bazy",async()=>
        {
            if(createBackup==null || !createBackup.IsEnabled)return;
            createBackup.IsEnabled=false; backupStatus.Text=L.T("Tworzenie i sprawdzanie kopii bazy…");
            try
            {
                var backup=await repository.CreateDatabaseBackupAsync();
                backupStatus.Text=L.T("Kopia została utworzona i sprawdzona.");
                await backupBrowser.RefreshAsync(backup.Path);
            }
            catch(Exception ex)
            {
                Serilog.Log.Error("Database backup failed: {Type}",ex.GetType().Name);
                backupStatus.Text=L.T("Nie udało się utworzyć kopii. Sprawdź wolne miejsce i uprawnienia katalogu danych. Istniejące kopie pozostają zachowane.");
            }
            finally { createBackup.IsEnabled=true; }
        });
        createBackup.Name="CreateDatabaseBackup"; createBackup.HorizontalAlignment=HorizontalAlignment.Left;
        backupContent.Children.Add(createBackup); backupContent.Children.Add(backupStatus); backupContent.Children.Add(backupBrowser);
        backupSection.Children.Add(backupContent);
        // Keep the result and privacy notice visible when the action is found by search.
        searchIndex.Field(backupSection,backupContent,"Kopia lokalnej bazy "+L.T("Kopia lokalnej bazy"));
        var logSection=Section(securityPage,"file","Logi i diagnostyka");
        var logContent=new StackPanel { Spacing=10 };
        logContent.Children.Add(DesignSystem.Hint(L.T("Przeczytaj logi aplikacji, wyszukaj komunikat błędu lub zapisz kopię do zgłoszenia problemu. Działanie nie wymaga zapisywania ustawień.")));
        var showLogs=Button("Otwórz logi",async()=> { if(settingsOwner!=null)await ApplicationLogs(settingsOwner,repository.LogDirectory); }); showLogs.Name="OpenApplicationLogs"; showLogs.HorizontalAlignment=HorizontalAlignment.Left;
        logContent.Children.Add(showLogs); logSection.Children.Add(logContent);
        searchIndex.Field(logSection,logContent,"Logi logs diagnostyka diagnostics pobierz download "+L.T("Logi i diagnostyka"));
        var languageSection=Section(appearancePage,"globe","Język interfejsu");
        var language=new ComboBox { Name="ApplicationLanguage",ItemsSource=new[]{"Polski","English"},SelectedIndex=p.Language=="en"?1:0,HorizontalAlignment=HorizontalAlignment.Stretch };
        Label(languageSection,"Język aplikacji / Language",language);
        var languageNote=DesignSystem.Label(L.T("Zmiana języka zostanie zastosowana po ponownym uruchomieniu ServerDesk."),true,12);
        languageNote.TextWrapping=TextWrapping.Wrap; languageNote.Name="LanguageRestartNotice"; languageSection.Children.Add(languageNote);
        var appearance=Section(appearancePage,"settings","Motyw i interfejs");
        var theme=new ComboBox { Name="ApplicationTheme",ItemsSource=DesignSystem.Themes.Select(t=>t.Name).Append(L.T("Systemowy")).ToArray(),SelectedIndex=originalTheme=="system"?DesignSystem.Themes.Length:Math.Max(0,Array.FindIndex(DesignSystem.Themes,t=>t.Id==originalTheme)),HorizontalAlignment=HorizontalAlignment.Stretch };
        string SelectedTheme()=>theme.SelectedIndex==DesignSystem.Themes.Length?"system":DesignSystem.Themes[Math.Clamp(theme.SelectedIndex,0,DesignSystem.Themes.Length-1)].Id;
        theme.ItemTemplate=new FuncDataTemplate<string>((name,_)=>
        {
            var option=DesignSystem.Themes.FirstOrDefault(t=>t.Name==name); var row=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
            if(option!=null)foreach(var color in new[]{option.Background,option.Sidebar,option.Accent})row.Children.Add(new Border { Width=12,Height=16,CornerRadius=new CornerRadius(3),Background=new SolidColorBrush(Color.Parse(color)) });
            row.Children.Add(DesignSystem.Label(name??"")); return row;
        });
        Label(appearance,"Motyw aplikacji",theme);
        var uiFont=Number(appearance,"Rozmiar tekstu interfejsu",p.InterfaceFontSize,11,16);
        var interfaceFamily=new ComboBox { Name="InterfaceFontFamily",ItemsSource=ApplicationPreferences.InterfaceFonts,SelectedItem=p.InterfaceFontFamily,HorizontalAlignment=HorizontalAlignment.Stretch,ItemTemplate=new FuncDataTemplate<string>((name,_)=>DesignSystem.Label(L.T(name??""))) }; Label(appearance,"Krój czcionki interfejsu",interfaceFamily);
        appearance.Children.Add(DesignSystem.Hint(L.T("Brak wybranej czcionki w systemie zastępuje domyślny krój. Czcionka terminala jest ustawiana osobno.")));
        var density=new ComboBox { ItemsSource=new[]{L.T("Kompaktowy"),L.T("Standardowy"),L.T("Przestronny")},SelectedIndex=(int)p.Density,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(appearance,"Odstępy i wysokość kontrolek",density);
        var cornerRadius=Number(appearance,"Zaokrąglenie kontrolek (px)",p.ControlCornerRadius,0,12); cornerRadius.Name="ControlCornerRadius";
        var menuPadding=Number(appearance,"Odstęp pionowy w menu (px)",p.MenuItemPadding,2,12); menuPadding.Name="MenuItemPadding";
        var accent=new TextBox { Text=p.AccentColor,PlaceholderText=L.T("Domyślny lub #RRGGBB"),HorizontalAlignment=HorizontalAlignment.Stretch }; Label(appearance,"Kolor akcentu",accent);
        var colors=new StackPanel { Name="AccentColorChoices",Orientation=Orientation.Horizontal,Spacing=8 };
        // Presets belong to the accent field, rather than the entire section.
        colors.Bind(Visual.IsVisibleProperty,new Avalonia.Data.Binding(nameof(Visual.IsVisible)) { Source=accent.Parent });
        foreach(var (name,color) in new[]{(L.T("Motyw"),""),(L.T("Niebieski"),"#58A6FF"),(L.T("Miętowy"),"#69CFB6"),(L.T("Fioletowy"),"#B39DFF"),(L.T("Pomarańczowy"),"#E5B567")})
        {
            var choice=Button(name,()=>accent.Text=color); if(color.Length>0)choice.Content=new Border { Width=20,Height=16,Background=new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color)),CornerRadius=new CornerRadius(3) };
            AutomationProperties.SetName(choice,name+L.T(" akcent")); ToolTip.SetTip(choice,name); colors.Children.Add(choice);
        }
        appearance.Children.Add(colors); appearance.Children.Add(DesignSystem.Hint(L.T("Motyw, akcent i rozmiar interfejsu mają podgląd na żywo. Anuluj przywraca poprzedni wygląd.")));
        var terminal=Section(terminalPage,"ssh","Wygląd terminala");
        var palette=new ComboBox { ItemsSource=TerminalAppearance.Names,SelectedItem=TerminalAppearance.Names.Contains(original.TerminalPalette)?original.TerminalPalette:TerminalAppearance.Names[0],HorizontalAlignment=HorizontalAlignment.Stretch }; Label(terminal,"Paleta terminala",palette);
        var terminalFont=Number(terminal,"Rozmiar czcionki terminala",original.TerminalFont,9,32);
        var family=new ComboBox { ItemsSource=ApplicationPreferences.TerminalFonts,SelectedItem=p.TerminalFontFamily,HorizontalAlignment=HorizontalAlignment.Stretch,ItemTemplate=new FuncDataTemplate<string>((name,_)=>DesignSystem.Label(L.T(name??""))) }; Label(terminal,"Krój czcionki terminala",family);
        terminal.Children.Add(DesignSystem.Hint(L.T("Paleta terminala jest niezależna od motywu aplikacji. Jeśli wybranej czcionki nie ma w systemie, terminal użyje czcionki monospace.")));
        Flag(terminal,"sessionStatus","Pokaż status pod panelem sesji",p.ShowSessionStatus);
        terminal=Section(terminalPage,"square-terminal","Kursor i historia");
        var cursor=new ComboBox { ItemsSource=new[]{L.T("Kreska"),L.T("Blok"),L.T("Podkreślenie")},SelectedIndex=(int)p.TerminalCursor,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(terminal,"Kształt kursora",cursor); cursor.Name="TerminalCursor";
        Flag(terminal,"blink","Migający kursor",p.TerminalCursorBlink);
        var scrollback=Number(terminal,"Wiersze historii terminala",p.TerminalScrollbackLines,0,100000); scrollback.Name="TerminalScrollbackLines";
        terminal.Children.Add(DesignSystem.Hint(L.T("Kursor ma podgląd na żywo. Programy zdalne mogą zmieniać jego wygląd. Limit historii obowiązuje przy następnym połączeniu SSH lub Serial; 0 wyłącza historię. Bieżący bufor pozostaje zachowany.")));
        var fileSettings=Section(sftpPage,"folder","Otwieranie przeglądarki plików");
        var localDirectory=new TextBox { Name="SftpLocalDirectory",Text=p.SftpLocalDirectory,MaxLength=4096,PlaceholderText=L.T("Puste: katalog domowy"),HorizontalAlignment=HorizontalAlignment.Stretch };
        var browse=DeskIcons.Button("folder","Wybierz katalog startowy SFTP",async()=>
        {
            if(settingsOwner==null)return;
            try
            {
                var selected=await settingsOwner.StorageProvider.OpenFolderPickerAsync(new() { Title=L.T("Wybierz katalog startowy SFTP"),AllowMultiple=false });
                if(selected.FirstOrDefault()?.TryGetLocalPath() is {} path)localDirectory.Text=path;
            }
            catch(Exception ex) { Serilog.Log.Warning("Wybór katalogu ustawień typu {Type}",ex.GetType().Name); error.Text=L.T("Nie udało się otworzyć wyboru katalogu. Wpisz pełną ścieżkę ręcznie."); }
        },28);
        browse.Name="BrowseSftpLocalDirectory"; localDirectory.InnerRightContent=browse;
        Label(fileSettings,"Lokalny katalog startowy",localDirectory);
        var startSide=new ComboBox { Name="SftpStartSide",ItemsSource=new[]{L.T("Lokalny"),L.T("Zdalny")},SelectedIndex=p.SftpStartRemote?1:0,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(fileSettings,"Początkowo aktywny panel",startSide);
        Flag(fileSettings,"hidden","Pokaż ukryte pliki po otwarciu SFTP",p.SftpShowHidden,false);
        fileSettings.Children.Add(DesignSystem.Hint(L.T("Te opcje są stosowane przy pierwszym otwarciu SFTP w karcie. Otwarta przeglądarka zachowuje bieżący katalog, filtry i sortowanie. Niedostępny katalog startowy zastąpi katalog domowy z powiadomieniem.")));
        fileSettings=Section(sftpPage,"transfers","Domyślne sortowanie plików");
        ComboBox Sort(string label,FileSort value)
        {
            var input=new ComboBox { ItemsSource=new[]{L.T("Nazwa"),L.T("Rozmiar"),L.T("Zmodyfikowano"),L.T("Typ")},SelectedIndex=(int)value,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(fileSettings,label,input); return input;
        }
        var localFileSort=Sort("Sortowanie lokalnego panelu",p.SftpLocalSort); localFileSort.Name="SftpLocalSort";
        Flag(fileSettings,"localDescending","Sortuj lokalne pliki malejąco",p.SftpLocalDescending,false);
        var remoteFileSort=Sort("Sortowanie zdalnego panelu",p.SftpRemoteSort); remoteFileSort.Name="SftpRemoteSort";
        Flag(fileSettings,"remoteDescending","Sortuj zdalne pliki malejąco",p.SftpRemoteDescending,false);
        var editing=Section(sftpPage,"edit","Edycja plików");
        var editor=new ComboBox { Name="SftpTextEditor",ItemsSource=new[]{L.T("Automatycznie (edytor tekstu)"),"Visual Studio Code","Notepad++","Kate","GNOME Text Editor",L.T("Własny program")},SelectedIndex=(int)p.SftpTextEditor,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(editing,"Domyślny edytor tekstu",editor);
        var editorPath=new TextBox { Name="SftpTextEditorPath",Text=p.SftpTextEditorPath,MaxLength=4096,PlaceholderText=L.T("Pełna ścieżka do programu, bez argumentów"),HorizontalAlignment=HorizontalAlignment.Stretch,IsEnabled=p.SftpTextEditor==TextEditorKind.Custom };
        editorPath.InnerRightContent=DeskIcons.Button("folder","Wybierz program edytora",async()=>
        {
            if(settingsOwner==null)return;
            try
            {
                var selected=await settingsOwner.StorageProvider.OpenFilePickerAsync(new() { Title=L.T("Wybierz program edytora"),AllowMultiple=false });
                if(selected.FirstOrDefault()?.TryGetLocalPath() is {} path)editorPath.Text=path;
            }
            catch(Exception ex) { Serilog.Log.Warning("Wybór edytora typu {Type}",ex.GetType().Name); error.Text=L.T("Nie udało się otworzyć wyboru pliku. Wpisz pełną ścieżkę ręcznie."); }
        },28);
        Label(editing,"Ścieżka własnego edytora",editorPath);
        editing.Children.Add(DesignSystem.Hint(L.T("Automatycznie: Notatnik w Windows lub pierwszy dostępny Kate, GNOME Text Editor, gedit, Mousepad, Xed lub KWrite w Linux. Pozostałe programy muszą być zainstalowane osobno. Wybór edytora obowiązuje po zapisaniu ustawień.")));
        editing.Children.Add(DesignSystem.Hint(L.T("SFTP → Pobierz i edytuj zapisuje kopię w otwartym katalogu lokalnym. Zapisz zmiany w edytorze, a potem użyj Wyślij, aby przesłać je na serwer. Zmiany nie są wysyłane automatycznie.")));
        var workspace=Section(workspacePage,"server","Lista połączeń");
        var sidebarWidth=Number(workspace,"Szerokość panelu połączeń",p.SidebarWidth,220,500);
        var sidebarStart=new ComboBox { ItemsSource=new[]{L.T("Zapamiętaj ostatni stan"),L.T("Zawsze pokaż"),L.T("Zawsze ukryj")},SelectedIndex=(int)p.SidebarOnStartup,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(workspace,"Panel połączeń przy uruchomieniu",sidebarStart);
        var connectionPadding=Number(workspace,"Odstęp pionowy w liście połączeń (px)",p.ConnectionRowPadding,0,10); connectionPadding.Name="ConnectionRowPadding";
        Flag(workspace,"protocols","Pokaż oznaczenia protokołów przy serwerach",p.ShowProtocolBadges);
        Flag(workspace,"addresses","Pokaż adresy pod nazwami serwerów",p.ShowConnectionAddresses,false);
        workspace=Section(workspacePage,"layers","Panele i karty");
        var tabWidth=Number(workspace,"Maksymalna szerokość nazwy karty",p.TabLabelWidth,140,360);
        var tabHeight=Number(workspace,"Wysokość paska kart",p.TabStripHeight,30,48); tabHeight.Name="TabStripHeight";
        var separatorSize=Number(workspace,"Szerokość separatorów paneli",p.PaneSeparatorSize,4,12); separatorSize.Name="PaneSeparatorSize";
        Flag(workspace,"panelNumbers","Pokaż numery paneli w podzielonym widoku",p.ShowPanelNumbers);
        Flag(workspace,"tabIcons","Pokaż ikony na kartach",p.ShowTabIcons);
        Flag(workspace,"tabClose","Pokaż przyciski zamykania kart",p.ShowTabCloseButtons);
        workspace.Children.Add(DesignSystem.Hint(L.T("Gdy przyciski zamykania są ukryte, zamkniesz kartę przez jej menu lub Ctrl+Shift+W. Wskaźniki stanu sesji pozostają widoczne.")));
        var tabTarget=new ComboBox { Name="NewTabTarget",ItemsSource=new[]{L.T("Aktywny panel"),L.T("Zawsze panel 1"),L.T("Panel z najmniejszą liczbą kart")},SelectedIndex=(int)p.NewTabTarget,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(workspace,"Otwieraj nowe karty w",tabTarget);
        workspace.Children.Add(DesignSystem.Hint(L.T("Rozmiary pasków i separatorów mają podgląd na żywo. Anuluj przywraca poprzedni wygląd. Nowe karty używają wybranej reguły bez przenoszenia otwartych sesji.")));
        workspace=Section(workspacePage,"history","Przy uruchomieniu");
        var startupLayout=new ComboBox { Name="WorkspaceOnStartup",ItemsSource=new[]{L.T("Zapamiętaj ostatni układ"),L.T("Jeden panel"),L.T("Panele obok siebie"),L.T("Panele jeden nad drugim"),L.T("Trzy panele (1 + 2)"),L.T("Cztery panele (2 × 2)")},SelectedIndex=(int)p.WorkspaceOnStartup,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(workspace,"Układ przy uruchomieniu",startupLayout);
        workspace.Children.Add(DesignSystem.Hint(L.T("Układ startowy obowiązuje przy kolejnym uruchomieniu. Przywrócone karty zostaną rozmieszczone w wybranym układzie bez automatycznego łączenia. Bieżący podział zmienisz w menu Układ.")));
        Flag(workspace,"remember","Przywracaj karty i układ po restarcie",original.RememberWorkspace);
        workspace.Children.Add(DesignSystem.Hint(L.T("Przywrócone karty czekają na Połącz. Hasła i zawartość terminala nie są zapisywane w układzie.")));
        var monitoring=Section(monitoringPage,"chart-no-axes-combined","Pasek statusu i monitoring");
        Flag(monitoring,"status","Pokaż dolny pasek statusu",p.ShowStatusBar);
        Flag(monitoring,"metrics","Pokaż parametry aktywnego serwera",p.ShowMetrics);
        Flag(monitoring,"transfers","Pokaż liczbę aktywnych transferów",p.ShowTransferCount);
        var interval=Number(monitoring,"Odświeżanie monitoringu (sekundy)",original.MonitorInterval,2,300);
        monitoring=Section(monitoringPage,"memory-stick","Pola paska parametrów");
        Flag(monitoring,"cpu","CPU",p.MetricCpu); Flag(monitoring,"ram","Pamięć RAM",p.MetricMemory); Flag(monitoring,"network","Odbieranie i wysyłanie",p.MetricNetwork);
        Flag(monitoring,"uptime","Czas pracy serwera",p.MetricUptime); Flag(monitoring,"disk","Zajętość dysku /",p.MetricDisk); Flag(monitoring,"system","Nazwa systemu operacyjnego",p.MetricSystem); Flag(monitoring,"clock","Zegar serwera",p.MetricClock);
        monitoring.Children.Add(DesignSystem.Hint(L.T("Pola, które nie mieszczą się w oknie, są automatycznie ukrywane. Pełne wartości są w podpowiedziach. Gdy pasek parametrów jest wyłączony, monitoring w tle czeka na otwarcie panelu Monitoring.")));
        bool On(string key)=>flags[key].IsChecked==true;
        ApplicationPreferences Draft()=>new()
        {
            DisabledPlugins=new(p.DisabledPlugins.Where(id=>!PluginCatalog.IsKnown(id)).Concat(PluginCatalog.BuiltIn.Where(plugin=>!On("plugin:"+plugin.Id)).Select(plugin=>plugin.Id))),
            Language=language.SelectedIndex==1?"en":"pl",
            InterfaceFontSize=(int)(uiFont.Value??13),Density=(InterfaceDensity)Math.Max(0,density.SelectedIndex),AccentColor=accent.Text?.Trim()??"",TerminalFontFamily=family.SelectedItem as string??"Domyślna",
            InterfaceFontFamily=interfaceFamily.SelectedItem as string??"Domyślna",ControlCornerRadius=(int)(cornerRadius.Value??6),MenuItemPadding=(int)(menuPadding.Value??7),ConnectionRowPadding=(int)(connectionPadding.Value??1),
            ShowConnectionAddresses=On("addresses"),ShowTabIcons=On("tabIcons"),ShowTabCloseButtons=On("tabClose"),
            TerminalScrollbackLines=(int)(scrollback.Value??10000),TerminalCursor=(TerminalCursorShape)Math.Max(0,cursor.SelectedIndex),TerminalCursorBlink=On("blink"),
            SftpLocalDirectory=localDirectory.Text??"",SftpShowHidden=On("hidden"),SftpLocalSort=(FileSort)Math.Max(0,localFileSort.SelectedIndex),SftpRemoteSort=(FileSort)Math.Max(0,remoteFileSort.SelectedIndex),SftpLocalDescending=On("localDescending"),SftpRemoteDescending=On("remoteDescending"),SftpStartRemote=startSide.SelectedIndex==1,
            SftpTextEditor=(TextEditorKind)Math.Max(0,editor.SelectedIndex),SftpTextEditorPath=editorPath.Text??"",
            SidebarWidth=(int)(sidebarWidth.Value??272),SidebarOnStartup=(SidebarStartup)Math.Max(0,sidebarStart.SelectedIndex),TabLabelWidth=(int)(tabWidth.Value??220),ShowProtocolBadges=On("protocols"),ShowSessionStatus=On("sessionStatus"),
            TabStripHeight=(int)(tabHeight.Value??35),PaneSeparatorSize=(int)(separatorSize.Value??4),ShowPanelNumbers=On("panelNumbers"),NewTabTarget=(NewTabPlacement)Math.Max(0,tabTarget.SelectedIndex),WorkspaceOnStartup=(WorkspaceStartup)Math.Max(0,startupLayout.SelectedIndex),
            ShowStatusBar=On("status"),ShowMetrics=On("metrics"),ShowTransferCount=On("transfers"),MetricCpu=On("cpu"),MetricMemory=On("ram"),MetricNetwork=On("network"),MetricUptime=On("uptime"),MetricDisk=On("disk"),MetricSystem=On("system"),MetricClock=On("clock")
        };
        preview=()=>
        {
            if(resetting)return;
            try { var draft=Draft(); draft.Validate(); if(main!=null)main.PreviewPreferences(draft); else DesignSystem.ApplyPreferences(draft); DesignSystem.Apply(SelectedTheme()); error.Text=""; accent.Classes.Remove("error"); localDirectory.Classes.Remove("error"); editorPath.Classes.Remove("error"); }
            catch(ArgumentException ex) { ShowValidation(ex); }
        };
        void ShowValidation(ArgumentException ex)
        {
            error.Text=ex.Message; accent.Classes.Set("error",ex.ParamName==nameof(ApplicationPreferences.AccentColor)); localDirectory.Classes.Set("error",ex.ParamName==nameof(ApplicationPreferences.SftpLocalDirectory));
            editorPath.Classes.Set("error",ex.ParamName==nameof(ApplicationPreferences.SftpTextEditorPath));
        }
        theme.SelectionChanged+=(_,_)=>preview(); density.SelectionChanged+=(_,_)=>preview(); family.SelectionChanged+=(_,_)=>preview(); sidebarStart.SelectionChanged+=(_,_)=>preview(); accent.TextChanged+=(_,_)=>preview();
        interfaceFamily.SelectionChanged+=(_,_)=>preview();
        cursor.SelectionChanged+=(_,_)=>preview(); localDirectory.TextChanged+=(_,_)=>preview(); localFileSort.SelectionChanged+=(_,_)=>preview(); remoteFileSort.SelectionChanged+=(_,_)=>preview(); startSide.SelectionChanged+=(_,_)=>preview();
        editor.SelectionChanged+=(_,_)=> { editorPath.IsEnabled=editor.SelectedIndex==(int)TextEditorKind.Custom; preview(); }; editorPath.TextChanged+=(_,_)=>preview();
        tabTarget.SelectionChanged+=(_,_)=>preview(); startupLayout.SelectionChanged+=(_,_)=>preview();
        TabItem Category(string glyph,string title,StackPanel page)
        {
            var scroll=new ScrollViewer { Content=page,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            DesignSystem.Resource(scroll,ScrollViewer.BackgroundProperty,"BackgroundPrimary");
            return new TabItem { Classes={"page-navigation","settings-category"},Header=DeskIcons.Labelled(glyph,L.T(title)),Content=scroll };
        }
        var categories=new TabControl { Name="SettingsCategories",TabStripPlacement=Dock.Left,Padding=new Thickness(0),ItemsSource=new[] {
            Category("settings","Wygląd",appearancePage),Category("ssh","Terminal",terminalPage),Category("folder","SFTP",sftpPage),Category("layers","Obszar pracy",workspacePage),Category("chart-no-axes-combined","Monitoring",monitoringPage),Category("key","Bezpieczeństwo",securityPage),Category("plugins","Pluginy",pluginsPage)
        },SelectedIndex=0 };
        SettingsNavigation.Configure(categories);
        DesignSystem.Resource(categories,TabControl.BackgroundProperty,"BackgroundSecondary");
        AutomationProperties.SetName(categories,L.T("Kategorie ustawień"));
        var actions=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),ColumnSpacing=8 };
        var pendingText=DesignSystem.Label("",true,12); pendingText.TextWrapping=TextWrapping.Wrap;
        var pendingHeading=DesignSystem.Label(L.T("Zmiana języka wymaga restartu")); pendingHeading.FontWeight=FontWeight.SemiBold; pendingHeading.TextWrapping=TextWrapping.Wrap;
        var pendingRow=new Grid { ColumnDefinitions=new ColumnDefinitions("24,*"),ColumnSpacing=10 };
        var saveRestart=Button("Zapisz i uruchom ponownie",()=>{}); saveRestart.Name="SettingsSaveRestart"; saveRestart.HorizontalAlignment=HorizontalAlignment.Left; saveRestart.IsVisible=main!=null;
        pendingRow.Children.Add(DeskIcons.Create("info",20,"AccentPrimary")); var pendingLabels=new StackPanel { Spacing=6,Children={pendingHeading,pendingText,saveRestart} }; Grid.SetColumn(pendingLabels,1); pendingRow.Children.Add(pendingLabels);
        var pending=new Border { Name="SettingsRestartReminder",IsVisible=false,Child=pendingRow,Padding=new Thickness(12,10),CornerRadius=new CornerRadius(6) };
        DesignSystem.Resource(pending,Border.BackgroundProperty,"SurfaceSelected");
        var footer=new StackPanel { Spacing=10,Children={pending,error,actions} };
        var footerSurface=new Border { Child=footer,Padding=new Thickness(18,12),BorderThickness=new Thickness(0,1,0,0) };
        DesignSystem.Resource(footerSurface,Border.BackgroundProperty,"BackgroundSecondary"); DesignSystem.Resource(footerSurface,Border.BorderBrushProperty,"BorderDefault");
        var search=new TextBox { Name="SettingsSearch",PlaceholderText=L.T("Znajdź ustawienie: motyw, terminal, CPU…"),MaxLength=200 };
        AutomationProperties.SetName(search,L.T("Szukaj ustawień")); search.InnerLeftContent=new Border { Child=DeskIcons.Create("search",15,"ForegroundSecondary"),Margin=new Thickness(8,0,0,0) };
        void ClearSettingsSearch() { search.Text=""; search.Focus(); }
        var clearSearch=DeskIcons.Button("close","Wyczyść wyszukiwanie ustawień",ClearSettingsSearch,26); clearSearch.IsVisible=false; search.InnerRightContent=clearSearch;
        var searchCount=DesignSystem.Label("",true,12); searchCount.Name="SettingsSearchCount"; searchCount.IsVisible=false;
        var headerTitle=DesignSystem.PageHeading("settings",L.T("Ustawienia"),L.T("Dostosuj wygląd i sposób pracy ServerDesk."));
        var searchArea=new StackPanel { Spacing=6,VerticalAlignment=VerticalAlignment.Center,Children={search,searchCount} };
        var headerBody=new Grid { ColumnDefinitions=new("*,330"),RowDefinitions=new("Auto,Auto"),ColumnSpacing=24,Children={headerTitle,searchArea} }; Grid.SetColumn(searchArea,1);
        headerBody.SizeChanged+=(_,_)=>
        {
            var stacked=headerBody.Bounds.Width<760 || DesignSystem.Preferences.InterfaceFontSize>14;
            Grid.SetColumn(searchArea,stacked?0:1); Grid.SetRow(searchArea,stacked?1:0);
            headerBody.ColumnDefinitions[1].Width=new GridLength(stacked?0:330); headerBody.ColumnSpacing=stacked?0:24; headerBody.RowSpacing=stacked?12:0;
        };
        var header=new Border { Padding=new Thickness(22,16),Child=headerBody,BorderThickness=new Thickness(0,0,0,1) };
        DesignSystem.Resource(header,Border.BorderBrushProperty,"BorderDefault");
        var emptySearch=DesignSystem.Hint(L.T("Brak pasujących ustawień. Zmień lub wyczyść wyszukiwanie.")); emptySearch.Name="SettingsSearchEmpty"; emptySearch.TextAlignment=TextAlignment.Center; emptySearch.IsVisible=false;
        var emptyHeading=DesignSystem.Label(L.T("Brak wyników"),false,18); emptyHeading.FontWeight=FontWeight.SemiBold; emptyHeading.TextWrapping=TextWrapping.Wrap; emptyHeading.TextAlignment=TextAlignment.Center;
        var showAllSettings=Button("Pokaż wszystkie ustawienia",ClearSettingsSearch); showAllSettings.Name="SettingsShowAll"; showAllSettings.HorizontalAlignment=HorizontalAlignment.Center; showAllSettings.Content=DeskIcons.Labelled("search",L.T("Pokaż wszystkie ustawienia"));
        var emptyIcon=new Border { Width=44,Height=44,CornerRadius=new CornerRadius(12),HorizontalAlignment=HorizontalAlignment.Center,Child=DeskIcons.Create("search",24,"AccentPrimary") }; DesignSystem.Resource(emptyIcon,Border.BackgroundProperty,"SurfaceSelected");
        var emptyState=new StackPanel { Name="SettingsSearchEmptyState",IsVisible=false,Spacing=12,Margin=new Thickness(24),MaxWidth=360,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Children={emptyIcon,emptyHeading,emptySearch,showAllSettings} };
        var emptyScroll=new ScrollViewer { Name="SettingsSearchViewport",Content=emptyState,IsVisible=false,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        emptyScroll.SizeChanged+=(_,_)=>
        {
            var compact=emptyScroll.Bounds.Height<250;
            emptyIcon.IsVisible=!compact; emptyState.Spacing=compact?8:12; emptyState.Margin=new Thickness(24,compact?12:24);
        };
        var categoryHost=new Grid { ClipToBounds=true,Children={categories,emptyScroll} };
        search.TextChanged+=(_,_)=>
        {
            var counts=searchIndex.Apply(search.Text??""); var tabs=categories.Items.OfType<TabItem>().ToArray(); var filtered=!string.IsNullOrWhiteSpace(search.Text);
            for(var index=0;index<tabs.Length;index++)tabs[index].IsVisible=!filtered || counts[index]>0;
            var any=counts.Sum()>0; categories.IsVisible=any; emptySearch.IsVisible=emptyState.IsVisible=emptyScroll.IsVisible=!any;
            if(any && (categories.SelectedIndex<0 || counts[categories.SelectedIndex]==0))categories.SelectedIndex=Array.FindIndex(counts,count=>count>0);
            clearSearch.IsVisible=filtered; searchCount.IsVisible=filtered; searchCount.Text=L.F($"Pasujące ustawienia: {counts.Sum()}");
        };
        var body=new DockPanel(); DockPanel.SetDock(header,Dock.Top); body.Children.Add(header); DockPanel.SetDock(footerSurface,Dock.Bottom); body.Children.Add(footerSurface); body.Children.Add(categoryHost);
        var dialog=new Window { Title=L.T("Ustawienia ServerDesk"),Width=960,Height=720,MinWidth=680,MinHeight=500,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=body };
        settingsOwner=dialog;
        void ResizeNavigation()
        {
            var compact=dialog.Bounds.Width<820;
            var icons=new[]{"settings","ssh","folder","layers","chart-no-axes-combined","key","plugins"}; var names=new[]{"Wygląd","Terminal","SFTP","Obszar pracy","Monitoring","Bezpieczeństwo","Pluginy"};
            var index=0;
            foreach(var category in categories.Items.OfType<TabItem>())
            {
                category.Width=compact?48:184; category.Header=compact?DeskIcons.Create(icons[index],20):DeskIcons.Labelled(icons[index],L.T(names[index]));
                AutomationProperties.SetName(category,L.T(names[index]));
                ToolTip.SetTip(category,L.T(names[index])); index++;
            }
        }
        dialog.SizeChanged+=(_,_)=>ResizeNavigation();
        FitToOwnerScreen(dialog,owner);
        var defaults=Button("Domyślne ustawienia",()=>
        {
            resetting=true;
            try { language.SelectedIndex=0; theme.SelectedIndex=0; uiFont.Value=13; interfaceFamily.SelectedItem="Domyślna"; cornerRadius.Value=6; menuPadding.Value=7; connectionPadding.Value=1; density.SelectedIndex=1; accent.Text=""; palette.SelectedItem=TerminalAppearance.Names[0]; terminalFont.Value=14; family.SelectedItem="Domyślna"; scrollback.Value=10000; cursor.SelectedIndex=0; localDirectory.Text=""; editor.SelectedIndex=0; editorPath.Text=""; localFileSort.SelectedIndex=remoteFileSort.SelectedIndex=0; startSide.SelectedIndex=1; sidebarWidth.Value=272; sidebarStart.SelectedIndex=0; tabWidth.Value=220; tabHeight.Value=35; separatorSize.Value=4; tabTarget.SelectedIndex=startupLayout.SelectedIndex=0; interval.Value=5; foreach(var (key,flag) in flags)flag.IsChecked=flagDefaults[key]; }
            finally { resetting=false; preview(); }
        }); defaults.HorizontalAlignment=HorizontalAlignment.Left; actions.Children.Add(defaults);
        var cancel=Button("Anuluj",()=> { if(!saving)dialog.Close(); }); Grid.SetColumn(cancel,1); actions.Children.Add(cancel);
        async Task SaveAsync(bool restart)
        {
            if(saving)return; saving=true; categories.IsEnabled=false; actions.IsEnabled=false; search.IsEnabled=false; saveRestart.IsEnabled=false;
            try { var draft=Draft(); draft.Validate(); await repository.SaveApplicationSettingsAsync(new(SelectedTheme(),palette.SelectedItem as string??TerminalAppearance.Names[0],(int)(terminalFont.Value??14),(int)(interval.Value??5),On("remember"),draft)); saved=true; main?.UpdateLanguageRestartNotice(draft.Language); preview(); if(main!=null)await main.ApplyPluginsAsync(draft); await changed(); restartAfterSave=restart?draft.Language:null; saving=false; dialog.Close(); }
            catch(ArgumentException ex)
            {
                ShowValidation(ex); search.Text="";
                var (index,field)=ex.ParamName switch {
                    nameof(ApplicationPreferences.SftpLocalDirectory)=>(2,(Control)localDirectory),
                    nameof(ApplicationPreferences.SftpLocalSort)=>(2,localFileSort),
                    nameof(ApplicationPreferences.SftpTextEditor)=>(2,editor),
                    nameof(ApplicationPreferences.SftpTextEditorPath)=>(2,editorPath),
                    nameof(ApplicationPreferences.TerminalCursor)=>(1,cursor),
                    nameof(ApplicationPreferences.TerminalScrollbackLines)=>(1,scrollback),
                    nameof(ApplicationPreferences.WorkspaceOnStartup)=>(3,startupLayout),
                    nameof(ApplicationPreferences.InterfaceFontFamily)=>(0,interfaceFamily),
                    nameof(ApplicationPreferences.ControlCornerRadius)=>(0,cornerRadius),
                    nameof(ApplicationPreferences.MenuItemPadding)=>(0,menuPadding),
                    nameof(ApplicationPreferences.ConnectionRowPadding)=>(3,connectionPadding),
                    _=>(0,accent)
                };
                categories.SelectedIndex=index; field.BringIntoView(); field.Focus();
            }
            catch(Exception ex) { Serilog.Log.Error("Błąd ustawień typu {Type}",ex.GetType().Name); error.Text=L.T("Nie udało się zapisać lub zastosować ustawień. Spróbuj ponownie."); }
            finally { saving=false; categories.IsEnabled=true; actions.IsEnabled=true; search.IsEnabled=true; saveRestart.IsEnabled=true; }
        }
        saveRestart.Click+=async (_,_)=>await SaveAsync(true);
        var save=Button("Zapisz",async()=>await SaveAsync(false)); save.Classes.Add("primary"); Grid.SetColumn(save,2); actions.Children.Add(save);
        void UpdateRestartReminder()
        {
            var target=language.SelectedIndex==1?"en":"pl";
            pending.IsVisible=target!=L.Language;
            languageNote.Text=L.T("Zmiana języka zostanie zastosowana po ponownym uruchomieniu ServerDesk.",target);
            pendingHeading.Text=L.T("Zmiana języka wymaga restartu",target);
            pendingText.Text=L.T("Zapisz i uruchom aplikację ponownie teraz lub wybierz Zapisz i zrób to później.",target);
            var label=L.T("Zapisz i uruchom ponownie",target); saveRestart.Content=DeskIcons.Labelled("refresh-cw",label); AutomationProperties.SetName(saveRestart,label);
            ToolTip.SetTip(save,pending.IsVisible?L.T("Zapisz ustawienia. Nowy język będzie dostępny po ponownym uruchomieniu.",target):L.T("Zapisz ustawienia"));
        }
        language.SelectionChanged+=(_,_)=>UpdateRestartReminder(); UpdateRestartReminder();
        dialog.AddHandler(Avalonia.Input.InputElement.KeyDownEvent,(_,e)=>
        {
            if(e.Key==Avalonia.Input.Key.F && e.KeyModifiers==Avalonia.Input.KeyModifiers.Control) { search.Focus(); search.SelectAll(); e.Handled=true; }
            else if(e.Key==Avalonia.Input.Key.Escape && search.IsKeyboardFocusWithin && !string.IsNullOrEmpty(search.Text)) { search.Text=""; e.Handled=true; }
        },Avalonia.Interactivity.RoutingStrategies.Tunnel);
        dialog.Closing+=(_,e)=> { if(saving)e.Cancel=true; };
        try { await dialog.ShowDialog(owner); }
        finally { backupBrowser.Dispose(); await backupBrowser.Shutdown; }
        if(!saved) { if(main!=null)main.PreviewPreferences(runtime); else DesignSystem.ApplyPreferences(runtime); DesignSystem.Apply(originalTheme); }
        if(restartAfterSave!=null)main?.RequestRestart(restartAfterSave);
    }
}
