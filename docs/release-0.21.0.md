# ServerDesk 0.21.0 — spójny wygląd aplikacji i nowe ustawienia

Ustawienia mają boczne menu z kategoriami zamiast wąskiego paska zakładek. Aktywna kategoria ma wyraźne tło; każda strona ma nagłówek z ikoną i krótkim opisem. Pola są ułożone obok etykiet przy szerokim oknie, a pod etykietami przy węższym. Sekcje mają wspólne obramowania, odstępy i promienie narożników. Wybrany motyw pokazuje próbki kolorów. Domyślny rozmiar okna to 960×720; jego maksymalna wysokość uwzględnia obszar roboczy i skalowanie monitora właściciela.

Pasek akcji jest oddzielony od przewijanej treści. Przywrócenie domyślnych ustawień pozostaje po lewej, Anuluj i Zapisz po prawej. Podgląd zmian na żywo, wycofywanie ich przez Anuluj i zapis ustawień działają jak wcześniej. Informacja o restarcie języka pozostaje widoczna również po zmianie kategorii. Przełączanie kategorii działa myszą i strzałkami klawiatury.

Wspólne poprawki obejmują:

- Motywy Dark i Light: odświeżone kolory tła, paneli, pól, obramowań i zaznaczeń. Pozostałe motywy i własny akcent pozostają dostępne. Tekst podstawowy i pomocniczy ma kontrast co najmniej 4,5:1 na standardowych powierzchniach wszystkich pięciu motywów.
- Główne okno: wyraźniejsze foldery, małe etykiety protokołów i spójne zaokrąglenia kontrolek.
- Edytor serwera: zaznaczenia nawigacji zgodne z ustawieniami i wspólne karty sekcji.
- Paski SSH/RDP/VNC/Serial: własna powierzchnia i subtelna linia oddzielająca narzędzia od sesji. W SFTP poprawiono odstępy przycisków.
- Monitoring: spójne nagłówki i karty System/CPU/RAM/Dyski/Sieć, ikony oraz wykres CPU dopasowany do szerokości karty.
- Ekrany gotowości RDP/VNC/Serial, historia, paleta poleceń, wybór ikony folderu, poświadczenia, pytania i komunikaty: wspólne nagłówki i powierzchnie.
- Menu kontekstowe, listy, pola i przyciski: spójne odstępy, narożniki oraz stany wskazania, zaznaczenia i fokusu.

Zmiany dotyczą prezentacji. Profile, poświadczenia, wybór języka, terminale i układ sesji zachowują dotychczasowy format i zachowanie.

## Weryfikacja

247/247 testów xUnit. Regresja UI obejmuje personalizację, ikony, układ, dokowanie, przywracanie kart, zapis/anulowanie ustawień oraz języki. Ustawienia: PL/EN, Dark/Light/Graphite, tekst 16, 680×500, 760×700 i 960×720, rzeczywiste kliknięcia i strzałki, kontrola przycinania etykiet. Edytor: 600/780 px, Dark/Light, tekst 13/16 i walidacja pól. SFTP i monitoring są dodatkowo renderowane z jawnymi danymi testowymi przy 650/1200 px w trzech motywach.

Rzeczywisty VNC na loopback sprawdza obraz, klawiaturę/mysz, dokowanie, utratę połączenia oraz ciągłość przy zapisie języka. Testy RDP sprawdzają sterowanie własnym procesem i osadzony host; nie zastępują logowania do zdalnego Windows. Pozostałe ograniczenia i fizyczna macierz monitorów: `docs/roadmap.md`, `docs/rdp-test-matrix.md`.

Szczegółowe wyniki i gotowe paczki: `artifacts/release-0210-verification.md`. Podglądy: `artifacts/ui-0210-restart`, `artifacts/ui-0210-surfaces`, `artifacts/ui-0210-editor`, `artifacts/ui-0210-localization`.
