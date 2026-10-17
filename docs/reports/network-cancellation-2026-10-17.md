# Network Tools — anulowanie i zamykanie kart, 2026-10-17

Mały etap roboczy po wydaniu 0.39.1. Wersja i zamrożone paczki nie zmieniają się; przygotowywany jest build deweloperski. Bez zmian protokołów sesji, schematu bazy, poświadczeń ani zależności.

Odtworzony błąd: ręczny Start pozostawiał opóźnione automatyczne przeliczenie podsieci, które mogło zastąpić jawny wynik. Stop/Escape nie obejmował tej kolejki. Zamykanie karty śledziło uruchomioną diagnostykę i eksport, ale pomijało inne akcje oraz opóźnione przeliczenie.

Poprawki obejmują anulowanie kolejki przy ręcznym Start/Stop/Escape, wspólne oczekiwanie na trwający Restart i odwołanie go przez późniejszy Stop. Karta śledzi akcje i opóźnione obliczenia. Zamknięcie anuluje własne okna zapisu i czeka na rozpoczęte operacje; odczyt kolekcji wracający mimo anulowania nie wypełnia zamkniętego formularza ani pól sekretów. Odświeżenie zapisanych list publikuje przygotowane dane dopiero po zakończeniu odczytów i sprawdzeniu stanu karty.

`--network-cancellation` używa własnej bazy, pamięciowego magazynu i serwera HTTP na losowym porcie 127.0.0.1. W PL/EN sprawdza zastąpienie debouncu przez Start, Stop/Escape, osiem szybkich zmian celu, zamknięcie przed przeliczeniem, oczekiwanie na blokowany odczyt kolekcji, brak późnych sekretów/zmian UI, anulowanie okna zapisu, powtarzany Restart/Stop oraz 14 rzeczywistych żądań HTTP z zamykaniem i zwolnieniem gniazd. Scenariusz włączono do przyszłej macierzy CI.

Wynik: Release build harnessu Windows i Linux bez ostrzeżeń/błędów; nowy scenariusz przeszedł na obu OS, w każdym po 14 rzeczywistych żądań dla PL i EN (56 łącznie). Na Windows dodatkowo przeszły `--network-tools` i `--mvp-hardening`: wszystkie formularze, współbieżny ICMP loopback, split/floating, automatyczny kalkulator, eksporty, błędy zapisu historii i niepoprawne zapisane WoL. To trzy celowane scenariusze Windows i jeden Linux, bez ponawiania całej macierzy/release. Kontrola repozytorium obejmuje 643 pliki. Wybrane logi są w `artifacts/reports/network-cancellation-2026-10-17`.

To ograniczone próby, bez twierdzenia o wielogodzinnym soak, rzeczywistym WAN/VPN/IPv6, sprzęcie SNMP/WoL i fizycznych gestach. Systemowy picker eksportu nie jest automatyzowany. Operacja systemowa rozpoczęta przed anulowaniem może wymagać czasu na zakończenie; kod czeka na nią zamiast uznawać samo żądanie anulowania za zakończenie.
