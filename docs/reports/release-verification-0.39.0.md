# ServerDesk 0.39.0 — zakres weryfikacji, 2026-10-17

Zmiana obejmuje katalog oraz zarządzanie cyklem życia dwóch wbudowanych pluginów, nowe ustawienia i zatrzymywanie opcjonalnych operacji SSH bez rozłączania terminala/SFTP. Szczegóły prób: [pluginy](plugins-2026-10-17.md).

Bramki: locked restore i Release build Windows/Linux, pełne jednostkowe, aktualna macierz CI UI plus network-tools/app-polish, własny OpenSSH dla aktywnego wyłączania/włączania pluginów, istniejące próby modułu komend i monitoringu oraz natywna regresja osadzonego RDP. Opublikowane paczki podlegają kontroli hosta RDP Windows bez logowania, startu/restartu, tekstów i izolowanego Setup, Linux TAR/DEB, źródeł i SHA-256. Końcowe wyniki/liczniki/commit są w VERIFICATION.md wydania i artifacts/reports/release-0.39.0.

Przed commitem: locked restore i Release build bez ostrzeżeń/błędów na Windows i Linux, 700/700 jednostkowych na każdej platformie (bez pominiętych), 25/25 scenariuszy UI Windows i 26/26 Linux. Próby obejmują nowy plugin lifecycle PL/EN oraz całą dotychczasową macierz CI. Audyt NuGet czterech projektów nie zwrócił advisory ani błędów źródeł. Nie zmieniono zależności ani locków. Kontrole treści, linków i wzorców poświadczeń obejmują 624 pliki źródłowe.

Własny OpenSSH zaliczył plugin lifecycle, dotychczasowe komendy oraz monitoring z częściowymi danymi i utratą jego osobnego połączenia na obu OS. Natywny Linux FreeRDP 2.11.5 z prywatnym serwerem NLA i dwoma Xvfb zaliczył GUI login/certyfikat/Anuluj, odrzucenie błędnego hasła, rzeczywisty pulpit i input, zachowanie PID/XID przy dokowaniu i czterech panelach, reconnect oraz zamknięcie bez pozostałego klienta. Końcowe paczki są sprawdzane po commicie.

Nie dodano migracji bazy ani NuGet. Próby nie używają prywatnych serwerów, poświadczeń, portfela ani aktywnego pulpitu użytkownika. Testy WSL/headless nie zastępują natywnego KDE, fizycznych DPI, pełnej macierzy RDP/Serial i długich sesji. Pluginy są wbudowane; nie ma zewnętrznego loadera ani sandboxa. [Platformy](../platform-support.md), [obsługa pluginów](../plugins.md).
