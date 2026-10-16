# Przerwane transfery i pliki częściowe przed 1.0

Wersja rozwojowa po 0.36.2; bez zmiany wersji, zależności i schematu danych. Trzy punkty stabilizacji: widoczne skutki nieudanego sprzątania plików częściowych, jasna obsługa anulowania/ponowienia oraz filtrowanie/wyszukiwanie ostrzeżeń w Transferach.

SFTP nadal publikuje ukończony plik dopiero po sprawdzeniu długości. Niepotwierdzone zdalne sprzątanie lub błąd usunięcia własnego lokalnego pliku częściowego zapisuje dokładną ścieżkę w wyniku zadania. Sukces ponowienia nie ukrywa ostrzeżenia ze starszej próby. Wiersz i nagłówek kolejki sygnalizują uwagę, a widok Transfery uwzględnia takie ukończone zadanie w filtrze ostrzeżeń. Opisy i ścieżki można wyszukać także podczas ponawiania.

Nie dodano automatycznej pętli reconnect, wznawiania od offsetu ani zbiorczego usuwania starszych `.part`. Przycisk Ponów zaczyna plik od początku i zachowuje dotychczasowe potwierdzenie nadpisania. Zamknięcie karty usuwa jej historię; ostrzeżenie nie jest trwałym rejestrem osieroconych plików i nie sprawdza ponownie ich istnienia po ręcznym usunięciu.

## Wykonana celowana weryfikacja

- 48/48 testów `TransferErrorMessagesTests`, `LocalizationTests`, `TransferTraversalTests`, bez pominięć. Nowe przypadki PL/EN zachowują ścieżkę z Unicode i klamrami, stronę lokalną/zdalną i instrukcję dotyczącą wcześniejszych prób.
- `--sftp-interruption` na własnym OpenSSH w Ubuntu WSL, loopback:22222, z klientem Windows i osobną bazą; PL oraz EN. Żadnych zapisanych serwerów ani poświadczeń użytkownika.
- Zerwanie transportu nadpisującego upload po potwierdzonym postępie: oryginał pozostaje zgodny, sloty wracają, SSH działa, zadanie wskazuje istniejący zdalny `.part`.
- Retry tego zadania z nową zawartością: porównanie SHA-256 nowego celu, zachowanie wcześniejszego pliku częściowego i ostrzeżenia. Osobny plik o podobnej nazwie pozostaje nietknięty.
- Ukończone zadanie z ostrzeżeniem jest widoczne w filtrze uwagi i po wyszukaniu nazwy `.part`. Zrzuty widoku PL/EN obejrzane; pełny opis jest w Transferach i podpowiedzi kolejki. Włączenie Ukryte pokazuje plik, a zwykły podgląd/usuwanie usuwa tylko wskazany plik fixture.
- Anulowanie downloadu po postępie: zachowanie innej lokalnej zawartości, usunięcie lokalnego `.part`, właściwy komunikat i udane ponowienie ze zgodnymi bajtami.
- Cicha utrata połączenia uploadu, następnie Anuluj: około 2,1 s w obu językach, zachowany status Anulowany z ostrzeżeniem, aktywny terminal i dwa wolne sloty; istniejący cel pozostaje zgodny.
- Podmiana aktywnego `.part` na dowiązanie do pliku kontrolnego, następnie anulowanie z działającym transportem: sprzątanie usuwa sam wpis dowiązania, pozostawia jego cel oraz dotychczasowy plik docelowy bez zmian. PASS PL/EN. Sprzątanie używa odczytu rodzica i dokładnego wpisu zamiast kanonikalizującego cel `SftpClient.DeleteFileAsync`.
- Regresje `--transfer-queue-recovery` i `--transfer-overview-network`: PASS. Zwolnienie slotów przed zatrzymanym odświeżaniem, kolejka/anulowanie/reconnect do pierwotnego celu, stabilny fokus kontrolek i widok Transfery przy wąskim panelu, Graphite/Light i czcionce 16.

Dowody lokalne: `artifacts/reports/dev-sftp-interruption/` — TRX, log scenariusza i reprezentatywne zrzuty. Zdalne pliki mają zakres `/tmp/serverdesk-interruption-<GUID>` i są sprzątane; własny serwer jest zatrzymywany po próbach.

Nie wykonywano pełnej macierzy, rzeczywistego WAN, braku miejsca/quota, testu twardego zakończenia aplikacji ani Linux GUI. Lokalny błąd usuwania ma komunikat sprawdzony jednostkowo; nie wymuszano odmowy NTFS/ACL dla rzeczywistego downloadu. Opóźnienie kontrolowanego przekaźnika nie zastępuje pomiaru WAN. SHA-256 to walidacja fixture, a nie nowy zdalny mechanizm weryfikacji każdego transferu. Dostarczany jest przyrostowy build Windows do testów z folderu.
