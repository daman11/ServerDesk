# ServerDesk 0.12.0 — nakładanie elementów i personalizacja

Windows z pełnymi dekoracjami nadal mógł rysować tytuł/ikonę w miejscu własnego nagłówka aplikacji. Poprzedni test renderowania zawartości okna nie obejmował dekoracji systemowych. Główne okno używa teraz BorderOnly na Windows i Linux oraz jednego kompletu przycisków minimalizacji/maksymalizacji/zamknięcia. Przyciski narzędzi i okna są w osobnych kolumnach tej samej siatki. Tytuł systemowego okna pozostaje dostępny w pasku zadań. Pusty obszar nagłówka służy do przesuwania i maksymalizacji dwuklikiem. Zamykanie nadal stosuje potwierdzenie aktywnych sesji. Dokumentacja API: [Avalonia — custom title bars](https://docs.avaloniaui.net/docs/platform-specific-guides/windows).

Dolny pasek ma skończony budżet szerokości, osobno dla statusu połączenia, parametrów i transferów. Każda komórka parametrów przycina zawartość; tekst dostaje mierzalną szerokość oraz ellipsis. Długa nazwa Debian GNU/Linux 13 (trixie) nie wychodzi na zegar. Pola są ukrywane według dostępnego miejsca, zamiast wyłącznie według szerokości całego okna. Pełne wartości i znaczenie pól są dostępne w tooltipach. Wspólny układ ikony i etykiety również używa siatki, dzięki czemu powiększenie czcionki nie ucina końców nazw w ustawieniach i narzędziach.

Ustawienia mają cztery przewijane kategorie oraz przyciski Zapisz/Anuluj/Domyślne ustawienia poza przewijaną treścią:

- **Wygląd:** sześć opcji motywu (w tym systemowy), rozmiar tekstu 11–16, kompaktowe/standardowe/przestronne kontrolki, własny akcent #RRGGBB i gotowe kolory. Akcent obejmuje przyciski, checkboxy i wybór kategorii; kolor tekstu na akcencie dobierany jest do kontrastu.
- **Terminal:** paleta, rozmiar 9–32, pięć opcji kroju czcionki (z fallbackiem monospace), pokazywanie statusu pod sesją. Zmiana wyglądu nie zastępuje transportu SSH ani bufora emulatora.
- **Obszar pracy:** szerokość panelu 220–500, jego stan przy uruchomieniu (ostatni/pokaż/ukryj), maksymalna szerokość nazwy karty 140–360, znaczniki SSH/RDP i przywracanie kart/układu.
- **Monitoring:** dolny pasek, parametry serwera i licznik transferów; interwał 2–300 s; osobno CPU, RAM, sieć, uptime, dysk, system i zegar. Wyłączenie parametrów w tle nie blokuje panelu Monitoring; poza nim próbkowanie czeka do ponownego włączenia paska.

Wygląd interfejsu ma podgląd na żywo. Anulowanie lub zamknięcie okna cofa niezapisane zmiany. Przywrócenie domyślnych najpierw zmienia formularz/podgląd; dopiero Zapisz utrwala je. Ustawienia są zapisywane w jednej transakcji SQLite. Stare klucze motywu, terminala, interwału i przywracania pozostają obsługiwane. Niepoprawny nowy zestaw preferencji pozwala otworzyć program z domyślnymi wartościami. Baza pozostaje w wersji 2.

Weryfikacja:

- 135/135 xUnit: zgodność starych ustawień, ponowny odczyt pełnego zestawu, walidacja i brak częściowego zapisu;
- UiSmoke `--settings-layout`: podgląd/anulowanie, niepoprawny akcent, zapis/ponowne otwarcie/reset, ukrywanie paska, długi Debian i duże wartości; 900/1320/1920 px, fonty 11/13/16 i renderowanie 200%. Log: `artifacts/ui-settings-layout-verification.txt`, obrazy: `artifacts/ui-settings-layout`;
- natywny Windows: odczyt stylu własnego HWND potwierdza brak WS_CAPTION, sprawdzane są pozycje przycisków w zwykłym/maksymalizowanym oknie oraz 900 px z fontem 16. Log/obrazy: `artifacts/ui-native-titlebar-0120`;
- pełne UI z rzeczywistym SSH/SFTP, reconnect, split/float/redock oraz stosowaniem ustawień do działającego terminala i wstrzymywaniem/wznawianiem próbkowania: `artifacts/ui-settings-full-verification.txt`, `artifacts/ui-settings-full`;
- natywny Linux/WSLg i uruchomienia paczek: `artifacts/ui-native-titlebar-linux-0120-verification.txt`, `artifacts/release-0.12.0-windows-smoke.txt`, `artifacts/release-0.12.0-linux-smoke.txt`. Kontrola archiwów: `artifacts/release-0.12.0-package-check.txt`.

Windows jest publikowany obok uruchomionej poprzedniej wersji: artifacts/win-x64-0.12.0 oraz ServerDesk-0.12.0-win-x64.zip. WSLg i renderowanie 200% nie zastępują testów fizycznych monitorów z mieszanym DPI, całej macierzy X11/Wayland ani rzeczywistego RDP. Pozostałe zadania: [roadmap.md](../roadmap.md).
