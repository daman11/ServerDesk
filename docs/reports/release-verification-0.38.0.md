# ServerDesk 0.38.0 — zakres weryfikacji

Wydanie obejmuje osadzenie klienta X11 FreeRDP w karcie Linux, graficzne dane logowania i certyfikaty oraz zmiany rozwijane po 0.37.0: monitoring, duża lista połączeń, dialogi/DPI, resize Linux i obsługa niepotwierdzonego zapisu hasła.

Bramki: locked restore, Release build i pełne testy jednostkowe Windows/Linux, aktualna macierz headless UI z CI plus network-tools/app-polish, izolowany serwer NLA/Xvfb, audyt advisory NuGet, natywny start, opublikowany host Windows RDP bez logowania, pozycjonowanie/restart, teksty i izolowana instalacja/upgrade/uninstall Setup, zgodność ZIP/TAR/DEB/źródeł i SHA-256. Ostateczne liczniki, commit i wyniki pakietów są w VERIFICATION.md dołączonym do lokalnego wydania. Dowody: artifacts/reports/release-0.38.0 oraz artifacts/reports/embedded-linux-rdp-2026-10-16.

Przed commitem: 691/691 jednostkowych na Windows i 691/691 na Linux, 24/24 scenariusze UI na Windows i 25/25 na Linux. Audyt NuGet czterech projektów nie zwrócił advisory ani błędów źródeł. Kandydat Windows przeszedł opublikowany host RDP bez logowania oraz izolowane testy tekstów Setup i instalacji/upgrade/uninstall. Natywny test Linux FreeRDP 2.11.5 zaliczył poprawne/błędne NLA, odmowę certyfikatu, PID/XID, rzeczywisty input, dokowanie, reconnect i zamknięcie. Końcowe paczki podlegają ponownej kontroli po commicie.

Nowe testy sprawdzają brak hasła w argumentach i plikach, stdin UTF-8, limit hasła, odprowadzanie wyjścia, zatrzymanie własnego procesu i usunięcie prywatnego katalogu klienta. Scenariusz --rdp-gui obejmuje PL/EN, Graphite/Light, font16, 480px/200%, maskowane hasło i dane celu, odcisk SHA-256, checkbox przed zgodą, Anuluj/Escape i przerwanie. Na Linux sprawdza także anulowanie rzeczywistej ścieżki połączenia przed natywnym hostem.

[Osobny raport RDP](embedded-linux-rdp-2026-10-16.md) podaje zakres protokołu i natywnych okien. Własny serwer i dwa prywatne ekrany nie korzystają z pulpitu, kont, zapisanych profili ani magazynu użytkownika. Windows zachowuje systemową kontrolkę i systemowe ostrzeżenia certyfikatu. Nie dodano NuGet ani migracji bazy (schema3).

Nie deklarujemy natywnej akceptacji Kali/KDE, fizycznego mieszanego DPI, wszystkich formatów schowka, AD/Microsoft, IME, sprzętowego Serial ani wielogodzinnych sesji. X11 embedding w XWayland nie jest natywnym backendem Wayland. Linux wymaga zainstalowanego xfreerdp3/xfreerdp, reconnect jest ręczny i nie ma strefy przeciągania plików Windows RDP. [Platformy i akceptacja](../platform-support.md).
