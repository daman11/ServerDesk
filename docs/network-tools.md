# Narzędzia sieciowe — ServerDesk 0.36.2

Moduł jest częścią MVP i istniejącego obszaru kart ServerDesk. Działa na ręcznie podanym celu także przy całkowicie pustej liście połączeń. Nie wymaga SSH, RDP ani zapisanego urządzenia. Serial Console pozostaje dotychczasową funkcją połączeń.

## Uruchamianie

Wybierz ikonę **Narzędzia sieciowe** na lewym pasku albo **Menu → Network Tools**. Kliknij narzędzie, wpisz cel, rozwiń **Parametry** i wybierz **Start**. Każde kliknięcie otwiera nową, niezależną kartę. Można równocześnie badać różne cele, zmieniać parametry, dzielić obszar na panele, przeciągać karty i odpinać je do okna.

Menu kontekstowe zapisanego serwera zawiera **Narzędzia sieciowe** z dziewięcioma narzędziami. Przekazuje adres jako edytowalne pole, bez nawiązywania sesji i bez modyfikacji profilu. HTTP otrzymuje adres HTTPS, kalkulator otrzymuje /32 lub /128 dla IP, a WoL używa adresu IPv4 jako celu pakietu i wymaga ręcznego MAC. Nazwy hostów nie dostarczają automatycznie MAC ani maski sieci.

**Ctrl+Enter** uruchamia; **Escape** zatrzymuje; **F5** zatrzymuje i uruchamia ponownie. Start nie uruchamia drugiej operacji w tej samej karcie. Zamknięcie karty anuluje pracę i zwalnia zasoby. Zamknięcie aplikacji czeka na zakończenie anulowania. Karty diagnostyczne nie są zapisywane jako fikcyjne profile połączeń ani automatycznie uruchamiane po restarcie; cele i kolekcje można ponownie otworzyć z historii.

Podczas operacji sieciowej pola celu/parametrów i wybór historii są zablokowane, aby wyniki nie mieszały się z nowym celem. Użyj Stop, zmień parametry i Start lub Restart. Kalkulator pozostaje edytowalny i przelicza automatycznie. Po zaznaczeniu wiersza rozwiń **Szczegóły zaznaczonego wyniku**, aby odczytać i skopiować całą treść. Wybór zapisanego hosta działa tak samo jak menu kontekstowe; w WoL zachowuje wpisany MAC i uzupełnia jedynie docelowy IPv4.

## Narzędzia

| Narzędzie | Zakres |
|---|---|
| Ping | IPv4/IPv6, hostname, tryb ciągły lub liczba pakietów, timeout, interwał i rozmiar danych ICMP; wyniki na żywo, wysłane/odebrane, straty, min/avg/max i wykres z przerwami dla braku odpowiedzi. |
| Trasa sieciowa / MTR | IPv4/IPv6, limit 1–64 hopów, opcjonalne nazwy DNS, pomiar ICMP TTL, powtarzanie cykli, tabela odpowiedzi/strat/RTT dla hopów, zatrzymywanie i eksport. Brak odpowiedzi routera nie jest oznaczany jako dowód awarii. |
| Zapytania DNS | A, AAAA, CNAME, MX, TXT, NS, PTR, SOA, SRV, CAA lub All; systemowe DNS, własne adresy IP/porty, presety Cloudflare/Google/Quad9 i porównanie do czterech resolverów. Każdy wynik zawiera źródło, TTL, czas i RCODE, także NXDOMAIN. PTR zamienia IPv4/IPv6 na domenę reverse. Wpisy własnych resolverów pozostają lokalnie. |
| Skaner portów | TCP jednego jawnie wskazanego hosta: pojedynczy port, lista i zakresy 1–65535; presety Common/Web/Windows/Linux/Database/Custom, timeout, 1–64 równoległe połączenia, postęp, czas i anulowanie. Open wymaga zestawienia TCP, Closed wymaga Connection refused; pozostałe przypadki są Filtered/Timeout lub Unknown. Nazwa usługi jest wskazówką według portu. |
| Wake-on-LAN | Walidacja MAC, docelowy IPv4/broadcast, lokalny interfejs/IP i port UDP; rzeczywisty Magic Packet, historia i nazwane ulubione urządzenia. Wysłanie nie potwierdza uruchomienia sprzętu. |
| Klient HTTP | GET/POST/PUT/PATCH/DELETE/HEAD, nagłówki, query, JSON/text/form-urlencoded, Basic/Bearer, timeout i przekierowania; status/czas/rozmiar, nagłówki i odpowiedź. JSON jest formatowany; do 64 KiB/4000 tokenów ma kolorowany, zaznaczalny podgląd. Większe odpowiedzi mają widok tekstowy. Kopiowanie i eksport odpowiedzi oraz bezpieczne kolekcje. |
| Inspektor TLS | Port i opcjonalne SNI/nazwa walidacji, Subject/Issuer/serial, daty i dni ważności, SAN, podpis, algorytm/rozmiar klucza (gdy dostępny), SHA-256, łańcuch i błędy walidacji, uzgodnione TLS/cipher suite, eksport PEM. Ważność: poprawny, blisko wygaśnięcia, wygasły lub niezaufany. |
| Kalkulator podsieci | Automatyczne obliczenia bez sieci, IPv4 CIDR lub maska dziesiętna, network/broadcast/wildcard, zakres hostów i przypadki /31,/32; IPv6: prefiks, pełny/skrócony zapis, zakres i liczba adresów bez broadcast. Podział do 4096 podsieci, pełna lista i eksport. Wpisz np. `192.0.2.10/24`, `192.0.2.10 255.255.255.0` albo `2001:db8::1/64`. -1 wyłącza podział. |
| Przeglądarka SNMP | v2c i v3, IPv4/IPv6/hostname, UDP/timeout/0–3 retries, GET/GETNEXT/WALK/GETBULK. Tabela OID/typ/wartość, popularne sysDescr/sysName/sysUpTime, historia i ulubione OID. v3: użytkownik, kontekst, noAuthNoPriv/authNoPriv/authPriv, SHA-256/384/512 i AES-128. Community i hasła są maskowane. |

## Historia, kolekcje i prywatność

**Historia i kolekcje** udostępnia ostatnie cele z datą i parametrami, wczytanie/ponowienie i usuwanie pojedynczych wpisów lub całej historii. Historia ma do 100 operacji, a lista w karcie pokazuje 30 najnowszych dla danego narzędzia. Własne resolvery, OID i ulubione WoL mają limit 30 wpisów.

Historia nie zawiera sekretów. HTTP zachowuje metodę i origin (schemat/host/port), bez ścieżki, query, body, nagłówków i loginu; mogą one zawierać tokeny. Po przywróceniu historii należy ponownie podać parametry prywatne. Pełny, powtarzalny request zapisuje się przez **Zapisz kolekcję**.

Kolekcja przechowuje cały request i poświadczenia wyłącznie w systemowym magazynie: DPAPI na Windows lub Secret Service na Linux. W zwykłej bazie pozostają identyfikator, typ narzędzia i nadana nazwa. Nie ma jawnego zapisu zastępczego przy niedostępnym magazynie. Limit: 100 kolekcji, 48 KiB na kolekcję. Linux wymaga `libsecret-tools` i odblokowanego Secret Service; brak tego środowiska nie blokuje jednorazowych operacji.

Wyniki można kopiować i eksportować jako TXT/CSV/JSON, certyfikat jako PEM, a body HTTP przez format **Response**. CSV chroni początkowe znaki formuł arkusza. Raporty nie zawierają obiektu żądania ani jego poświadczeń. HTTP maskuje typowe pola password/token/API key/cookie i znane wartości sekretów, także echa w odpowiedzi. Arbitralna treść serwera może zawierać inne poufne dane; przed udostępnieniem sprawdź wynik. Profile eksportowane przez główne menu nadal nie zawierają historii i kolekcji narzędzi.

## Limity i różnice platform

- Do ośmiu równoległych operacji narzędzi; pozostałe czekają z możliwością anulowania. Skaner ma własny limit współbieżności, domyślnie 16. Nie odkrywa sieci i nie rozszerza zadanego zakresu.
- Skaner zachowuje maksymalnie 65 535 wyników; kalkulator całą listę do 4096 podsieci. Inne długie sesje zachowują ostatnie 2000 wierszy, wykres ostatnie 360 próbek. Interfejs informuje o pominiętych starszych wierszach. Statystyki Ping obejmują cały bieżący pomiar; eksport obejmuje zachowane wyniki. WALK kończy się na granicy poddrzewa, końcu MIB, braku postępu lub limicie 2000 OID.
- Maksymalny timeout 60 s; interwał 100 ms–60 s; ICMP do 1400 B. Windows używa .NET ICMP. Linux bez prawa do surowych gniazd używa systemowego **iputils-ping**, z osobnymi argumentami, bez powłoki, z ograniczonym wyjściem i zamknięciem procesu po Stop. Nie uruchamiaj całego GUI jako root. Brak ping/uprawnień jest komunikowany. Różnice firewalli mogą powodować Timeout zamiast Closed.
- MTR jest cyklicznym pomiarem ICMP TTL; nie jest implementacją wszystkich metod CLI mtr, UDP/TCP traceroute ani lokalizacją przyczyny awarii. Routery mogą ograniczać lub filtrować odpowiedzi. Fizyczne trasy WAN, VPN i wszystkie konfiguracje IPv6 wymagają weryfikacji w danej sieci.
- HTTP: body do 256 KiB, nagłówki/query do 16 KiB, odebrana odpowiedź do 2 MiB po dekompresji, do ośmiu przekierowań. HTTPS zawsze sprawdza certyfikat. Przekierowanie nie przekazuje poświadczeń/nagłówków do innego origin i nie przechodzi z HTTPS na HTTP. Przekazanie body do innego serwera wymaga ręcznego otwarcia adresu. Nie jest to pełny Postman: brak skryptów i pracy strumieniowej.
- TLS Inspector wykonuje diagnostyczny handshake bez danych aplikacyjnych. Raportuje błędy certyfikatu, może więc odczytać także niezaufany certyfikat. To osobna ścieżka od walidowanego HTTPS klienta HTTP. Nie sprawdza odwołania certyfikatu, wszystkich wersji TLS/cipher suites ani pełnej zgodności polityki organizacji.
- SNMP v2c i v3 noAuthNoPriv nie szyfrują danych; dla ochrony transmisji wybierz v3 authPriv. Udostępniono SHA-2/AES-128, bez MD5/SHA-1/DES. GETBULK pobiera do 10 powtórzeń. Nie ma SNMP SET ani kompilatora MIB: OID podaje się liczbowo. Timeout może oznaczać także odrzucenie błędnego community/hasła przez agenta.
- Windows/Linux mają implementację; testy Linux wykonano w Ubuntu/WSLg jako zwykły użytkownik, nie na wszystkich dystrybucjach i fizycznych desktopach. Potwierdzono pakiety WoL na loopback, bez twierdzenia, że obudzono rzeczywisty komputer.

## Architektura, pliki i zależności

`src/ServerDesk.Core/NetworkTools.cs` definiuje żądania, wyniki, walidację i eksport; `SubnetCalculation.cs` wykonuje obliczenia na BigInteger. Logika infrastruktury jest w `NetworkToolsService.cs`, `NetworkDns.cs`, `NetworkHttpTls.cs`, `NetworkSnmp.cs`, `IcmpProbe.cs`, `LinuxIcmpProbe.cs` i `NetworkToolsStore.cs`.

UI jest w `NetworkToolPane.cs`, `NetworkToolForms.cs`, `NetworkToolPersistence.cs`, `NetworkJsonPreview.cs`, `NetworkLatencyChart.cs` i `NetworkWorkspace.cs`. `WorkspacePane.cs` jest wspólną podstawą karty połączenia i diagnostyki; istniejący `SessionDocking.cs` zarządza obydwoma, tymi samymi TabControl/WorkspaceState. To niewielkie uogólnienie kontenera zamiast drugiego mechanizmu nawigacji. Logika protokołów Serial/SSH/RDP/VNC nie została przepisana.

Dodane biblioteki: [DnsClient 1.8.0](https://github.com/MichaCo/DnsClient.NET/tree/f1e7ca33d713dc3bc70e7b3664aa3a2b6c090d5d), Apache-2.0, oraz [SharpSnmpLib 12.5.7](https://github.com/lextudio/sharpsnmplib/tree/6f6937b81e6bb14dae890c8ee7e0642323907d33), MIT. Pełne licencje i ich sumy są w `third-party/licenses/manifest.json` i paczkach. Pozostałe operacje używają .NET; bez nowego projektu i bez drugiego runtime. Ograniczenie ICMP .NET na Linux opisuje [dokumentacja Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping.sendpingasync?view=net-10.0).

## Testowanie

`NetworkToolsTests.cs` i `NetworkServicesTests.cs`: kalkulacje IPv4/IPv6, /31,/32, walidacja, ograniczenia, CSV, prywatność historii i kolekcji, ICMP/statystyki/timeout, TCP open/refused, UDP WoL, własny DNS dla wszystkich typów/NXDOMAIN/porównania, HTTP metody/redirect/timeout/limit, TLS i odmowa niezaufanego HTTPS, rzeczywiste pakiety SNMP v2c. Fixture'y są lokalne i niezależne od usług Internetu.

`ServerDesk.UiSmoke --network-tools` sprawdza pustą bazę, dziewięć narzędzi, niezależne instancje z rzeczywistym ICMP, context-aware bez modyfikacji profilu, automatyczny kalkulator, zamykanie/anulowanie, split/floating, PL/EN, Graphite/Light, większą czcionkę i małe panele.

`--network-snmp HOST PORT` jest oddzielnym testem integracyjnym dla własnego agenta Net-SNMP: trzy poziomy ochrony, SHA-256/384/512, AES-128 w authPriv oraz GET/GETNEXT/WALK/GETBULK. Testowe nazwy/hasła w tym narzędziu są publicznymi fixture'ami, nie konfiguracją produkcyjną. `scripts/start-test-snmp.sh`/`stop-test-snmp.sh` uruchamiają wyłącznie rozpakowanego agenta w `.tools/network-lab`; wymagają przygotowania pakietów snmpd, libsnmp40t64, libsnmp-base i zależności bibliotecznych dla danego WSL. Używają własnego persistent dir i PID, bez instalowania usługi. Zatrzymaj agenta po testach.

Końcowe wyniki i granice weryfikacji: `release-verification-0.29.0.md`. Narzędzia wykonują rzeczywiste operacje; nie ma widoków zastępujących niewdrożony backend.
