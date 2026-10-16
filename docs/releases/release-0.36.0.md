# ServerDesk 0.36.0 — Smart Cheat Sheet i własne komendy

Nowy wspólny panel **Komendy** (Ctrl+Shift+K) działa obok terminala oraz całkowicie offline. Biblioteka 1.0.0 zawiera 165 wariantów / 134 operacje w 10 kategoriach, z opisami PL/EN, ryzykiem, uprawnieniami, wymaganymi narzędziami i parametrami. Własne szablony mają edycję/duplikowanie/usuwanie, kategorie i podfoldery, tagi, zakres hosta/grupy, wspólne ulubione, sortowanie/wyszukiwanie i import/eksport JSON v1.

Automatyczny profil SSH odczytuje Linux lub Windows na osobnych kanałach istniejącego połączenia, z limitem czasu, pamięci i anulowaniem. Cache jest przypisany do konkretnego połączenia; rozpoznanie nie trafia do interaktywnej powłoki. Tryb ręczny i przypięcie pozwalają przeglądać inne systemy bez przełączania wraz z kartami. RDP, VNC i Serial nie otrzymują dostępu do wykonywania komend.

**Wstaw** nie wysyła Enter. **Wykonaj** pokazuje cały kod i cel, wymaga zgody, a poziom Dangerous drugiej zgody. Zmiana aktywnej karty lub reconnect unieważnia cel. Własne komendy są szyfrowane w magazynie systemowym; opcjonalna historia zapisuje tylko ID i czas. Sekretne parametry i skrypty wieloliniowe są copy-only. Bash/Zsh, PowerShell i konserwatywny CMD mają osobne zasady cytowania; surowy Code wymaga świadomej zgody.

Obsługiwane profile: ogólne, Linux, Ubuntu/Debian, RHEL/Rocky/Alma/CentOS Stream/Fedora, Arch, Alpine oraz Windows 10/11/Server z PowerShell/CMD. Warunki narzędzi nie wynikają z samej nazwy dystrybucji. Windows Update pozostaje osobne od winget. Biblioteka jest lokalna i nie potrzebuje AI.

Publiczna beta MIT, bez nowych zależności i migracji tabel SQLite. Linux potrzebuje odblokowanego Secret Service do zapisania własnej biblioteki. Nie dodano YAML, WinRM, zdalnego edytora ani pełnych bibliotek urządzeń sieciowych. Wykryta powłoka dotyczy logowania; zagnieżdżone powłoki wymagają uwagi. [Obsługa i architektura](../commands.md), [testy](../reports/commands-2026-10-16.md), [zakres wydania](../reports/release-verification-0.36.0.md), [ograniczenia](../known-issues.md).
