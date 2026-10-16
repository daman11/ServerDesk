# ServerDesk 0.20.1 — informacja o restarcie i poprawki wyglądu

Po wybraniu innego języka ustawienia pokazują wyróżniony komunikat o konieczności ponownego uruchomienia. Jest widoczny nad przyciskami także po przejściu do innej kategorii. Po zapisaniu ustawień przypomnienie pojawia się w głównym oknie i podaje język następnego uruchomienia. Można je ukryć przyciskiem ×. Ponowne zapisanie obecnie używanego języka usuwa przypomnienie; po uruchomieniu aplikacji w wybranym języku nie jest wyświetlane.

Język nadal zmienia się przy następnym uruchomieniu. Zapis i zamknięcie przypomnienia zachowują otwarte sesje. Anulowanie wyboru lub nieudany zapis nie wyświetlają komunikatu o zapisaniu zmiany.

Ustawienia mają nagłówek, obramowane sekcje tematyczne z ikonami, spójne odstępy między etykietami i polami oraz osobny pasek akcji. Przywracanie domyślnych opcji jest po lewej, Anuluj i Zapisz po prawej. Kategorie przewijają zawartość, a zapis i przypomnienie pozostają dostępne. Komunikat błędu zajmuje miejsce tylko wtedy, gdy zawiera tekst.

Ekran startowy ma wyraźniejsze karty akcji i ikony na osobnych tłach. Filtry protokołów mają wspólną powierzchnię i czytelniejsze zaznaczenie. Nagłówek i dolny pasek oddzielono subtelną linią. Motywy i własny akcent korzystają z istniejących ustawień. Przypomnienie zajmuje własne miejsce pod paskiem tytułu, więc nie zakrywa sesji ani przycisków okna.

## Weryfikacja

247/247 testów xUnit. Test przypomnień obejmuje oba języki, anulowanie, niepoprawny zapis, zapis, ponowne otwarcie ustawień, powrót do bieżącego języka i rzeczywiste kliknięcie ukrycia. Układ czterech kategorii sprawdzany jest w Dark/Light, przy czcionce 16, odstępach przestronnych i rozmiarach 680×500 oraz 760×700. Rzeczywisty serwer VNC na loopback potwierdza utrzymanie połączenia po zapisaniu języka i ukryciu komunikatu. Główne okno 900×600 zachowuje osobne miejsce dla nagłówka, przypomnienia i sesji.

Pełne wyniki regresji i gotowych paczek: `artifacts/release-0201-verification.md`. W tym wydaniu nie zmieniano transportów SSH/RDP/VNC/Serial ani nie powtarzano logowania do prywatnego serwera RDP i fizycznej macierzy DPI.
