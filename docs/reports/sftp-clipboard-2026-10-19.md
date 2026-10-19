# Ergonomia SFTP — schowek i klawiatura — 2026-10-19

Mały przyrost etapu C pierwotnego MVP po 0.43.0, bez migracji i zmian transportu/poświadczeń. Nie zamyka akceptacji WAN, innych serwerów, fizycznej klawiatury ani Linuxa.

## Zmiana

- Obie listy i menu Operacje otrzymują podmenu Kopiuj do schowka: nazwy, pełne ścieżki zaznaczonych pozycji albo ścieżka otwartego katalogu. Kontekst listy wiąże kopiowanie z jej stroną; Operacje korzysta z aktualnego panelu. Podmenu ma ikony, etykiety PL/EN i widoczny skrót.
- Ctrl+Shift+C działa w liście plików; Ctrl+F przechodzi do filtra aktywnego panelu, a Escape w filtrze czyści zapytanie i wraca do listy. Nie przechwytuje zwykłego kopiowania tekstu ani wejścia terminala.
- To tekst, nie transfer ani gotowy argument powłoki. Nazwy i ścieżki zachowują Unicode, spacje, apostrofy, separatory i wielkość liter. `..` i duplikaty są pomijane zgodnie z istniejącymi zasadami zaznaczania. Dowiązanie nie jest rozwiązywane. Przygotowanie ma limit 1 048 576 jednostek UTF-16 i odrzuca nadmiar przed wywołaniem schowka; nie zwraca obciętego wyniku.
- Odczyt pamięciowego, potwierdzonego widoku może działać po utracie połączenia. Brak zaznaczenia, niezatwierdzona ścieżka, ładowanie, filtrowanie w tle lub zamknięta karta nie zapisują starego tekstu. W przypadku pustego zaznaczenia nadal można skopiować ścieżkę katalogu.
- Porównanie menu z paskiem wykazało aktywne Wyślij/Pobierz i Nowy katalog po rozłączeniu. Teraz ich dostępność jest zgodna z gotowością SSH/SFTP, celem i zaznaczeniem. Operacje na zaznaczeniu nie korzystają z listy czekającej na odfiltrowanie. Początek i koniec pracy w tle odświeżają dostępność akcji, bez ponownego przechwytywania dużej selekcji osobno dla każdej pozycji nowego podmenu.

## Sprawdzenie

- `FileClipboardTests`, `FileSelectionTests`, `LocalizationTests`: **35/35 PASS**, bez pominięć. Testy ścieżek Windows i POSIX są niezależne od systemu uruchomienia; regułę wielkości liter lokalnego zaznaczenia sprawdzono na Windows.
- `tools/ServerDesk.UiSmoke --sftp-clipboard`: **PASS**, PL/EN, Graphite/Light, font 16 i okno 900×640. Zaznaczenia grupowe, nazwy/ścieżki/katalog, źródło menu mimo fokusu drugiej listy, menu Operacje, etykiety/ikony/skrót, cache bez połączenia, rodzic i pusty wybór, Ctrl+Shift+C/Ctrl+F/Escape i natywne Ctrl+C w polu. Niezatwierdzona ścieżka, odczyt/pending filter/zamknięcie nie nadpisują sentinela. Rzeczywista lista 2501 pozycji sprawdza automatyczne ponowne włączenie akcji po filtrowaniu. Podczas samego oglądania i zmiany zaznaczenia schowek pozostaje bez zmian.
- Istniejący `--large-file-views`: **PASS**, po 50 000 wpisów w obu panelach. Responsywność dispatchera, 30 szybkich filtrów, sortowanie, zachowanie zaznaczenia i niezależność paneli, pomijanie starszych wyników, zastąpienie małym katalogiem oraz anulowanie/drain przy zamykaniu. PL/EN, Graphite/Light.
- Kompilacja harnessu Release: **0 ostrzeżeń, 0 błędów**. Wybrane zrzuty podmenu oceniono wizualnie; dowody w `artifacts/reports/sftp-clipboard-2026-10-19/`.
- Kontrola repozytorium: **PASS**, 795 plików; `git diff --check`: **PASS**. Pomoc PL/EN ma zgodne ID, cztery sekcje SFTP i osobne wiersze nowych skrótów. Nowy scenariusz dołączono do przyszłego CI Windows/Linux.

Scenariusze używają losowych katalogów i własnych baz, atrap magazynu oraz pamięciowych list/schowka headless. Nie wykonują transferów, nie odczytują osobistych plików ani nie łączą z serwerem. Odczyt/zapis/usunięcie poświadczeń: zero. Lokalny zakres to Windows; nie jest to fizyczna akceptacja gestów/DPI ani test schowka KDE. Bez pełnej macierzy i wydania. Build deweloperski: `artifacts/dev-harness/ServerDesk.App.exe`.
