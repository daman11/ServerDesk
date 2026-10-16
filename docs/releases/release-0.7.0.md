# ServerDesk 0.7.0 — personalizacja i obszar sesji

Edytor serwera pozwala wybrać kolor z palety albo podać dowolny `#RRGGBB`. Kolor oznacza wiersz drzewa i górną krawędź nagłówka zakładki; pusty kolor usuwa znacznik. Tekst, stan połączenia i tło pozostają zależne od motywu. Zmiana koloru oraz grafiki jest widoczna również w otwartych i odpiętych sesjach.

Można importować własne PNG, JPEG i WebP do 4 MiB (do 4096 px na bok i 16 mln pikseli). Dekodowanie w tle tworzy statyczny PNG 128×128 z zachowaniem proporcji, przezroczystości i orientacji zdjęcia. Profil przechowuje grafikę wewnętrznie, więc SQLite, duplikowanie i eksport/import JSON zachowują ją bez zależności od lokalnej ścieżki. Usunięcie grafiki przywraca ikonę z listy. Walidacja odrzuca nadmierne rozmiary, uszkodzone PNG i niepoprawny kolor przed transakcyjnym importem. Konfiguracje JSON mają limit 32 MiB.

Menu Układ zawiera jeden panel oraz dwa panele obok siebie lub jeden nad drugim. Każdy panel ma własną wybraną zakładkę. Przeciąganie i menu zakładki pozwalają zmienić panel lub kolejność; separator reguluje proporcje. Ctrl+Tab / Ctrl+Shift+Tab działa w aktywnym panelu. Po połączeniu paneli wszystkie sesje zostają zachowane.

Zakładkę można odpiąć do osobnego okna. Przyciski pozwalają zadokować ją do panelu 1 lub 2. Zamknięcie odpiętego okna dokuje sesję do panelu 1; osobny przycisk zamknięcia sesji rozłącza po potwierdzeniu. Układ zawiera też przywrócenie wszystkich odpiętych sesji. Anulowanie zamknięcia głównego okna zachowuje jego sesje i odpięte okna. Kierunek/proporcje podziału są zapamiętywane, a połączenia są uruchamiane przez użytkownika.

W WorkspaceState każdy wpis oznacza instancję sesji, dlatego dwa połączenia do tego samego profilu są niezależne. W obrębie głównego okna terminal pozostaje w tym samym rodzicu; zmienia się pozycja w siatce. Przenoszenie do okna używa BeginReparent/EndReparent istniejącej kontrolki. Obie widoczne sesje mogą próbkować monitoring; zminimalizowane odpięte okno wstrzymuje jego próbkowanie.

## Weryfikacja

106 testów xUnit zaliczonych, kompilacja bez ostrzeżeń/błędów. Nowe regresje obejmują kolor i PNG, zapis/import/eksport, brak cofania personalizacji przez starszą sesję, atomowe odrzucenie uszkodzonego importu, limity strumieni oraz niezmienność liczby/tożsamości sesji po zmianach układu, dokowaniu, zamknięciu i sortowaniu zakładek.

UiSmoke sprawdza PNG/JPEG/WebP, proporcje/przezroczystość, zapis formularza, aktualizację prezentacji, oba podziały, małe okno i render scaling 150%, klawiaturę grup, menu dokowania, odpięcie/powrót, zamknięcie pojedynczej sesji, wiele odpiętych okien, anulowanie zamknięcia i przywrócenie układu bez automatycznych połączeń. Dotychczasowa regresja obejmuje pięć motywów oraz render scaling 150/200%. Wynik: `artifacts/ui-workspace-verification.txt`; zrzuty: `artifacts/ui-workspace`.

Rzeczywiste SSH Ubuntu/WSL potwierdza ciągłość tych samych transportów i emulatorów podczas zmiany podziału, odpięcia oraz ponownego dokowania. Polecenia po przeniesieniu mają widoczne wyniki. Druga sesja jednocześnie pokazuje rzeczywisty monitoring. Zachowano testy SSH/SFTP/reconnect, nano, dwóch sesji i skrótów.

## Zakres pozostający

Układ ma dwie grupy oraz osobne okna; zagnieżdżone podziały i przywracanie geometrii odpiętych okien są przyszłymi rozszerzeniami. Obsługiwane grafiki to PNG/JPEG/WebP, bez własnego SVG ani animacji. Systemowe gesty D&D, natywny dialog plików, wiele monitorów i mieszane DPI wymagają dalszych ręcznych testów na docelowych systemach; headless sprawdza obsługę poleceń/menu i renderowanie. RDP nadal otwiera zewnętrzny klient, a test jego procesu nie potwierdza połączenia z serwerem Windows. Pełny MVP, KWallet i pozostała macierz systemów są opisane w [roadmap.md](../roadmap.md).
