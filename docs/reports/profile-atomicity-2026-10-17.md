# Atomowy zapis profili i folderów — 2026-10-17

Mała zmiana robocza po 0.39.0 domyka trzy konkretne operacje w obszarze danych pierwotnego MVP. Nie zmienia schematu bazy ani magazynu poświadczeń i nie stanowi nowego wydania.

## Poprawione zachowanie

1. **Zapis serwera i tworzenie folderów.** Nowy lub edytowany profil oraz brakujący łańcuch folderów zapisują się w jednej transakcji SQLite. Dotyczy to także samodzielnego tworzenia zagnieżdżonego folderu. Błąd kolejnego zapisu cofa poprzednie kroki tej operacji.
2. **Przenoszenie serwera.** Utworzenie brakujących folderów docelowych i zmiana przypisania profilu mają wspólną transakcję. Błąd tworzenia folderu lub aktualizacji profilu zachowuje źródłowe przypisanie oraz dotychczasowe metadane, bez pozostawiania nowej części drzewa.
3. **Zmiana nazwy drzewa folderów.** Dotychczasowa transakcja obejmuje teraz także tworzenie brakujących folderów nadrzędnych celu. Błąd tego ostatniego kroku cofa zmiany nazw, ikon i przypisań profili. Wcześniej operacja mogła zgłosić błąd już po zatwierdzeniu zmiany nazw.

## Weryfikacja

- Siedem nowych przypadków używa własnych tymczasowych baz i kontrolowanych błędów SQLite `RAISE(ABORT)` w wybranym zapisie. Przed poprawką sześć wykazywało częściowy zapis; przypadek błędu aktualizacji profilu wewnątrz dotychczasowej transakcji zmiany nazwy już zachowywał poprawny stan.
- Testy porównują pełne metadane profili, listy folderów i ikony przed operacją i po błędzie. Po usunięciu przyczyny błędu ponowienie każdej operacji kończy się poprawnym stanem; osobno sprawdzane jest przeniesienie profilu do głównego poziomu.
- Celowany zestaw Windows: **38/38 PASS**, bez pominięć. Obejmuje nowe przypadki, istniejące testy repozytorium, ikon folderów oraz kopii bazy i ich katalogu.
- Regresja UI drzewa połączeń: **PASS w PL/EN**, na odizolowanych danych. Sprawdza dostępność pustych folderów, lokalizowane menu z ikonami, zachowanie zaznaczenia przy sortowaniu i odświeżeniu, filtry i blokadę otwarcia ukrytego profilu oraz zapis sortowania i zwinięcia folderów po restarcie. Nie wykonuje połączeń sieciowych ani fizycznego przeciągania.
- Szczegółowe logi i wyniki TRX pozostają lokalnie w `artifacts/reports/profile-atomicity-2026-10-17/`, poza repozytorium.

To sprawdzenie granic transakcji przy rzeczywistym błędzie SQLite, a nie fizycznego zapełnienia dysku, odcięcia zasilania czy awarii procesu. Nie zamyka szerszej macierzy przerwanych zapisów, blokad, migracji historycznych danych ani rzeczywistych gestów drag and drop. Pozostały zakres opisuje [plan 1.0](../stable-release-plan.md).
