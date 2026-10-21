# Weryfikacja ServerDesk 0.45.1

Data: 2026-10-21. Wyniki końcowych bramek i sumy paczek są dołączane do wydania jako `VERIFICATION.md`.

Zakres zmiany: opcjonalny skrót na pulpicie w Windows Setup, zachowanie jego wyboru przy aktualizacji oraz decyzje powiadomienia o nowej wersji. Schemat bazy i zależności pozostają bez zmian. Testy wykorzystują izolowane dane; nie aktualizują instalacji ani połączeń maintenera.

Celowane testy aktualizacji obejmują PL/EN, czcionkę 16 px i okno 900 px: jednorazowe sprawdzanie przy starcie, respektowanie wyłączenia, przypomnienie bez zapisu, trwałe pominięcie dokładnej wersji po ponownym otwarciu repozytorium, powiadomienie o kolejnej wersji oraz zachowanie połączeń. Dotychczasowe próby pobrania, kopii bazy, anulowania i przekazania instalacji korzystają z syntetycznych odpowiedzi HTTP. Pobrany syntetyczny instalator nie jest wykonywany.

Instalator przechodzi kompilację Unicode i testy świeżej instalacji, aktualizacji z 0.45.0, zajętych plików, naprawy oraz odinstalowania. Dodatkowy scenariusz sprawdza zapis opcji skrótu, zachowanie jej przez cichą aktualizację i jawne wyłączenie. `/NOREGISTER` zachowuje izolację od istniejących skrótów i rejestracji maintenera. Test rzeczywistej rejestracji/skrótów jest pomijany, gdy na komputerze są już dane ServerDesk albo użyto `-SkipRegistration`; raport jawnie podaje zakres.

Testy jednostkowe: **Windows 1035/1035 i Linux 1035/1035**, bez pominięć. Audyt zależności obejmuje pięć projektów bez zgłoszonych podatności/diagnostyki i 23 przypadki bramki audytu. Końcowa weryfikacja obejmuje też bieżącą macierz headless UI, DataProbe, kontrolę źródeł i zawartości paczek. Konkretne wyniki końcowych paczek są w `VERIFICATION.md`; ten opis nie zastępuje wyników bramek.

Publiczny test pobrania po publikacji może potwierdzić metadane 0.45.1 dla klienta 0.45.0 i SHA-256 instalatora. Nie jest to dowód wykonania instalacji z GUI na komputerze maintenera. W 0.45.0 sprawdzanie przy starcie trzeba najpierw włączyć; trzy nowe decyzje są dostępne od 0.45.1. Nie zmieniamy starego taga ani paczek.

Pozostają ograniczenia z [0.45.0](release-verification-0.45.0.md), w tym brak Authenticode, fizyczna macierz RDP/DPI i czysta VM dla rejestracji instalatora. Bieżące sprawdzenia nie zaliczają tych prób.
