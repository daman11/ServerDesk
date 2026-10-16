# ServerDesk 0.22.0 — zaznaczanie grup i zbiorcze transfery SFTP

W obu panelach SFTP można zaznaczyć kilka plików i katalogów. Ctrl+klik dodaje lub usuwa element, Shift+klik wybiera zakres, Ctrl+A zaznacza widoczne wpisy bez katalogu nadrzędnego, a Escape czyści wybór. Skróty dotyczą list plików; pole ścieżki, filtr i terminal zachowują własną obsługę klawiatury. Liczniki pod panelami pokazują zaznaczone i widoczne elementy.

Wyślij oraz Pobierz tworzą zadania dla całej zaznaczonej grupy. Można też przeciągnąć zaznaczenie do drugiego panelu tej samej sesji. Rozpoczęcie przeciągania zachowuje grupę, zwykły klik po puszczeniu przycisku wybiera jeden wiersz, a prawy klik zaznaczonego wiersza pozostawia wybór. Upuszczenie kilku plików z menedżera systemowego korzysta z tego samego planowania transferów.

Wszystkie źródła i cele są ustalane przed dodaniem zadań. Zmiana katalogu, zaznaczenia lub filtra podczas oczekiwania nie przekierowuje transferów. Nadal obowiązują dwa równoczesne transfery w całej aplikacji, indywidualny postęp i anulowanie, anulowanie wszystkich oraz ponawianie do pierwotnego celu. Błąd jednego zadania nie zatrzymuje pozostałych. Nadpisanie nadal wymaga potwierdzenia dla danego pliku.

Odświeżanie i sortowanie zachowują wszystkie widoczne zaznaczone ścieżki, używając aktualnych danych pliku. Filtr usuwa z wyboru niewidoczne wpisy; jego wyczyszczenie nie zaznacza ich ponownie. Oba panele mają niezależne zaznaczenia. Tło wybranych wierszy korzysta z powierzchni motywu o sprawdzonym kontraście.

Zanim rozpocznie się jakiekolwiek zadanie z grupy, aplikacja odrzuca niepoprawne nazwy, jawnie zaznaczone dowiązania oraz powtarzające się cele. Pobieranie na Windows uwzględnia kolizje nazw różniących się wielkością liter. Zmiana nazwy, przeniesienie, szczegóły, chmod i usuwanie nadal dotyczą jednego elementu; menu blokuje je dla grup zamiast wybierać przypadkowy plik. Dowiązania wewnątrz kopiowanego katalogu pozostają pomijane i rozliczane w podsumowaniu.

## Weryfikacja

262/262 testów xUnit. Nowy zakres: deduplikacja i katalog nadrzędny, zachowanie aktualnych metadanych, reguły wielkości liter, utrwalone ścieżki, kolizje celów, dowiązania, niedozwolone nazwy i 10 tys. wpisów.

Test UI obejmuje rzeczywiste Ctrl/Shift/klik i początek przeciągania, menu kontekstowe, Ctrl+A/Escape, filtry/sortowanie, niezależne panele i wirtualizowaną listę 1000 plików. PL/EN, Dark/Light, szerokości 650/1200, tekst 16. Fizyczne gesty zakończonego przeciągania między systemowymi oknami i pełna macierz X11/Wayland pozostają do sprawdzenia.

Rzeczywisty OpenSSH na izolowanym WSL: wysyłanie i pobieranie grupy plików/katalogu, SHA-256, zachowanie wyboru po odświeżeniu, zmiana katalogów przy oczekujących zadaniach, limit dwóch transferów, odmowa nadpisania, anulowanie grupy, ponowienie do pierwotnego celu i niezależne zakończenie po błędzie jednego elementu. Przeszła także wcześniejsza regresja SFTP z aktywnym anulowaniem, potwierdzeniami i limitem historii kolejki.

Szczegółowe wyniki i paczki: `artifacts/release-0220-verification.md`. Podglądy: `artifacts/ui-0220-selection`, `artifacts/ui-0220-batch-network`.

## Pozostały zakres

Wydanie nie dodaje zbiorczego usuwania/przenoszenia, wznowienia pliku od konkretnego bajtu ani trwałej kolejki po restarcie. Utrata sieci i sprzątanie zdalnego `.part` po ponownym połączeniu pozostają osobnym etapem. Pełne MVP i macierz platform nie są jeszcze zakończone; aktualna lista: `docs/roadmap.md`.
