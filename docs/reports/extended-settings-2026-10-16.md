# Ustawienia terminala i SFTP — 2026-10-16

Nawigacja kategorii pozostaje pojedynczą przewijaną kolumną także przy sześciu pozycjach i otwartym przypomnieniu języka. Wybrana kategoria jest odsłaniana; nie zawija się do kolejnej kolumny. Regresja restartu sprawdza wszystkie sześć pozycji, wskaźnik/klawiaturę, font16, trzy motywy oraz 680x500/760x700/960x720.

Zakres 0.34.0: sześć przeszukiwalnych kategorii, kształt/miganie kursora, historia 0–100 000 wierszy oraz lokalny folder startowy, ukryte pliki, osobne sortowanie/kierunki i aktywna strona SFTP. Dotychczasowe dane i wartości domyślne pozostają zgodne; bez nowych zależności i migracji bazy.

Testy jednostkowe: 522/522, bez pominiętych. Nowe przypadki obejmują stary JSON, kompletny roundtrip z dotychczasowymi polami, granice historii, nieznane enumy, ścieżki względne/NUL/null/długość, atomowy brak zapisu po błędzie, dostępny folder, domyślny domowy, niedostępny folder/plik zamiast katalogu, brak domowego i anulowanie. Edycja preferencji nie sprawdza dysku; dostępność jest sprawdzana w tle dopiero przy otwarciu SFTP.

UiSmoke `--extended-settings`: PL/EN, Graphite/Light, font16, 680x500 i 960x720; wyszukiwanie, właściwa kategoria błędu po filtrowaniu, podgląd kursora, anulowanie, zapis/ponowne otwarcie i przywrócenie mieszanych domyślnych. Zdalny DECSCUSR jest zachowany przy niezwiązanej zmianie interfejsu. Ustawienia nie przycinają ani nie resetują istniejącego bufora. Testy układu sprawdzają szerokości pól i oddzielenie przewijanej treści od stałych akcji.

UiSmoke `--extended-settings-network`: jednorazowy OpenSSH/SFTP na loopback 22222, własne dane aplikacji i lokalny katalog z Unicode. Sprawdzane są zapisane domyślne przy pierwszym otwarciu, ukryty plik, oddzielne kierunki/sortowanie i aktywna strona, zachowanie transportu/emulatora/bufora przy zapisie, zastosowanie limitu po reconnect, utrzymanie wyborów otwartej przeglądarki oraz nowe domyślne i jawny fallback w nowej karcie. Kontrola rzeczywistego bufora obejmuje dodatni limit 200 i wyłączenie historii przez 0. Przypięty pakiet wymaga zapisania limitu także w publicznej opcji emulatora; sama właściwość kontrolki pozostawiała poprzedni limit 10 000. Ten przypadek jest objęty akceptacją.

Macierz Windows/Linux obejmuje UI i rzeczywisty OpenSSH/SFTP. Końcowe wyniki bramek dystrybucji zapisuje VERIFICATION.md wydania; szczegółowe zrzuty/TRX są w artifacts/reports/release-0.34.0/. Testy lokalne nie oznaczają wykonania CI na GitHub. Sześć kategorii obejmuje też dotychczasowe ustawienia wyglądu, terminala, obszaru pracy, monitoringu, zaufania SSH oraz przypomnienia o restarcie.

Serial dodatkowo przeszedł rzeczywisty System.IO.Ports na jednorazowym Linux PTY z kontrolowanym echem: zapisany kursor/historia, wejście/wyjście, przeniesienie do okna i powrót, zapis bez przycięcia bufora, reconnect z historią 0 i zamknięcie portu. Nie otwierano urządzeń fizycznych.

Granice: brak nowego testu fizycznego portu Serial, wszystkich klawiatur/IME ani monitorów o mieszanym DPI. Program zdalny nadal może zmieniać kursor. Systemowy wybór folderu korzysta z Avalonia StorageProvider, ale rzeczywiste gesty natywnego pickera wymagają ręcznej akceptacji. Ubuntu działa pod WSL/WSLg. Rozszerzenie preferencji nie zmienia zasad uwierzytelniania i nie oznacza pełnego zakończenia pierwotnego MVP.
