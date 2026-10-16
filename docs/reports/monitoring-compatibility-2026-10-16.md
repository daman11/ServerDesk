# Monitoring: zgodność odczytu sieci i liczników CPU

Etap roboczy po 0.37.0, 2026-10-16. Nie zmienia wersji, schematu danych, zależności ani zamrożonych paczek wydania.

## Zmiany

- Gdy `ip -json` zwróci błąd, monitoring próbuje `ip -o address show`, `ip -o link show` i tekstowych tras domyślnych. Zachowuje wiele adresów IPv4/IPv6, nazwę interfejsu bez sufiksu peer, stan i adres sprzętowy, także loopback. Nie wymaga instalowania nowego narzędzia przez użytkownika. Przy całkowitym braku `ip` dane interfejsów nadal są oznaczone jako niedostępne; pozostałe sekcje działają.
- Trasy domyślne IPv4 i IPv6 są odczytywane osobno, z niezależnym fallbackiem. Widok Monitoring pokazuje obie dostępne bramy i usuwa powtórzenia. Niedostępna rodzina nie usuwa danych drugiej.
- Niespójny przyrost liczników CPU, np. przyrost idle większy od całego przyrostu, daje brak procentu zamiast wymuszonego 0%. Kolejna poprawna para przywraca pomiar; prawdziwe 0% i 100% pozostają poprawne.

## Wykonane sprawdzenia

- **56/56 celowanych testów xUnit na Windows**, zero pominiętych: `MonitoringCompatibilityTests`, `MonitoringSampleTests`, `LinuxDetailsTests`, `NetworkTrafficTests`, `CoreTests`. Nowa regresja obejmuje obie rodziny adresów/bram, tekstowe linki Ethernet/loopback, interfejs bez adresu, odrzucanie niepoprawnych prefiksów/wierszy i powrót CPU po niespójnej próbce.
- **Rzeczywisty OpenSSH w izolowanym Ubuntu/WSL**, aplikacja/harness na Windows: własny wrapper `ip` odrzuca opcje JSON i deleguje pozostałe do rzeczywistego narzędzia. Wynik fallbacku zgadza się z odczytem JSON dla wszystkich interfejsów, adresów, stanów, adresów sprzętowych i dostępnych tras tego fixture.
- Zachowana regresja braku `df/ip/lscpu` oraz oddzielnej odmowy odczytu CPU i RAM. Inne dostępne sekcje pozostają widoczne.
- Widok i pasek parametrów: **PL/EN, dark/light, 650/900 px**; komunikat częściowej dostępności mieści się w oknie, brak RAM nie wygląda jak zerowe użycie, odzyskane dane przywracają wskaźniki. Sprawdzono też przekazanie danych tekstowego fallbacku do widoku sieci oraz czyszczenie po zamknięciu.
- Zerwanie tylko transportu monitoringu czyści stare wartości, pozostawia terminal SSH połączony i przywraca próbki po istniejącym backoff.
- Kompilacja aplikacji/harnessu bez ostrzeżeń; kontrola zawartości repozytorium i diff. Bez pełnej macierzy oraz nowych instalatorów zgodnie z cyklem małych zmian.

Wyniki lokalne: `artifacts/reports/monitoring-compatibility-2026-10-16/` — TRX, log akceptacji i wybrane zrzuty. Dane i katalogi testowe są odrębne od zapisanych połączeń użytkownika.

## Granice

Wrapper bieżącego iproute2 symuluje brak opcji JSON; nie jest rzeczywistą instalacją każdej starszej wersji. Fallback zakłada format jednowierszowy iproute2; inne implementacje, w tym BusyBox, wymagają osobnej akceptacji. Własny serwer nie ma domyślnej trasy IPv6: obie rodziny tras i niezależny fallback sprawdzono danymi parsera, bez deklarowania testu rzeczywistego routingu IPv6. Brak rozszerzenia na trasy policy/multipath, fizyczne VPN i inne dystrybucje. Nie przeprowadzono nowych wielogodzinnych pomiarów ani pełnej macierzy platform.
