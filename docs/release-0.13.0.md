# ServerDesk 0.13.0 — pliki SFTP i kolejka transferów

Panele lokalny i zdalny mają niezależne filtry nazw oraz sortowanie po nazwie, rozmiarze, modyfikacji lub typie. Kierunek można odwrócić. Wpis `..` pozostaje pierwszy, katalogi przed plikami, a ukryte wpisy nadal kontroluje przełącznik „Ukryte”. Filtr nie przeszukuje zawartości plików ani podkatalogów. Odświeżanie i sortowanie zachowują zaznaczenie po pełnej ścieżce, jeśli wpis pozostaje widoczny. Wejście do innego katalogu czyści filtr tylko tego panelu.

Kliknięcie lub fokus panelu ustawia cel operacji Lokalny/Zdalny. Menu kontekstowe listy udostępnia transfer i operacje na plikach. W obrębie SFTP działają F5 (odśwież), Enter na katalogu (otwórz), Alt+↑ (katalog nadrzędny) i Ctrl+L (ścieżka aktywnego panelu). Skróty nie przechwytują wejścia terminala. W wąskim obszarze pozostaje jeden panel wybierany kontrolką Lokalny/Zdalny; separator jest wtedy schowany, a po poszerzeniu wracają proporcje.

Kolejka pokazuje osobno oczekiwanie, łączenie, przesyłanie, zakończenie, pominięcie, zakończenie z pominięciami, anulowanie i błąd. Postęp dotyczy bieżącego pliku; katalog nie ma wyliczonego łącznego procentu. Licznik aktywnych zadań pozostaje widoczny po zwinięciu kolejki. Można anulować pojedyncze zadanie lub wszystkie, ponowić błąd/anulowanie i wyczyścić zakończone wpisy. Najnowsze zadania są na górze; historia przechowuje do 50 zakończonych zadań oraz wszystkie aktywne. Historia jest w pamięci bieżącej sesji.

Ponowienie zachowuje pierwotne źródło i cel, także gdy zmieniły się katalogi w panelach. Wymaga połączonej sesji, tworzy nowe połączenie SFTP i ponownie pyta o istniejący cel. Odmowa nadpisania jest pominięciem. Dowiązania w kopiowanym katalogu są pomijane i uwzględniane w podsumowaniu. Anulowanie zamyka oczekujące pytanie o nadpisanie; błąd samego odświeżania listy po kopiowaniu nie zmienia udanego transferu w błąd. Nadal obowiązuje limit dwóch równoczesnych transferów w całej aplikacji i publikowanie pliku dopiero po ukończeniu transferu.

## Weryfikacja

- 145/145 testów xUnit: dotychczasowy zakres oraz filtrowanie Unicode, ukryte wpisy, sortowanie i rozliczanie pominięć. Wynik: `artifacts/test-results/unit.trx`.
- Rzeczywisty OpenSSH Ubuntu/WSL: niezależne filtry i zachowanie wyboru, skróty nawigacji, wysłanie/pobranie 1 MiB z porównaniem bajtów, odmowa nadpisania pliku i części folderu, anulowanie oczekującego i aktywnego zadania wraz z pytaniem o nadpisanie, ponowienie do pierwotnego celu oraz odzyskanie błędnego zadania. Sprawdzono zwijanie/czyszczenie i limit 50 zakończonych wpisów. Wynik: `artifacts/sftp-workflow-checks.txt`.
- Renderowanie SFTP i kolejki: szerokości 650/1200, tekst 13/16; obrazy w `artifacts/ui-sftp-workflow`. Jest to renderowanie Avalonia Headless/Skia, nie sprzętowy test monitorów.
- Pełny UiSmoke z prawdziwym SSH: terminal i nano, reconnect, monitoring, split/dokowanie, skróty terminala oraz ustawienia zastosowane do aktywnej sesji. Wynik: `artifacts/ui-sftp-full-checks.txt`.
- Oddzielny scenariusz sprawdza również fokus przycisków sortowania/katalogu nadrzędnego i Ctrl+L kierowany do właściwego panelu: `artifacts/sftp-focus-checks.txt`.
- Wydania self-contained 0.13.0 uruchomiono natywnie na Windows (okno odpowiada) oraz w Linux/WSLg (proces i okno 1320×840). Wyniki: `artifacts/release-0130-windows-smoke.txt`, `artifacts/release-0130-linux-smoke.txt`.

Nie zmienia się zakres RDP: klient zewnętrzny, brak serwera Windows do weryfikacji. Nadal pozostają testy utraty sieci w trakcie SFTP i sprzątania zdalnych plików `.part` po utracie transportu, duże katalogi i długie sesje. Zależności nie zostały zmienione; dostęp do usługi audytu NuGet okresowo zgłaszał NU1900.
