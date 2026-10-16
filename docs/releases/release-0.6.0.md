# ServerDesk 0.6.0 — ikony i rozpoznawanie serwerów

Dodano jednolity zestaw 66 wektorowych ikon Lucide dla nawigacji, akcji, menu, zakładek, monitoringu i plików SFTP. Ikony korzystają z kolorów aktualnego motywu i stanów kontrolek. Przyciski z samą ikoną mają tooltip oraz nazwę dostępności. Lokalne SVG, przypięta wersja 1.55.0, manifest SHA-256 i pełne notice ISC/MIT są dołączone do projektu oraz paczek; brak zależności od sieci podczas pracy.

W edycji serwera można wybrać ikonę automatyczną albo jedną z 14 ikon ról, np. bazę danych, chmurę, kontenery, router lub kopie zapasowe. Wybór jest zapisywany w SQLite i JSON, zachowywany przy importowaniu oraz duplikowaniu i aktualizowany w otwartej zakładce. W drzewie obok ikony pozostaje oznaczenie SSH/RDP. Starsze profile dostają wartość automatyczną, a nieznane poprawne identyfikatory z nowszych eksportów nie giną.

Zakładka pokazuje osobną ikonę stanu połączenia, z rozróżnieniem błędu i zwykłego zakończenia. SFTP otrzymało ikony kategorii plików. Dodano własny znak ServerDesk w nagłówku, ikonie EXE/okna i skrócie Linux. ICO zawiera siedem rozmiarów od 16 do 256 px.

Naprawiono nadpisywanie świeżej edycji profilu przy ponownym połączeniu ze starszej sesji: zapis udanego SSH aktualizuje tylko LastConnected. Usunięto wyjątek przy zamykaniu sesji przed utworzeniem terminala; konsolowy proces RDP jest kończony przed odłączeniem PTY.

## Weryfikacja

88 testów xUnit zaliczonych, kompilacja bez ostrzeżeń i błędów. Nowe regresje obejmują stare profile, walidację identyfikatorów, nieznane ikony, zapis/import/duplikowanie, klasyfikację plików oraz zachowanie zmian profilu podczas reconnect.

UiSmoke sprawdza zgodność wszystkich osadzonych SVG z manifestem, geometrię, 16/24 px w dark/light, dziedziczenie koloru, stany sesji, wybór/zapis ikony, przełączenie automatycznej ikony SSH/RDP oraz aktualizację otwartej kontrolki. Sprawdzane są również zamknięcie niezainicjalizowanych sesji SSH/RDP i powtórne Dispose. Wynik oraz pozostałe scenariusze UI: `artifacts/ui-icons-verification.txt`; zrzuty: `artifacts/ui-icons`.

Pełne uruchomienie UiSmoke przeszło: pięć motywów, kontrast tokenów, render scaling 150/200%, małe okna i zapis ustawień. Na rzeczywistym izolowanym SSH Ubuntu/WSL sprawdzono terminal/nano, EOF, reconnect, błędny passphrase i odzyskanie, SFTP/monitoring, zamknięcie podczas komendy oraz dwie zakładki głównego okna i skróty klawiatury. Test procesu RDP korzysta z adaptera testowego; nie jest połączeniem RDP.

## Pozostały zakres

To kolejny etap MVP. Pełny MVP i wszystkie kryteria redesignu pozostają otwarte: osadzony obraz RDP, rzeczywiste testy RDP, KWallet, pozostałe systemy docelowe, czytniki ekranu, Follow System i mieszane DPI. Kolory serwerów, import własnych grafik i split view nie są częścią tej wersji. Szczegóły: [roadmap.md](../roadmap.md), [icons.md](../icons.md).
