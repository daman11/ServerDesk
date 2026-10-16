# ServerDesk 0.25.0 — weryfikacja 2026-10-15

## Zakres

Dziewięć narzędzi Network Tools z rzeczywistymi operacjami: Ping, Traceroute/MTR, DNS Lookup, TCP Port Scanner, Wake-on-LAN, HTTP Client, SSL/TLS Inspector, Subnet Calculator i SNMP Browser v2c/v3. Dostęp bez zapisanych serwerów i z edytowalnego menu kontekstowego. Wspólne karty/split/dokowanie, historia, kolekcje w systemowym magazynie, eksport oraz PL/EN. Szczegóły funkcji, plików i ograniczeń: `docs/network-tools.md`.

## Wyniki

- **Windows: 423/423 testy rozwiązania**, zero pominiętych; kompilacja i narzędzie UI: zero błędów i ostrzeżeń. 66 nowych przypadków backendu Network Tools obejmuje również deterministyczne integracje na własnych endpointach loopback.
- **Linux: 66/66 tych samych przypadków backendu** przez self-contained UiSmoke `--network-backend` na Ubuntu 24.04.4/WSL, jako zwykły użytkownik. Harness linkuje dokładnie dwa nowe pliki testowe i ich asercje xUnit; nie zastępuje testów atrapami i nie wymaga SDK na hoście wykonującym testy. Zależności testowe należą wyłącznie do narzędzia developerskiego, nie aplikacji.
- Backend na obu systemach: IPv4 /24,/30,/31,/32, maski/wildcard/podział, IPv6 i BigInteger, walidacja hostów/portów/MAC, limity i CSV; historia bez sekretów, oddzielenie kolekcji od jawnej bazy i brak jawnego fallbacku przy niedostępnym magazynie.
- ICMP: rzeczywisty loopback IPv4/IPv6 i hostname; statystyki i przerwy po timeoutach, rosnące TTL, anulowanie MTR, parser źródeł hopów i błędów iputils. Linux bez root obsługuje wymagany systemowy ping przez bezpieczne argumenty i zarządzany proces.
- TCP: rzeczywisty port otwarty i Connection refused, także IPv6. UDP: odebrany, zweryfikowany bajt po bajcie Magic Packet.
- DNS: własny serwer UDP dla dziesięciu typów, A/AAAA/CNAME/MX/TXT/NS/PTR/SOA/SRV/CAA, nazwy SRV z podkreśleniami (`_ldap._tcp.fixture.test`), TTL, NXDOMAIN, reverse IPv4, niestandardowy port oraz porównanie dwóch resolverów. Brak zależności od publicznych usług DNS w testach.
- HTTP: sześć metod, body/query/nagłówki/Bearer, odpowiedź, charset UTF-16 i znaki polskie, redakcja tokenów/cookie i echa sekretów, brak przekazania poświadczeń/nagłówków do innego origin, limit 2 MiB i timeout. TLS: rzeczywisty handshake z własnym certyfikatem, Subject/SHA-256/TLS/PEM i oznaczenie braku zaufania; oddzielny klient HTTPS prawidłowo odrzuca ten sam niezaufany certyfikat.
- SNMP v2c: rzeczywiste wiadomości UDP GET/GETNEXT/WALK/GETBULK. SNMP v3: **28 kombinacji na Windows i 28 na Linux** przeciw własnemu Net-SNMP 5.9.4 — GET/GETNEXT/WALK/GETBULK, noAuthNoPriv, authNoPriv i authPriv, SHA-256/384/512; AES-128 stosowany w authPriv. Sprawdzono także anulowanie przed operacją. Konta/hasła agenta są publicznymi fixture'ami, nie danymi użytkownika.
- UI Windows i Linux/WSLg: wszystkie dziewięć narzędzi przy pustej bazie; niezależne równoczesne instancje Ping z rzeczywistym ICMP; edytowalne cele kontekstowe bez zmiany zapisanego profilu; automatyczny kalkulator; anulowanie i zakończenie pracy po zamknięciu; istniejące split/floating i zapis obszaru bez fikcyjnych profili. PL/EN, Graphite/Light, font16 i wąskie panele. Sprawdzono kolorowany podgląd JSON i widoczność przycisków akcji.
- Regresja istniejącego UI: pięć motywów i kontrast, ikony, skalowanie renderera 150/200%, preferencje, filtry, historia, klawisze, personalizacja, split, floating/redock, przywracanie kart bez odczytu poświadczeń/łączenia i śledzenie procesu RDP. Logika protokołu Serial nie została zmieniona.
- Rzeczywiste SSH/SFTP na własnym OpenSSH: 1 MiB w obie strony z porównaniem treści, katalogi, sortowanie/filtry/zaznaczenie, anulowanie kolejki i pytania o nadpisanie, ponowienie na pierwotny cel, odzyskanie błędu i limit 50 zakończonych zadań.
- Gotowy EXE **0.25.0.0**: manifest Windows 10/11 asInvoker/uiAccess=false; utworzenie natywnego RDP, zachowanie HWND w split/floating/redock/resize i zwolnienie. Bez logowania do prywatnego RDP i bez poświadczeń. Trzy odizolowane starty potwierdziły korektę okna poza ekranem, maksymalizację i zmianę monitorów.
- Instalator: świeża instalacja w ścieżce ze spacjami i polskimi znakami, start z własną bazą, odmowa aktualizacji/deinstalacji przy blokadzie plików bez zabijania sesji, zachowanie danych i obcych plików, zgodność bajtowa i usuwanie wyłącznie plików aplikacji. Domyślną rejestrację pominięto, zachowując obecną instalację/skróty użytkownika; pozostałe próby korzystały z `/NOREGISTER`.
- Linux/WSLg: rzeczywisty proces i nowe natywne okno z odizolowanymi danymi. TAR i DEB: wszystkie **329 plików** zgodne z payloadem, wersja 0.25.0/amd64, prawa wykonania, właściciel root, launcher i desktop entry.
- NuGet `list package --vulnerable --include-transitive --format json`: poprawne zakończenie, brak błędów źródła i brak zgłoszonych podatnych pakietów w czterech projektach rozwiązania. Sprawdzono pełne licencje nowych bibliotek z commitów wskazanych przez NuGet oraz sumy zapisane w manifestach. Wynik bazy ostrzeżeń nie jest gwarancją braku podatności.

Logi lokalne: `artifacts/network-*.txt`, `network-dependency-audit.json`, `network-native-rdp.json`, zrzuty w odpowiadających katalogach UI i TRX w `artifacts/test-results`. Serwery OpenSSH/Net-SNMP po testach zatrzymano; nie zmieniano normalnej bazy ani magazynu użytkownika.

## Granice

Pełny zestaw 423 testów uruchomiono na Windows, a na Linux 66 nowych testów backendu, UI/odzyskiwanie, rzeczywiste ICMP i integrację Net-SNMP v3. Linux to WSLg, nie cała macierz fizycznych dystrybucji. Nie potwierdzono budzenia fizycznego sprzętu WoL, wszystkich tras WAN/VPN i polityk routerów, wszystkich agentów SNMP ani pełnego audytu TLS. Nadal pozostają dawne ograniczenia RDP/Serial/DPI opisane w `known-issues.md`. Pakiety są publiczną betą, bez podpisu zaufanego wydawcy i bez automatycznego aktualizatora.

Wszystkie dziewięć backendów jest wdrożonych. Zakresy celowo ograniczone (ICMP traceroute, liczbowe OID, SHA-2/AES-128, pojedynczy handshake TLS, limity danych) są opisane w instrukcji. Nie ma pustych ekranów udających działające narzędzia.
