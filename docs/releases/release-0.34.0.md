# ServerDesk 0.34.0 — więcej ustawień terminala i SFTP

Ustawienia mają sześć przeszukiwalnych kategorii: Wygląd, Terminal, SFTP, Obszar pracy, Monitoring i Bezpieczeństwo. Nowe pola i opisy są dostępne po polsku i angielsku. Wąskie okno używa ikon kategorii z podpowiedziami; zawartość przewija się nad stałymi przyciskami zapisu.

**Terminal → Kursor i historia** pozwala wybrać kreskę, blok lub podkreślenie oraz miganie. Kursor ma podgląd na żywo w SSH/Serial, a Anuluj przywraca poprzedni wybór. Program zdalny może zmienić kursor; zmiana niezwiązanych ustawień nie nadpisuje takiego wyboru. Przy nowym połączeniu aplikacja ponownie stosuje zapisany kursor.

Historia ma limit **0–100 000 wierszy**, domyślnie 10 000. **0** wyłącza historię. Zmiana obowiązuje przy następnym połączeniu SSH lub Serial. Podgląd, Zapisz, Anuluj i reset ustawień nie przycinają bieżącego bufora ani nie rozłączają sesji.

**SFTP** umożliwia wybór lokalnego katalogu startowego (ręcznie lub przyciskiem folderu), ukrytych plików, aktywnej strony i niezależnego sortowania/kierunku obu paneli. Pusta ścieżka oznacza domowy. Dostępność folderu jest sprawdzana poza UI przy pierwszym otwarciu SFTP; niedostępny folder powoduje powrót do domowego z komunikatem do ukrycia. Brak także domowego jest zgłaszany jako błąd.

Domyślne opcje SFTP są stosowane raz, przy pierwszym otwarciu w karcie. Otwarta przeglądarka zachowuje katalogi, filtry i sortowanie po zapisie ustawień i reconnect tej karty. Nowa karta używa ostatnio zapisanych opcji. Historia odwiedzonych katalogów nadal znika przy reconnect, zgodnie z wydaniem 0.33.0.

Przywracanie domyślnych uwzględnia opcje domyślnie wyłączone. Błąd ścieżki oznacza właściwe pole; Zapisz odsłania je także po wyszukiwaniu innej kategorii. Starsze ustawienia zachowują wartości i otrzymują zgodne wartości domyślne nowych pól.

Publiczna beta MIT, bez nowych zależności i migracji bazy. [Instrukcja PL](../getting-started.md), [English guide](../getting-started.en.md), [weryfikacja funkcji](../reports/extended-settings-2026-10-16.md), [bramki wydania](../reports/release-verification-0.34.0.md), [ograniczenia](../known-issues.md).
