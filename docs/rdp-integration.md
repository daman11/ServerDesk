# RDP — pulpit w karcie ServerDesk

## Linux od 0.38.0

ServerDesk osadza zainstalowany klient **X11 FreeRDP** (`xfreerdp3`, następnie `xfreerdp`) w `NativeControlHost` przez `/parent-window`. W sesji Wayland aplikacja i klient korzystają z **XWayland**. Nie włączamy natywnego backendu Wayland i nie osadzamy klientów SDL/wlfreerdp. FreeRDP nie jest dołączony do paczki; DEB rekomenduje freerdp3-x11 lub freerdp2-x11. Wymagane są X11 i libxcb1.

Linux zawsze używa karty, także dla dawnych profili zapisanych jako zewnętrzne. Okno hasła pokazuje adres i login; pole jest maskowane, zapis w Secret Service opcjonalny. Niepotwierdzony zapis daje jawny wybór użycia hasła bez zapisywania albo anulowania. Hasło nie trafia do argumentów, pliku RDP ani logu; jest przekazywane pojedynczą linią przez stdin. Ograniczenie klienta: 511 bajtów UTF-8, bez CR/LF/NUL, sprawdzane przed zapisem i uruchomieniem.

Przed startem klienta odczytujemy NLA i certyfikat TLS **bez poświadczeń**. Przy błędzie systemowej weryfikacji okno pokazuje adres, ostrzeżenia, kopiowalny SHA-256 i zwijane szczegóły. Porównaj odcisk z zaufanym źródłem i zaznacz potwierdzenie; dopiero wtedy działa **Zaufaj w tej karcie**. Escape, Anuluj i przerwanie nie uruchamiają klienta. Zgoda pozostaje w pamięci tej karty i nie instaluje certyfikatu w systemie.

Klient otrzymuje `/sec:nla`, `/cert:deny,fingerprint:sha256:…` i własny pusty, prywatny `XDG_CONFIG_HOME`. Nie przejmuje zaufania z innych sesji FreeRDP. Odcisk jest wyjątkiem dla zatwierdzonego certyfikatu; inne niezaufane certyfikaty są odrzucane bez pytania w CLI. Nie zastępuje całego zaufania systemowego: inny certyfikat poprawny według CA i nazwy może przejść zwykłą weryfikację FreeRDP. Zmiana niezaufanego certyfikatu przy następnym połączeniu wymaga nowej zgody. `/from-stdin:force` służy tutaj dostarczeniu hasła po graficznej decyzji; surowe wyjście klienta nie jest zapisywane.

Stan zalogowania wymaga utworzenia pulpitu przez uwierzytelniony klient X11; sam proces i dostępny port nie wystarczą. Sprawdzamy rodzica i PID dziecka. Zachowanie natywnego hosta pozwala przełączać karty i przenosić sesję między panelami/odpiętym oknem bez nowego logowania. Resize uwzględnia skalę TopLevel; dynamiczna rozdzielczość zależy od serwera. Po jej wyłączeniu smart sizing zapewnia dopasowanie obrazu.

**Ctrl+Alt+←/→** oddaje fokus aplikacji; można też kliknąć pasek sesji. Menedżer okien może przejąć skrót. Schowek i przekazany folder zależą od klienta/serwera. Linux nie obsługuje strefy przeciągania plików RDP Windows ani automatycznego reconnect; użyj **Połącz ponownie**. Zamknięcie karty/aplikacji i reconnect czekają na zakończenie własnego procesu oraz sprzątnięcie konfiguracji. Błędy mają komunikaty GUI.

[Izolowana akceptacja](reports/embedded-linux-rdp-2026-10-16.md) nie zastępuje natywnego Kali/KDE, pełnej macierzy schowka/IME/klawiszy, fizycznych mieszanych DPI i długich sesji: [macierz](rdp-test-matrix.md), [platformy](platform-support.md).

## Windows

WindowsRdpHost osadza systemowy ActiveX przez AtlAxWin. Ukryty HWND utrzymuje kontrolkę podczas odłączenia od drzewa wizualnego. Profil pozwala wybrać kartę albo mstsc; dawne profile zachowują tryb zewnętrzny. Hasło trafia w pamięci do write-only ClearTextPassword. `AuthenticationLevel=2` zachowuje systemowe ostrzeżenie certyfikatu; nie zastępujemy go dialogiem Linux ani nie wyłączamy weryfikacji. Połączenie potwierdza OnLoginComplete.

Wcześniejsze rzeczywiste testy Windows obejmowały lokalne konto, pulpit, split, dokowanie, resize, reconnect, zamknięcie, tekst/obraz/pliki schowka i przerwanie transportu. Nie oznacza to pełnej akceptacji AD/Microsoft ani wszystkich urządzeń: [wykonane i pozostałe próby](rdp-test-matrix.md). Komponent Windows pochodzi z systemu i nie jest redystrybuowany.

## Źródła i dalszy zakres

[Avalonia NativeControlHost](https://docs.avaloniaui.net/docs/app-development/native-interop), [FreeRDP X11 parent window](https://github.com/FreeRDP/FreeRDP/blob/3.16.0/client/X11/xf_window.c), [FreeRDP argumenty](https://github.com/FreeRDP/FreeRDP/blob/3.16.0/client/common/cmdline.c), [FreeRDP certyfikaty](https://github.com/FreeRDP/FreeRDP/blob/3.16.0/libfreerdp/crypto/tls.c), [AuthenticationLevel Windows](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings4-authenticationlevel).

Natywny Wayland wymaga przyszłego adaptera framebuffer z kontrolowanymi callbackami uwierzytelniania, wejścia i schowka. Nie wdrożono zależnego od ABI P/Invoke struktur FreeRDP. Gateway, multimedia i macOS pozostają dalszym zakresem.
