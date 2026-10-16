# Systemy, DPI i dostępność — bramka 1.0

Ten dokument rozdziela cele zgodności od zakończonej akceptacji. Wydanie pozostaje Public Beta. Bieżący etap roboczy po 0.37.0 nie nadaje nowemu systemowi statusu stabilnego wsparcia.

## Macierz środowisk

| Środowisko x64 | Status i pozostałe próby |
|---|---|
| Windows 11, Win32 | Lokalny start i pozycja rzeczywistych okien; celowane testy interfejsu. Fizyczne mieszane DPI, czytnik ekranu i długie sesje wymagają szerszej akceptacji. |
| Windows 10 22H2 | Historyczny cel zgodności. Brak oddzielnej akceptacji na tym systemie; nie zaliczamy go do zweryfikowanej macierzy 1.0. |
| Ubuntu 24.04 x64 | Lokalne testy Linux w Ubuntu/WSL, w tym headless UX. WSLg nie zastępuje natywnego GNOME/X11 ani GNOME/Wayland. |
| **Kali Linux 2026.3, KDE Plasma 6.7.4, Wayland, 100%** | Konfiguracja podana przez maintenera; priorytet do ręcznej akceptacji. Brak bezpośredniego dostępu z obecnego środowiska. Wersje te nie zostały niezależnie odczytane na maszynie Kali. |
| Kubuntu/KDE, Debian, Fedora | Pozostają celami pierwotnego zakresu. Dokładne wersje i sesje graficzne trzeba zapisać przy akceptacji; brak podstaw do zadeklarowania całej rodziny jako zweryfikowanej. |
| ARM, macOS, 32-bit | Brak paczek tego projektu. |

## Kali/KDE/Wayland: uruchomienie i diagnostyka

ServerDesk ma przypięte Avalonia 12.0.4 i używa `UsePlatformDetect()` z backendem X11. W sesji Wayland wymaga więc XWayland i dostępnego `DISPLAY`; sam `WAYLAND_DISPLAY` nie wystarczy. Bieżące [wyjaśnienie backendów Avalonia](https://docs.avaloniaui.net/docs/platform-specific-guides/linux) opisuje również nowszy, opcjonalny backend Wayland; ten projekt go nie włącza ani nie aktualizuje w ramach tej zmiany.

Skopiuj **cały katalog** deweloperskiego buildu Linux, zachowując pliki wykonywalne i biblioteki. Przed uruchomieniem można wykonać odczytowy preflight:

```bash
bash check-linux-desktop.sh ./ServerDesk.App
```

W checkoutcie skrypt znajduje się w `scripts/check-linux-desktop.sh`. Nie instaluje pakietów, nie uruchamia usług portfela, nie odblokowuje magazynu i nie odczytuje poświadczeń. Sprawdza typ sesji, obecność narzędzi/bibliotek i aktualnego właściciela `org.freedesktop.secrets`; pomija adres D-Bus, nazwy hostów i zapisane profile. Komunikat `cached` odnosi się do cache linkera, a nie do wszystkich możliwości `LD_LIBRARY_PATH`. Sam wynik skryptu nie potwierdza działania okna ani schowka.

Zależności opisują [instrukcje rozwoju](development.md). Na Kali sprawdź dostępność XWayland, bibliotek X11/fontconfig oraz `secret-tool` z libsecret-tools. Przeprowadź diagnostykę w zwykłej sesji użytkownika, bez `sudo` przy uruchamianiu aplikacji. Nie zmieniaj globalnych ustawień KDE ani osobistego portfela na potrzeby testu.

ServerDesk korzysta z Secret Service przez `secret-tool`, a nie bezpośrednio z API KWallet. Obecność KWallet nie potwierdza dostępności właściwego interfejsu w konkretnej sesji. Otwieranie portfela zależy od konfiguracji pulpitu; [podręcznik KDE](https://docs.kde.org/stable_kf6/en/kwalletmanager/kwalletmanager/introduction.html) opisuje także warunkowe odblokowanie podczas logowania. Brak serwisu lub odmowa odblokowania muszą pozwolić na pracę z hasłem bez zapisywania; nie należy obiecywać trwałego zapisu bez tej akceptacji.

### Błąd przy zaznaczonym zapamiętaniu hasła

Jeżeli połączenie wymaga hasła, można odznaczyć **Zapamiętaj w systemowym magazynie poświadczeń** i użyć hasła tylko do bieżącej sesji. W bieżącym buildzie roboczym niepotwierdzony zapis otwiera osobny komunikat z akcjami **Połącz bez zapisywania** i **Anuluj**. Pierwsza używa podanego hasła bez kolejnej próby zapisu, druga kończy ten przebieg bez uruchomienia połączenia. Aplikacja nie przełącza się na zapis jawny, nie usuwa istniejącego wpisu i nie ponawia zapisu automatycznie. Niepotwierdzony zapis nie gwarantuje, że dostawca magazynu nie zdążył zachować wpisu przed błędem.

Do ustalenia przyczyny uruchom preflight w tej samej zwykłej sesji graficznej co ServerDesk i odczytaj osobno trzy linie:

- `secret-tool: not on PATH` — brakuje narzędzia lub nie jest dostępne dla aplikacji. Na Kali/Debian narzędzie dostarcza [pakiet libsecret-tools](https://packages.debian.org/sid/libsecret-tools); jeśli nie jest zainstalowany, użyj `sudo apt install libsecret-tools`. Samo narzędzie nie zastępuje usługi magazynu.
- `DBUS_SESSION_BUS_ADDRESS: absent` — aplikacja nie ma wskazanego adresu magistrali sesji. Uruchom ją z menu pulpitu lub Konsole tej samej sesji użytkownika, bez `sudo`.
- `Secret Service: not currently owned` — podczas odczytu brak właściciela interfejsu; skrypt nie próbuje aktywacji. `owned` potwierdza właściciela, ale nie stan odblokowania ani możliwość zapisu. Sprawdź portfel i ewentualne okno zgody w swojej sesji KDE. Nie dodawaj drugiego dostawcy magazynu tylko na podstawie tej linii.

Samo `present` przy zmiennej środowiskowej nie rozstrzyga przyczyny błędu. Do zgłoszenia wystarczą powyższe trzy niesekretne linie; nie dołączaj haseł ani eksportu portfela. Poprawka komunikatu nie zastępuje konfiguracji systemowego Secret Service.

### RDP: błąd TLS/NLA i pytanie o certyfikat

Zielony wynik „Serwer RDP dostępny” potwierdza odpowiedź negocjacji i deklarowane NLA. Nie oznacza zweryfikowanego certyfikatu ani poprawnego logowania. Dokładny błąd klienta pozostaje w **Konsoli RDP**; przyciskiem w pasku sesji można przełączać ją z **Podsumowaniem RDP**. Po błędzie konsola jest widoczna, a po ponownym połączeniu służy do nowych pytań klienta.

Wcześniej ServerDesk uruchamiał FreeRDP w PTY z `/from-stdin:force`. Ten tryb blokuje interaktywne pytanie o niezaufany certyfikat w [FreeRDP 2.11.5](https://github.com/FreeRDP/FreeRDP/blob/2.11.5/client/common/client.c#L419) oraz [3.16.0](https://github.com/FreeRDP/FreeRDP/blob/3.16.0/client/common/client.c#L616). Bieżący build usuwa ten parametr: dane logowania i decyzję o certyfikacie obsługuje zwykły interaktywny klient. Zachowuje `/sec:nla`; nie dodaje `cert:ignore`, automatycznego zaufania ani hasła w argumentach procesu.

Przy pytaniu klienta porównaj odcisk z zaufanym źródłem. W konsolowym pytaniu `Y/T/N` można odmówić przez `N`, a `T` oznacza zaufanie na tę próbę. Nie potwierdzaj zmienionego certyfikatu bez sprawdzenia. Usunięcie parametru przywraca możliwość odpowiedzi; nie rozwiązuje wszystkich błędów zgodności TLS, CredSSP ani kont Windows. Po odrzuceniu certyfikatu błąd połączenia jest oczekiwany.

### Zmiana rozmiaru głównego okna na Linux

Własny pasek tytułu zachowuje przyciski minimalizacji, maksymalizacji/przywrócenia i zamknięcia. Build roboczy dodaje do głównego okna obszary przeciągania przy wszystkich krawędziach i narożnikach oraz widoczny uchwyt w prawym dolnym rogu. Jeżeli okno jest zmaksymalizowane, najpierw wybierz **Maksymalizuj / przywróć**. Uchwyty są wtedy przywracane; zmiana rozmiaru zachowuje dotychczasowe minima dopasowane do ekranu. Nie dodano pasków przewijania do całego obszaru pracy.

## Ręczna akceptacja na Kali lub innym docelowym desktopie

Wykorzystaj osobne konto/VM i jednorazowe profile. Można uruchomić aplikację z osobnym katalogiem danych przez `SERVERDESK_DATA_HOME`; nie kopiuj osobistej bazy ani haseł do zgłoszenia.

1. Zapisz wersję systemu, pulpitu, X11/Wayland, skalę i wersję/commit aplikacji. Uruchom preflight, następnie aplikację; sprawdź zamknięcie i ponowne uruchomienie.
2. Otwórz ustawienia: PL/EN, pięć motywów, tekst 16, małe okno. Przyciski zapisu/anulowania i wybrana kategoria muszą pozostać dostępne. Sprawdź Tab/Shift+Tab, strzałki/Home/End w kategoriach, Ctrl+F, Escape oraz widoczny fokus.
3. Sprawdź kopiowanie/wklejanie UTF-8 w obie strony z Konsole i edytorem, zaznaczanie, AltGr, polskie znaki oraz systemowy wybór plików/grafik. Wynik headless nie zastępuje tej próby.
4. Na jednorazowym profilu i testowym SSH sprawdź zapis/odczyt/usunięcie hasła po restarcie. Powtórz odmowę odblokowania i brak zapisu. Pełną próbę zablokowanego portfela wykonuj na osobnym koncie/VM; nie blokuj osobistego portfela używanego przez inne aplikacje. Nie przesyłaj hasła ani zawartości portfela w raporcie.
5. Jeśli są dostępne monitory, przenieś główne okno, ustawienia, edytor profilu i odpiętą sesję między skalami 100/150/200%. Otwórz dialog na drugim ekranie, odłącz go i sprawdź powrót do widocznego obszaru. Powrót na większy ekran musi przywrócić możliwość powiększenia okna.
6. Sprawdź własny czytnik ekranu: nazwy kategorii/akcji, kolejność fokusu, komunikaty błędów i pola haseł. Test nazw automatyzacji w kodzie nie jest certyfikacją czytnika ani dostępności Linux.
7. Pracuj kilka godzin na jednorazowych sesjach, z reconnect, SFTP i dokowaniem. Zapisz okresowe CPU/pamięć/uchwyty lub deskryptory/wątki. Krótki test automatyczny nie zamyka tej bramki.

Do raportu wystarczą wyniki powyższych punktów, wersja/commit, niesekretne wyjście preflight i zrzuty z danymi testowymi. Otwarte bramki pozostają w [planie stabilizacji](stable-release-plan.md).
