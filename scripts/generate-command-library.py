#!/usr/bin/env python3
"""Maintain the offline command data. No network access or command execution.

Run from the repository root with Python 3. The generated JSON is embedded by Core.
Each row is one independently reviewable variant, keyed by stable operation + ID.
"""
import json
from pathlib import Path

rows = []


def param(key, label, kind="Text", default="", required=True, choices=None):
    return dict(key=key, label=label, kind=kind, default=default,
                required=required, choices=choices or [])


service = param("service", "Service / usługa", default="ssh")
path = param("path", "Path / ścieżka", "FilePath", "/var/log")
host = param("host", "Host", "Hostname", "localhost")
package = param("package", "Package / pakiet", default="curl")
container = param("container", "Container / kontener", default="web")
user = param("user", "User / użytkownik", default="admin")


def add(id, name, en, template, category, tools=(), *, summary="", detail="",
        summary_en="", detail_en="", risk="ReadOnly", platform="Linux",
        shell=None, family="", distros=(), params=(), tags=(), operation=None,
        privileges="user", sub="", minimum="", maximum="", examples=(), priority=0):
    selected_shell = shell or ("PowerShell" if platform == "Windows" else "Bash")
    tools = list(tools)
    if template.startswith("sudo ") and "sudo" not in tools:
        tools.append("sudo")
    if "docker compose " in template:
        tools.append("docker-compose-plugin")
    if not examples and params:
        example = template
        for p in params:
            value = p["default"]
            quoted = '"'+value+'"' if selected_shell == "Cmd" else "'"+value.replace("'", "'\"'\"'")+"'"
            example = example.replace("{{"+p["key"]+"}}", quoted)
        examples = [example]
    rows.append(dict(id=id, operation=operation or id, name=name, nameEn=en,
        summary=summary or name, summaryEn=summary_en or en,
        explanation=detail or ((summary or name) + ". " +
            ("Zmienia stan hosta; sprawdź cel i uprawnienia przed wykonaniem." if risk != "ReadOnly" else
             "Odczytuje dane hosta. Wynik zależy od uprawnień użytkownika i dostępności narzędzia.")),
        explanationEn=detail_en or ((summary_en or en) + ". " +
            ("Changes host state; verify the target and permissions before executing." if risk != "ReadOnly" else
             "Reads host data. Results depend on user permissions and tool availability.")),
        template=template, category=category, subcategory=sub, tags=[t for t in tags if t],
        platform=platform, shell=selected_shell,
        family=family, distributions=list(distros), minVersion=minimum, maxVersion=maximum,
        tools=list(tools), privileges=privileges, risk=risk, origin="BuiltIn",
        parameters=list(params), examples=list(examples), priority=priority))


# Portable and generic Linux layer.
add("general.identity", "Bieżący użytkownik", "Current user", "whoami", "system", ["whoami"], platform="General", shell="Any", tags=["login", "identity", "tozsamosc"])
add("general.hostname", "Nazwa komputera", "Computer name", "hostname", "system", ["hostname"], platform="General", shell="Any", tags=["host", "nazwa"])
for id, pl, en, cmd, tool, tags in [
    ("kernel", "Wersja jądra i architektura", "Kernel and architecture", "uname -a", "uname", ["system", "kernel", "architektura"]),
    ("release", "Dystrybucja i wersja", "Distribution and release", "cat /etc/os-release", "cat", ["os", "linux", "wersja"]),
    ("uptime", "Czas pracy i obciążenie", "Uptime and load", "uptime", "uptime", ["load", "obciazenie"]),
    ("memory", "Pamięć RAM", "Memory usage", "free -h", "free", ["ram", "pamiec"]),
    ("processes", "Procesy według CPU", "Processes by CPU", "ps aux --sort=-%cpu", "ps", ["cpu", "process", "proces"]),
    ("sessions", "Zalogowani użytkownicy", "Logged-in users", "who", "who", ["sesje", "users"]),
    ("time", "Data i strefa czasowa", "Date and time zone", "date -Iseconds", "date", ["czas", "time"]),
    ("environment", "Wybrana zmienna środowiskowa", "Named environment variable", "printenv {{variable}}", "printenv", ["env", "srodowisko"]),
]:
    add("linux."+id, pl, en, cmd, "system", [tool], tags=tags,
        params=[param("variable", "Variable / zmienna", default="SHELL")] if id == "environment" else [])
add("alpine.processes", "Procesy Alpine Linux", "Alpine Linux processes", "ps", "system", ["ps"], distros=["alpine"], operation="linux.processes", priority=4, detail="BusyBox ps nie gwarantuje opcji GNU --sort. Ten wariant pokazuje przenośną listę procesów.", detail_en="BusyBox ps does not guarantee GNU --sort support. This variant displays the portable process list.")

network = [
    ("listeners", "Otwarte porty", "Listening ports", "ss -tulpn", "ss", ["otwarte porty", "open ports", "nasluch", "listen", "sockets"]),
    ("connections", "Aktywne połączenia TCP", "Active TCP connections", "ss -tpn", "ss", ["tcp", "connections", "polaczenia"]),
    ("addresses", "Adresy interfejsów", "Interface addresses", "ip -brief address", "ip", ["ip", "adres", "interface"]),
    ("routes", "Tablica routingu", "Routing table", "ip route show", "ip", ["routing", "brama", "gateway"]),
    ("neighbors", "Tablica sąsiadów ARP", "ARP neighbor table", "ip neighbor show", "ip", ["arp", "mac", "neighbors"]),
    ("dns", "Rozwiąż nazwę DNS", "Resolve DNS name", "dig {{host}}", "dig", ["dns", "resolve", "nazwa"]),
    ("nslookup", "Zapytanie DNS nslookup", "DNS lookup with nslookup", "nslookup {{host}}", "nslookup", ["dns"]),
    ("ping", "Sprawdź osiągalność", "Check reachability", "ping -c 4 {{host}}", "ping", ["ping", "osiagalnosc", "icmp"]),
    ("trace", "Trasa pakietów", "Trace packet route", "traceroute {{host}}", "traceroute", ["trasa", "hops", "trace"]),
    ("http", "Nagłówki odpowiedzi HTTP", "HTTP response headers", "curl -I --max-time 15 -- {{url}}", "curl", ["http", "headers", "naglowki"]),
    ("network-manager", "Połączenia NetworkManager", "NetworkManager connections", "nmcli connection show", "nmcli", ["networkmanager", "siec"]),
    ("network-devices", "Urządzenia NetworkManager", "NetworkManager devices", "nmcli device status", "nmcli", ["wifi", "networkmanager"]),
    ("netplan", "Konfiguracja Netplan", "Netplan configuration", "netplan get", "netplan", ["netplan", "yaml"]),
    ("socket-files", "Procesy używające portu", "Processes using a port", "lsof -i :{{port}}", "lsof", ["port", "process"]),
]
for id, pl, en, cmd, tool, tags in network:
    params = [host] if "{{host}}" in cmd else [param("url", "URL", default="https://example.com")] if "{{url}}" in cmd else []
    if id == "socket-files":
        # Keep the argument a complete token, including the -i option's argument.
        cmd = "lsof -i {{endpoint}}"; params = [param("endpoint", "Endpoint", default=":443")]
    add("linux.net."+id, pl, en, cmd, "network", [tool], params=params, tags=tags)
add("linux.net.report", "Krótki raport sieciowy", "Short network report", "ip -brief address\nip route show\nss -tuln", "network", ["ip", "ss"], tags=["skrypt", "script", "raport"], detail="Trzy odczyty diagnostyczne. Skrypt wieloliniowy jest dostępny wyłącznie do podglądu i kopiowania.", detail_en="Three diagnostic reads. This multiline script is available for preview and copying only.")

# Service management variants share stable operations. OpenRC is selected by tools,
# never just because the distro happens to be Alpine.
for id, pl, en, cmd, risk in [
    ("status", "Sprawdź usługę", "Check service", "systemctl status --no-pager -- {{service}}", "ReadOnly"),
    ("start", "Uruchom usługę", "Start service", "sudo systemctl start -- {{service}}", "Modify"),
    ("stop", "Zatrzymaj usługę", "Stop service", "sudo systemctl stop -- {{service}}", "Dangerous"),
    ("restart", "Zrestartuj usługę", "Restart service", "sudo systemctl restart -- {{service}}", "Dangerous"),
    ("enable", "Włącz autostart usługi", "Enable service at boot", "sudo systemctl enable -- {{service}}", "Modify"),
    ("disable", "Wyłącz autostart usługi", "Disable service at boot", "sudo systemctl disable -- {{service}}", "Dangerous"),
]:
    add("systemd."+id, pl, en, cmd, "services", ["systemctl"], params=[service], operation="service."+id, risk=risk, privileges="user" if risk == "ReadOnly" else "sudo / root", tags=["usluga", "service", "systemd", "sprawdz usluge"] if id == "status" else ["service", "systemd"], priority=2)
    if id in ("status", "start", "stop", "restart"):
        add("openrc."+id, pl+" (OpenRC)", en+" (OpenRC)", "rc-service {{service}} "+id, "services", ["rc-service"], params=[service], operation="service."+id, risk=risk, privileges="user" if risk == "ReadOnly" else "root", tags=["usluga", "service", "openrc"]+(["sprawdz usluge"] if id == "status" else []))
add("systemd.failed", "Usługi zakończone błędem", "Failed services", "systemctl --failed --no-pager", "services", ["systemctl"], tags=["failed", "blad", "systemd"])
add("systemd.units", "Lista usług", "List services", "systemctl list-units --type=service --no-pager", "services", ["systemctl"], operation="service.list", tags=["uslugi", "services"], priority=2)
add("openrc.units", "Lista usług OpenRC", "List OpenRC services", "rc-status -a", "services", ["rc-status"], operation="service.list", tags=["uslugi", "services", "openrc"])

# Distro family package managers, with explicit tool requirements and optional
# fallbacks for older RHEL hosts. Updates never silently install or reboot.
managers = [
    ("apt", "debian", "apt", "apt list --installed", "apt-cache show {{package}}", "apt list --upgradable", "sudo apt-get update", "sudo apt-get install -- {{package}}", ["apt-cache", "apt-get"]),
    ("dnf", "rhel", "dnf", "dnf list --installed", "dnf info {{package}}", "dnf check-update", "sudo dnf makecache", "sudo dnf install {{package}}", []),
    ("yum", "rhel", "yum", "yum list installed", "yum info {{package}}", "yum check-update", "sudo yum makecache", "sudo yum install {{package}}", []),
    ("pacman", "arch", "pacman", "pacman -Q", "pacman -Si {{package}}", "pacman -Qu", "sudo pacman -Syu", "sudo pacman -S {{package}}", []),
    ("apk", "alpine", "apk", "apk info", "apk info {{package}}", "apk version -l '<'", "apk update", "apk add {{package}}", []),
]
for id, family, tool, listed, info, outdated, refresh, install, extra in managers:
    for action, pl, en, cmd, risk, params in [
        ("list", "Zainstalowane pakiety", "Installed packages", listed, "ReadOnly", []),
        ("info", "Informacje o pakiecie", "Package information", info, "ReadOnly", [package]),
        ("outdated", "Dostępne aktualizacje pakietów", "Available package updates", outdated, "ReadOnly", []),
        ("refresh", "Odśwież indeks pakietów", "Refresh package index", refresh, "Modify", []),
        ("install", "Zainstaluj pakiet", "Install package", install, "Modify", [package]),
    ]:
        if id == "pacman" and action == "refresh":
            pl, en, risk = "Pełna aktualizacja Arch Linux", "Full Arch Linux upgrade", "Dangerous"
        required = [extra[0]] if id == "apt" and action == "info" else [extra[-1]] if id == "apt" and action in ("refresh", "install") else [tool]
        add("pkg."+id+"."+action, pl+" · "+id.upper(), en+" · "+id.upper(), cmd, "packages", required,
            family=family, operation="package."+action, params=params, risk=risk,
            privileges="user" if risk == "ReadOnly" else "root / sudo", tags=["pakiety", "packages", "updates", "aktualizacja", id],
            detail="Arch wymaga pełnej, spójnej aktualizacji; nie wykonuj częściowej aktualizacji baz pacman -Sy." if id == "pacman" and action == "refresh" else "",
            detail_en="Arch requires a coherent full upgrade; do not perform a partial pacman -Sy database upgrade." if id == "pacman" and action == "refresh" else "")

for id, pl, en, cmd, tool, params, risk in [
    ("list", "Pliki i uprawnienia", "Files and permissions", "ls -lah -- {{path}}", "ls", [path], "ReadOnly"),
    ("usage", "Rozmiar katalogu", "Directory size", "du -sh -- {{path}}", "du", [path], "ReadOnly"),
    ("find", "Znajdź pliki po nazwie", "Find files by name", "find {{path}} -name {{pattern}}", "find", [path, param("pattern", "Pattern / wzorzec", default="*.log")], "ReadOnly"),
    ("read", "Odczytaj plik", "Read a file", "cat -- {{path}}", "cat", [path], "ReadOnly"),
    ("tail", "Ostatnie linie pliku", "Last lines of a file", "tail -n 50 -- {{path}}", "tail", [path], "ReadOnly"),
    ("mkdir", "Utwórz katalog", "Create directory", "mkdir -p -- {{path}}", "mkdir", [path], "Modify"),
    ("copy", "Skopiuj plik", "Copy a file", "cp -i -- {{source}} {{destination}}", "cp", [param("source", "Source / źródło", "FilePath", "/tmp/source"), param("destination", "Destination / cel", "FilePath", "/tmp/copy")], "Modify"),
    ("move", "Przenieś plik", "Move a file", "mv -i -- {{source}} {{destination}}", "mv", [param("source", "Source / źródło", "FilePath", "/tmp/source"), param("destination", "Destination / cel", "FilePath", "/tmp/renamed")], "Modify"),
    ("chmod", "Zmień uprawnienia pliku", "Change file permissions", "chmod {{mode}} -- {{path}}", "chmod", [param("mode", "Mode / uprawnienia", "Choice", "640", choices=["600", "640", "644", "700", "750", "755"]), path], "Modify"),
    ("owner", "Zmień właściciela", "Change ownership", "sudo chown {{owner}} -- {{path}}", "chown", [param("owner", "Owner / właściciel", default="root:root"), path], "Modify"),
    ("remove", "Usuń pojedynczy plik", "Remove one file", "rm -i -- {{path}}", "rm", [path], "Dangerous"),
]:
    add("linux.files."+id, pl, en, cmd, "files", [tool], params=params, risk=risk, tags=["pliki", "files", "permissions", "uprawnienia"])
for id, pl, en, cmd, tool, params, risk in [
    ("id", "Identyfikatory użytkownika", "User identifiers", "id {{user}}", "id", [user], "ReadOnly"),
    ("users", "Lista kont", "List accounts", "getent passwd", "getent", [], "ReadOnly"),
    ("groups", "Lista grup", "List groups", "getent group", "getent", [], "ReadOnly"),
    ("create", "Utwórz konto", "Create account", "sudo useradd -m -- {{user}}", "useradd", [user], "Modify"),
    ("group", "Dodaj użytkownika do grupy", "Add user to group", "sudo usermod -aG {{group}} -- {{user}}", "usermod", [param("group", "Group / grupa", default="developers"), user], "Modify"),
    ("lock", "Zablokuj hasło konta", "Lock account password", "sudo passwd -l -- {{user}}", "passwd", [user], "Dangerous"),
]:
    add("linux.users."+id, pl, en, cmd, "users", [tool], params=params, risk=risk, privileges="user" if risk == "ReadOnly" else "sudo / root", tags=["konto", "user", "grupy", "groups"])
for id, pl, en, cmd, tool in [
    ("space", "Wolne miejsce na dysku", "Free disk space", "df -hT", "df"),
    ("devices", "Urządzenia blokowe", "Block devices", "lsblk -f", "lsblk"),
    ("mounts", "Punkty montowania", "Mount points", "findmnt", "findmnt"),
    ("lvm-lv", "Woluminy logiczne LVM", "LVM logical volumes", "lvs", "lvs"),
    ("lvm-vg", "Grupy woluminów LVM", "LVM volume groups", "vgs", "vgs"),
    ("lvm-pv", "Woluminy fizyczne LVM", "LVM physical volumes", "pvs", "pvs"),
    ("smart", "Stan SMART urządzenia", "Device SMART health", "sudo smartctl -a {{device}}", "smartctl"),
    ("zpool", "Stan puli ZFS", "ZFS pool status", "zpool status", "zpool"),
    ("zfs", "Zbiory danych ZFS", "ZFS datasets", "zfs list", "zfs"),
]:
    add("linux.storage."+id, pl, en, cmd, "storage", [tool], params=[param("device", "Device / urządzenie", "FilePath", "/dev/sda")] if id == "smart" else [], privileges="sudo / root" if id == "smart" else "user", tags=["dysk", "disk", "storage", "lvm", "smart", "zfs"])
for id, pl, en, cmd, params in [
    ("boot", "Logi bieżącego rozruchu", "Current boot logs", "journalctl -b -n 100 --no-pager", []),
    ("service", "Logi usługi", "Service logs", "journalctl -u {{service}} -n 100 --no-pager", [service]),
    ("errors", "Ostatnie błędy systemu", "Recent system errors", "journalctl -p err -n 100 --no-pager", []),
    ("boots", "Historia rozruchów", "Boot history", "journalctl --list-boots --no-pager", []),
]:
    add("linux.logs."+id, pl, en, cmd, "logs", ["journalctl"], params=params, tags=["log", "dziennik", "bledy", "troubleshooting"])
add("linux.logs.openrc", "Log systemowy Alpine / OpenRC", "Alpine / OpenRC system log", "tail -n 100 /var/log/messages", "logs", ["tail"], family="alpine", tags=["log", "openrc", "troubleshooting"])
for id, pl, en, cmd, tool, risk in [
    ("ufw", "Stan zapory UFW", "UFW firewall status", "sudo ufw status verbose", "ufw", "ReadOnly"),
    ("firewalld", "Aktywne strefy firewalld", "Active firewalld zones", "firewall-cmd --get-active-zones", "firewall-cmd", "ReadOnly"),
    ("firewalld-rules", "Reguły firewalld", "Firewalld rules", "firewall-cmd --list-all", "firewall-cmd", "ReadOnly"),
    ("nft", "Reguły nftables", "Nftables ruleset", "sudo nft list ruleset", "nft", "ReadOnly"),
    ("iptables", "Reguły iptables", "Iptables rules", "sudo iptables -L -n -v", "iptables", "ReadOnly"),
    ("ufw-enable", "Włącz zaporę UFW", "Enable UFW firewall", "sudo ufw enable", "ufw", "Dangerous"),
]:
    add("linux.security."+id, pl, en, cmd, "security", [tool], risk=risk, privileges="root / sudo or policy authorization", tags=["firewall", "zapora", "bezpieczenstwo", "security"],
        detail="Włączenie zapory może przerwać SSH. Najpierw sprawdź regułę dostępu i zapewnij konsolę awaryjną." if id == "ufw-enable" else "",
        detail_en="Enabling the firewall can disconnect SSH. Check the access rule and arrange recovery console access first." if id == "ufw-enable" else "")

# DevOps layer available conditionally on every supported Linux distribution.
for id, pl, en, cmd, tool, params, risk in [
    ("docker-ps", "Kontenery Docker", "Docker containers", "docker ps -a", "docker", [], "ReadOnly"),
    ("docker-images", "Obrazy Docker", "Docker images", "docker image ls", "docker", [], "ReadOnly"),
    ("docker-logs", "Logi kontenera", "Container logs", "docker logs --tail 100 {{container}}", "docker", [container], "ReadOnly"),
    ("docker-stats", "Zasoby kontenerów", "Container resource usage", "docker stats --no-stream", "docker", [], "ReadOnly"),
    ("docker-inspect", "Metadane kontenera", "Container metadata", "docker inspect {{container}}", "docker", [container], "ReadOnly"),
    ("docker-restart", "Zrestartuj kontener", "Restart container", "docker restart {{container}}", "docker", [container], "Dangerous"),
    ("compose-ps", "Usługi Docker Compose", "Docker Compose services", "docker compose ps", "docker", [], "ReadOnly"),
    ("compose-logs", "Logi Docker Compose", "Docker Compose logs", "docker compose logs --tail 100", "docker", [], "ReadOnly"),
    ("git-status", "Stan repozytorium Git", "Git repository status", "git status --short --branch", "git", [], "ReadOnly"),
    ("git-log", "Historia Git", "Git history", "git log -10 --oneline --decorate", "git", [], "ReadOnly"),
    ("git-diff", "Lokalne zmiany Git", "Local Git changes", "git diff --stat", "git", [], "ReadOnly"),
    ("git-fetch", "Pobierz referencje Git", "Fetch Git refs", "git fetch --prune", "git", [], "Modify"),
    ("kube-nodes", "Węzły Kubernetes", "Kubernetes nodes", "kubectl get nodes -o wide", "kubectl", [], "ReadOnly"),
    ("kube-pods", "Pody wszystkich przestrzeni", "Pods in all namespaces", "kubectl get pods --all-namespaces -o wide", "kubectl", [], "ReadOnly"),
    ("kube-services", "Usługi Kubernetes", "Kubernetes services", "kubectl get services --all-namespaces", "kubectl", [], "ReadOnly"),
    ("kube-context", "Aktywny kontekst Kubernetes", "Current Kubernetes context", "kubectl config current-context", "kubectl", [], "ReadOnly"),
    ("kube-events", "Zdarzenia Kubernetes", "Kubernetes events", "kubectl get events --all-namespaces --sort-by=.lastTimestamp", "kubectl", [], "ReadOnly"),
    ("ansible-version", "Wersja Ansible", "Ansible version", "ansible --version", "ansible", [], "ReadOnly"),
    ("ansible-hosts", "Hosty z inwentarza Ansible", "Ansible inventory hosts", "ansible all --list-hosts", "ansible", [], "ReadOnly"),
]:
    add("devops."+id, pl, en, cmd, "devops", [tool], params=params, risk=risk, platform="General", shell="Any", tags=["devops", tool, "kontenery" if tool == "docker" else "administracja"])

# Windows PowerShell: RDP alone is browse-only; these require an actual SSH shell
# for Insert/Execute. No WinRM or credential lookup is implied by the catalog.
windows = [
    ("os", "Informacje o Windows", "Windows information", "Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, OsBuildNumber, OsArchitecture", "system", ["Get-ComputerInfo"], [], "ReadOnly", []),
    ("memory", "Pamięć Windows", "Windows memory", "Get-CimInstance Win32_OperatingSystem | Select-Object TotalVisibleMemorySize, FreePhysicalMemory", "system", ["Get-CimInstance"], [], "ReadOnly", ["ram", "pamiec"]),
    ("processes", "Procesy według CPU", "Processes by CPU", "Get-Process | Sort-Object CPU -Descending | Select-Object -First 20 Name, Id, CPU", "system", ["Get-Process"], [], "ReadOnly", ["cpu", "procesy"]),
    ("uptime", "Ostatni rozruch Windows", "Windows last boot", "Get-CimInstance Win32_OperatingSystem | Select-Object LastBootUpTime", "system", ["Get-CimInstance"], [], "ReadOnly", ["uptime", "restart"]),
    ("listeners", "Otwarte porty", "Listening ports", "Get-NetTCPConnection -State Listen | Sort-Object LocalPort", "network", ["Get-NetTCPConnection"], [], "ReadOnly", ["otwarte porty", "open ports", "nasluch", "sockets"]),
    ("addresses", "Adresy interfejsów Windows", "Windows interface addresses", "Get-NetIPAddress | Select-Object InterfaceAlias, IPAddress, PrefixLength", "network", ["Get-NetIPAddress"], [], "ReadOnly", ["ip", "siec", "network"]),
    ("dns", "Rozwiąż nazwę DNS", "Resolve DNS name", "Resolve-DnsName -Name {{host}}", "network", ["Resolve-DnsName"], [host], "ReadOnly", ["dns", "resolve"]),
    ("test-port", "Sprawdź port TCP", "Test TCP port", "Test-NetConnection -ComputerName {{host}} -Port {{port}}", "network", ["Test-NetConnection"], [host, param("port", "Port", "Number", "443")], "ReadOnly", ["port", "test"]),
    ("service-status", "Sprawdź usługę", "Check service", "Get-Service -Name {{service}}", "services", ["Get-Service"], [param("service", "Service / usługa", default="Spooler")], "ReadOnly", ["sprawdz usluge", "service", "usluga"]),
    ("service-list", "Lista usług Windows", "Windows services", "Get-Service | Sort-Object Status, Name", "services", ["Get-Service"], [], "ReadOnly", ["services", "uslugi"]),
    ("service-start", "Uruchom usługę Windows", "Start Windows service", "Start-Service -Name {{service}}", "services", ["Start-Service"], [param("service", "Service", default="Spooler")], "Modify", ["service"]),
    ("service-stop", "Zatrzymaj usługę Windows", "Stop Windows service", "Stop-Service -Name {{service}}", "services", ["Stop-Service"], [param("service", "Service", default="Spooler")], "Dangerous", ["service"]),
    ("service-restart", "Zrestartuj usługę Windows", "Restart Windows service", "Restart-Service -Name {{service}}", "services", ["Restart-Service"], [param("service", "Service", default="Spooler")], "Dangerous", ["service"]),
    ("packages", "Zainstalowane aplikacje winget", "Installed winget applications", "winget list", "packages", ["winget"], [], "ReadOnly", ["pakiety", "applications"]),
    ("package-updates", "Aktualizacje aplikacji winget", "Winget application updates", "winget upgrade", "packages", ["winget"], [], "ReadOnly", ["aktualizacja", "updates"]),
    ("package-update-all", "Aktualizuj aplikacje winget", "Upgrade winget applications", "winget upgrade --all", "packages", ["winget"], [], "Modify", ["aktualizacja", "updates"]),
    ("hotfix", "Zainstalowane poprawki Windows", "Installed Windows hotfixes", "Get-HotFix | Sort-Object InstalledOn -Descending", "packages", ["Get-HotFix"], [], "ReadOnly", ["windows update", "hotfix", "poprawki"]),
    ("files", "Pliki i katalogi Windows", "Windows files and directories", "Get-ChildItem -LiteralPath {{path}} -Force", "files", [], [param("path", "Path / ścieżka", "FilePath", "C:\\Windows\\Temp")], "ReadOnly", ["pliki", "files"]),
    ("file-content", "Odczytaj plik Windows", "Read Windows file", "Get-Content -LiteralPath {{path}} -Tail 100", "files", [], [param("path", "Path", "FilePath", "C:\\Windows\\win.ini")], "ReadOnly", ["log", "plik"]),
    ("file-hash", "Suma SHA-256 pliku", "File SHA-256 hash", "Get-FileHash -LiteralPath {{path}} -Algorithm SHA256", "files", ["Get-FileHash"], [param("path", "Path", "FilePath", "C:\\Windows\\win.ini")], "ReadOnly", ["checksum", "hash", "suma"]),
    ("file-acl", "Uprawnienia pliku Windows", "Windows file permissions", "Get-Acl -LiteralPath {{path}} | Format-List", "files", ["Get-Acl"], [param("path", "Path", "FilePath", "C:\\Windows\\Temp")], "ReadOnly", ["acl", "permissions", "uprawnienia"]),
    ("users", "Konta lokalne Windows", "Windows local accounts", "Get-LocalUser | Select-Object Name, Enabled, LastLogon", "users", ["Get-LocalUser"], [], "ReadOnly", ["konto", "users"]),
    ("groups", "Grupy lokalne Windows", "Windows local groups", "Get-LocalGroup", "users", ["Get-LocalGroup"], [], "ReadOnly", ["groups", "grupy"]),
    ("storage", "Woluminy Windows", "Windows volumes", "Get-Volume | Select-Object DriveLetter, FileSystemLabel, SizeRemaining, Size, HealthStatus", "storage", ["Get-Volume"], [], "ReadOnly", ["dysk", "disk", "space"]),
    ("disks", "Dyski Windows", "Windows disks", "Get-Disk | Select-Object Number, FriendlyName, OperationalStatus, Size", "storage", ["Get-Disk"], [], "ReadOnly", ["dysk", "disk"]),
    ("events", "Ostatnie zdarzenia systemowe", "Recent system events", "Get-WinEvent -LogName System -MaxEvents 50 | Select-Object TimeCreated, Id, LevelDisplayName, Message", "logs", ["Get-WinEvent"], [], "ReadOnly", ["logs", "dziennik", "bledy", "events"]),
    ("application-events", "Dziennik aplikacji Windows", "Windows application events", "Get-WinEvent -LogName Application -MaxEvents 50", "logs", ["Get-WinEvent"], [], "ReadOnly", ["logs", "application"]),
    ("firewall", "Reguły zapory Windows", "Windows firewall rules", "Get-NetFirewallRule -Enabled True | Select-Object DisplayName, Direction, Action", "security", ["Get-NetFirewallRule"], [], "ReadOnly", ["zapora", "firewall"]),
    ("defender", "Stan Microsoft Defender", "Microsoft Defender status", "Get-MpComputerStatus", "security", ["Get-MpComputerStatus"], [], "ReadOnly", ["antivirus", "defender", "security"]),
    ("tasks", "Zaplanowane zadania", "Scheduled tasks", "Get-ScheduledTask | Select-Object TaskName, State", "system", ["Get-ScheduledTask"], [], "ReadOnly", ["tasks", "zadania"]),
    ("dism", "Sprawdź stan obrazu Windows", "Check Windows image health", "DISM.exe /Online /Cleanup-Image /CheckHealth", "logs", ["DISM.exe"], [], "ReadOnly", ["dism", "health", "troubleshooting"]),
    ("sfc", "Zweryfikuj pliki systemowe Windows", "Verify Windows system files", "sfc.exe /verifyonly", "logs", ["sfc.exe"], [], "ReadOnly", ["sfc", "integrity"]),
]
for id, pl, en, cmd, category, tools, params, risk, tags in windows:
    detail = ""; detail_en = ""
    if id.startswith("package-"):
        detail = "winget obsługuje aktualizacje aplikacji, nie aktualizacje systemu Windows Update. Wymaga zainstalowanego App Installer; dostępność sprawdzana osobno."
        detail_en = "winget manages application updates, not Windows Update operating-system updates. It requires App Installer; availability is checked separately."
    add("windows.ps."+id, pl, en, cmd, category, tools, platform="Windows", params=params, risk=risk, tags=tags, detail=detail, detail_en=detail_en,
        operation="service."+id.removeprefix("service-") if id.startswith("service-") else None,
        privileges="administrator" if risk != "ReadOnly" or id in ("dism", "sfc") else "user")

for id, pl, en, cmd, category, tools, params, risk in [
    ("version", "Wersja Windows (CMD)", "Windows version (CMD)", "ver", "system", [], [], "ReadOnly"),
    ("info", "Informacje systemowe (CMD)", "System information (CMD)", "systeminfo", "system", ["systeminfo"], [], "ReadOnly"),
    ("ip", "Konfiguracja IP (CMD)", "IP configuration (CMD)", "ipconfig /all", "network", ["ipconfig"], [], "ReadOnly"),
    ("ports", "Porty i połączenia (CMD)", "Ports and connections (CMD)", "netstat -ano", "network", ["netstat"], [], "ReadOnly"),
    ("ping", "Sprawdź osiągalność (CMD)", "Check reachability (CMD)", "ping -n 4 {{host}}", "network", ["ping"], [host], "ReadOnly"),
    ("dns", "DNS (CMD)", "DNS lookup (CMD)", "nslookup {{host}}", "network", ["nslookup"], [host], "ReadOnly"),
    ("route", "Routing (CMD)", "Routing table (CMD)", "route print", "network", ["route"], [], "ReadOnly"),
    ("service", "Sprawdź usługę (CMD)", "Check service (CMD)", "sc.exe query {{service}}", "services", ["sc.exe"], [param("service", "Service", default="Spooler")], "ReadOnly"),
    ("files", "Lista plików (CMD)", "List files (CMD)", "dir {{path}}", "files", [], [param("path", "Path", "FilePath", "C:\\Windows")], "ReadOnly"),
    ("users", "Konta użytkowników (CMD)", "User accounts (CMD)", "net user", "users", ["net"], [], "ReadOnly"),
    ("processes", "Lista procesów (CMD)", "List processes (CMD)", "tasklist", "system", ["tasklist"], [], "ReadOnly"),
    ("system-events", "Ostatnie zdarzenia (CMD)", "Recent events (CMD)", "wevtutil qe System /c:20 /rd:true /f:text", "logs", ["wevtutil"], [], "ReadOnly"),
]:
    add("windows.cmd."+id, pl, en, cmd, category, tools, platform="Windows", shell="Cmd", params=params, risk=risk, tags=["cmd", "windows", "sprawdz usluge" if id == "service" else category, "otwarte porty" if id == "ports" else ""])

# Explicit distro overrides demonstrate inheritance without copying entire lists.
add("ubuntu.netplan.status", "Stan sieci Netplan Ubuntu", "Ubuntu Netplan status", "netplan status --all", "network", ["netplan"], distros=["ubuntu"], minimum="24.04", tags=["ubuntu", "netplan", "siec"], detail="Polecenie status jest dostępne w nowszych wersjach Netplan. Profil Ubuntu 24.04+ ogranicza starsze warianty, ale obecność narzędzia jest sprawdzana niezależnie.", detail_en="Status is available in newer Netplan versions. The Ubuntu 24.04+ profile excludes older releases; tool presence is checked independently.")

assert len({r["id"] for r in rows}) == len(rows)
target = Path(__file__).resolve().parents[1] / "src/ServerDesk.Core/Commands/library.v1.json"
target.write_text(json.dumps(dict(schemaVersion=1, version="1.0.0", commands=rows), ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
print(f"Wrote {len(rows)} command variants in {len({r['category'] for r in rows})} categories")
