# Przeglądarka połączeń: 5000 profili i puste foldery

Etap roboczy po 0.37.0, 2026-10-16. Mała zmiana interfejsu bez zmiany wersji, zależności i danych; zamrożone wydania pozostają bez zmian.

## Zmiany

- Szablon wiersza serwera powstaje przy prezentowaniu go przez Avalonia, zamiast tworzyć cały zestaw kontrolek także wewnątrz zwiniętych folderów. Element drzewa zachowuje tożsamość, zaznaczenie i obsługę przeciągania. Stan połączenia jest odczytywany ze wspólnego zbioru identyfikatorów zamiast przeszukiwania sesji dla każdego profilu.
- Akcje menu folderu powstają dopiero przy jego otwarciu. Wspólny mechanizm obsługuje foldery i serwery, także programowe otwarcie menu, bez powielania pozycji przy ponownym otwieraniu.
- Komunikat pustej listy pojawia się dopiero, gdy drzewo nie zawiera również folderów. Użytkownik może pracować z pustymi folderami, a wyszukiwanie bez wyników nadal pokazuje właściwą podpowiedź.

## Pomiary i celowane sprawdzenia

Windows, własna baza z 5000 importowanymi profilami i 200 folderami. Początkowo zwinięte 100 folderów głównych. Przed i po zmianie uruchomiono osobny proces tego samego harnessu; pomiar dotyczy jednego kolejnego wywołania `BuildTree` po otwarciu okna.

| Pomiar | Przed | Po |
|---|---:|---:|
| Przebudowa drzewa na wątku UI | 1088,84 ms | 346,90 ms |
| Bajty przydzielone na wątku UI | 311,28 MiB | 100,54 MiB |

W tym porównaniu czas i przydziały spadły o około 68%. To lokalny pojedynczy pomiar konkretnej operacji, bez czasu późniejszego renderowania; nie pomiar RSS całej aplikacji ani gwarancja takich samych czasów na innym komputerze. Koszt przygotowania widocznego wiersza jest odroczony, a nie usunięty.

Przeszły **cztery celowane scenariusze UI**:

1. `--large-browser`: import 5000 profili, 200 folderów, wyszukiwanie wielowyrazowe/puste/wyczyszczenie, filtr SSH/RDP, sortowanie adresów IP w folderach, ikony rozwiniętych wierszy, zachowanie pasującego zaznaczenia i blokada szybkiego Enter po ukryciu zaznaczonego profilu. Poprzedni test oczekujący odtworzenia ukrytego profilu dostosowano do zachowania wprowadzonego w 0.37.0.
2. `--connection-browser`: PL/EN, dostęp do pustych folderów i podpowiedź wyszukiwania, dwukrotne otwarcie menu z czterema właściwymi akcjami/ikonami, zaznaczenie po sort/reload, filtry, tymczasowe rozwijanie wyszukiwania i odtworzenie stanu po restarcie; Graphite/Light, 900 px, czcionka 16.
3. `--sorting`: nazwy/foldery/adresy, oba kierunki, ostatnie/ulubione, zachowanie zaznaczenia, szybkie zmiany i zapis/restarter przy blokadzie zapisu bazy; osobne sortowanie lokalnych/zdalnych list SFTP, PL/EN i kompaktowy układ.
4. `--workspace-details`: menu oraz wybór, zapis i anulowanie ikony folderu, pojedynczy pasek tytułowy i kompaktowy pasek parametrów.

Kompilacja aplikacji/harnessu bez ostrzeżeń; kontrola zawartości repozytorium i diff. Testy nie korzystają z serwerów, danych ani poświadczeń użytkownika. Wybrane logi, pomiary JSON i zrzuty zachowano w `artifacts/reports/browser-scalability-2026-10-16/`.

## Uruchamianie

Po zbudowaniu UiSmoke: `ROOT --large-browser`. Opcjonalne `--measure-only` kończy po pomiarze bez dalszej macierzy interakcji. `ROOT` musi wskazywać własny, jednorazowy katalog danych testowych. Bieżący harness wymaga późniejszego ręcznego posprzątania tego katalogu.

## Pozostały zakres

Nie wykonano w tym etapie nowej macierzy Linux, testu 10 000 profili, bardzo głębokich folderów, wielu importowanych grafik, fizycznego drag and drop, czytnika ekranu ani wielogodzinnych pomiarów CPU/pamięci/uchwytów. Model i przebudowa drzewa nadal pracują na UI; nie jest to pełna wirtualizacja zbioru profili. Dalsze bramki stabilnego wydania opisuje [plan 1.0](../stable-release-plan.md).
