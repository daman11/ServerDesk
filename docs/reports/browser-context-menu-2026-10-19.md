# Menu kontekstowe panelu połączeń — 2026-10-19

Mały przyrost po 0.43.0; bez zmiany wersji, schematu danych ani wydania.

## Zakres

- Prawy klik na nagłówku lub pustym obszarze Połączeń, Ulubionych i Ostatnich: Nowe połączenie, Szybkie połączenie, Nowy folder, Import konfiguracji, Eksport konfiguracji i Wyczyść filtry. Ostatnia akcja jest dostępna tylko przy aktywnym filtrze. Akcje mają istniejące ikony i tłumaczenia PL/EN oraz kolory aktualnego motywu.
- Tworzenie z pustego obszaru rozpoczyna profil w katalogu głównym, niezależnie od poprzednio zaznaczonego serwera. Menu folderu zachowuje tworzenie w tym konkretnym folderze. Menu serwera i edycji tekstu mają pierwszeństwo. Menu połączeń nie otwiera się w Transferach ani Narzędziach sieciowych.
- Synchronizacja zaznaczenia z istniejącą listą nie przebudowuje już drzewa. Wybór folderu wywoływał filtrowanie listy, a jej zdarzenie usuwało kliknięty wiersz przed zwolnieniem prawego przycisku; menu elementu nie mogło się wtedy otworzyć.
- Poprawiono zapis nowego profilu z menu folderu i szybkiego połączenia. Wypełniony szkic był przekazywany jako istniejący rekord, więc kontrola nieaktualnego zapisu odrzucała brak tego ID w bazie. Osobna ścieżka tworzenia przekazuje szkic do edytora i `null` jako oryginał. Walidacja równoległej edycji oraz dotychczasowy mechanizm zapisu/poświadczeń pozostają w repozytorium.
- Uzupełniono podręcznik oraz oba zasoby Pomocy offline.

Menu używa standardowego routingu [Avalonia 12.0.4 ContextMenu](https://github.com/AvaloniaUI/Avalonia/blob/12.0.4/src/Avalonia.Controls/ContextMenu.cs): obsłużone żądanie elementu potomnego nie otwiera menu przodka. Stan filtra jest aktualizowany przy każdym otwarciu.

## Weryfikacja

- `tools/ServerDesk.UiSmoke --browser-context-menu`: **PASS**, PL/EN. Zdarzenia myszy otwierają menu na pustej liście, nagłówku, folderze i serwerze; akcje wybierane są kliknięciem. Sprawdzono ikony i tłumaczenia w Graphite/Light, natywne menu wyszukiwarki, aktualizację Wyczyść filtry, niezależność modułów oraz zapis folderu, profilu w folderze, profilu głównego i profilu z szybkiego połączenia. Ponowne otwarcie repozytorium potwierdza trzy profile; istniejący profil pozostaje identyczny. Atrapa magazynu zgłasza każdą próbę odczytu/zapisu/usunięcia hasła — wywołań było zero, sesji sieciowych zero.
- Istniejący `--browser-workflow`: **PASS**, PL/EN. Zapis i anulowanie nowego profilu, domyślne foldery, zmiana nazw/usuwanie folderów, zachowanie ikon i zwinięć, głębokość 32, przeciąganie, selekcja, odrzucanie nieaktualnych celów i skróty klawiatury. Obejmuje też układ 900 px, font 16, Graphite/Light.
- Kompilacja harnessu Release: **0 ostrzeżeń, 0 błędów**. Parsowanie Pomocy PL/EN: **PASS**, 14 zgodnych identyfikatorów tematów i nowe instrukcje menu.
- Wybrane zrzuty Graphite/Light oceniono wizualnie. Dowody: `artifacts/reports/browser-context-menu-2026-10-19/`; regresja drzewa: `artifacts/reports/browser-context-menu-workflow-2026-10-19/`.

Testy używały wyłącznie osobnych baz z losowym katalogiem, bez danych ani połączeń użytkownika. To celowana weryfikacja Avalonia headless na Windows, bez pełnej macierzy protokołów lub natywnej akceptacji Linuxa. Nie testowano ponownie systemowych okien wyboru plików importu/eksportu; menu wywołuje ich istniejące akcje.

Build do testowania z folderu: `artifacts/dev-harness/ServerDesk.App.exe`. Bez instalatora, nowego tagu ani publikacji.
