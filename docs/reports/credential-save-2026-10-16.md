# Niepotwierdzony zapis hasła przy łączeniu

Etap roboczy po 0.37.0, 2026-10-16. Zgłoszenie dotyczy Kali/KDE: komunikat Secret Service pojawia się po zaznaczeniu zapamiętania hasła przy łączeniu. Odpowiedź `present` nie ustala, czy problemem jest helper, dostępność usługi czy odblokowanie portfela.

## Zmiana

Dotychczas `SessionSecretAsync` przepuszczał błąd zapisu przez ogólny dialog błędu i po jego zamknięciu zwracał podane hasło do połączenia. Teraz oczekiwany błąd magazynu otwiera osobne potwierdzenie PL/EN:

- **Połącz bez zapisywania** — użycie podanego hasła bez kolejnej próby zapisu;
- **Anuluj**, Escape lub zamknięcie — brak zgody na rozpoczęcie tego połączenia;
- anulowanie przez czas życia okna zamyka potwierdzenie z odmową.

Linux otrzymuje wskazówkę o `secret-tool`, Secret Service i odblokowanym portfelu. Log zawiera tylko typ błędu. Nie zmieniono backendu, formatu, tożsamości poświadczeń ani protokołów. Nie dodano zapisu jawnego, automatycznego ponawiania ani usuwania wcześniejszych wpisów. Nazwa „nie potwierdzono” uwzględnia możliwość błędu dostawcy po częściowo wykonanym zapisie; nie obiecuje, że w portfelu nic nie powstało.

## Celowane sprawdzenia

- **9/9** testów Windows: `LocalizationTests` i istniejący `MissingCredentialBackendFallsBackButSaveFails`.
- `ROOT --credential-save` na Windows i Ubuntu/WSL: PL/EN, rzeczywisty błąd uruchomienia celowo nieistniejącego helpera, kontrolowana odmowa zapisu, anulowanie i jawne użycie jednorazowe. Dokładnie jedna próba zapisu na wybór zapamiętania, brak usuwania/ponawiania, maskowane pole i brak hasła w treści komunikatu. Sprawdzono też poprawny zapis/odczyt w pamięci oraz anulowanie potwierdzenia tokenem.
- Graphite/Light, tekst 16, renderowanie 200% na obu systemach: przyciski pozostają wewnątrz okna; obejrzano zrzuty komunikatu, także wskazówkę Linux.
- `ROOT HOST KEY --credential-save-network` na Windows i Linux, wyłącznie własny OpenSSH na loopback 22222: po błędzie helpera sesja nie rusza przed decyzją, użycie jednorazowe uwierzytelnia SSH i wykonuje znacznik, anulowanie nie otwiera sesji. Test używa jednorazowego klucza/passphrase fixture; nie dotyka systemowego portfela. Serwer zatrzymano po próbach.
- Windows `--stability`: dotychczasowe odzyskiwanie po błędzie odczytu, ręczne użycie bez zapisu, jawne zastąpienie i odczyt zachowane; istniejące dwie próby błędnego/cichego VNC również przeszły.
- Kompilacje celowanych projektów bez ostrzeżeń; nowy scenariusz offline dołączono do przyszłego CI Windows/Linux. Pełnej macierzy i wydania nie uruchamiano dla tej poprawki obsługi komunikatu.

Dowody: `artifacts/reports/credential-save-2026-10-16/` — wybrane logi, TRX i zrzuty, bez baz testowych i kluczy. Instrukcja odtworzenia: [rozwój i testy](../development.md); diagnostyka użytkownika: [Linux/Secret Service](../platform-support.md#błąd-przy-zaznaczonym-zapamiętaniu-hasła).

## Pozostałe ograniczenia

Nie wykonano natywnego testu zapisu na zgłoszonym Kali/KDE ani rzeczywistego zablokowanego portfela. Błąd brakującego helpera i kontrolowana odmowa nie zastępują tych prób. Przyczyna niedostępności magazynu na maszynie maintenera pozostaje nieustalona. Poprawka umożliwia czytelny wybór dalszego postępowania; trwały zapis nadal wymaga działającego Secret Service w sesji użytkownika.
