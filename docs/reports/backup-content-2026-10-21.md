# Kontrola zawartości kopii bazy — 2026-10-21

Mały krok MVP po 0.44.0, dotyczący ochrony danych i przygotowania do odtworzenia. Bez migracji, zmiany wersji, nowych zależności, releasu ani zmian w cyklu życia sesji i poświadczeń.

## Przyczyna i zmiana

Dotychczasowy przycisk **Sprawdź kopię / Check backup** sprawdzał `quick_check(1)`, numer schematu i metadane pliku. Poprawność SQLite nie oznaczała obecności tabel ServerDesk ani poprawności JSON połączeń. Przed poprawką dziesięć nowych regresji potwierdziło, że brak tabeli/kolumny lub widok zamiast `profiles` otrzymywały pozytywny wynik.

Kontrola wybranej kopii dodatkowo wymaga zwykłych tabel i kolumn aplikacji oraz sprawdza wszystkie profile: tekstowy JSON, jednoznaczne pola, niepusty identyfikator zgodny z kluczem wiersza, brak powtórzonych tożsamości i dotychczasowe `ServerProfile.Validate()`. Nie otwiera plików kluczy i portów, nie łączy się z serwerem i nie odczytuje systemowego magazynu sekretów. Komunikat o błędzie zawiera numer odczytanego wiersza, bez nazwy, adresu, notatek ani JSON profilu.

Schematy 0–4 pozostają obsługiwane. Starsze schematy mogą nie mieć `folder_icons` i `history.snapshot`; pominięte późniejsze pola profilu zachowują wartości domyślne, nieznane pola nie są usuwane. Nie dodano migracji ani zapisu do kopii. Jeden rekord JSON ma budżet kontroli 32 MiB; większy rekord zatrzymuje kontrolę bez usuwania lub skracania danych. Anulowanie jest sprawdzane między wierszami i polami JSON, a wywołania SQLite pozostają synchroniczne na workerze.

Zmieniono opis i wynik kontroli w ustawieniach PL/EN oraz Pomoc offline. Tworzenie kopii nadal wykonuje swoją dotychczasową kontrolę SQLite; rozszerzona kontrola zawartości jest uruchamiana jawnym **Sprawdź kopię**. Nie zmieniono plików ani paczek wydania 0.44.0.

## Weryfikacja lokalna Windows

- Przed poprawką: **10/10 regresji wykazało lukę**, ponieważ oczekiwane odrzucenie nie nastąpiło. Pierwsze uruchomienie w izolacji nie połączyło runnera z testhost i nie wykonało żadnego testu; nie zaliczono go jako wyniku testów. Powtórka poza izolacją wykonała wszystkie dziesięć przypadków na poprzednim buildzie.
- Po poprawce: **85/85 PASS**, bez pominiętych, filtrem `FullyQualifiedName~DatabaseBackup|FullyQualifiedName~LocalizationTests`. W tej liczbie **40 nowych przypadków** sprawdzających zawartość kopii.
- Nowe przypadki: brak sześciu wymaganych tabel, brak kolumn, widok zamiast tabeli, niepoprawny/null/tablicowy JSON, brak/pusty/niezgodny identyfikator, błędne host/port/folder/grafika/opcje, null nazwy, powtórzone pola także wewnątrz opcji, binarny JSON, niepoprawny/null klucz wiersza, powtórzona tożsamość w różnych zapisach GUID, przekroczony budżet i niepoprawny rekord po prawidłowym.
- Zgodność: pięć historycznych schematów z dawnym JSON/nieznanym polem oraz cztery protokoły, bez dostępu do nieistniejących kluczy/urządzeń. Testy porównują bajty kopii i listę plików przed/po; przy odrzuceniu również potwierdzają zachowanie żywego profilu i ustawienia fixture'a.
- Celowany UiSmoke **`--database-backups`: PASS PL/EN** — pozytywny wynik, odrzucenie niepoprawnego profilu i brakującej tabeli, uszkodzony/znikający plik, odświeżenie, kopiowanie ścieżki i zamykanie. Obejrzano zrzuty obu nowych rodzajów komunikatów.
- Celowany UiSmoke **`--help`: PASS PL/EN**, 14 tematów, wyszukiwanie, układ, nawigacja i dokowanie.
- Przyrostowy build aplikacji/harnessu Windows x64 self-contained: bez ostrzeżeń i błędów. Build dla maintenera: `artifacts/dev-harness/ServerDesk.App.exe`.

Wybrane TRX/logi i obrazy: `artifacts/reports/backup-content-2026-10-21/`. Bazy są wyłącznie syntetyczne; nie korzystano z kopii, połączeń ani sekretów maintenera.

## Granice

Nie jest to pełny test odtworzenia ani przenośny backup sekretów. Kontrola nie potwierdza treści wszystkich ustawień, historii, zaufania SSH, map ikon ani odnośników pluginów i ich dostępności w keyringu. Nie sprawdzono nowych konfiguracji Linux/sprzętu, pełnej macierzy protokołów ani wielogodzinnej pracy; brak nowych prób VMware i brak releasu. Wynik dotyczy wybranego pliku w chwili kontroli, nie późniejszych zmian lub niekooperujących równoległych zapisów.

[Instrukcja kopii i odtwarzania](../database-backups.md), [plan stabilnej wersji](../stable-release-plan.md).
