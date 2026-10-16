# Komendy / Commands — ServerDesk 0.36.0

Jeden panel **Komendy** łączy Smart Cheat Sheet, własne szablony, ulubione i opcjonalną historię. Otwórz go ikoną terminala w lewym pasku, przez Menu, przyciskiem **Komendy** w sesji SSH albo **Ctrl+Shift+K**. Panel jest po prawej stronie istniejącego obszaru pracy. Nie tworzy osobnych terminali ani systemu zakładek. Ctrl+Shift+P nadal otwiera paletę akcji aplikacji.

EN: Open **Commands** with **Ctrl+Shift+K**, the activity bar, Menu, or the SSH toolbar. The shared side panel contains the offline Smart Cheat Sheet, My commands, Favorites, Recent and Search. Copy works without connections. Insert sends no Enter. Execute always previews the entire command and asks for confirmation; Dangerous commands require a second confirmation. Multiline scripts and secret parameters are copy-only. See the implementation and test details below.

## Szybki start

1. Bez serwera rozwiń **Profil systemu**, wybierz Ręczny, system i powłokę. Można opcjonalnie podać wersję. Dostępne są Ogólne, Linux, Ubuntu, Debian, RHEL, Rocky, AlmaLinux, CentOS Stream, Fedora, Arch, Alpine, Windows, Windows 10/11 i Windows Server.
2. Wpisz np. `otwarte porty`, `open ports`, `sprawdź usługę`, `serwis status`, `memory` albo nazwę narzędzia. Filtruj po kategorii i sortuj po trafności, nazwie lub ryzyku.
3. Wybierz komendę. Zobacz szablon, opis, system/powłokę, narzędzia, uprawnienia i ryzyko. Uzupełnij parametry; zobaczysz wygenerowany podgląd. Sekretne wartości pozostają ukryte.
4. **Kopiuj** działa offline. **Wstaw** wymaga wspieranej aktywnej sesji SSH i wstawia bez Enter. **Wykonaj** pokazuje cel i pełną treść przed świadomym wysłaniem Enter. Sprawdź, że terminal jest w powłoce, a bieżący wiersz jest pusty; otwarty edytor lub program może inaczej potraktować wejście.
5. **Ulubione** przechowuje odnośnik do definicji. **Duplikuj** tworzy własny wariant; wbudowana biblioteka pozostaje niezmieniona. **Edytuj** jest dostępne dla własnych wpisów.

Przy małej wysokości zwija się konfiguracja profilu, a opis ma własny przewijany obszar. Przy wąskim oknie panel połączeń jest chowany, aby zachować miejsce terminala. Można go ponownie otworzyć. Szerokość panelu komend zmienia separator. Zmiana wielkości okna ponownie dopasowuje szerokość; sam fakt otwarcia panelu nie jest zapamiętywany.

## Automatyczny, ręczny i przypięty profil

Automatyczny tryb SSH wykonuje stałe, tylko odczytowe próby na **osobnym kanale exec istniejącego, uwierzytelnionego połączenia**. Nie dopisuje poleceń do interaktywnej powłoki, nie zestawia dodatkowego dostępu i nie pobiera kolejnych poświadczeń. Odczytuje `/etc/os-release` jako dane (ID, ID_LIKE, VERSION_ID, PRETTY_NAME), nazwę jądra przy braku pliku, loginową zmienną SHELL i `command -v` dla znanych narzędzi. Plik nie jest wykonywany ani source'owany. Rozpoznanie nie instaluje niczego i nie używa sudo. Próba `docker compose version` sprawdza również wtyczkę Compose.

Każde narzędzie ma stan **Dostępne / Niewykryte / Nieznane**. Błąd uprawnień, kanału lub niekompletna odpowiedź pozostawia nieznany stan. Obecność programu nie dowodzi zgodności każdej jego wersji ani dostępu do usługi, demona Docker lub klastra. Wymagane uprawnienia pozostają widoczne, a wynik i błędy wykonania trafiają do zwykłego terminala. Aplikacja raportuje wysłanie wejścia, nie sukces zdalnej operacji.

Wynik jest buforowany **osobno dla każdego połączenia**, do reconnect lub zamknięcia. Przycisk odświeżania wykonuje rozpoznanie ponownie. Zadania działają asynchronicznie, mają limity czasu i odpowiedzi oraz anulowanie wraz z transportem. Starszy wynik nie zmienia kontekstu innej sesji. Ręczne profile i ustawienia panelu są zapisywane globalnie i dla identyfikatora hosta; **Przypnij profil** zatrzymuje automatyczne przełączanie systemu przy wyborze innych kart.

Windows przez istniejące SSH używa osobnego, jawnego PowerShell `-NoProfile -NonInteractive -EncodedCommand` do odczytu metadanych i Get-Command. Domyślny interpreter SSH jest sprawdzany osobno, żeby uruchomienie subprocessu PowerShell nie oznaczało błędnie, że interaktywny terminal jest PowerShell. **Samo RDP nie daje kanału powłoki**: dostępne są przeglądanie Windows i profil ręczny, bez Insert/Execute. VNC i Serial również nie są automatycznie sondowane; Serial zachowuje swoje dotychczasowe działanie. Nie dodano WinRM.

Rozpoznawana powłoka jest powłoką logowania, a nie dowolną zagnieżdżoną powłoką/edytorem uruchomionym później w terminalu. Nieznana powłoka wymaga świadomego ręcznego wyboru do poleceń zależnych od jej składni. Nie polegaj na automatycznym dopasowaniu po wejściu do kontenera, REPL lub innej powłoki; użyj kopiowania i sprawdź docelowy interpreter.

## Własne polecenia i parametry

Wybierz **+** albo **Opcje komend → Nowa komenda**. Formularz obejmuje nazwę/opisy, szablon, kategorię i podkategorię, tagi, system, powłokę, ryzyko, uprawnienia, narzędzia, rodzinę/dystrybucje, zakres wersji i przykłady. Zakres może być globalny, dla konkretnego aktywnego hosta lub folderu/grupy hostów. **Moje komendy**, Ulubione i Ostatnie pozwalają zobaczyć także wpisy spoza bieżącego kontekstu; są oznaczone jako inny profil/system, a wysłanie do niezgodnego hosta jest blokowane.

Kategorie używają ścieżek, np. `Linux/Produkcja/Usługi`. **Kategorie komend** umożliwiają dodanie pustego folderu, zmianę nazwy/przeniesienie całego poddrzewa i usunięcie pustego poddrzewa. Przeniesienie pojedynczej komendy wykonaj przez edycję jej kategorii. Zarządzanie dotyczy własnych wpisów; nie zmienia wbudowanych kategorii.

Parametry mają zapis `{{service}}`, np. `systemctl status --no-pager -- {{service}}`. Dla zwykłych danych parametr musi być kompletnym argumentem, bez otaczających cudzysłowów i bez sklejenia z opcją. Formularz parametrów obsługuje Text, Number, IpAddress, Hostname, FilePath, Choice i jawny Code, wymagane/opcjonalne wartości, domyślne i sekretne. Choice ma listę opcji oddzielonych `|`.

Bash/Zsh używają pojedynczych cudzysłowów z bezpiecznym rozdzieleniem apostrofu; PowerShell podwaja zwykłe i typograficzne pojedyncze cudzysłowy. CMD dopuszcza konserwatywny zestaw danych i odrzuca `%`, `!`, cudzysłowy, operatory i inne niejednoznaczne znaki, zamiast udawać uniwersalny escaping. Ścieżki CMD zakończone backslashem wymagają ręcznego wariantu Code, ponieważ parser poleceń wbudowanych i CRT różnią się. Znaki sterujące i kierunkowe znaki Unicode, separatory linii i dane zaczynające się od myślnika są odrzucane (liczby mogą być ujemne). Cytowanie nie zastępuje walidacji opcji ani uprawnień danego narzędzia.

Parametry umieszczone w cytowanej składni, heredoc lub znanych konstrukcjach ponownie interpretujących tekst jako kod (eval, sh -c, Invoke-Expression itp.) wymagają jawnego typu **Code**. Taki parametr nie jest automatycznie cytowany; po każdej zmianie jego wartości trzeba ponownie zaznaczyć zgodę. Wykonanie nadal wymaga pełnego podglądu. Inne niestandardowe interpretery i narzędzia mogą nadawać argumentom dodatkową semantykę — autor szablonu musi oznaczyć takie argumenty jako Code. Nie importuj ani nie wykonuj szablonów z niezaufanego źródła bez przeglądu.

## Biblioteka 1.0.0: 165 wariantów, 134 operacje

| Kategoria | Warianty | Przykładowy zakres |
|---|---:|---|
| Informacje systemowe | 19 | system, kernel, pamięć, procesy, czas, konta sesji |
| Sieć | 25 | ss, ip, DNS, ping, TCP, nmcli, netplan, Windows networking |
| Usługi | 19 | status/start/stop/restart, systemd, OpenRC, PowerShell i sc.exe |
| Pakiety | 29 | APT, DNF/YUM, Pacman, APK, winget, zainstalowane hotfixy |
| Pliki i uprawnienia | 16 | odczyt, rozmiar, wyszukiwanie, kopiowanie, przenoszenie, chmod/ACL |
| Użytkownicy i grupy | 9 | id/getent, konta/grupy lokalne, tworzenie i blokowanie |
| Dyski i pamięć masowa | 11 | df, lsblk, mounty, LVM, SMART, ZFS, woluminy Windows |
| Logi i diagnostyka | 10 | journalctl, OpenRC log, Windows Event Log, DISM, SFC verify |
| Bezpieczeństwo | 8 | UFW, firewalld, nftables, iptables, Windows Firewall, Defender |
| DevOps | 19 | Docker/Compose, Git, kubectl, Ansible; wspólna warstwa CLI |

21 wpisów jest ogólnych/przenośnych, 100 Linux i 44 Windows. Wynik listy jest mniejszy po zastosowaniu profilu i wyborze jednego wariantu operacji. Dziedziczenie obejmuje warstwę ogólną, Linux, rodzinę, dystrybucję, opcjonalny zakres wersji i warunki narzędzi. Wykryte narzędzie ma pierwszeństwo przed domyślnym priorytetem wariantu. Brak wykrycia nie powoduje automatycznej instalacji. Ubuntu/Debian dziedziczą APT; RHEL/Rocky/Alma/CentOS Stream/Fedora DNF/YUM; Arch Pacman; Alpine APK i przenośny wariant procesów BusyBox. Dla nieznanego Linux pozostaje ogólna warstwa Linux.

winget aktualizuje aplikacje, **nie Windows Update**. Arch ma pełne `pacman -Syu`, bez niebezpiecznego przykładu częściowej aktualizacji `pacman -Sy`. SMART/LVM/ZFS, zapory, Netplan i moduły Windows są warunkowe; ich obecność nie wynika wyłącznie z nazwy systemu. Nie jest to encyklopedia wszystkich narzędzi i wersji.

## Dane, import i ograniczenia

Własna biblioteka jest szyfrowana: DPAPI CurrentUser na Windows lub Secret Service na Linux, bez jawnego fallbacku. Duży dokument jest dzielony na ograniczone części mieszczące się w istniejącym limicie magazynu poświadczeń. Referencja w SQLite jest atomowo podmieniana dopiero po zapisaniu części. Niedostępny/uszkodzony magazyn nie powoduje zastąpienia biblioteki pustą. Wbudowana ściągawka, kopiowanie jej komend i ulubione nadal działają; edycja/zapis własnych szablonów wymaga odblokowanego magazynu.

Limit dokumentu wynosi 4 MiB, 2000 komend, 500 kategorii, 24 parametry na komendę, szablon 16 000 znaków; ulubione 4000 odnośników. Rozpoznanie ma 12 s limitu na kanał (10 s komendy), stdout 128 KiB i stderr 16 KiB. Biblioteka i indeks wbudowanych wpisów są ładowane lokalnie raz; wyszukiwanie ma krótkie opóźnienie 160 ms. Dane użytkownika nie są wysyłane do AI ani usługi wyszukiwania.

**Import/eksport: JSON v1**, bez zależności YAML. Import odrzuca nieznane pola, powtórzone właściwości/ID, niepoprawne typy i zbyt duże dane. Niczego nie wykonuje, nie nadpisuje ustawień ani istniejących wpisów. Importowane definicje dostają nowe identyfikatory i tracą powiązania obcych hostów. Eksport zawiera wyłącznie własne szablony i kategorie, bez wartości wpisanych w formularzu, historii, ulubionych i preferencji; powiązania hostów są usuwane. Plik eksportu jest jawny i wymaga świadomej zgody. Skan typowych literalnych sekretów jest dodatkową ochroną, a nie dowodem braku wszystkich sekretów w dowolnym kodzie.

Historia jest domyślnie wyłączona. W menu można ją włączyć lub wyczyścić; wyłączenie ją czyści. Zapisuje do 100 identyfikatorów i czas ostatniego użycia, bez wygenerowanych komend, wyników ani wartości parametrów. Sekretne parametry nie mają domyślnych wartości i nie trafiają do historii. Są copy-only, aby nie dopisywać ich do historii zdalnej powłoki. Clipboard jest wspólnym zasobem systemu; przy sekrecie wymagane jest dodatkowe potwierdzenie.

Całe wieloliniowe skrypty są **copy-only**, także gdy terminal obsługuje bracketed paste: bieżący program i shell nie dają ogólnej gwarancji wklejenia bez wykonania. Panel sprawdza tożsamość karty i konkretnego połączenia po potwierdzeniu i przed wysłaniem. Przełączenie karty lub reconnect unieważnia wcześniej przechwycony cel. Nie resetuje linii, nie wysyła Ctrl+U, nie czyści terminala ani nie dodaje sudo samoistnie.

## Dla dewelopera

- `src/ServerDesk.Core/Commands/`: model, walidacja, renderer, ograniczenia wysyłki, dopasowanie/wyszukiwanie i parsery rozpoznania; bez Avalonia i zależności SSH.
- `library.v1.json` jest wersjonowanym zasobem osadzonym; `scripts/generate-command-library.py` jest jego przeglądalnym źródłem utrzymaniowym. Nie pobiera danych z sieci.
- `CommandStore` używa istniejących ustawień SQLite i ICredentialStore. Nie zmienia tabel ani user_version=3; stan i odnośnik biblioteki mają osobne klucze `.v1`.
- `SshTerminalSession.ProbeAsync` otwiera osobny kanał tego samego SshClient. `SessionCommands` wiąże cache i cancellation z transportem. `CommandDispatchTicket` obejmuje WorkspaceId i nowy GUID każdego połączenia.
- `CommandWorkspace`, `CommandPanel`, `CommandPanelActions`, `CommandEditors` korzystają z istniejących ikon, motywów, dialogów, terminala i zakładek. Wyniki asynchroniczne mają rewizję kontekstu.

Dodając komendę, dopisz w generatorze stabilne ID i Operation, PL/EN, kategorię, szablon, system/powłokę, wymagane narzędzia, uprawnienia i ryzyko; ewentualnie rodzinę, dystrybucje, zakres wersji, parametry i priorytet. Wspólna Operation grupuje zamienne warianty, nie kopiuje całych bibliotek per distro. Uruchom `python3 scripts/generate-command-library.py`. Dla nowego narzędzia uzupełnij stały allowlist sondowania w CommandDetection; nigdy nie buduj prób z importowanych szablonów. Dodaj testy walidacji/dopasowania/quotingu i sprawdź `--commands` oraz `--commands-network` na fixture. Nowa platforma może rozszerzyć CommandPlatform i parser z kontrolowaną migracją formatu; pełnych bibliotek MikroTik/Cisco/Proxmox nie dodano.

Podstawy składni: [Bash quoting](https://www.gnu.org/software/bash/manual/bash.html), [PowerShell quoting and smart quotes](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_quoting_rules), [CMD expansion rules](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/cmd), [Arch partial-upgrade guidance](https://wiki.archlinux.org/title/System_maintenance#Partial_upgrades_are_unsupported), [winget upgrade](https://learn.microsoft.com/en-us/windows/package-manager/winget/upgrade). Wyniki i zakres rzeczywistych testów: [raport funkcji](reports/commands-2026-10-16.md), [weryfikacja wydania](reports/release-verification-0.36.0.md).
