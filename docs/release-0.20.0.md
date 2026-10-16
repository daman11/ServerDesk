# ServerDesk 0.20.0 — język polski i angielski

W **Ustawienia → Wygląd → Język aplikacji / Language** można wybrać **Polski** lub **English**. Po wybraniu języka zapisz ustawienia i uruchom ServerDesk ponownie. Komunikat przy wyborze wyjaśnia, że język jest stosowany po restarcie. Zapis nie uruchamia samoczynnie ponownego startu ani nie przerywa otwartych sesji. Anuluj zachowuje poprzedni wybór; domyślne ustawienia przywracają polski.

W angielskim interfejsie wybór jest w **Settings → Appearance → Application language / Język**. Katalogi obejmują główne okno, menu i skróty, edytor i walidację profili SSH/RDP/VNC/Serial, ustawienia, ikony i grafikę, historię i wyszukiwarkę sesji, SFTP/transfery, monitoring, pasek stanu oraz komunikaty aplikacji i poświadczeń. Nazwy serwerów i folderów, login, adresy, ścieżki, pliki i treść terminala są danymi użytkownika i nie są tłumaczone. Okna systemowe, zewnętrzne klienty i zdalne aplikacje używają własnego języka.

Wybór jest częścią istniejących ustawień aplikacji w SQLite. Starsze ustawienia bez pola Language pozostają po polsku. Aktualizacja nie zmienia profili, ikon, zapisanych poświadczeń ani układu. Eksport konfiguracji serwerów zachowuje dotychczasowy zakres i nie zawiera ustawienia języka.

Teksty pochodzą z dołączonych katalogów `src/ServerDesk.Core/Localization/pl.json` i `en.json`. Interpolowane komunikaty tłumaczą szablon, zachowując parametry użytkownika i formatowanie wartości. Identyfikatory protokołów, ikon, działań i ustawień pozostają stałe. Język jest ładowany przed tworzeniem głównego okna, więc pierwszy interfejs używa zapisanego wyboru.

## Weryfikacja

247/247 xUnit. Nowe testy obejmują zgodność starych ustawień, zapis i odrzucanie nieobsługiwanego języka, zgodność katalogów i wszystkich parametrów formatowania, przełączanie tłumaczeń, walidację oraz zapis/eksport profilu z nazwą Połącz i folderem Ustawienia bez ich przetłumaczenia.

Test interfejsu sprawdza zapis/anulowanie i ponowne odczytanie języka, angielskie główne okno, cztery kategorie ustawień, edytor i walidację profili, karty czterech protokołów, historię oraz paletę poleceń. Zrzuty edytora: Dark/Light, szerokości 600 i 780, wysokość 520. Działający test VNC korzysta z serwera RFB na loopback; nie zastępuje testów z konkretnym produkcyjnym serwerem.

Pełna regresja dotychczasowego polskiego interfejsu przeszła, obejmując motywy, ikony, personalizację, foldery, ustawienia, układ i przywracanie kart. W tym wydaniu nie powtarzano zdalnego logowania RDP, fizycznego adaptera Serial ani sprzętowej macierzy DPI.

Logi: `artifacts/language-0200-tests.txt`, `artifacts/language-0200-ui.txt`, `artifacts/language-0200-regression.txt`. Obrazy: `artifacts/ui-0200-localization`. Wyniki paczek i pozostałych testów: `artifacts/release-0200-verification.md`.
