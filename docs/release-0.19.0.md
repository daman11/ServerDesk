# ServerDesk 0.19.0 — anulowanie połączeń i wspólna historia

Przycisk **Anuluj łączenie** przerywa bieżącą próbę SSH/VNC, sprawdzanie dostępności RDP oraz oczekiwanie na gotowość jego kontrolki. W osadzonym RDP można również anulować trwające logowanie. Anulowana karta wraca do stanu gotowości bez komunikatu błędu; zamknięcie czeka na zakończenie sprzątania transportu. Anulowanie potwierdzenia klucza SSH zamyka jego okno bez zaakceptowania klucza. Natywne otwarcie Serial zależy od sterownika: anulowanie jest sprawdzane przed i po jego zakończeniu, bez deklaracji natychmiastowego przerwania sterownika.

**Historia połączeń** obejmuje otwarte sesje SSH, VNC i Serial oraz potwierdzone logowanie osadzonego RDP Windows. Uruchomienie zewnętrznego klienta RDP nie potwierdza logowania i nie tworzy wpisu. Nieudane i anulowane próby nie tworzą wpisów. Jedna udana próba daje jeden wpis; zdarzenia automatycznego przywrócenia sieci RDP go nie powielają.

Nowy wpis zawiera nazwę, protokół, adres/port lub urządzenie i login z chwili połączenia, bez hasła, ścieżki klucza ani treści sesji. Zmiana nazwy, grafiki lub folderu nie zmienia zapisanego wpisu. Zmiana celu/loginu/protokołu blokuje Połącz w dawnym wpisie, a usunięcie profilu zachowuje samą historię. Stara działająca sesja nie aktualizuje daty ostatniego połączenia profilu, który wskazuje już inny cel. Edycja celu w aplikacji zeruje poprzednią datę.

Okno historii ma wyszukiwanie po nazwie, celu i użytkowniku, filtr Wszystkie/SSH/RDP/VNC/Serial oraz strony po 100 wpisów. Kilka słów musi wystąpić jednocześnie, wielkość liter nie ma znaczenia także dla polskich znaków. Filtry obejmują całą bazę przed stronicowaniem. Wpisy pochodzące ze starszych wersji bez zapamiętanego celu korzystają z obecnych danych profilu i pokazują tę informację po zaznaczeniu. Okno jest skalowalne, lista przewijana, a przyciski pozostają przy dolnej krawędzi.

SQLite przechodzi transakcyjnie do schematu 3 z opcjonalnym polem snapshot w historii. Migracja zachowuje stare wpisy i profile; ponowne uruchomienie jest bezpieczne. Starszy ServerDesk może odrzucić tę bazę jako pochodzącą z nowszego wydania. Eksport konfiguracji zachowuje dotychczasowy format i nie zawiera historii.

## Weryfikacja

226/226 xUnit: wszystkie dotychczasowe testy oraz utrwalenie czterech protokołów, historyczny cel po zmianie/usunięciu profilu, zgodność tożsamości, Unicode/wiele słów, filtrowanie przed stronicowaniem, anulowanie zapisu, migracja v2 i ochrona nowszego schematu.

Nowy scenariusz interfejsu używa rzeczywistych TCP na loopback dla wstrzymanego handshake SSH/VNC i adaptera czekającego na anulowanie dla sprawdzania RDP. Sprawdza przycisk anulowania, token zewnętrzny, brak wpisu/błędu, zamknięcie i anulowanie potwierdzenia klucza. Udane RFB 3.8 potwierdza rzeczywisty zapis VNC. Test historii obejmuje filtr, Unicode, szybkie zmiany filtrów, zmianę celu, bezpieczne Połącz i zamknięcie podczas opóźnionego wyszukiwania. Obrazy Dark/Light: 600/780 × 460, tekst 16. Nie jest to ponowny test rzeczywistego logowania do zdalnego Windows; zapis RDP jest podpięty do istniejącego zdarzenia OnLoginComplete.

Wyniki: `artifacts/mvp-0190-tests.txt`, `artifacts/mvp-0190-connection-control.txt`, `artifacts/mvp-0190-ui-regression.txt`. Zakres pozostałych testów i paczek: `artifacts/release-0190-verification.md`.

Fizyczny Serial/USB, konkretne serwery VNC, konta AD/Microsoft, mieszane DPI oraz szersza macierz systemów pozostają do weryfikacji. Szczegóły wcześniejszych funkcji i ograniczenia VNC: [release-0.18.0.md](release-0.18.0.md), [rdp-test-matrix.md](rdp-test-matrix.md).
