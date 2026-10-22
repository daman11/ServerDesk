#!/usr/bin/env python3
"""Reviewed PL/EN education and option summaries. No network or command execution.

Official URLs support an explicit read-only documentation refresh in the app.
Descriptions below are original concise summaries, not copies of upstream manuals.
"""
import json
from pathlib import Path

sources, tools, guides = [], [], []


def source(id, title, url):
    sources.append(dict(id=id, title=title, url=url))
    return id


gnu = "https://www.gnu.org/software/coreutils/manual/html_node/"
ls_files = source("gnu-ls-files", "GNU ls · Which files are listed", gnu+"Which-files-are-listed.html")
ls_info = source("gnu-ls-info", "GNU ls · File information", gnu+"What-information-is-listed.html")
ls_sort = source("gnu-ls-sort", "GNU ls · Sorting", gnu+"Sorting-the-output.html")
ls_format = source("gnu-ls-format", "GNU ls · Output formatting", gnu+"General-output-formatting.html")
grep = source("gnu-grep", "GNU grep manual", "https://www.gnu.org/software/grep/manual/grep.html")
find = source("gnu-find", "GNU findutils manual", "https://www.gnu.org/software/findutils/manual/find.html")
tail = source("gnu-tail", "GNU tail manual", gnu+"tail-invocation.html")
df = source("gnu-df", "GNU df manual", gnu+"df-invocation.html")
du = source("gnu-du", "GNU du manual", gnu+"du-invocation.html")
chmod = source("gnu-chmod", "GNU chmod manual", gnu+"chmod-invocation.html")
sshkey = source("openssh-keygen", "OpenSSH · ssh-keygen", "https://man.openbsd.org/ssh-keygen")
ssh = source("openssh-client", "OpenSSH · ssh", "https://man.openbsd.org/ssh")
journal = source("systemd-journal", "systemd · journalctl", "https://www.freedesktop.org/software/systemd/man/latest/journalctl.html")
systemctl = source("systemd-services", "systemd · systemctl", "https://www.freedesktop.org/software/systemd/man/latest/systemctl.html")
networkd = source("systemd-network", "systemd · .network files", "https://www.freedesktop.org/software/systemd/man/latest/systemd.network.html")
nm = source("networkmanager", "NetworkManager · nmcli", "https://networkmanager.dev/docs/api/latest/nmcli.html")
netplan = source("netplan-try", "Netplan · try and rollback limits", "https://netplan.readthedocs.io/en/stable/netplan-try/")
netplan_yaml = source("netplan-yaml", "Netplan · YAML configuration", "https://netplan.readthedocs.io/en/stable/netplan-yaml/")
debian = source("debian-network", "Debian · Network setup", "https://www.debian.org/doc/manuals/debian-reference/ch05.en.html")
interfaces = source("debian-interfaces", "Debian ifupdown · interfaces", "https://manpages.debian.org/unstable/ifupdown/interfaces.5.en.html")
ip = source("iproute-ip", "Debian/iproute2 · ip", "https://manpages.debian.org/unstable/iproute2/ip.8.en.html")
ss = source("iproute-ss", "Debian/iproute2 · ss", "https://manpages.debian.org/unstable/iproute2/ss.8.en.html")
curl = source("curl-manual", "curl · command-line reference", "https://curl.se/docs/manpage.html")
docker = source("docker-logs", "Docker · container logs", "https://docs.docker.com/reference/cli/docker/container/logs/")
git = source("git-reference", "Git · global options", "https://git-scm.com/docs/git")
apt = source("debian-apt", "Debian/APT · apt", "https://manpages.debian.org/unstable/apt/apt.8.en.html")
win_key = source("windows-ssh", "Microsoft · OpenSSH key management", "https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_keymanagement")
win_event = source("windows-events", "Microsoft · Get-WinEvent", "https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.diagnostics/get-winevent?view=powershell-7.6")
win_files = source("powershell-files", "Microsoft · Get-ChildItem", "https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-childitem?view=powershell-7.6")
win_ip = source("windows-ip", "Microsoft · Get-NetIPConfiguration", "https://learn.microsoft.com/en-us/powershell/module/nettcpip/get-netipconfiguration?view=windowsserver2025-ps")
win_network = source("windows-network-settings", "Microsoft · Network and Internet settings", "https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings#network--internet")


def tool(id, names, title, scope, scope_en, refs, options):
    platform = "Windows" if id.startswith("get-") else "General" if id in ("ssh", "ssh-keygen", "curl", "docker-logs", "git") else "Linux"
    tools.append(dict(id=id, tools=names, title=title, scope=scope, scopeEn=scope_en, platform=platform,
                      sources=refs, options=[dict(syntax=s, description=p, descriptionEn=e,
                                                example=x[0] if x else "") for s, p, e, *x in options]))


tool("ls", ["ls"], "ls", "GNU coreutils. BusyBox/BSD mogą obsługiwać inny zestaw opcji. Wielkość liter ma znaczenie; -l i -1 to różne przełączniki.", "GNU coreutils. BusyBox/BSD may support different options. Case matters; -l and -1 are different switches.", [ls_files, ls_info, ls_sort, ls_format], [
    ("-a, --all", "Także ukryte pliki oraz . i ..", "Include dotfiles, . and .."),
    ("-A, --almost-all", "Ukryte pliki bez . i ..", "Include dotfiles except . and .."),
    ("-l", "Szczegóły, uprawnienia, właściciel i rozmiar.", "Detailed permissions, ownership and size."),
    ("-h, --human-readable", "Czytelne rozmiary z -l lub -s.", "Readable sizes with -l or -s.", "ls -lh -- /var/log"),
    ("-d, --directory", "Sam katalog zamiast jego zawartości.", "Directory itself, not its contents."),
    ("-R, --recursive", "Także zawartość podkatalogów.", "Descend into subdirectories."),
    ("-t", "Najnowsza modyfikacja jako pierwsza.", "Newest modification first."),
    ("-r, --reverse", "Odwróć wybrane sortowanie.", "Reverse the selected order."),
    ("-S", "Sortuj malejąco według rozmiaru.", "Sort by size, largest first."),
    ("-1", "Jedna nazwa w wierszu.", "One name per line."),
    ("-F, --classify", "Znaczniki typu, np. / i *.", "Type markers, e.g. / and *."),
    ("-i, --inode", "Pokaż numer i-węzła.", "Show inode numbers."),
    ("-n, --numeric-uid-gid", "UID/GID zamiast nazw.", "Numeric UID/GID instead of names."),
    ("--color=auto", "Kolory dla wyjścia terminala.", "Colors for terminal output."),
    ("--group-directories-first", "Katalogi przed pozostałymi wpisami.", "Group directories before other entries."),
    ("--", "Koniec opcji; dalej nazwy plików.", "End options; filenames follow.", "ls -lah -- /var/log"),
])
tool("grep", ["grep"], "grep", "GNU grep; wzorzec cytuj w powłoce. -r pomija napotkane linki, -R może za nimi podążać.", "GNU grep; quote the pattern in the shell. -r skips encountered symlinks; -R may follow them.", [grep], [
    ("-i, --ignore-case", "Bez rozróżniania wielkości liter.", "Ignore letter case."),
    ("-n, --line-number", "Numery pasujących wierszy.", "Include matching line numbers."),
    ("-v, --invert-match", "Wiersze, które nie pasują.", "Select non-matching lines."),
    ("-r, --recursive", "Przeszukaj podkatalogi.", "Search subdirectories."),
    ("-F, --fixed-strings", "Zwykły tekst zamiast regex.", "Literal strings, not regex."),
    ("-E, --extended-regexp", "Rozszerzone wyrażenia regularne.", "Extended regular expressions."),
    ("-l, --files-with-matches", "Tylko nazwy pasujących plików.", "Only names of matching files."),
    ("-c, --count", "Liczba pasujących wierszy.", "Count matching lines."),
    ("-A N / -B N / -C N", "Kontekst po / przed / z obu stron.", "Context after / before / both sides.", "grep -n -C 2 -- 'error' app.log"),
    ("-e WZORZEC / PATTERN", "Jawny wzorzec, także zaczynający się od -.", "Explicit pattern, including a leading -."),
])
tool("find", ["find"], "find", "GNU find. Testy nie są zwykłymi opcjami. Cytuj wzorce; -delete i -exec mogą zmieniać dane.", "GNU find. Tests are not ordinary options. Quote patterns; -delete and -exec can modify data.", [find], [
    ("-name 'WZORZEC' / 'PATTERN'", "Nazwa pasująca do globu.", "Name matching a glob."),
    ("-iname 'WZORZEC' / 'PATTERN'", "Nazwa bez wielkości liter.", "Case-insensitive name."),
    ("-type f / -type d", "Tylko pliki / katalogi.", "Only files / directories."),
    ("-maxdepth N", "Ogranicz głębokość wyszukiwania.", "Limit search depth."),
    ("-mtime +N", "Starsze według pełnych dób modyfikacji.", "Older by rounded modification days."),
    ("-size +100M", "Pliki większe niż 100 MiB.", "Files larger than 100 MiB."),
    ("-print0", "Nazwy zakończone bajtem NUL.", "NUL-terminated filenames."),
    ("-L (przed ścieżką / before path)", "Podążaj za linkami symbolicznymi.", "Follow symbolic links."),
])
tool("journalctl", ["journalctl"], "journalctl", "Linux z systemd. Widoczność dziennika zależy od uprawnień; starszy systemd może nie mieć nowszych opcji.", "Linux with systemd. Journal access depends on permissions; older versions may lack newer options.", [journal], [
    ("-u, --unit=UNIT", "Dziennik wskazanej jednostki.", "Entries for the selected unit."),
    ("-b, --boot", "Bieżący rozruch; -b -1 poprzedni.", "Current boot; -b -1 previous boot."),
    ("-n, --lines=N", "Ostatnie N wpisów.", "Last N entries."),
    ("-f, --follow", "Nowe wpisy na żywo; Ctrl+C kończy.", "Follow new entries; Ctrl+C stops."),
    ("--since=TIME", "Początek zakresu czasu.", "Start of the time range."),
    ("--until=TIME", "Koniec zakresu czasu.", "End of the time range."),
    ("-p, --priority=LEVEL", "Poziom i poważniejsze, np. warning.", "Level and more severe, e.g. warning."),
    ("-k, --dmesg", "Komunikaty jądra.", "Kernel messages."),
    ("--user", "Dziennik usług bieżącego użytkownika.", "Current user's service journal."),
    ("--no-pager", "Wypisz bez przeglądarki less.", "Output without a pager."),
    ("--list-boots", "Lista dostępnych rozruchów.", "List available boots."),
    ("--disk-usage", "Miejsce zajęte przez dzienniki.", "Disk usage of journal files."),
])
tool("systemctl", ["systemctl"], "systemctl", "Tylko systemd. Status, włączenie przy starcie i działanie teraz to różne stany.", "systemd only. Runtime state and enablement at boot are different states.", [systemctl], [
    ("status UNIT", "Bieżący stan i ostatnie komunikaty.", "Current state and recent messages."),
    ("list-units --failed", "Jednostki z błędem.", "Units in failed state."),
    ("--type=service", "Ogranicz do usług.", "Restrict to services."),
    ("--all", "Także jednostki nieaktywne.", "Include inactive units."),
    ("--user", "Menedżer bieżącego użytkownika.", "Current user's service manager."),
    ("--no-pager", "Bez przeglądarki wyników.", "Disable the output pager."),
    ("--full", "Bez skracania wierszy.", "Do not ellipsize lines."),
    ("enable --now UNIT", "Włącz przy starcie i uruchom teraz.", "Enable at boot and start now."),
    ("daemon-reload", "Wczytaj zmienione definicje jednostek.", "Reload changed unit definitions."),
])
tool("ip", ["ip"], "ip", "iproute2. 'show' odczytuje stan; 'add', 'del' i 'set' mogą zerwać połączenie. To nie zapis trwałej konfiguracji.", "iproute2. 'show' reads state; 'add', 'del' and 'set' may disconnect you. They do not provide persistent configuration.", [ip], [
    ("-br, -brief", "Zwięzły widok interfejsów.", "Brief interface output."),
    ("-4 / -6", "Tylko IPv4 / IPv6.", "IPv4 / IPv6 only."),
    ("-s, -stats", "Statystyki interfejsów.", "Interface statistics."),
    ("-d, -details", "Dodatkowe szczegóły.", "Additional details."),
    ("-j, -json", "Wynik JSON.", "JSON output."),
    ("address show", "Adresy przypisane interfejsom.", "Addresses assigned to interfaces."),
    ("route show", "Tablica routingu.", "Routing table."),
    ("link show", "Łącza i ich stan.", "Links and their state."),
])
tool("ss", ["ss"], "ss", "iproute2. Informacje o procesach (-p) mogą wymagać dodatkowych uprawnień.", "iproute2. Process details (-p) may require extra permissions.", [ss], [
    ("-t, --tcp", "Gniazda TCP.", "TCP sockets."), ("-u, --udp", "Gniazda UDP.", "UDP sockets."),
    ("-l, --listening", "Tylko nasłuchujące gniazda.", "Listening sockets only."),
    ("-a, --all", "Także nienasłuchujące gniazda.", "Include non-listening sockets."),
    ("-n, --numeric", "Bez zamiany portów na nazwy.", "Do not resolve service names."),
    ("-p, --processes", "Procesy korzystające z gniazd.", "Processes using the sockets."),
    ("-s, --summary", "Podsumowanie gniazd.", "Socket summary."),
    ("-4 / -6", "Tylko IPv4 / IPv6.", "IPv4 / IPv6 only."),
])
tool("ssh-keygen", ["ssh-keygen"], "ssh-keygen", "OpenSSH. Hasło klucza podawaj w interaktywnym pytaniu, nie w argumentach. Nie nadpisuj istniejącego klucza.", "OpenSSH. Enter passphrases at the interactive prompt, not in arguments. Do not overwrite an existing key.", [sshkey], [
    ("-t TYPE", "Typ klucza, np. ed25519 lub rsa.", "Key type, e.g. ed25519 or rsa."),
    ("-a ROUNDS", "Koszt KDF ochrony hasłem klucza.", "Passphrase-protection KDF work factor."),
    ("-C COMMENT", "Komentarz identyfikujący klucz.", "Comment identifying the key."),
    ("-f FILE", "Plik wejściowy lub docelowy.", "Input or destination file."),
    ("-l", "Pokaż odcisk klucza.", "Show a key fingerprint."),
    ("-E sha256", "Format skrótu odcisku.", "Fingerprint hash algorithm."),
    ("-p", "Zmień hasło istniejącego klucza.", "Change an existing key's passphrase."),
    ("-y", "Wyprowadź klucz publiczny z prywatnego.", "Derive the public key from private material."),
])
tool("ssh", ["ssh"], "ssh", "Klient OpenSSH. Serwer RDP nie jest serwerem SSH. Nie wyłączaj sprawdzania tożsamości hosta przy diagnozowaniu.", "OpenSSH client. An RDP server is not an SSH server. Keep host identity checking enabled while diagnosing.", [ssh], [
    ("-p PORT", "Port serwera SSH.", "SSH server port."),
    ("-i FILE", "Wybrany plik klucza logowania.", "Selected identity key file."),
    ("-l USER", "Login na serwerze.", "Server username."),
    ("-v / -vv / -vvv", "Rosnący poziom diagnostyki.", "Increasing diagnostic verbosity."),
    ("-o IdentitiesOnly=yes", "Ogranicz proponowane tożsamości.", "Restrict offered identities."),
    ("-F FILE", "Alternatywny plik konfiguracji klienta.", "Alternative client configuration file."),
    ("-J HOST", "Host pośredni ProxyJump.", "ProxyJump gateway host."),
])
tool("nmcli", ["nmcli"], "nmcli", "Tylko interfejsy zarządzane przez NetworkManager. Zmiana profilu i jego aktywacja to oddzielne operacje.", "Only interfaces managed by NetworkManager. Editing and activating a profile are separate operations.", [nm], [
    ("-t, --terse", "Zwięzły format dla skryptów.", "Terse script-friendly output."),
    ("-f, --fields FIELDS", "Wybierz wyświetlane pola.", "Choose displayed fields."),
    ("-g, --get-values FIELDS", "Same wartości wybranych pól.", "Only selected field values."),
    ("-p, --pretty", "Czytelne formatowanie dla człowieka.", "Human-readable formatting."),
    ("-a, --ask", "Pytaj o brakujące dane interaktywnie.", "Prompt for missing data interactively."),
    ("connection show", "Zapisane profile połączeń.", "Saved connection profiles."),
    ("device status", "Stan urządzeń sieciowych.", "Network device status."),
    ("device show IFACE", "Szczegóły wybranego urządzenia.", "Details of the selected device."),
])
tool("netplan", ["netplan"], "netplan", "Ubuntu/inne systemy używające Netplan. Dostępność poleceń zależy od wersji; try ma ograniczenia wycofywania zmian.", "Ubuntu/other systems using Netplan. Commands depend on version; try has rollback limitations.", [netplan, netplan_yaml], [
    ("get", "Pokaż scaloną konfigurację.", "Show merged configuration."),
    ("generate", "Sprawdź i wygeneruj konfigurację backendu.", "Validate and generate backend configuration."),
    ("try --timeout N", "Próba zmian z terminem potwierdzenia.", "Try changes with a confirmation deadline."),
    ("apply", "Zastosuj zmiany bez interaktywnego rollbacku.", "Apply changes without interactive rollback."),
    ("--debug", "Więcej informacji diagnostycznych.", "More diagnostic information."),
    ("--help", "Pomoc dla zainstalowanej wersji.", "Help for the installed version."),
])
tool("curl", ["curl"], "curl", "curl; wielkość liter ma znaczenie. -I sprawdza nagłówki. Nie używaj -k jako naprawy niezaufanego TLS.", "curl; case matters. -I requests headers. Do not use -k as a fix for untrusted TLS.", [curl], [
    ("-I, --head", "Zapytanie o nagłówki HEAD.", "Request HEAD response headers."),
    ("-L, --location", "Podążaj za przekierowaniami HTTP.", "Follow HTTP redirects."),
    ("--max-time SECONDS", "Limit całej operacji.", "Overall operation timeout."),
    ("--connect-timeout SECONDS", "Limit ustanawiania połączenia.", "Connection establishment timeout."),
    ("-s, --silent", "Bez paska postępu i komunikatów błędu.", "Hide progress and error messages."),
    ("-S, --show-error", "Pokaż błędy także z -s.", "Show errors even with -s."),
    ("-f, --fail", "Błąd statusu dla HTTP 400+.", "Fail for HTTP status 400+."),
    ("-o, --output FILE", "Zapisz odpowiedź do pliku.", "Save the response to a file."),
    ("-H, --header HEADER", "Dodaj wskazany nagłówek.", "Add a request header."),
])
tool("tail", ["tail"], "tail", "GNU tail. Do rotowanych plików logów przydatne jest -F.", "GNU tail. -F is useful for rotated log files.", [tail], [
    ("-n N, --lines=N", "Ostatnie N wierszy.", "Last N lines."),
    ("-f, --follow", "Śledź dopisywaną zawartość.", "Follow appended content."),
    ("-F", "Śledź nazwę i ponawiaj po rotacji.", "Follow by name and retry after rotation."),
    ("-c N, --bytes=N", "Ostatnie N bajtów.", "Last N bytes."),
    ("--pid=PID", "Zakończ po zniknięciu procesu przy -f.", "Stop following after the process exits."),
])
tool("df", ["df"], "df", "GNU df: zajętość systemu plików, nie rozmiar pojedynczego katalogu.", "GNU df: filesystem usage, not an individual directory's size.", [df], [
    ("-h, --human-readable", "Rozmiary w potęgach 1024.", "Sizes in powers of 1024."),
    ("-H, --si", "Rozmiary w potęgach 1000.", "Sizes in powers of 1000."),
    ("-T, --print-type", "Pokaż typ systemu plików.", "Show filesystem type."),
    ("-i, --inodes", "Wolne i-węzły zamiast bloków.", "Free inodes instead of blocks."),
    ("-x, --exclude-type=TYPE", "Pomiń wskazany typ systemu plików.", "Exclude a filesystem type."),
])
tool("du", ["du"], "du", "GNU du. Odczyt dużego drzewa może potrwać; brak uprawnień daje niepełny wynik.", "GNU du. Large trees may take time; access denial yields incomplete results.", [du], [
    ("-h, --human-readable", "Czytelne rozmiary.", "Human-readable sizes."),
    ("-s, --summarize", "Tylko podsumowanie argumentów.", "Only totals for arguments."),
    ("-d N, --max-depth=N", "Ogranicz poziomy raportu.", "Limit reported depth."),
    ("-x, --one-file-system", "Nie przechodź na inne systemy plików.", "Stay on one filesystem."),
    ("--apparent-size", "Rozmiar logiczny zamiast alokacji.", "Logical size, not allocated storage."),
])
tool("chmod", ["chmod"], "chmod", "Zmienia uprawnienia. -R dotyczy całego drzewa; pliki i katalogi mają inne potrzeby dotyczące x.", "Changes permissions. -R affects a whole tree; files and directories have different needs for x.", [chmod], [
    ("u / g / o / a", "Właściciel / grupa / inni / wszyscy.", "Owner / group / others / all."),
    ("+r / -w / +x", "Dodaj odczyt / odbierz zapis / dodaj wykonanie.", "Add read / remove write / add execute."),
    ("600 / 640 / 750", "Zapis ósemkowy trójek rwx.", "Octal notation of rwx triplets."),
    ("-R, --recursive", "Zmień też potomne wpisy.", "Also change descendant entries."),
    ("-c, --changes", "Raportuj tylko faktyczne zmiany.", "Report only actual changes."),
    ("--reference=FILE", "Użyj uprawnień pliku wzorcowego.", "Use a reference file's permissions."),
])
tool("docker-logs", ["docker"], "docker logs", "Te opcje dotyczą podpolecenia logs, nie wszystkich poleceń Docker. Działanie zależy od sterownika logowania.", "These options belong to the logs subcommand, not every Docker command. Availability depends on the logging driver.", [docker], [
    ("logs --follow, logs -f", "Śledź nowe komunikaty kontenera.", "Follow new container messages."),
    ("logs --tail N", "Ostatnie N wierszy.", "Last N lines."),
    ("logs --since TIME", "Początek czasu lub np. 30m.", "Start timestamp or e.g. 30m."),
    ("logs --until TIME", "Koniec zakresu czasu.", "End of the time range."),
    ("logs --timestamps, logs -t", "Dodaj znaczniki czasu.", "Add timestamps."),
    ("logs --details", "Dodatkowe atrybuty wpisów.", "Additional log entry attributes."),
])
tool("git", ["git"], "git · global", "Opcje globalne stoją przed podpoleceniem, np. git -C /repo status. Parametry status/log/diff mają oddzielne znaczenie.", "Global options precede the subcommand, e.g. git -C /repo status. status/log/diff have their own parameters.", [git], [
    ("-C PATH", "Działaj w podanym katalogu.", "Operate in the specified directory."),
    ("--no-pager", "Bez przeglądarki wyników.", "Disable output paging."),
    ("--git-dir=PATH", "Wskaż katalog danych repozytorium.", "Select repository metadata directory."),
    ("--work-tree=PATH", "Wskaż drzewo robocze.", "Select the working tree."),
    ("--version", "Wersja klienta Git.", "Git client version."),
    ("--help", "Pomoc dla podpolecenia.", "Help for a subcommand."),
])
tool("apt", ["apt"], "apt", "Debian/Ubuntu. Interfejs dla człowieka; do skryptów dobierz apt-get. update odświeża indeks, a upgrade zmienia pakiety.", "Debian/Ubuntu. Human-facing interface; use apt-get as appropriate in scripts. update refreshes indexes; upgrade changes packages.", [apt], [
    ("list --installed", "Lista zainstalowanych pakietów.", "List installed packages."),
    ("list --upgradable", "Dostępne aktualizacje.", "Available upgrades."),
    ("show PACKAGE", "Informacje o pakiecie.", "Package information."),
    ("search TERM", "Przeszukaj opisy pakietów.", "Search package descriptions."),
    ("-s, --simulate", "Symulacja planowanej operacji.", "Simulate the planned operation."),
    ("--no-install-recommends", "Bez automatycznych rekomendacji.", "Do not automatically install recommendations."),
])
tool("get-childitem", ["Get-ChildItem"], "Get-ChildItem", "PowerShell. Alias ls nie oznacza GNU ls; przełączników -lah nie przenoś z Linuxa.", "PowerShell. The ls alias is not GNU ls; do not transfer Linux -lah switches.", [win_files], [
    ("-Path PATH", "Ścieżka z obsługą wildcardów.", "Path supporting wildcards."),
    ("-LiteralPath PATH", "Ścieżka bez interpretowania wildcardów.", "Path without wildcard interpretation."),
    ("-Force", "Także ukryte wpisy; bez obejścia ACL.", "Include hidden items; does not bypass ACLs."),
    ("-Recurse", "Zawartość podkatalogów.", "Subdirectory contents."),
    ("-Depth N", "Głębokość przeglądania.", "Traversal depth."),
    ("-File / -Directory", "Tylko pliki / katalogi.", "Files / directories only."),
    ("-Filter PATTERN", "Filtr dostawcy systemu plików.", "Filesystem provider filter."),
    ("-Name", "Tylko nazwy zamiast obiektów.", "Names instead of objects."),
])
tool("get-winevent", ["Get-WinEvent"], "Get-WinEvent", "Windows PowerShell/PowerShell na Windows. Dostęp do części logów wymaga podniesionych uprawnień.", "Windows PowerShell/PowerShell on Windows. Some logs require elevated access.", [win_event], [
    ("-LogName NAME", "Wybrany dziennik.", "Selected event log."),
    ("-MaxEvents N", "Ogranicz liczbę wpisów.", "Limit the entry count."),
    ("-FilterHashtable @{...}", "Filtrowanie m.in. czasu, ID i poziomu.", "Filter time, event ID and level."),
    ("-FilterXPath QUERY", "Zapytanie XPath do dziennika.", "XPath event query."),
    ("-ListLog PATTERN", "Lista dostępnych dzienników.", "List available event logs."),
    ("-Oldest", "Najstarsze wpisy jako pierwsze.", "Oldest entries first."),
    ("-Path FILE", "Odczytaj zapisany plik EVTX.", "Read a saved EVTX file."),
])
tool("get-netipconfiguration", ["Get-NetIPConfiguration"], "Get-NetIPConfiguration", "Moduł NetTCPIP w Windows; odczyt stanu sieci.", "Windows NetTCPIP module; reads network state.", [win_ip], [
    ("-InterfaceAlias NAME", "Wskaż nazwę interfejsu.", "Select an interface name."),
    ("-InterfaceIndex N", "Wskaż numer interfejsu.", "Select an interface index."),
    ("-Detailed", "Dodatkowe właściwości.", "Additional properties."),
    ("-All", "Także interfejsy wirtualne/rozłączone.", "Include virtual/disconnected interfaces."),
    ("-AllCompartments", "Wszystkie przedziały sieciowe.", "All network compartments."),
])


def step(title, en, text, text_en, code="", risk="ReadOnly"):
    return dict(title=title, titleEn=en, text=text, textEn=text_en, code=code, risk=risk)


def guide(id, title, en, summary, summary_en, category, platform, applies, applies_en, refs, steps, *, warning="", warning_en="", tags=()):
    guides.append(dict(id=id, title=title, titleEn=en, summary=summary, summaryEn=summary_en,
        category=category, platform=platform, appliesTo=applies, appliesToEn=applies_en,
        warning=warning, warningEn=warning_en, tags=list(tags), sources=refs, steps=steps))


guide("ssh-key-linux", "Jak wygenerować klucz SSH w Linuxie", "Generate an SSH key on Linux", "Klucz Ed25519, odcisk i bezpieczne przekazanie części publicznej.", "Ed25519 key, fingerprint and safe public-key sharing.", "security", "Linux", "Linux z klientem OpenSSH", "Linux with OpenSSH client", [sshkey, ssh], [
    step("Sprawdź istniejące pliki", "Check existing files", "Wykonaj na komputerze, z którego będziesz się łączyć. Jeśli klucz już istnieje, wybierz inną nazwę; nie zastępuj go bez planu migracji.", "Run on the computer you will connect from. If a key exists, choose another name; do not replace it without a migration plan.", "ls -la -- ~/.ssh"),
    step("Utwórz klucz", "Create the key", "Zmień komentarz. Program zapyta o plik i hasło ochronne; wpisz hasło w pytaniu, nie w komendzie. Domyślnie powstaną id_ed25519 i id_ed25519.pub.", "Change the comment. The program prompts for a file and protective passphrase; enter it at the prompt, not in the command. Defaults are id_ed25519 and id_ed25519.pub.", "ssh-keygen -t ed25519 -a 64 -C 'user@example.com'", "Modify"),
    step("Sprawdź odcisk części publicznej", "Check the public fingerprint", "Plik .pub można przekazać administratorowi serwera. Plik bez .pub jest prywatny: nie wklejaj go do zgłoszeń ani logów.", "The .pub file can be shared with the server administrator. The file without .pub is private: keep it out of tickets and logs.", "ssh-keygen -lf ~/.ssh/id_ed25519.pub"),
    step("Wybierz klucz w ServerDesk", "Select the key in ServerDesk", "W profilu SSH ustaw Klucz prywatny i wskaż plik bez .pub. Administrator dopisuje część publiczną do authorized_keys właściwego konta. Zweryfikuj odcisk hosta przy pierwszym połączeniu.", "Choose Private key in the SSH profile and select the file without .pub. The administrator adds the public part to the correct account's authorized_keys. Verify the host fingerprint on first connection."),
], tags=["ssh-keygen", "klucz", "key", "ed25519", "authorized_keys", "ssh"])
guide("ssh-key-windows", "Jak wygenerować klucz SSH w Windows", "Generate an SSH key on Windows", "OpenSSH w PowerShell i lokalizacja plików użytkownika.", "OpenSSH in PowerShell and user key locations.", "security", "Windows", "Windows z zainstalowanym klientem OpenSSH", "Windows with the OpenSSH client installed", [win_key, sshkey], [
    step("Sprawdź klienta i katalog", "Check client and directory", "Otwórz PowerShell. Brak ssh-keygen oznacza konieczność zainstalowania funkcji Klient OpenSSH. Domyślny katalog to .ssh w profilu bieżącego użytkownika.", "Open PowerShell. If ssh-keygen is missing, install the OpenSSH Client optional feature. The default directory is .ssh in the current user's profile.", 'Get-Command ssh-keygen\nGet-ChildItem -Force -LiteralPath "$HOME\\.ssh"'),
    step("Wygeneruj Ed25519", "Generate Ed25519", "Podaj komentarz i odpowiedz na pytania o ścieżkę i passphrase. Nie potwierdzaj nadpisania istniejącego pliku.", "Set a comment and answer the path/passphrase prompts. Do not approve overwriting an existing file.", "ssh-keygen -t ed25519 -a 64 -C 'user@example.com'", "Modify"),
    step("Odczytaj wyłącznie klucz publiczny", "Read only the public key", "Wysyłaj plik z rozszerzeniem .pub. Nie zmieniaj ACL klucza prywatnego na dostęp dla wszystkich.", "Share the file ending in .pub. Do not grant everyone access to the private key's ACL.", 'Get-Content -LiteralPath "$HOME\\.ssh\\id_ed25519.pub"'),
    step("Zapisz profil SSH", "Save an SSH profile", "W ServerDesk wybierz plik id_ed25519 jako Klucz prywatny. Opcja Klucz SSH z sejfu służy do osobnego wpisu SSH Key w Bitwarden/Vaultwarden. Login to konto na serwerze docelowym.", "Select id_ed25519 as a Private key in ServerDesk. SSH key from vault instead uses a separate SSH Key item in Bitwarden/Vaultwarden. The username is an account on the destination server."),
], tags=["ssh-keygen", "klucz", "key", "ed25519", "powershell"])
guide("linux-network-owner", "Linux: gdzie zmieniać ustawienia sieci", "Linux: where to change network settings", "Najpierw ustal, który menedżer zarządza interfejsem.", "First identify which manager owns the interface.", "network", "Linux", "Linux; dostępność poleceń zależy od instalacji", "Linux; command availability depends on installation", [debian, nm, netplan_yaml, networkd], [
    step("Zapisz stan wyjściowy", "Record the current state", "Zanotuj adresy, nazwę interfejsu i bramę. Zmiany ip są zwykle ulotne; trwałą konfigurację wprowadza się w narzędziu zarządzającym siecią.", "Record addresses, interface name and gateway. ip changes are usually temporary; persistent settings belong to the active network manager.", "ip -brief address\nip route show"),
    step("Ustal właściciela interfejsu", "Find the interface owner", "Sprawdź nmcli device status i networkctl status. Sama obecność lub aktywność usługi nie dowodzi, że zarządza konkretnym interfejsem. Nie konfiguruj go jednocześnie w dwóch menedżerach.", "Check nmcli device status and networkctl status. A running service alone does not prove it owns a particular interface. Do not configure it in two managers at once.", "nmcli device status\nnetworkctl status"),
    step("Wybierz właściwą konfigurację", "Choose the correct configuration", "NetworkManager: zapisane profile; Netplan: /etc/netplan/*.yaml; networkd: /etc/systemd/network/*.network; ifupdown: /etc/network/interfaces. Cloud-init może ponownie generować pliki.", "NetworkManager: saved profiles; Netplan: /etc/netplan/*.yaml; networkd: /etc/systemd/network/*.network; ifupdown: /etc/network/interfaces. Cloud-init may regenerate files."),
    step("Sprawdź po zmianie", "Verify after changing", "Porównaj adres, trasę i DNS. Testuj oddzielnie dostęp po IP i po nazwie; błąd DNS nie oznacza automatycznie braku sieci.", "Compare address, route and DNS. Test IP reachability separately from name resolution; DNS failure does not automatically mean the network is down.", "ip -brief address\nip route show\ngetent hosts example.com"),
], warning="Zmiana adresu, bramy lub profilu może zerwać SSH/RDP. Przed zmianą przygotuj konsolę i sposób powrotu.", warning_en="Changing address, gateway or profile may disconnect SSH/RDP. Prepare console access and a recovery plan first.", tags=["ip", "siec", "network", "dns", "adres", "gateway", "brama", "konfiguracja"])
guide("networkmanager-profile", "NetworkManager: profil, adres i DNS", "NetworkManager: profile, address and DNS", "Odróżnij zapisany profil od aktywnego urządzenia.", "Distinguish a saved profile from an active device.", "network", "Linux", "Interfejsy obsługiwane przez NetworkManager", "Interfaces managed by NetworkManager", [nm], [
    step("Znajdź profil i urządzenie", "Find profile and device", "Nazwy profilu i interfejsu mogą się różnić. Zanotuj UUID oraz nazwę aktywnego profilu.", "Profile and interface names may differ. Record the UUID and active profile name.", "nmcli connection show\nnmcli device status"),
    step("Otwórz ustawienia", "Open settings", "W środowisku graficznym otwórz ustawienia połączenia. W konsoli możesz użyć nmtui, jeśli jest zainstalowane. Ustaw adres/prefiks, bramę i DNS w jednym wybranym profilu.", "Use the desktop connection settings. At a console, nmtui is an option if installed. Set address/prefix, gateway and DNS in one chosen profile.", "sudo nmtui", "Modify"),
    step("Sprawdź profil przed aktywacją", "Review before activation", "Zapis profilu nie jest tym samym co aktywacja. Pliki profili zwykle znajdują się w /etc/NetworkManager/system-connections/ i mogą zawierać sekrety; nie publikuj ich.", "Saving a profile is separate from activation. Profile files usually live in /etc/NetworkManager/system-connections/ and may contain secrets; do not publish them."),
    step("Zweryfikuj aktywny stan", "Verify active state", "Aktywuj profil świadomie z dostępną konsolą awaryjną. Potem sprawdź stan urządzenia, trasę i DNS; nie zakładaj, że sam zapis zmienił działające połączenie.", "Activate deliberately with recovery console access. Then check device state, route and DNS; saving alone does not prove the running connection changed.", "nmcli device show\nip route show"),
], warning="Ponowna aktywacja profilu może odłączyć bieżącą sesję. Nie włączaj --show-secrets do zwykłej diagnostyki.", warning_en="Reactivating a profile can disconnect the session. Do not enable --show-secrets for ordinary diagnostics.", tags=["nmcli", "nmtui", "dns", "networkmanager", "static", "siec", "ip"])
guide("netplan-network", "Ubuntu / Netplan: trwała konfiguracja sieci", "Ubuntu / Netplan: persistent network configuration", "Pliki YAML, sprawdzenie składni i próba z limitem czasu.", "YAML files, syntax checking and a time-limited trial.", "network", "Linux", "Systemy z Netplan; wybierz backend zgodny z istniejącą instalacją", "Systems using Netplan; retain the installation's existing renderer", [netplan_yaml, netplan], [
    step("Znajdź źródło konfiguracji", "Find the configuration source", "Sprawdź pliki /etc/netplan/*.yaml oraz adnotacje cloud-init. Nie edytuj wygenerowanych plików w /run. Przed zmianą zachowaj kopię konfiguracji i jej uprawnienia.", "Inspect /etc/netplan/*.yaml and cloud-init notices. Do not edit generated files under /run. Preserve a configuration backup and its permissions first.", "ls -l -- /etc/netplan"),
    step("Edytuj właściwy YAML", "Edit the correct YAML", "Użyj rzeczywistej nazwy interfejsu. Ustaw addresses z prefiksem, trasę domyślną i nameservers według dokumentacji. Zachowaj renderer networkd albo NetworkManager. Nie wklejaj przykładowych adresów w ciemno.", "Use the real interface name. Set addresses with prefixes, a default route and nameservers using the reference. Retain the networkd or NetworkManager renderer. Do not blindly paste sample addresses.", risk="Modify"),
    step("Sprawdź i wypróbuj", "Validate and try", "generate sprawdza konfigurację i generuje pliki backendu. try wymaga świadomego potwierdzenia zmian przed upływem czasu. W razie problemu sprawdź, czy wycofanie faktycznie nastąpiło.", "generate validates configuration and generates backend files. try requires deliberate confirmation before its deadline. If it fails, verify that rollback actually occurred.", "sudo netplan generate\nsudo netplan try --timeout 120", "Dangerous"),
    step("Potwierdź działanie", "Confirm operation", "Sprawdź adresy, trasy i rozwiązywanie nazw z drugiej sesji. netplan apply stosuje zmiany bez interaktywnego okna wycofania.", "Check addresses, routes and name resolution from another session. netplan apply applies changes without an interactive rollback window.", "ip -brief address\nip route show\ngetent hosts example.com"),
], warning="Dostęp konsolowy i kopia są konieczne przed ryzykowną zmianą zdalnej sieci. Netplan dokumentuje ograniczenia rollbacku, zwłaszcza dla urządzeń wirtualnych.", warning_en="Arrange console access and a backup before risky remote network changes. Netplan documents rollback limitations, especially for virtual devices.", tags=["ubuntu", "netplan", "yaml", "network", "siec", "dns", "ip"])
guide("debian-ifupdown", "Debian: konfiguracja ifupdown", "Debian: ifupdown configuration", "Kiedy używać interfaces i dlaczego nie restartować sieci w ciemno.", "When interfaces applies and why not to restart networking blindly.", "network", "Linux", "Wyłącznie interfejsy zarządzane przez ifupdown", "Only interfaces managed by ifupdown", [interfaces, debian], [
    step("Sprawdź menedżer", "Check the manager", "Nie każdy Debian używa ifupdown. NetworkManager lub networkd mogą obsługiwać ten sam komputer, ale inne interfejsy.", "Not every Debian system uses ifupdown. NetworkManager or networkd may manage the same machine, but different interfaces.", "ip -brief address\nsystemctl status networking --no-pager"),
    step("Znajdź aktywne pliki", "Find active files", "Główny plik to /etc/network/interfaces. Pliki z interfaces.d są używane zgodnie z dyrektywami source/source-directory, a nie samym faktem istnienia katalogu.", "The main file is /etc/network/interfaces. interfaces.d files are included by source/source-directory directives, not merely because that directory exists.", "cat /etc/network/interfaces"),
    step("Zmień tylko właściwy interfejs", "Change only the intended interface", "Zachowaj kopię; dopasuj stanza iface i rodzinę inet/inet6. Konfigurację DNS dopasuj do zainstalowanego resolvera. W dokumentacji sprawdź składnię swojej wersji.", "Keep a backup; match the iface stanza and inet/inet6 family. Adapt DNS to the installed resolver. Check your version's syntax in the reference.", risk="Modify"),
    step("Zastosuj z planem powrotu", "Apply with a recovery plan", "ifdown odłącza interfejs. Nie wykonuj restartu całej sieci przez jedyną sesję SSH. Po zastosowaniu porównaj trasę i rozwiązywanie nazw.", "ifdown disconnects an interface. Do not restart all networking through your only SSH session. After applying, compare routes and name resolution.", "ip route show\ngetent hosts example.com"),
], tags=["debian", "interfaces", "ifupdown", "network", "siec", "dns"])
guide("networkd-config", "systemd-networkd: pliki .network", "systemd-networkd: .network files", "Dopasowanie interfejsu, trwałe pliki i kontrola backendu.", "Interface matching, persistent files and backend verification.", "network", "Linux", "Interfejsy zarządzane bezpośrednio przez systemd-networkd", "Interfaces managed directly by systemd-networkd", [networkd], [
    step("Sprawdź właściciela", "Check ownership", "networkctl pokaże stan łącza i plik konfiguracji, jeśli jest dostępny. Netplan może generować konfigurację networkd — wtedy zmieniaj źródłowy YAML.", "networkctl shows link state and the configuration file when available. Netplan may generate networkd configuration; in that case edit the source YAML.", "networkctl status"),
    step("Znajdź trwały plik", "Find the persistent file", "Lokalne pliki administratora są w /etc/systemd/network/*.network. W /run są dane ulotne. Kolejność plików i dopasowanie [Match] wpływają na wybór konfiguracji.", "Local administrator files live in /etc/systemd/network/*.network. /run is temporary. File ordering and [Match] determine which configuration applies.", "ls -l -- /etc/systemd/network"),
    step("Edytuj ostrożnie", "Edit carefully", "W sekcji [Match] dopasuj właściwe łącze, a w [Network] ustaw adresy, DHCP lub DNS. Zapisz kopię i zapewnij konsolę przed rekonfiguracją.", "Match the intended link in [Match]; set addresses, DHCP or DNS in [Network]. Keep a backup and arrange console access before reconfiguration.", risk="Modify"),
    step("Zweryfikuj efekt", "Verify the result", "Sposób reload/reconfigure dobierz do zainstalowanej wersji networkctl. Sprawdź logi jednostki i stan IP; nie uruchamiaj drugiego menedżera na tym samym interfejsie.", "Choose reload/reconfigure according to the installed networkctl version. Check unit logs and IP state; do not activate a second manager for the same interface.", "journalctl -u systemd-networkd -n 50 --no-pager\nip -brief address"),
], tags=["systemd", "networkd", "networkctl", "network", "siec"])
guide("windows-network", "Windows: gdzie zmienić IP i DNS", "Windows: where to change IP and DNS", "Właściwy adapter, ustawienia systemu i kontrola PowerShell.", "The correct adapter, system settings and PowerShell checks.", "network", "Windows", "Windows 10/11 i Windows Server; ścieżki GUI zależą od edycji", "Windows 10/11 and Server; GUI paths depend on edition", [win_ip, win_network], [
    step("Zidentyfikuj adapter", "Identify the adapter", "Zanotuj InterfaceAlias i InterfaceIndex. Nie pomyl adaptera fizycznego z VPN, Hyper-V albo rozłączonym urządzeniem.", "Record InterfaceAlias and InterfaceIndex. Distinguish the physical adapter from VPN, Hyper-V or disconnected devices.", "Get-NetIPConfiguration -All"),
    step("Otwórz właściwości sieci", "Open network properties", "W Windows 11: Ustawienia → Sieć i Internet → wybrane połączenie → Przypisanie adresu IP / DNS. Klasyczne właściwości adapterów otwiera ncpa.cpl. Server Core wymaga narzędzi konsolowych.", "On Windows 11: Settings → Network & internet → selected connection → IP assignment / DNS assignment. ncpa.cpl opens classic adapter properties. Server Core requires console tools."),
    step("Wprowadź dane dla właściwej sieci", "Enter settings for the correct network", "Przed zmianą zapisz poprzedni stan i zapewnij alternatywny dostęp. Ustaw adres, prefiks/maskę, bramę i DNS zgodnie z planem sieci; nie kopiuj cudzych adresów.", "Record the old state and arrange alternative access first. Set address, prefix/mask, gateway and DNS for your network; do not copy someone else's addresses.", risk="Modify"),
    step("Sprawdź adres i DNS", "Verify address and DNS", "Sprawdź aktywną konfigurację i oddzielnie zapytanie DNS. Logi klientów VPN i reguły zapory mogą wyjaśnić brak dostępu mimo poprawnego adresu.", "Check active configuration and a separate DNS query. VPN logs and firewall rules may explain lack of access even when the address is correct.", "Get-NetIPConfiguration -Detailed\nGet-DnsClientServerAddress\nResolve-DnsName example.com"),
], warning="Zmiana adaptera używanego przez RDP może natychmiast zerwać pulpit. W środowisku zdalnym zapewnij konsolę lub inny kanał zarządzania.", warning_en="Changing the RDP network adapter may immediately disconnect the desktop. Arrange a console or another management channel.", tags=["windows", "ip", "dns", "network", "siec", "adapter", "rdp"])
guide("linux-journal", "Linux: jak sprawdzać logi", "Linux: how to inspect logs", "Rozruch, konkretna usługa, zakres czasu i śledzenie zmian.", "Boot, service, time range and following new entries.", "logs", "Linux", "Linux z systemd-journald; inne systemy mogą używać plików/syslog", "Linux with systemd-journald; other systems may use files/syslog", [journal, tail], [
    step("Zacznij od bieżącego rozruchu", "Start with the current boot", "Ogranicz wynik do ostatnich wpisów. Brak dostępu do dziennika może wymagać innej roli; nie zmieniaj uprawnień logów dla wszystkich.", "Limit the output to recent entries. Journal access may require another role; do not make logs world-readable.", "journalctl -b -n 100 --no-pager"),
    step("Wybierz usługę i czas", "Choose a service and time", "Zastąp nazwę jednostki właściwą dla serwera; SSH bywa ssh.service albo sshd.service. Filtr czasu ułatwia powiązanie logu z konkretnym problemem.", "Replace the unit name for your server; SSH may be ssh.service or sshd.service. Time filters help correlate entries with the problem.", "journalctl -u ssh --since '1 hour ago' -n 100 --no-pager"),
    step("Śledź na żywo", "Follow live entries", "-f czeka na nowe zdarzenia. Ctrl+C kończy obserwację. Wpisy mogą zawierać prywatne adresy i identyfikatory; sprawdź je przed udostępnieniem.", "-f waits for new events. Ctrl+C stops following. Entries may contain private addresses and identifiers; review before sharing.", "journalctl -u ssh -f"),
    step("Gdy usługa zapisuje do pliku", "When a service logs to a file", "Ścieżkę ustal w konfiguracji usługi. /var/log i kontenery nie gwarantują jednego wspólnego pliku. GNU tail -F radzi sobie z częścią przypadków rotacji.", "Find the path in the service configuration. /var/log and containers do not guarantee a single shared file. GNU tail -F handles common rotation cases.", "ls -lah -- /var/log"),
], tags=["journalctl", "logi", "logs", "bledy", "tail", "systemd", "ssh"])
guide("windows-event-log", "Windows: Podgląd zdarzeń i PowerShell", "Windows: Event Viewer and PowerShell", "Dzienniki System/Application, filtry poziomu i czasu.", "System/Application logs with level and time filters.", "logs", "Windows", "Windows; PowerShell z Get-WinEvent", "Windows; PowerShell with Get-WinEvent", [win_event], [
    step("Wybierz dziennik", "Choose a log", "Otwórz Podgląd zdarzeń → Dzienniki systemu Windows → System lub Aplikacja. Dodatkowe kanały są w Dzienniki aplikacji i usług. Sprawdź uprawnienia, jeśli log jest niedostępny.", "Open Event Viewer → Windows Logs → System or Application. Additional channels are under Applications and Services Logs. Check permissions if access is denied.", "Get-WinEvent -ListLog System,Application"),
    step("Odczytaj ostatnie zdarzenia", "Read recent events", "MaxEvents ogranicza wynik. Domyślnie najnowsze wpisy są pierwsze. Nie czyść dziennika podczas diagnozowania.", "MaxEvents limits output. Newest entries come first by default. Do not clear the log during diagnosis.", "Get-WinEvent -LogName System -MaxEvents 30"),
    step("Filtruj na źródle", "Filter at the source", "Poziomy 2 i 3 oznaczają Error i Warning. FilterHashtable zawęża wynik po stronie dziennika i bywa skuteczniejszy niż pobieranie wszystkich wpisów.", "Levels 2 and 3 mean Error and Warning. FilterHashtable narrows the query at the event log instead of retrieving every entry.", "Get-WinEvent -FilterHashtable @{LogName='System';Level=2,3;StartTime=(Get-Date).AddHours(-1)} -MaxEvents 50"),
    step("Powiąż z objawem", "Correlate with the symptom", "Porównaj czas, dostawcę, Event ID i treść. Sam numer zdarzenia bez nazwy dostawcy nie identyfikuje jednoznacznie przyczyny. Przed wysłaniem usuń prywatne dane.", "Compare timestamp, provider, Event ID and message. An event number alone without its provider does not uniquely identify the cause. Remove private data before sharing."),
], tags=["windows", "logi", "logs", "get-winevent", "event", "bledy", "powershell"])
guide("systemd-service", "Linux: diagnoza niedziałającej usługi", "Linux: diagnose a failed service", "Stan, definicja jednostki i powiązany dziennik.", "State, unit definition and related journal.", "services", "Linux", "Linux z systemd; przykładowa jednostka ssh", "Linux with systemd; example ssh unit", [systemctl, journal], [
    step("Sprawdź stan", "Check state", "Zastąp ssh właściwą jednostką. Włączona przy starcie nie znaczy działająca teraz; active/failed i enabled to osobne informacje.", "Replace ssh with the correct unit. Enabled at boot does not mean running now; active/failed and enabled are separate facts.", "systemctl status ssh --no-pager --full\nsystemctl list-units --failed --no-pager"),
    step("Odczytaj dziennik", "Read the journal", "Szukaj pierwszego błędu z czasu uruchomienia, a nie tylko ostatniego komunikatu o zakończeniu procesu.", "Look for the first error at startup time, not just the final process-exit message.", "journalctl -u ssh -b -n 100 --no-pager"),
    step("Sprawdź jednostkę", "Inspect the unit", "systemctl cat pokazuje jednostkę i nadpisania. Treść może zawierać ustawienia środowiska; przed publikacją usuń sekrety. Po zmianie definicji potrzebny bywa daemon-reload.", "systemctl cat shows the unit and overrides. Content may include environment settings; remove secrets before sharing. Unit-definition changes may need daemon-reload.", "systemctl cat ssh"),
    step("Dopiero potem naprawiaj", "Repair after diagnosis", "Sprawdź konfigurację poleceniem walidującym właściwym dla usługi. Restart zmienia stan i może rozłączyć sesję; wykonaj go świadomie z dostępnym planem powrotu.", "Use the service's own configuration validator. Restart changes state and may disconnect the session; do it deliberately with a recovery plan."),
], tags=["systemctl", "uslugi", "service", "failed", "bledy", "journalctl"])
guide("docker-log-check", "Docker: jak czytać logi kontenera", "Docker: read container logs", "Nazwa kontenera, ograniczony wynik i obserwacja w czasie.", "Container name, bounded output and live following.", "devops", "Linux", "Host z dostępem do demona Docker; przykład kontenera web", "Host with Docker daemon access; example container web", [docker], [
    step("Wybierz właściwy kontener", "Choose the right container", "Podmień web na nazwę lub ID. Uprawnienia do demona Docker dają szeroki dostęp do hosta; nie zmieniaj grup użytkownika tylko po to, by ominąć błąd dostępu.", "Replace web with a name or ID. Docker daemon access is powerful; do not change group membership just to bypass access denial.", "docker ps -a"),
    step("Ogranicz wynik", "Bound the output", "Odczytaj ostatnie wiersze i znaczniki czasu. Obsługa zależy od sterownika logowania i sposobu zapisu aplikacji.", "Read recent lines with timestamps. Support depends on the logging driver and how the application writes logs.", "docker logs --tail 100 --timestamps web"),
    step("Śledź zakres czasu", "Follow a time range", "--since 30m zawęża początek, a --follow dopisuje nowe wpisy. Ctrl+C kończy obserwację, nie zatrzymuje kontenera.", "--since 30m narrows the start; --follow streams new entries. Ctrl+C stops following without stopping the container.", "docker logs --since 30m --follow web"),
    step("Gdy brakuje komunikatów", "When messages are missing", "Aplikacja może zapisywać do własnego pliku zamiast stdout/stderr. Sprawdź jej konfigurację i driver. Nie usuwaj kontenera ani wolumenów w ramach samego odczytu logów.", "The application may log to its own file rather than stdout/stderr. Check its configuration and driver. Reading logs does not require deleting containers or volumes."),
], tags=["docker", "kontener", "container", "logi", "logs", "tail"])
guide("linux-disk-space", "Linux: co zajmuje miejsce na dysku", "Linux: investigate disk usage", "System plików, i-węzły i rozmiary katalogów bez usuwania danych.", "Filesystem usage, inodes and directory sizes without deleting data.", "storage", "Linux", "GNU df/du; BusyBox może mieć inne opcje", "GNU df/du; BusyBox options may differ", [df, du], [
    step("Sprawdź właściwy mount", "Check the correct mount", "df raportuje systemy plików. Zwróć uwagę, czy problem dotyczy /, /var czy osobnego woluminu.", "df reports filesystems. Determine whether /, /var or a separate volume is affected.", "df -hT"),
    step("Sprawdź i-węzły", "Check inodes", "Brak i-węzłów może uniemożliwiać tworzenie plików mimo wolnych bajtów. Dotyczy to często bardzo wielu małych plików.", "Inode exhaustion can prevent file creation despite free bytes, often because of many small files.", "df -i"),
    step("Zawęź duży katalog", "Narrow down a large directory", "Podmień /var na badany katalog. -x pozostaje na tym samym systemie plików; błędy dostępu mogą zaniżyć wynik. Duże drzewo wymaga czasu.", "Replace /var with the directory under investigation. -x stays on one filesystem; access errors may undercount. Large trees take time.", "du -x -h --max-depth=1 -- /var"),
    step("Wyjaśnij rozbieżności", "Explain differences", "df i du mierzą inne rzeczy. Przyczyną różnic mogą być otwarte usunięte pliki, snapshoty, rezerwacje lub brak uprawnień. Nie kasuj logów i baz bez sprawdzenia retencji i kopii.", "df and du measure different things. Open deleted files, snapshots, reserved space or permissions may explain differences. Review retention and backups before deleting logs or databases."),
], tags=["df", "du", "disk", "dysk", "miejsce", "inode", "storage"])
guide("ssh-debug", "SSH: jak ustalić powód problemu z logowaniem", "SSH: investigate login failure", "Rozdziel połączenie TCP, tożsamość hosta i uwierzytelnianie.", "Separate TCP connection, host identity and authentication.", "security", "General", "Klient OpenSSH i dostęp do logów serwera przez administratora", "OpenSSH client and administrator access to server logs", [ssh, sshkey], [
    step("Sprawdź etap błędu", "Identify the failure stage", "Timeout, connection refused i permission denied oznaczają różne etapy. Zweryfikuj adres, port, VPN oraz login zapisany w profilu ServerDesk.", "Timeout, connection refused and permission denied refer to different stages. Check address, port, VPN and the ServerDesk profile username."),
    step("Włącz diagnostykę klienta", "Enable client diagnostics", "Zastąp host i login. Czytaj, czy klient dociera do serwera i jakie metody proponuje. Nie wyłączaj kontroli klucza hosta i nie publikuj całego logu bez przeglądu.", "Replace host and username. Inspect whether the client reaches the server and which methods it offers. Keep host-key checks enabled and review the log before sharing.", "ssh -v user@server.example"),
    step("Porównaj klucz publiczny", "Compare the public key", "Sprawdź odcisk .pub i authorized_keys właściwego konta na serwerze. Windows OpenSSH może mieć osobne reguły dla kont administratorów. Nie wysyłaj klucza prywatnego administratorowi.", "Compare the .pub fingerprint with the correct server account's authorized_keys. Windows OpenSSH may have separate administrator-account rules. Do not send the administrator your private key."),
    step("Skoreluj z logiem serwera", "Correlate server logs", "Administrator sprawdza log SSH i zasady konta w czasie próby. Sam klient często nie otrzymuje szczegółowej przyczyny odrzucenia; kolejne próby w ciemno mogą blokować konto.", "The administrator checks SSH logs and account policy at the attempt time. The client often does not receive the detailed rejection reason; blind retries may lock the account."),
], tags=["ssh", "debug", "logowanie", "login", "permission", "blad", "klucz", "key"])

assert len({s["id"] for s in sources}) == len(sources)
assert len({t["id"] for t in tools}) == len(tools)
assert len({g["id"] for g in guides}) == len(guides)
target = Path(__file__).resolve().parents[1]/"src/ServerDesk.Core/Commands/knowledge.v1.json"
target.write_text(json.dumps(dict(schemaVersion=1, version="1.0.0", reviewedOn="2026-10-22", sources=sources, tools=tools, guides=guides), ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
print(f"Wrote {len(guides)} mini guides, {len(tools)} tools, {sum(len(t['options']) for t in tools)} options and {len(sources)} official sources")
