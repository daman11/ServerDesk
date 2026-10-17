# Osadzone RDP Linux — 2026-10-16/17

Implementacja 0.38.0: natywny host Avalonia/X11, klient xfreerdp3 lub xfreerdp jako własny proces, hasło przez stdin i graficzna decyzja o niezaufanym certyfikacie. XCB sprawdza rodzica/PID dziecka i wykonuje kontrolowane operacje rozmiaru/fokusu bez modyfikacji globalnego handlera błędów Xlib.

Testy jednostkowe obejmują opcje NLA/rodzica/certyfikatu bez hasła w argv, odrzucenie niepoprawnego odcisku, UTF-8 oraz CR/LF/NUL/limit wejścia, opróżnianie stdout/stderr i sprzątanie własnego procesu/konfiguracji. FreeRDP 2.11.5 wymaga odcisku w zapisie z dwukropkami i nie pozwala jednocześnie użyć smart sizing i dynamic resolution; oba przypadki mają regresję w testach. Tryb dynamiczny wymaga wsparcia serwera; po jego wyłączeniu używany jest smart sizing.

UiSmoke --rdp-gui sprawdza maskowane pole i tożsamość celu, selektowalny SHA-256, jawne zaznaczenie weryfikacji, zgodę, Anuluj/Escape/cancellation, PL/EN Graphite/Light, font16 i 480px/200%. Linux dodatkowo przechodzi rzeczywistą ścieżkę logowania i odmawia certyfikatu przed uruchomieniem natywnego hosta.

Opt-in scripts/test-embedded-linux-rdp.sh tworzy własny serwer freerdp-shadow z NLA, jednorazowym SAM, certyfikatem self-signed i dwoma prywatnymi ekranami Xvfb. Nie używa kont systemowych ani osobistego ekranu. SAM przechowuje hash NT w formacie domyślnym winpr-hash; nie NTOWFv2. Narzędzia pobrano wyłącznie do .tools, bez instalacji systemowej. W WSLg zastosowano prywatną przestrzeń montowań z narzędziami XKB; nie zmieniono /usr/bin ani uprawnień osobistego gniazda X11.

Wyniki natywnego scenariusza i wybrane obrazy są w artifacts/reports/embedded-linux-rdp-2026-10-16. Scenariusz wymaga dokładnego odcisku własnego fixture przed automatycznym potwierdzeniem. Próby obejmują anulowanie bez procesu, zły/poprawny login, osadzenie i PID/XID, brak hasła w argv, rzeczywistą klawiaturę/mysz do drugiego prywatnego ekranu, zachowanie procesu po zmianach kart/paneli/dokowania, reconnect i zamknięcie.

To nie jest akceptacja natywnego KDE/Kali ani pełnej macierzy clipboard/IME/klawiszy, AD/Microsoft, fizycznego mieszanego DPI i długich sesji. Nie ma natywnego Wayland framebuffer ani gwarancji przejęcia skrótu zarezerwowanego przez menedżer okien. [Bieżąca obsługa i model zaufania](../rdp-integration.md), [ręczna akceptacja](../platform-support.md).

Wynik: PASS wszystkich pięciu etapów natywnych oraz osobnego guardu poprawnego NLA i odmowy innego niezaufanego certyfikatu. Użyty klient/serwer to FreeRDP 2.11.5. Odczyt źródeł 3.16.0 potwierdza użyte opcje, ale nie zastępuje próby z binarnym FreeRDP 3 na Kali. Brak podstaw do deklaracji nowego rzeczywistego logowania do serwera Windows w tej zmianie.
