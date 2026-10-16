using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace ServerDesk.App;

internal static partial class DeskIcons
{
    // Application-owned geometry; the pinned upstream SVG files stay unchanged.
    internal static readonly IReadOnlyDictionary<string,string> ApplicationPaths=new Dictionary<string,string>
    {
        ["layout-single"]="M3,4 H21 V20 H3 Z",
        ["layout-columns"]="M3,4 H21 V20 H3 Z M12,4 V20",
        ["layout-rows"]="M3,4 H21 V20 H3 Z M3,12 H21",
        ["layout-three"]="M3,4 H21 V20 H3 Z M11,4 V20 M11,12 H21",
        ["layout-four"]="M3,4 H21 V20 H3 Z M12,4 V20 M3,12 H21",
        ["layout-equalize"]="M3,4 V20 M21,4 V20 M5,12 H19 M8,9 L5,12 L8,15 M16,9 L19,12 L16,15 M12,7 V17",
        ["font-increase"]="M3,18 L8,5 L13,18 M5,13 H11 M16,10 H22 M19,7 V13",
        ["font-decrease"]="M3,18 L8,5 L13,18 M5,13 H11 M16,10 H22",
        ["action-run"]="M8,5 L19,12 L8,19 Z",
        ["action-stop"]="M5,5 H19 V19 H5 Z"
    };

    internal static MenuItem ActionMenu(string label,string? glyph=null,string? shortcut=null)
    {
        var title=L.T(label);
        var icon=glyph??ForAction(label);
        var menu=new MenuItem { Header=title,Tag=label,Icon=icon==null?null:Create(icon,16),InputGesture=shortcut==null?null:KeyGesture.Parse(shortcut) };
        AutomationProperties.SetName(menu,title+(shortcut==null?"":" · "+shortcut));
        var description=DescriptionForAction(label);
        if(description!=null)ToolTip.SetTip(menu,L.T(description));
        return menu;
    }

    internal static string? DescriptionForAction(string label)=>label switch
        {
            "Paleta akcji"=>"Wyszukaj akcję lub ustawienie",
            "Komendy"=>"Ściągawka poleceń i własne komendy",
            "Narzędzia sieciowe"=>"Diagnostyka sieci bez zapisanego serwera",
            _=>null
        };

    // Match stable source labels, before translation and independent of shortcut text.
    internal static string? ForAction(string label)=>label.Split(" · ",2,StringSplitOptions.None)[0] switch
    {
        "Jeden panel"=>"layout-single",
        "Panele obok siebie"=>"layout-columns",
        "Panele jeden nad drugim"=>"layout-rows",
        "Trzy panele (1 + 2)"=>"layout-three",
        "Cztery panele (2 × 2)"=>"layout-four",
        "Wyrównaj wielkość paneli"=>"layout-equalize",
        "Dokuj do panelu 1" or "Dokuj do panelu 2" or "Dokuj do panelu 3" or "Dokuj do panelu 4" or "Przywróć wszystkie odpięte sesje" or "Pokaż / ukryj panel boczny" or "Pokaż / ukryj panel połączeń"=>"panel-left",
        "Odłącz do osobnego okna" or "Otwórz w osobnym oknie"=>"external-link",
        "Połącz" or "Szybkie połączenie"=>"connect",
        "Uruchom" or "Start" or "Wykonaj" or "Uruchom z historii"=>"action-run",
        "Stop"=>"action-stop",
        "Rozłącz" or "Rozłącz RDP"=>"disconnect",
        "Połącz ponownie" or "Uruchom ponownie sesję" or "Uruchom ponownie" or "Restart" or "Odśwież" or "Ponów odczyt biblioteki" or "Zapisz i uruchom ponownie"=>"refresh",
        "Terminal" or "Komendy" or "Paleta akcji" or "Paleta poleceń"=>"square-terminal",
        "Narzędzia sieciowe" or "Network Tools" or "Sprawdź serwer"=>"network",
        "Otwarte sesje"=>"search",
        "Przenieś do folderu" or "Przenieś do katalogu"=>"folder-input",
        "SFTP" or "Zmień ikonę folderu" or "Domyślna ikona" or "Kategorie komend"=>"folder",
        "Monitoring" or "Server Info"=>"chart-no-axes-combined",
        "Opcje" or "Menu" or "Operacje"=>"more",
        "Wyślij" or "Eksport konfiguracji" or "Eksport komend JSON" or "Eksportuj wyniki"=>"upload",
        "Pobierz" or "Import konfiguracji" or "Import komend JSON"=>"download",
        "Anuluj" or "Zamknij" or "Anuluj transfery" or "Anuluj łączenie" or "Zamknij sesję"=>"close",
        "Zapisz" or "Zapisz kolekcję" or "Zapisz OID" or "Zapisz urządzenie WoL"=>"save",
        "Potwierdź" or "OK"=>"check",
        "Nowe połączenie" or "Nowy serwer" or "Nowy serwer w folderze" or "Nowe połączenie w folderze" or "Nowa komenda" or "Dodaj parametr"=>"plus",
        "Edytuj" or "Zmień nazwę" or "Zmień nazwę folderu" or "Zmień nazwę / przenieś"=>"edit",
        "Duplikuj" or "Kopiuj" or "Kopiuj wyniki" or "Kopiuj odcisk" or "Kopiuj zawartość dowiązania…" or "Kopiuj zaznaczenie"=>"copy",
        "Ulubiony" or "Ulubione"=>"star",
        "Usuń" or "Usuń zaznaczone…" or "Usuń trwale" or "Usuń serwer" or "Usuń pusty folder" or "Usuń plik / pusty katalog" or "Usuń grafikę" or "Usuń kolekcję" or "Usuń komendę" or "Usuń parametr" or "Usuń pustą kategorię" or "Usuń wpis historii" or "Wyczyść historię komend" or "Wyczyść historię narzędzi"=>"trash",
        "Poświadczenia" or "Usuń zapisane hasło" or "Zarządzaj kluczami…"=>"key",
        "Usuń zaufanie…"=>"shield",
        "Wklej" or "Wstaw"=>"clipboard-paste",
        "Nowy katalog" or "Nowy folder" or "Dodaj folder" or "Dodaj kategorię"=>"folder-plus",
        "Uprawnienia chmod (zdalny)"=>"lock-keyhole",
        "Szczegóły" or "O aplikacji"=>"info",
        "Historia połączeń" or "Historia tego serwera" or "Historia komend: włącz / wyłącz"=>"history",
        "Ustawienia" or "Ustawienia wyglądu" or "Ustawienia pulpitu RDP" or "Domyślne ustawienia"=>"settings",
        "Uruchom RDP" or "Pokaż okno"=>"monitor",
        "Importuj grafikę"=>"image",
        "Wybierz plik" or "Wybierz pliki"=>"folder-open",
        "Wczytaj kolekcję"=>"archive",
        "Przejdź"=>"arrow-right",
        "Utwórz zdalne dowiązanie…"=>"link",
        "Większa czcionka"=>"font-increase",
        "Mniejsza czcionka"=>"font-decrease",
        "Ctrl+Alt+Del"=>"key",
        "Wyczyść filtry"=>"close",
        _=>null
    };
}
