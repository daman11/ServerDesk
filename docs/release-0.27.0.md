# ServerDesk 0.27.0 — pierwotne MVP: pliki i dowiązania SFTP

Wydanie rozwija podstawowy menedżer plików SSH/SFTP z pierwotnej specyfikacji. Aktualny stan całego pierwotnego zakresu: [mvp-status.md](mvp-status.md).

## Dowiązania

- **Szczegóły** wybranego dowiązania pokazują rozwiązaną ścieżkę i typ rzeczywistego celu. Niedostępny lub zapętlony cel kończy się błędem, bez tworzenia transferu.
- Dwuklik lub Enter na dowiązaniu do katalogu otwiera rzeczywistą lokalizację; pole ścieżki pokazuje tę lokalizację. Również odświeżenie ścieżki względnej normalizuje pole do rzeczywistego zdalnego katalogu.
- Zaznacz jeden zdalny plik lub katalog i wybierz **Utwórz zdalne dowiązanie…**. Podaj nazwę nowego dowiązania w bieżącym zdalnym katalogu. Powstaje dowiązanie do istniejącej, rozwiązanej ścieżki absolutnej. Nazwy ze spacjami, znakami Unicode i apostrofami są przekazywane jako dane przez SSH.NET; aplikacja nie uruchamia komendy powłoki. Istniejący wpis, także zerwane dowiązanie, nie jest nadpisywany.
- Dla jednego dowiązania wybierz **Kopiuj zawartość dowiązania…** w Operacjach/menu kontekstowym. Potwierdzenie pokazuje serwer, ścieżkę dowiązania, rzeczywisty cel i miejsce zapisu. Plik lub katalog jest kopiowany pod nazwą dowiązania jako zwykły obiekt. Kolejka/retry przechowuje pokazane źródło i cel; późniejsza zmiana aliasu nie przekierowuje transferu.

To kopiowanie zawartości, a nie zachowywanie obiektu dowiązania. Cel może być poza katalogiem aliasu. Zagnieżdżone dowiązania są pomijane i liczone w wyniku; aplikacja nie śledzi całego grafu linków. Zwykłe Wyślij/Pobierz oraz D&D nadal odrzuca zaznaczenie zawierające dowiązanie i wskazuje osobną akcję. Nie jest potrzebne włączanie Developer Mode ani podnoszenie uprawnień do tworzenia lokalnych linków. Upload lokalnego linku wymaga istniejącego linku w lokalnym systemie plików; sprawdzono go na Linux.

## Kopiowanie katalogów i ochrona celu

Rekurencja ma limit **20 000 elementów / 64 poziomów na zadanie** i kontrolę aktywnych przodków. Anulowanie jest sprawdzane przy kolejnych wpisach. Lokalna enumeracja odbywa się poza wątkiem UI, a listowanie drzewa po stronie SFTP jest ograniczone. Przekroczenie limitu zatrzymuje zadanie z błędem; nie wycofuje wcześniej zakończonych plików ani nie usuwa ich automatycznie.

Upload odrzuca zdalny cel będący dowiązaniem lub innym typem niż oczekiwany. Download odrzuca lokalny cel będący dowiązaniem. Sprawdzane są ponownie typy źródeł i cel przed publikacją pliku. Oryginalny plik nie jest kasowany jako sposób nadpisania; dotychczasowy tymczasowy plik i bezpieczny rename pozostają w użyciu. Po zerwaniu sieci zdalny .part nadal może pozostać.

Sprawdzenia nie stanowią transakcyjnej ochrony przed każdą równoczesną zmianą zdalnego systemu plików. [Raport](release-verification-0.27.0.md), [ograniczenia bety](known-issues.md). Brak nowych zależności, zmian schematu i automatycznego uruchamiania uprzywilejowanych komend. Aplikacja pozostaje publiczną betą MIT.
