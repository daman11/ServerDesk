# ServerDesk 0.10.0 — dopracowanie designu

Główne okno ma spokojniejszą hierarchię. Ekran startowy pokazuje własny znak ServerDesk, krótkie wprowadzenie oraz wyrównane wiersze akcji: nowe połączenie, Quick Connect i paleta poleceń. Skróty są przypisane do konkretnych akcji. Przy małej szerokości dodatkowe podpowiedzi znikają, tekst zawija się, a krótki panel można przewinąć.

Paski narzędziowe oraz przyciski ikon mają wspólny płaski szablon ze stanami hover, pressed, keyboard focus i disabled. Działające SSH pokazuje czytelne „Połączono” z ikoną sukcesu; przycisk pozostaje nieaktywny i nie przerywa połączenia. Stan uruchomionego klienta RDP nadal oznacza proces, bez potwierdzania logowania. Terminal/SFTP/Monitoring mają subtelniejszy wybór z podkreśleniem i pogrubieniem, a opcje sesji są pod ikoną wielokropka z tooltipem. Zakładki mają neutralne tło aktywnego elementu i więcej miejsca na nazwę. Puste panele nie otrzymują niebieskiej linii fokusu.

Wyszukiwarka połączeń dostała ikonę oraz przycisk czyszczenia. Sortowanie jest wizualnie lżejsze; drzewo ma kompaktowe odstępy. Powierzchnie pól tekstowych, także podczas fokusu, korzystają z tokenów aktualnego motywu. Dialogi potwierdzenia i wprowadzania danych mają spójne odstępy między akcjami. Podstawowe kolory pięciu motywów i niezależne palety terminala pozostały zachowane.

Krótki przegląd problemów przed zmianami: [design-review-0.10.0.md](design-review-0.10.0.md). Zrzuty przed: `artifacts/ui-reliability-final`; po: `artifacts/ui-design`. Dane profili w galerii pochodzą wyłącznie z narzędzia testowego, a zrzut `live-responsive-sessions.png` przedstawia rzeczywiste SSH do izolowanego Ubuntu/WSL.

## Weryfikacja

- Build Release: 0 błędów i ostrzeżeń; 120/120 testów xUnit.
- Pełne UiSmoke: pięć motywów, 900×600, skale renderowania 150/200%, personalizacja, dokowanie, wyszukiwarka i przywracanie kart.
- Nowy szablon przycisków: rzeczywiste zdarzenia myszy i klawiatury aktywują przyciski; disabled blokuje aktywację. Czyszczenie wyszukiwarki działa. Kontrast tekstu „Połączono” wynosi co najmniej 4,5:1 w Dark/Light, bez zmniejszenia opacity tego stanu. Główna akcja zachowuje kontrast ≥4,5:1 także podczas trzymania Spacji, bez hover myszy; końcowe obrazy stanów: `artifacts/ui-design-final`, wynik: `artifacts/design-preview-verification.txt`.
- Rzeczywiste SSH: terminal/nano, reconnect, SFTP/monitoring, ciągłość transportu po split/float/redock, skróty i anulowanie restartu. Wyniki: `artifacts/ui-design-verification.txt`.
- Zapis/odtworzenie po nagłym zakończeniu procesu: `scripts/test-workspace-recovery.ps1`.
- Start gotowych paczek: `artifacts/release-0.10-windows-smoke.txt`, `artifacts/release-0.10-linux-smoke.txt`; zawartość i wersje: `artifacts/release-0.10-package-check.txt`.

## Dalszy design

Ten etap obejmuje główne okno i wspólne paski narzędziowe. Formularz profilu, ustawienia i tabele SFTP wymagają kolejnego dopracowania. Testy renderowania nie zastępują sprzętowych testów mieszanego DPI, gestów D&D ani czytnika ekranu. RDP pozostaje klientem zewnętrznym. Pełny zakres prac: [roadmap.md](roadmap.md).
