# ServerDesk 0.42.0 — Public Beta

Wydanie zbiera zmiany od 0.41.0: kolorowe tagi i filtry połączeń, spokojniejsze drzewo i nawigację, szybkie połączenie i przełącznik sesji, sześć dodatkowych motywów oraz wybór edytora tekstu SFTP. Plugin Komendy ma czytelniejsze karty i szczegóły, stałe filtry oraz więcej miejsca na wybór systemu. Okno O aplikacji zawiera informacje o wersji i licencjach.

W **Ustawienia → Bezpieczeństwo → Logi i diagnostyka** można czytać i przeszukiwać logi, odświeżać listę, otworzyć katalog oraz zapisać pełną kopię wybranego pliku. Podgląd obejmuje najwyżej 128 KiB; eksport zachowuje pełny plik. Przed udostępnieniem kopii sprawdź zawarte w niej adresy i ścieżki.

Naprawiono usuwanie zapisanych SSH/RDP przy braku katalogu lub pliku poświadczeń. Zmiana w bazie jest trwała, błędy plików/bazy mają odrębne komunikaty, a diagnostyka zapisuje stos wywołań. Nietypowe testy wykryły i pozwoliły poprawić dopuszczanie nazw logów zakończonych nową linią oraz możliwość eksportu do aktywnego katalogu logów przez dowiązanie/junction.

Windows x64 Setup/ZIP, Linux x64 DEB/TAR.GZ i źródła ZIP; MIT. Bez zmiany schematu SQLite i zależności aplikacji. Przed aktualizacją zamknij ServerDesk i przygotuj kopię danych. Instalator zachowuje dane użytkownika.

**Nadal Public Beta.** Natywne testy maszyn VMware, Kali/KDE/Wayland, czystej instalacji z rejestracją, fizycznego Serial i mieszanego DPI oraz długotrwały pilotaż pozostają otwarte. WSL i headless nie zastępują tych prób. Brak podpisu Authenticode i automatycznego aktualizatora. Pakiety są przygotowywane lokalnie; publikacja zdalna wymaga osobnej decyzji.

[Weryfikacja i ograniczenia](../reports/release-verification-0.42.0.md), [instrukcja PL](../getting-started.md), [English guide](../getting-started.en.md), [warunki 1.0](../stable-release-plan.md).
