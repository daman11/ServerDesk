using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Templates;
using Avalonia.Media;
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
        var error=DesignSystem.Label("",true,12); DesignSystem.Resource(error,TextBlock.ForegroundProperty,"Danger"); error.TextWrapping=TextWrapping.Wrap; error.IsVisible=false;
        error.PropertyChanged+=(_,e)=> { if(e.Property==TextBlock.TextProperty)error.IsVisible=!string.IsNullOrEmpty(error.Text); };
        StackPanel Page(string icon,string title,string detail)
        {
            var page=new StackPanel { Spacing=18,Margin=new Thickness(22) };
            page.Children.Add(DesignSystem.PageHeading(icon,L.T(title),L.T(detail))); searchIndex.Page(page,title+" "+L.T(title)); return page;
        }
        StackPanel Section(StackPanel page,string glyph,string title)
        {
            var fields=new StackPanel { Spacing=12 };
            var heading=DeskIcons.Labelled(glyph,L.T(title),17,"AccentPrimary");
            var content=new StackPanel { Spacing=14,Children={heading,fields} };
            var card=DesignSystem.Card(content); card.Classes.Add("settings-card");
            page.Children.Add(card); searchIndex.Section(page,card,fields,title+" "+L.T(title)); return fields;
        }
        void Label(StackPanel panel,string label,Control control)
        {
            var originalLabel=label; label=L.T(label);
            var caption=DesignSystem.Label(label); caption.TextWrapping=TextWrapping.Wrap;
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
        CheckBox Flag(StackPanel panel,string key,string label,bool value)
        {
            var originalLabel=label; label=L.T(label);
            var input=new CheckBox { Content=new TextBlock { Text=label,TextWrapping=TextWrapping.Wrap },IsChecked=value,HorizontalAlignment=HorizontalAlignment.Stretch }; AutomationProperties.SetName(input,label);
            flags[key]=input; panel.Children.Add(input); searchIndex.Field(panel,input,originalLabel+" "+label); input.IsCheckedChanged+=(_,_)=>preview(); return input;
        }
        var appearancePage=Page("settings","Wygląd","Motyw, język i czytelność interfejsu.");
        var terminalPage=Page("ssh","Terminal","Kolory i czcionka sesji terminalowych.");
        var workspacePage=Page("layers","Obszar pracy","Panele, karty i zachowanie przy uruchamianiu.");
        var monitoringPage=Page("chart-no-axes-combined","Monitoring","Parametry serwera i informacje na pasku statusu.");
        var securityPage=Page("key","Bezpieczeństwo","Tożsamość serwerów i zapisane zaufanie SSH.");
        var trustSection=Section(securityPage,"key","Weryfikacja serwerów SSH");
        Window? settingsOwner=null;
        var manageTrust=Button("Zarządzaj kluczami…",async()=> { if(settingsOwner!=null)await TrustedHosts(settingsOwner,repository); }); manageTrust.Name="ManageTrustedHosts";
        Label(trustSection,"Zaufane klucze SSH",manageTrust);
        trustSection.Children.Add(Text(L.T("Usunięcie zaufania wymaga osobnego potwierdzenia i działa od razu, niezależnie od zapisu ustawień. Następne połączenie wymaga ponownego porównania odcisku."),12));
        var languageSection=Section(appearancePage,"globe","Język interfejsu");
        var language=new ComboBox { Name="ApplicationLanguage",ItemsSource=new[]{"Polski","English"},SelectedIndex=p.Language=="en"?1:0,HorizontalAlignment=HorizontalAlignment.Stretch };
        Label(languageSection,"Język aplikacji / Language",language);
        var languageNote=DesignSystem.Label(L.T("Zmiana języka zostanie zastosowana po ponownym uruchomieniu ServerDesk."),true,12);
        languageNote.TextWrapping=TextWrapping.Wrap; languageNote.Name="LanguageRestartNotice"; languageSection.Children.Add(languageNote);
        var appearance=Section(appearancePage,"settings","Motyw i interfejs");
        var theme=new ComboBox { ItemsSource=DesignSystem.Themes.Select(t=>t.Name).Append(L.T("Systemowy")).ToArray(),SelectedIndex=originalTheme=="system"?5:Math.Max(0,Array.FindIndex(DesignSystem.Themes,t=>t.Id==originalTheme)),HorizontalAlignment=HorizontalAlignment.Stretch };
        theme.ItemTemplate=new FuncDataTemplate<string>((name,_)=>
        {
            var option=DesignSystem.Themes.FirstOrDefault(t=>t.Name==name); var row=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
            if(option!=null)foreach(var color in new[]{option.Background,option.Sidebar,option.Accent})row.Children.Add(new Border { Width=12,Height=16,CornerRadius=new CornerRadius(3),Background=new SolidColorBrush(Color.Parse(color)) });
            row.Children.Add(DesignSystem.Label(name??"")); return row;
        });
        Label(appearance,"Motyw aplikacji",theme);
        var uiFont=Number(appearance,"Rozmiar tekstu interfejsu",p.InterfaceFontSize,11,16);
        var density=new ComboBox { ItemsSource=new[]{L.T("Kompaktowy"),L.T("Standardowy"),L.T("Przestronny")},SelectedIndex=(int)p.Density,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(appearance,"Odstępy i wysokość kontrolek",density);
        var accent=new TextBox { Text=p.AccentColor,PlaceholderText=L.T("Domyślny lub #RRGGBB"),HorizontalAlignment=HorizontalAlignment.Stretch }; Label(appearance,"Kolor akcentu",accent);
        var colors=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        foreach(var (name,color) in new[]{(L.T("Motyw"),""),(L.T("Niebieski"),"#58A6FF"),(L.T("Miętowy"),"#69CFB6"),(L.T("Fioletowy"),"#B39DFF"),(L.T("Pomarańczowy"),"#E5B567")})
        {
            var choice=Button(name,()=>accent.Text=color); if(color.Length>0)choice.Content=new Border { Width=20,Height=16,Background=new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color)),CornerRadius=new CornerRadius(3) };
            AutomationProperties.SetName(choice,name+L.T(" akcent")); ToolTip.SetTip(choice,name); colors.Children.Add(choice);
        }
        appearance.Children.Add(colors); appearance.Children.Add(Text(L.T("Motyw, akcent i rozmiar interfejsu mają podgląd na żywo. Anuluj przywraca poprzedni wygląd."),12));
        var terminal=Section(terminalPage,"ssh","Wygląd terminala");
        var palette=new ComboBox { ItemsSource=TerminalAppearance.Names,SelectedItem=TerminalAppearance.Names.Contains(original.TerminalPalette)?original.TerminalPalette:TerminalAppearance.Names[0],HorizontalAlignment=HorizontalAlignment.Stretch }; Label(terminal,"Paleta terminala",palette);
        var terminalFont=Number(terminal,"Rozmiar czcionki terminala",original.TerminalFont,9,32);
        var family=new ComboBox { ItemsSource=ApplicationPreferences.TerminalFonts,SelectedItem=p.TerminalFontFamily,HorizontalAlignment=HorizontalAlignment.Stretch,ItemTemplate=new FuncDataTemplate<string>((name,_)=>DesignSystem.Label(L.T(name??""))) }; Label(terminal,"Krój czcionki terminala",family);
        terminal.Children.Add(Text(L.T("Paleta terminala jest niezależna od motywu aplikacji. Jeśli wybranej czcionki nie ma w systemie, terminal użyje czcionki monospace."),12));
        Flag(terminal,"sessionStatus","Pokaż status pod panelem sesji",p.ShowSessionStatus);
        var workspace=Section(workspacePage,"layers","Panele i karty");
        var sidebarWidth=Number(workspace,"Szerokość panelu połączeń",p.SidebarWidth,220,500);
        var sidebarStart=new ComboBox { ItemsSource=new[]{L.T("Zapamiętaj ostatni stan"),L.T("Zawsze pokaż"),L.T("Zawsze ukryj")},SelectedIndex=(int)p.SidebarOnStartup,HorizontalAlignment=HorizontalAlignment.Stretch }; Label(workspace,"Panel połączeń przy uruchomieniu",sidebarStart);
        var tabWidth=Number(workspace,"Maksymalna szerokość nazwy karty",p.TabLabelWidth,140,360);
        Flag(workspace,"protocols","Pokaż oznaczenia protokołów przy serwerach",p.ShowProtocolBadges);
        workspace=Section(workspacePage,"history","Przy uruchomieniu");
        Flag(workspace,"remember","Przywracaj karty i układ po restarcie",original.RememberWorkspace);
        workspace.Children.Add(Text(L.T("Przywrócone karty czekają na Połącz. Hasła i zawartość terminala nie są zapisywane w układzie."),12));
        var monitoring=Section(monitoringPage,"chart-no-axes-combined","Pasek statusu i monitoring");
        Flag(monitoring,"status","Pokaż dolny pasek statusu",p.ShowStatusBar);
        Flag(monitoring,"metrics","Pokaż parametry aktywnego serwera",p.ShowMetrics);
        Flag(monitoring,"transfers","Pokaż liczbę aktywnych transferów",p.ShowTransferCount);
        var interval=Number(monitoring,"Odświeżanie monitoringu (sekundy)",original.MonitorInterval,2,300);
        monitoring=Section(monitoringPage,"memory-stick","Pola paska parametrów");
        Flag(monitoring,"cpu","CPU",p.MetricCpu); Flag(monitoring,"ram","Pamięć RAM",p.MetricMemory); Flag(monitoring,"network","Odbieranie i wysyłanie",p.MetricNetwork);
        Flag(monitoring,"uptime","Czas pracy serwera",p.MetricUptime); Flag(monitoring,"disk","Zajętość dysku /",p.MetricDisk); Flag(monitoring,"system","Nazwa systemu operacyjnego",p.MetricSystem); Flag(monitoring,"clock","Zegar serwera",p.MetricClock);
        monitoring.Children.Add(Text(L.T("Pola, które nie mieszczą się w oknie, są automatycznie ukrywane. Pełne wartości są w podpowiedziach. Gdy pasek parametrów jest wyłączony, monitoring w tle czeka na otwarcie panelu Monitoring."),12));
        bool On(string key)=>flags[key].IsChecked==true;
        ApplicationPreferences Draft()=>new()
        {
            Language=language.SelectedIndex==1?"en":"pl",
            InterfaceFontSize=(int)(uiFont.Value??13),Density=(InterfaceDensity)Math.Max(0,density.SelectedIndex),AccentColor=accent.Text?.Trim()??"",TerminalFontFamily=family.SelectedItem as string??"Domyślna",
            SidebarWidth=(int)(sidebarWidth.Value??272),SidebarOnStartup=(SidebarStartup)Math.Max(0,sidebarStart.SelectedIndex),TabLabelWidth=(int)(tabWidth.Value??220),ShowProtocolBadges=On("protocols"),ShowSessionStatus=On("sessionStatus"),
            ShowStatusBar=On("status"),ShowMetrics=On("metrics"),ShowTransferCount=On("transfers"),MetricCpu=On("cpu"),MetricMemory=On("ram"),MetricNetwork=On("network"),MetricUptime=On("uptime"),MetricDisk=On("disk"),MetricSystem=On("system"),MetricClock=On("clock")
        };
        preview=()=>
        {
            if(resetting)return;
            try { var draft=Draft(); draft.Validate(); if(main!=null)main.PreviewPreferences(draft); else DesignSystem.ApplyPreferences(draft); DesignSystem.Apply(theme.SelectedIndex==5?"system":DesignSystem.Themes[Math.Max(0,theme.SelectedIndex)].Id); error.Text=""; accent.Classes.Remove("error"); }
            catch(ArgumentException ex) { error.Text=ex.Message; accent.Classes.Add("error"); }
        };
        theme.SelectionChanged+=(_,_)=>preview(); density.SelectionChanged+=(_,_)=>preview(); family.SelectionChanged+=(_,_)=>preview(); sidebarStart.SelectionChanged+=(_,_)=>preview(); accent.TextChanged+=(_,_)=>preview();
        TabItem Category(string glyph,string title,StackPanel page)
        {
            var scroll=new ScrollViewer { Content=page,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            DesignSystem.Resource(scroll,ScrollViewer.BackgroundProperty,"BackgroundPrimary");
            return new TabItem { Classes={"page-navigation","settings-category"},Header=DeskIcons.Labelled(glyph,L.T(title)),Content=scroll };
        }
        var categories=new TabControl { Name="SettingsCategories",TabStripPlacement=Dock.Left,Padding=new Thickness(0),ItemsSource=new[] {
            Category("settings","Wygląd",appearancePage),Category("ssh","Terminal",terminalPage),Category("layers","Obszar pracy",workspacePage),Category("chart-no-axes-combined","Monitoring",monitoringPage),Category("key","Bezpieczeństwo",securityPage)
        },SelectedIndex=0 };
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
        var clearSearch=DeskIcons.Button("close","Wyczyść wyszukiwanie ustawień",()=> { search.Text=""; search.Focus(); },26); clearSearch.IsVisible=false; search.InnerRightContent=clearSearch;
        var searchCount=DesignSystem.Label("",true,12); searchCount.Name="SettingsSearchCount"; searchCount.IsVisible=false;
        var headerBody=new StackPanel { Spacing=12,Children={DesignSystem.PageHeading("settings",L.T("Ustawienia"),L.T("Dostosuj wygląd i sposób pracy ServerDesk.")),search,searchCount} };
        var header=new Border { Padding=new Thickness(22,16),Child=headerBody,BorderThickness=new Thickness(0,0,0,1) };
        DesignSystem.Resource(header,Border.BorderBrushProperty,"BorderDefault");
        var emptySearch=DesignSystem.Label(L.T("Brak pasujących ustawień. Zmień lub wyczyść wyszukiwanie."),true); emptySearch.Name="SettingsSearchEmpty"; emptySearch.TextWrapping=TextWrapping.Wrap; emptySearch.Margin=new Thickness(28); emptySearch.HorizontalAlignment=HorizontalAlignment.Center; emptySearch.VerticalAlignment=VerticalAlignment.Center; emptySearch.IsVisible=false;
        var categoryHost=new Grid { Children={categories,emptySearch} };
        search.TextChanged+=(_,_)=>
        {
            var counts=searchIndex.Apply(search.Text??""); var tabs=categories.Items.OfType<TabItem>().ToArray(); var filtered=!string.IsNullOrWhiteSpace(search.Text);
            for(var index=0;index<tabs.Length;index++)tabs[index].IsVisible=!filtered || counts[index]>0;
            var any=counts.Sum()>0; categories.IsVisible=any; emptySearch.IsVisible=!any;
            if(any && (categories.SelectedIndex<0 || counts[categories.SelectedIndex]==0))categories.SelectedIndex=Array.FindIndex(counts,count=>count>0);
            clearSearch.IsVisible=filtered; searchCount.IsVisible=filtered; searchCount.Text=L.F($"Pasujące ustawienia: {counts.Sum()}");
        };
        var body=new DockPanel(); DockPanel.SetDock(header,Dock.Top); body.Children.Add(header); DockPanel.SetDock(footerSurface,Dock.Bottom); body.Children.Add(footerSurface); body.Children.Add(categoryHost);
        var dialog=new Window { Title=L.T("Ustawienia ServerDesk"),Width=960,Height=720,MinWidth=680,MinHeight=500,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=body };
        settingsOwner=dialog;
        void ResizeNavigation()
        {
            var compact=dialog.Bounds.Width<820;
            var icons=new[]{"settings","ssh","layers","chart-no-axes-combined","key"}; var names=new[]{"Wygląd","Terminal","Obszar pracy","Monitoring","Bezpieczeństwo"};
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
            try { language.SelectedIndex=0; theme.SelectedIndex=0; uiFont.Value=13; density.SelectedIndex=1; accent.Text=""; palette.SelectedItem=TerminalAppearance.Names[0]; terminalFont.Value=14; family.SelectedItem="Domyślna"; sidebarWidth.Value=272; sidebarStart.SelectedIndex=0; tabWidth.Value=220; interval.Value=5; foreach(var flag in flags.Values)flag.IsChecked=true; }
            finally { resetting=false; preview(); }
        }); defaults.HorizontalAlignment=HorizontalAlignment.Left; actions.Children.Add(defaults);
        var cancel=Button("Anuluj",()=> { if(!saving)dialog.Close(); }); Grid.SetColumn(cancel,1); actions.Children.Add(cancel);
        async Task SaveAsync(bool restart)
        {
            if(saving)return; saving=true; categories.IsEnabled=false; actions.IsEnabled=false; search.IsEnabled=false; saveRestart.IsEnabled=false;
            try { var draft=Draft(); draft.Validate(); await repository.SaveApplicationSettingsAsync(new(theme.SelectedIndex==5?"system":DesignSystem.Themes[Math.Max(0,theme.SelectedIndex)].Id,palette.SelectedItem as string??TerminalAppearance.Names[0],(int)(terminalFont.Value??14),(int)(interval.Value??5),On("remember"),draft)); saved=true; main?.UpdateLanguageRestartNotice(draft.Language); preview(); await changed(); restartAfterSave=restart?draft.Language:null; saving=false; dialog.Close(); }
            catch(ArgumentException ex) { error.Text=ex.Message; if(accent.Classes.Contains("error")) { search.Text=""; categories.SelectedIndex=0; accent.BringIntoView(); accent.Focus(); } }
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
        await dialog.ShowDialog(owner);
        if(!saved) { if(main!=null)main.PreviewPreferences(runtime); else DesignSystem.ApplyPreferences(runtime); DesignSystem.Apply(originalTheme); }
        if(restartAfterSave!=null)main?.RequestRestart(restartAfterSave);
    }
}
