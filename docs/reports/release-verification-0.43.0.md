# ServerDesk 0.43.0 — zakres weryfikacji, 2026-10-18–19

Public Beta. Etap obejmuje odporność zapisu danych i odzyskiwanie SSH/SFTP; nie zalicza odłożonych testów VMware/sprzętu ani wszystkich warunków 1.0. Końcowy commit i wyniki paczek są zapisane w `VERIFICATION.md` w folderze wydania. Dowody: `artifacts/reports/stability-0.43.0/`.

## Poprawione przyczyny

- Usuwanie hasła przed zapisem zmienionego/usuwanego profilu mogło utracić poświadczenie przy odmowie zapisu SQLite. Nowe powiązanie jest zatwierdzane razem z edycją profilu; usuwanie profilu jest zatwierdzane przed sprzątaniem magazynu. Testy błędów SQL sprawdzają zachowanie hasła, a zablokowany magazyn nie daje nowemu celowi dostępu do starego hasła.
- Semafory osobnych instancji pluginów nie chroniły wspólnego indeksu Network Tools ani biblioteki Komend. Mutacje indeksów mają transakcję SQLite; biblioteka ma kontrolę wersji odczytu i atomową podmianę odnośnika. Próby obejmują równoczesne okna, błąd/anulowanie po części chunków i udane ponowienie.
- Zwykły SSH keepalive nie wymaga odpowiedzi, a upload czekający na ACK może nie zgłosić cichej utraty sieci. Kontrola cichego SSH i licznik postępu SFTP ograniczają te oczekiwania. Obsługa awarii zachowuje kontekst SFTP i rozdziela błąd transportu od anulowania użytkownika.
- Schemat 4 blokuje starsze klienty, które ignorowałyby nowe pole powiązania poświadczeń. Migracja tworzy kopię i zachowuje oryginalne JSON-y oraz legacy referencje. Import/duplikowanie/eksport nie dziedziczą lokalnego powiązania.

## Wykonane kontrole robocze

- Windows/Linux: pełne zestawy po **893 testy jednostkowe**, bez błędów. Wyniki końcowych paczek i ich commit podaje `VERIFICATION.md`.
- Rzeczywisty własny OpenSSH i izolowany relay: cichy upload/download, zachowanie oryginałów, lokalne sprzątanie i zdalne ostrzeżenie `.part`; krótka pauza z poprawnym SHA-256; utrata SSH podczas transferu, jawny reconnect z zachowaniem panelu/katalogów/filtrów/zaznaczenia/historii oraz retry z SHA-256; produkcyjny watchdog przy symulowanym wieku połączenia i zamknięcie cichego odczytu katalogu.
- Testy UI edycji/usuwania profili obejmują błąd bazy, zablokowany plik DPAPI, niedostępny magazyn, rzeczywisty odczyt po nowym procesie i zachowanie innych profili/plików.
- Dotychczasowe testy resetu fabrycznego i diagnostyki SSH pozostają częścią regresji.
- Rzeczywista blokada SQLite nie blokuje wywołującego wątku edytora; błąd zachowuje profil i pozwala ponowić. Na Linuxie wykonano rzeczywistą odmowę zapisu w katalogu bez prawa zapisu. DataProbe: osiem prób na każdym systemie (schematy 0–3, zakończenie procesu przy eksporcie i trzy transakcje drzewa), a osobno rzeczywisty ENOSPC/SQLITE_FULL na tmpfs 8 MiB z udanym retry.
- Cztery równoległe sesje, 16 reconnect, EOF, split/floating/redock i sprzątanie browsera/monitoringu zaliczone na Windows/Linux. Wszystkie 16 poprzednich źródeł anulowania zebrano; cichy link zamknięto i zwolniono oba sloty transferów. Usunięcie karty: 29 ms Windows / 9 ms Linux; pełne sprzątanie około 2,1 s. Są to ograniczone próby, bez deklaracji wielogodzinnej stabilności.
- Test cichego katalogu działa bez zamykania programu. Zajęty kanał Komend nie powoduje fałszywego alarmu watchdoga; heartbeat nie wstawia niczego do PTY ani nie powtarza poleceń użytkownika.
- Macierz UI: 43 scenariusze Windows i 44 Linux, wszystkie zaliczone. Wykryła regresję fokusu zachowanej listy SFTP; poprawiono dostęp do odczytu cache, zachowując blokady operacji zdalnych. Zaktualizowano także historyczne oczekiwanie schematu 3 w teście podglądu kopii. W teście anulowania Network Tools usunięto zależność od szybkości renderowania względem debounce; Escape i zamknięcie sprawdzają konkretną zakolejkowaną operację. Powtórzono nieudane i pozostałe scenariusze.
- Audyt NuGet: cztery projekty, bez advisory/diagnostyki; 23 fixture bramki audytu zaliczone. Natywny host RDP bez logowania i natywne położenie/start okna Windows zaliczone. Wstępny Setup przeszedł aktualizację z 0.42.0, naprawę i deinstalację w izolacji; rejestracja pominięta. Końcowe paczki są odbudowywane z czystego commita i sprawdzane ponownie przed tagiem.

## Granice

Próby używają wyłącznie własnych katalogów i serwera testowego. Nie ingerują w zapisane połączenia maintenera ani sieć systemową. Utrata ACK przy końcowym zatwierdzeniu może oznaczać niepotwierdzony wynik; brak automatycznego replay, ponowienie wymaga decyzji użytkownika. Plik `.part` może pozostać przy niedostępnym serwerze. Kopia bazy nie zawiera osobnego magazynu sekretów. Zabezpieczone osierocone wpisy po błędzie sprzątania mogą pozostać w DPAPI/Secret Service.

Nie wykonano fizycznego uśpienia komputera, natywnej akceptacji Kali/KDE/KWallet, czystej VM z rejestracją instalatora, pełnej macierzy protokołów/sprzętu ani wielogodzinnego pilotażu. Publikacja zdalna, publiczne CI i podpis Windows nie są zaliczone.
