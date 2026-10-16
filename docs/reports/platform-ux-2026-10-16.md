# Punkt 5: systemy, DPI, dostępność i zasoby

Etap roboczy po 0.37.0, 2026-10-16. Nie zamyka całej akceptacji sprzętu i systemów; wersja pozostaje Public Beta. [Macierz i checklista](../platform-support.md) zawiera także konfigurację podaną przez maintenera: Kali Linux 2026.3, KDE Plasma 6.7.4, Wayland, 100%.

## Zmiany

- Wspólne dopasowanie dialogów śledzi ich pozycję, DPI i listę ekranów. Po zmianie monitora ogranicza rozmiar i przenosi okno do obszaru roboczego. Używa ekranu dialogu, nie stale ekranu właściciela.
- Pierwotne limity rozmiaru są zachowane i odtwarzane po powrocie na większy ekran. Wcześniej jednorazowe, kumulacyjne obniżenie `MaxWidth/MaxHeight` mogło pozostać na cały czas życia dialogu.
- Przewijana treść wspólnych potwierdzeń reaguje na zmieniony limit wysokości; akcje pozostają poza przewijaną treścią. Zamknięcie odłącza śledzenie ekranów i zatrzymuje timer.
- Odczytowy `check-linux-desktop.sh` opisuje sesję i wymagania X11/XWayland/Secret Service bez instalacji, aktywacji portfela i odczytu sekretów.
- Harness sesji zapisuje również CPU, working set i liczniki po zamknięciu; nie dodano telemetrii ani zbierania danych w zwykłej aplikacji.

## Wykonane sprawdzenia

- **21/21** celowanych testów Windows: geometria okien i zapis/odtwarzanie układu.
- `--platform-ux` na **Windows oraz Ubuntu 24.04/WSL**: PL/EN, pięć motywów, tekst 16, symulowane 100/150/200/100%, ujemne współrzędne i odzyskanie okna poza ekranem, przywrócenie limitów powiększania, widoczność stopki/wyszukiwarki/kategorii. Strzałki/Home/End, Ctrl+F, Escape, dostęp do Zapisz/Anuluj przez Tab i nazwy automatyzacji kategorii. Długie potwierdzenie przy 200% zachowuje przewijanie i widoczne akcje; Escape odmawia.
- Po 20 dodatkowych otwarciach/zamknięciach dialogu na język: **0/40** zachowanych okien po kontrolowanym GC na Windows oraz **0/40** na Linux. To test subskrypcji i zamkniętych kontrolek, nie całej pamięci aplikacji.
- Natywny Windows `--native-startup`: korekta startu przy dolnej/prawej krawędzi, restart zmaksymalizowanego okna, powrót do normalnego rozmiaru, zamknięcie zminimalizowanego okna i widoczne odpięte okno sesji.
- Windows `--settings-layout`: podgląd/anulowanie, walidacja, zapis/ponowne otwarcie/reset, kontrola motywu systemowego w zasobach i granic paska parametrów przy większej czcionce. Nie jest to fizyczny test przełączania motywu OS.
- Składnia Bash i odczytowy preflight w Ubuntu/WSL. Brak `libICE/libSM` w systemowym cache został wskazany; harness używa własnego `LD_LIBRARY_PATH` laboratorium. Nie instalowano bibliotek globalnie i nie zmieniano sesji użytkownika.
- Kompilacje bez ostrzeżeń. Scenariusz `platform-ux` dodano do przyszłej macierzy CI Windows/Linux; lokalnie nie uruchamiano pełnej macierzy ani nie budowano nowego wydania.

## Ograniczona próba sesji — Windows i własny OpenSSH

**10 minut, osiem połączeń, 36 reconnect**, znaczniki terminala, monitoring, SFTP, wielokrotne przenoszenie między panelami i oknem. Wszystkie osiem sesji pozostawało połączonych w próbkach. Źródłem był wyłącznie własny OpenSSH na loopback 22222; po próbie go zatrzymano.

| Licznik procesu harnessu | Zakres w 36 próbkach |
|---|---:|
| Pamięć zarządzana po kontrolowanym GC | 31,14–47,35 MiB |
| Private bytes | 92,54–135,25 MiB |
| Uchwyty | 680–790 |
| Wątki | 38–45 |
| Średni CPU po pierwszej minucie | 0,43% całej puli CPU dostępnej procesowi |

Po zamknięciu: około 3,39 MiB pamięci zarządzanej, 72,95 MiB private bytes, 506 uchwytów, 27 wątków i oba sloty transferów wolne. Working set również znajduje się w JSON. Część pamięci runtime i cache pozostaje po zamknięciu sesji; te wartości nie mają progu gwarantującego brak wszystkich wycieków.

To pomiar procesu testowego z ośmioma sesjami i okresowym GC. Równolegle działały celowane testy/kompilacje. Nie jest to pomiar jałowej, opublikowanej aplikacji ani wielogodzinny soak, benchmark sprzętu czy gwarancja WAN.

## Dowody i pozostałe bramki

Wybrane logi, TRX, JSON i zrzuty: `artifacts/reports/platform-ux-2026-10-16/`. Dane testowe odseparowano od profili i poświadczeń maintenera.

Nie wykonano bezpośredniej próby na Kali/KDE, zablokowanym/odblokowanym KWallet ani nowego rzeczywistego GNOME Keyring. Pozostają fizyczne monitory i odłączenie monitora, systemowy picker/schowek/AltGr/IME, czytniki ekranu, natywne GNOME/KDE/X11/Wayland, inne docelowe wersje OS i wielogodzinne pomiary na obu platformach. Dlatego całego punktu 5 nie oznaczamy jako zakończonego.
