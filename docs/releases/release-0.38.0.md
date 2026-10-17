# ServerDesk 0.38.0 — Public Beta

Linux RDP działa teraz w karcie ServerDesk. Zainstaluj klient X11 FreeRDP (`xfreerdp3` lub `xfreerdp`); w sesji Wayland potrzebny jest XWayland. Dawne profile trybu zewnętrznego na Linux również korzystają z karty. Binariów FreeRDP nie dołączamy.

Hasło wpisuje się w maskowanym oknie z adresem i loginem. Przy niezaufanym certyfikacie osobne okno pokazuje ostrzeżenia, kopiowalny SHA-256 i szczegóły. Zaufanie wymaga porównania odcisku i jawnego potwierdzenia, dotyczy bieżącej karty. Anulowanie nie uruchamia klienta. NLA i zwykła weryfikacja CA/nazwy pozostają aktywne; hasło jest przekazywane przez stdin, bez argumentów i plików.

Osadzony pulpit zachowuje sesję przy przełączaniu kart i dokowaniu. Rozmiar uwzględnia skalę okna. Ctrl+Alt+←/→ lub kliknięcie paska oddaje fokus aplikacji. Ponowne połączenie jest ręczne. [Szczegóły i granice](../rdp-integration.md).

Wydanie obejmuje także wcześniejsze poprawki monitoringu na starszym iproute2, listy połączeń z 5000 profilami, dialogów przy zmianach DPI, krawędzi zmiany rozmiaru Linux oraz jawny wybór po niepotwierdzonym zapisie hasła. Schemat bazy pozostaje 3, bez nowych pakietów NuGet.

Paczki: Windows x64 Setup/ZIP, Linux x64 DEB/TAR.GZ, źródła ZIP. MIT, bez podpisu Authenticode. Zamknij aplikację i zabezpiecz dane przed aktualizacją; archiwa rozpakuj do nowego katalogu.

[Weryfikacja](../reports/release-verification-0.38.0.md), [instrukcja PL](../getting-started.md), [English guide](../getting-started.en.md), [ograniczenia](../known-issues.md), [plan 1.0](../stable-release-plan.md). Próby Xvfb/WSL nie zastępują natywnego Kali/KDE, fizycznych DPI, pełnej macierzy schowka i kont AD/Microsoft. Linux nie ma natywnego backendu Wayland ani przeciągania plików do RDP Windows. Przygotowanie lokalnego wydania nie oznacza publikacji w GitHub.
