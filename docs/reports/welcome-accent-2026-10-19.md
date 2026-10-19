# Ikony ekranu startowego i kolor akcentu — 2026-10-19

Mała poprawka UI po 0.43.0, bez zmiany wersji i pakowania wydania.

## Przyczyny i poprawki

- Akcje ekranu startowego dziedziczyły lokalne centrowanie zawartości z ogólnego przycisku. Różne długości opisów przesuwały cały blok poziomo, a ikona była centrowana względem opisu i opcjonalnego skrótu. Zawartość zajmuje teraz szerokość przycisku, a ikony mają wspólną kolumnę i wyrównanie do nagłówka. Płaski szablon respektuje także pionowe wyrównanie zawartości przycisku.
- Zasób `AccentPrimary` nie aktualizował palety Fluent, z której pochodzą odcienie używane przez standardowe kontrolki. Obie palety korzystają teraz z wybranego akcentu, również dla dwóch wariantów motywu systemowego.
- Własny akcent nie zmieniał przygaszonej powierzchni zaznaczenia ani wcześniej utworzonego zasobu obramowania pola z fokusem. Zaznaczenie otrzymuje delikatny odcień wybranego koloru; wyczyszczenie nadpisania przywraca powierzchnię motywu.
- Szablon standardowego przycisku nadpisywał akcent i tekst podczas najechania/naciśnięcia. Stany przycisku głównej akcji zachowują teraz wspólne zasoby koloru i kontrastowego tekstu.
- Zaznaczony tekst oraz znaczniki checkbox/radio i włączonych przełączników korzystają z kontrastowego czarnego lub białego koloru. Własny bardzo jasny akcent nie ukrywa już białego znacznika na białym tle.

Przegląd obejmuje współdzielone zasoby i style, ekran startowy, ustawienia, aktywne moduły oraz podstawowe kontrolki. Kolory statusów, tagów i profili, paleta terminala oraz oryginalne logo pozostają niezależnymi, celowymi kolorami funkcjonalnymi lub grafiką marki.

## Celowana weryfikacja

Harness `--welcome-accent` korzysta wyłącznie z własnych katalogów i bazy pod `artifacts/reports/welcome-accent-2026-10-19/final/`:

- 24 układy ekranu startowego: PL/EN, Graphite/Light, font 13/16, okna 1320/900 px i wąski obszar roboczy przy szerszym panelu połączeń. Sprawdza współrzędne ikon i nagłówków, jednakowe wcięcia oraz rzeczywiste przejście między jedną a dwiema kolumnami.
- 55 kombinacji palet: 11 motywów, akcent motywu oraz własne fioletowy, zielony, biały i czarny. Sprawdza zasoby Fluent i aplikacji, checkbox, toggle, radio, slider, zaznaczenie tekstu, pasek postępu i obramowanie pola z fokusem.
- Standardowe i płaskie główne przyciski: stan zwykły, rzeczywiste zdarzenie najechania kursorem i naciśnięcie klawisza Space; kontrast tekstu co najmniej 4,5:1. Także kontrast zaznaczonego tekstu i znaczników radio/toggle.
- Rzeczywisty dialog ustawień: podgląd własnego akcentu, zmiana motywu, Anuluj i odtworzenie palety, zapis, ponowne otwarcie, wyczyszczenie nadpisania i anulowanie tej zmiany; zgodność obu wariantów System.

Osobny istniejący harness `--design-refresh` sprawdza regresje ekranu startowego, ustawień i paneli plików w PL/EN. Dowody są w podkatalogu `design-regression/`. Wybrane zrzuty ekranu są oceniane wizualnie; testy nie używają zapisanych połączeń ani magazynu haseł użytkownika.

Wynik: **PASS** obu harnessów; kompilacja Release bez ostrzeżeń i błędów. **PASS** `scripts/check-repository.ps1` (778 plików) i `git diff --check`.

To weryfikacja renderowania Avalonia headless na Windows, bez pełnej macierzy protokołów, natywnej akceptacji Linuxa ani fizycznych monitorów o mieszanym DPI. Build deweloperski: `artifacts/dev-welcome-accent/ServerDesk.App.exe`.

## Źródła techniczne

Zgodność kluczy i punktów rozszerzenia sprawdzono dla używanej wersji Avalonia 12.0.4: [Fluent ColorPaletteResources](https://docs.avaloniaui.net/api/avalonia/themes/fluent/colorpaletteresources), [stany szablonu Button](https://github.com/AvaloniaUI/Avalonia/blob/12.0.4/src/Avalonia.Themes.Fluent/Controls/Button.xaml), [zasoby kontrolek Fluent](https://github.com/AvaloniaUI/Avalonia/blob/12.0.4/src/Avalonia.Themes.Fluent/Accents/FluentControlResources.xaml).
