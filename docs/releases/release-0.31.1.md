# ServerDesk 0.31.1 — instalator i wygodna zmiana języka

Naprawiono polskie znaki oraz separatory protokołów w instalatorze Windows. Kompilator NSIS odczytuje teraz źródła i dołączane pliki jawnie jako UTF-8, niezależnie od systemowej strony kodowej.

Informacja o wymaganym restarcie pojawia się w języku, na który użytkownik przechodzi: po wyborze English jest angielska, a po wyborze Polski — polska. Dotyczy treści, przycisków, podpowiedzi i potwierdzenia restartu; pozostały interfejs zachowuje bieżący język do ponownego uruchomienia.

Ustawienia oferują „Zapisz i uruchom ponownie”, a przypomnienie w głównym oknie — „Uruchom ponownie”. Zapis przechodzi zwykłą walidację. Restart korzysta z obecnego zamykania sesji, transferów i diagnostyki oraz zapisuje układ. Przy otwartych kartach wymaga potwierdzenia. Anulowanie lub błąd uruchomienia pozostawia sesje działające.

Nowy proces czeka na zakończenie konkretnej poprzedniej instancji, zanim otworzy dane aplikacji. Sprawdza PID wraz z czasem utworzenia, nie odtwarza poprzednich argumentów poleceń i nie pobiera poświadczeń. Karty wracają zgodnie z ustawieniem odtwarzania obszaru pracy i czekają na Połącz. Oczekiwanie na zakończenie poprzednika jest ograniczone do dwóch minut; w razie jego zawieszenia może być potrzebny ręczny start po zamknięciu starej aplikacji.

Publiczna beta MIT, bez nowych zależności i migracji danych. [Weryfikacja](../reports/release-verification-0.31.1.md), [ograniczenia](../known-issues.md).
