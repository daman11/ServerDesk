using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static Task About(Window owner)
    {
        var assembly=typeof(App).Assembly;
        var version=assembly.GetName().Version?.ToString(3)??"";
        var build=assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??version;
        var copyright=assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright??"";
        var avalonia=typeof(Application).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??typeof(Application).Assembly.GetName().Version?.ToString(3);
        var technical=$"ServerDesk {build}\n{RuntimeInformation.OSDescription} · {RuntimeInformation.ProcessArchitecture}\n{RuntimeInformation.FrameworkDescription}\nAvalonia {avalonia}";
        var heading=new Grid { ColumnDefinitions=new("Auto,*"),ColumnSpacing=14 };
        heading.Children.Add(new BrandMark { Width=44,Height=44,VerticalAlignment=VerticalAlignment.Top });
        var headingText=new StackPanel { Spacing=6,Children={DesignSystem.Label("ServerDesk",false,24),DesignSystem.Hint(L.T("Twoje połączenia, terminale i pliki w jednym miejscu."))} };
        var badges=new WrapPanel { Orientation=Orientation.Horizontal,Children={DesignSystem.Badge(version),DesignSystem.Badge(L.T("Publiczna beta")),DesignSystem.Badge("MIT · open source")} };
        foreach(var badge in badges.Children)badge.Margin=new Thickness(0,0,6,4);
        headingText.Children.Add(badges); Grid.SetColumn(headingText,1); heading.Children.Add(headingText);

        var tabs=new TabControl { Name="AboutTabs" };
        void Tab(string title,string icon,StackPanel content)
        {
            var item=new TabItem { Header=DeskIcons.Labelled(icon,L.T(title)),Content=new ScrollViewer { Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,Margin=new Thickness(0,12,0,0) } };
            AutomationProperties.SetName(item,L.T(title)); tabs.Items.Add(item);
        }
        var overview=new StackPanel { Spacing=14 };
        overview.Children.Add(Text(L.T("ServerDesk to bezpłatny program open source dla Windows i Linux. Łączy zarządzanie serwerami, terminale i transfery plików we wspólnym obszarze pracy.")));
        overview.Children.Add(DesignSystem.SectionHeading("server",L.T("Połączenia i obszar pracy")));
        overview.Children.Add(DesignSystem.Hint(L.T("SSH, SFTP, RDP, VNC i porty szeregowe. Foldery i tagi połączeń, podział na maksymalnie cztery panele oraz opcjonalne pluginy: monitoring Linuxa i komendy.")));
        overview.Children.Add(DesignSystem.Hint(L.T("Wersja publiczna beta. Projekt rozwija się w kierunku wydania 1.0.")));
        overview.Children.Add(DesignSystem.SectionHeading("info",L.T("Dane techniczne")));
        var buildText=new SelectableTextBlock { Name="AboutBuildInfo",Text=technical,TextWrapping=TextWrapping.Wrap };
        overview.Children.Add(DesignSystem.Card(buildText,new Thickness(12)));
        overview.Children.Add(DesignSystem.Hint(L.T("Program nie wysyła telemetrii. Sprawdzanie aktualizacji przy starcie jest opcją w ustawieniach. Informacje o wersji możesz skopiować i dołączyć do zgłoszenia problemu.")));
        overview.Children.Add(DesignSystem.Hint(copyright));
        Tab("Program","info",overview);

        var license=new StackPanel { Spacing=12 };
        license.Children.Add(DesignSystem.SectionHeading("shield",L.T("Licencja MIT")));
        license.Children.Add(Text(L.T("Możesz bezpłatnie używać, modyfikować i rozpowszechniać ServerDesk, także komercyjnie. Zachowaj informację o prawach autorskich i tekst licencji. Oprogramowanie jest dostarczane bez gwarancji.")));
        license.Children.Add(DesignSystem.Hint(L.T("Poniżej oryginalny angielski tekst licencji ServerDesk. Biblioteki i grafiki zachowują własne licencje.")));
        using(var stream=assembly.GetManifestResourceStream("ServerDesk.App.Legal.LICENSE")??throw new InvalidOperationException("Missing ServerDesk license resource."))
        using(var reader=new StreamReader(stream))
            license.Children.Add(new SelectableTextBlock { Name="AboutLicenseText",Text=reader.ReadToEnd(),TextWrapping=TextWrapping.Wrap });
        Tab("Licencja","shield",license);

        var components=new StackPanel { Spacing=14 };
        components.Children.Add(Text(L.T("ServerDesk powstaje dzięki projektom open source. Najważniejsze biblioteki i zasoby:")));
        void Component(string title,string names)
        {
            components.Children.Add(new StackPanel { Spacing=4,Children={DesignSystem.Label(L.T(title)),DesignSystem.Hint(names)} });
        }
        Component("Interfejs i grafika","Avalonia · Inter · Lucide · Simple Icons");
        Component("Terminal","Iciclecreek.Avalonia.Terminal · Porta.Pty · XTerm.NET");
        Component("Połączenia","SSH.NET · MarcusW.VncClient · System.IO.Ports");
        Component("Dane i diagnostyka","Microsoft.Data.Sqlite · SQLitePCLRaw · Serilog");
        Component("Narzędzia sieciowe","DnsClient · SharpSnmpLib");
        components.Children.Add(DesignSystem.Hint(L.T("Pełny wykaz zależności i teksty ich licencji są dołączane do paczek instalacyjnych i przenośnych: THIRD-PARTY-NOTICES.md, DEPENDENCIES.json i folder licenses. Licencja MIT ServerDesk nie zastępuje licencji tych składników.")));
        components.Children.Add(DesignSystem.Hint(L.T("FreeRDP i systemowy magazyn haseł są składnikami zewnętrznymi. Nazwy i znaki graficzne należą do ich właścicieli; ich obecność nie oznacza rekomendacji ServerDesk.")));
        Tab("Składniki","layers",components);

        var status=DesignSystem.Hint(""); status.Name="AboutCopyStatus"; status.IsVisible=false;
        var actions=new WrapPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
        var footer=new StackPanel { Spacing=6,Children={status,actions} };
        var body=new Grid { RowDefinitions=new("Auto,*,Auto"),RowSpacing=16,Children={heading,tabs,footer} }; Grid.SetRow(tabs,1); Grid.SetRow(footer,2);
        var dialog=Window(L.T("O ServerDesk"),body); dialog.Name="AboutDialog"; dialog.Width=680; dialog.Height=640; dialog.MinWidth=440; dialog.MinHeight=380; dialog.SizeToContent=SizeToContent.Manual;
        var copy=Button("Kopiuj informacje",()=> { }); copy.Name="AboutCopyInfo"; copy.Margin=new Thickness(0,0,8,4);
        copy.Click+=async(_,_)=>
        {
            copy.IsEnabled=false;
            try
            {
                var clipboard=dialog.Clipboard??throw new InvalidOperationException(); await clipboard.SetTextAsync(technical);
                status.Text=L.T("Skopiowano informacje o wersji."); DesignSystem.Resource(status,TextBlock.ForegroundProperty,"ForegroundSecondary");
            }
            catch { status.Text=L.T("Nie udało się skopiować informacji. Możesz zaznaczyć dane techniczne i skopiować je ręcznie."); DesignSystem.Resource(status,TextBlock.ForegroundProperty,"Danger"); }
            finally { status.IsVisible=true; copy.IsEnabled=true; }
        };
        actions.Children.Add(copy);
        var updates=Button("Sprawdź aktualizacje",async()=> { var prepared=await ApplicationUpdates(dialog,((MainWindow)owner).UpdateRepository); if(prepared!=null) { dialog.Close(); ((MainWindow)owner).RequestUpdateInstall(prepared); } });
        updates.Name="AboutUpdates"; updates.Margin=new Thickness(0,0,8,4); updates.IsVisible=owner is MainWindow; actions.Children.Add(updates);
        var close=Button("Zamknij",()=>dialog.Close()); close.Name="AboutClose"; close.IsCancel=true; close.Margin=new Thickness(0,0,0,4); actions.Children.Add(close);
        return dialog.ShowDialog(owner);
    }
}
