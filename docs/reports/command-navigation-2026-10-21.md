# Komendy: nawigacja szczegółów i formularz ulubionych — 2026-10-21

Mały krok rozwojowy po 0.44.0. Bez zmiany wersji, releasu, zależności, schematu bazy, formatu biblioteki i zasad wysyłania komend.

## Zachowanie

Szczegóły pokazują **Poprzednia komenda / Następna komenda**, licznik pozycji i skróty **Alt+← / Alt+→**. Porządek pochodzi z aktualnej listy po zapytaniu, źródle, kategorii, ryzyku i sortowaniu. Na krańcach strzałka jest niedostępna; nie ma przejścia cyklicznego. Samo przejście otwiera opis/formularz, bez kopiowania, wysyłania lub wpisu historii. Każda inna komenda zaczyna swój formularz; wartości robocze nie są zapamiętywane między komendami. Nawigacja jest blokowana podczas akcji dodatku, po jego wyłączeniu lub zamknięciu; Ctrl+Alt nie jest traktowane jako Alt.

Zmiana ulubionych wcześniej ustawiała ponownie `ItemsSource`, a przejściowe `SelectionChanged` czyściło formularz. Programowa przebudowa listy pomija te pośrednie zdarzenia i jawnie ustawia końcową selekcję. Po gwiazdce można zachować istniejący formularz, podgląd i zgodę Code wyłącznie przy tej samej definicji oraz niezmienionej rewizji kontekstu. Zniknięcie wpisu z Ulubionych czy zmiana kontekstu zachowuje normalne czyszczenie/nową walidację. Nie zmieniono potwierdzeń, kontroli celu, copy-only dla sekretów/skryptów ani przechowywania danych.

Dodano ikony, nazwy dostępności, podpowiedzi i tłumaczenia. Nawigacja jest nad przewijanymi szczegółami; kontekst systemu pozostaje w osobnym wierszu. Uaktualniono Pomoc offline i [instrukcję](../commands.md).

## Lokalne sprawdzenie Windows

- Przyrostowy build Windows x64 self-contained aplikacji i UiSmoke: **PASS**, bez ostrzeżeń/błędów.
- **71/71 PASS**, bez pominiętych, filtrem `FullyQualifiedName~CommandEngineTests|FullyQualifiedName~LocalizationTests`: walidacja/quoting, ograniczenia wysyłki, katalog i tłumaczenia.
- **UiSmoke `--commands`: PASS PL/EN** — dotychczasowe karty, filtry/ryzyko i puste stany, klawiatura wyszukiwania, edytor i kategorie, kopiowanie offline, ulubione, pięć motywów, układ 920×620 przy czcionkach 13/16 i izolacja RDP/Serial. Scenariusz obejmuje też nową nawigację.
- **Końcowe `--command-navigation`: PASS PL/EN** — filtrowana lista trzech własnych szablonów, porządek A–Z i Z–A, licznik/nazwy dostępności, przyciski i Alt+strzałki, granice, brak przejęcia Ctrl+Alt, zachowanie formularza/podglądu/zgody Code po gwiazdce, trwały zapis ulubionego, czyszczenie parametrów/zgody po zmianie kontekstu i powrót do pustej listy po usunięciu ostatniego ulubionego.
- Schowek testowy i pusta historia pozostają bez zmian podczas przeglądania; nie pojawia się potwierdzenie wykonania. Nie zestawiano SSH i nie używano zapisanych poświadczeń. Własna biblioteka fixture'a korzysta z pamięciowego `ICredentialStore` i osobnej bazy.
- Obejrzano zrzuty PL/EN z kompaktowego panelu, w tym większą czcionkę. Kontrola treści/linków repozytorium i `git diff --check`: **PASS**.

Początkowy build narzędzia testowego wymagał importu rozszerzeń schowka Avalonia. Dodatkowa próba kontekstu szukała pola wewnątrz zwiniętego Expandera; poprawiono test, aby najpierw wracał do listy i otwierał profil systemu tak jak użytkownik. Obie próby nie są liczone jako PASS; końcowe testy przechodzą.

Wybrane TRX, logi i zrzuty: `artifacts/reports/command-navigation-2026-10-21/`. Odtwarzalne dane i buildy robocze: `artifacts/command-navigation-work/`. Build dla maintenera: `artifacts/dev-harness/ServerDesk.App.exe`.

## Granice

Bez pełnej macierzy wydania, nowych prób Linux/VM, fizycznego AltGr/IME i wysyłania poleceń do rzeczywistego serwera. Syntetyczne Ctrl+Alt nie jest akceptacją fizycznej klawiatury. Nie dodano zapamiętywania wartości parametrów, pobierania bibliotek ani zewnętrznego SDK pluginów. Pozostałe ograniczenia [Komend](../commands.md) i [planu 1.0](../stable-release-plan.md) pozostają aktualne.
