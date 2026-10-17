# ServerDesk 0.41.0 — Public Beta, przygotowanie 1.0

Wydanie zbiera usprawnienia rozwijane po 0.40.0: wyróżnione nagłówki folderów i oznaczenia pustych gałęzi, 36 dodatkowych ikon systemów/usług, skróty i przeciąganie w drzewie połączeń oraz ponowne używanie niezmienionych grafik. Dołączone oryginalne symbole mają przypięte źródła, sumy i wymagane licencje.

Naprawiono ryzyko utraty sekretu kolekcji Network Tools przy błędzie zapisu indeksu. Usunięcie najpierw zatwierdza zmianę w bazie, a potem usuwa zaszyfrowany wpis. Błąd bazy zachowuje działającą kolekcję i pozwala ponowić operację; błąd sprzątania nie maskuje pierwotnego błędu zapisu. Po zatwierdzonym usunięciu niedostępny magazyn może zachować nieużywany zaszyfrowany wpis.

Rozszerzono testy danych: migracja i odtworzenie schematów 0–2 z profilami, folderami, historią i ustawieniami, przerwanie procesu podczas eksportu i zmian drzewa, rzeczywisty brak miejsca na ograniczonym 8 MiB tmpfs oraz ponowienie po awarii. SQLite backup nie obejmuje sekretów i pozostaje kopią tylko części danych aplikacji. Dodano także bramkę audytu zależności i próby aktualizacji oraz napraw instalatora.

Windows x64 Setup/ZIP, Linux x64 DEB/TAR.GZ i źródła ZIP; MIT. Zachowany schema3, bez zmiany zależności aplikacji. Zamknij aplikację i przygotuj kopię danych przed aktualizacją.

**Status pozostaje Public Beta.** Na prośbę maintenera testy maszyn VMware są odłożone do czasu przygotowania dostępu. Nie zaliczono natywnego Kali/KDE/Wayland, czystej VM Windows z rejestracją instalatora, fizycznego Serial, monitorów o różnych DPI ani wielogodzinnego pilotażu. Lokalne testy i WSL nie zastępują tych prób. Brak podpisu Authenticode i automatycznego aktualizatora; publikacja zdalna wymaga osobnej decyzji.

[Weryfikacja i ograniczenia](../reports/release-verification-0.41.0.md), [instrukcja PL](../getting-started.md), [English guide](../getting-started.en.md), [pozostałe warunki 1.0](../stable-release-plan.md).
