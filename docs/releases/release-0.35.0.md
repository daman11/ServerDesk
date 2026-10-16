# ServerDesk 0.35.0 — trzy i cztery panele oraz ustawienia obszaru pracy

W menu **Układ** są teraz **Trzy panele (1 + 2)** i **Cztery panele (2 × 2)**. W trzech panelach lewy zajmuje pełną wysokość, a dwa prawe są ułożone pionowo. Siatka 2×2 pozwala pracować w czterech terminalach; można też pozostawić jeden obszar pusty. Otwarte karty ze wspólnej grupy wypełniają puste panele przy zmianie podziału. Sesje w osobnych oknach pozostają osobno.

Każda grupa ma własne karty, wybór, kolejność i Ctrl+Tab. Karty można przeciągać między panelami lub dokować przez menu do panelu 1–4, także z osobnego okna. Kliknięcie numeru pustego panelu wskazuje miejsce następnej karty przy domyślnej regule. Przełącznik sesji pokazuje wszystkie cztery grupy. Zmniejszenie liczby paneli przenosi karty ukrywanych grup do pierwszej; nie zamyka sesji ani nie resetuje terminali.

Separatory zmieniają szerokości i wysokości niezależnie. **Wyrównaj wielkość paneli** przywraca obie proporcje do 50%. Poziomy separator obsługuje ↑/↓ i Home; zmienia wysokości treści, zachowując wysokość zakładek. Układ, obie proporcje i wybrane karty czterech grup są zapamiętywane. Odtworzenie nadal czeka na ręczne **Połącz**.

**Ustawienia → Obszar pracy** dodaje pięć opcji: układ przy uruchomieniu, cel nowych kart (aktywny/pierwszy/najmniej kart), wysokość pasków 30–48, separatory 4–12 i numery paneli. Rozmiary mają podgląd i anulowanie. Wybór układu startowego działa przy kolejnym uruchomieniu; nie zmienia bieżącego podziału podczas edycji. Rozłożenie przywróconych kart nie pobiera poświadczeń.

Poprawiono także inicjalizację ukrytego terminala przy zmianie kursora w przywróconych kartach. Starsze preferencje i snapshoty otrzymują zgodne wartości domyślne; dotychczasowa wartość osobnych okien jest zachowana. Starsze wersje aplikacji mogą odrzucić zapis z nowymi trybami, dlatego przed downgrade zachowaj kopię danych.

Publiczna beta MIT, bez nowych zależności i migracji SQLite. Małe panele ograniczają miejsce na terminal; można ukryć sidebar albo użyć większego okna. [Instrukcja PL](../getting-started.md), [English](../getting-started.en.md), [weryfikacja funkcji](../reports/four-panel-workspace-2026-10-16.md), [zakres wydania](../reports/release-verification-0.35.0.md), [ograniczenia](../known-issues.md).
