# ServerDesk 0.28.0 — pierwotne MVP: interaktywny terminal SSH

Escape w terminalu SSH na Windows wysyła właściwy kod VT. Wcześniej obejście biblioteki przeznaczone dla lokalnego ConPTY wysyłało rekordy klawiatury Windows do zwykłego zdalnego terminala, co utrudniało m.in. wyjście z trybu edycji vim. Poprawka obejmuje również strumień Serial; jawnie wynegocjowane tryby Kitty/Win32 pozostają pod kontrolą emulatora.

**Ctrl+Shift+C** kopiuje zaznaczenie. Bez zaznaczenia nie wysyła już zdalnego Ctrl+C, więc przypadkowe kopiowanie nie przerywa procesu. **Ctrl+C** nadal jest skrótem terminalowym. **Ctrl+Shift+V** wkleja tekst, z obsługą bracketed paste w programach, które ten tryb włączają. Zdalne żądania dostępu do schowka OSC 52 pozostają wyłączone.

Otwarcie kanału i negocjacja PTY odbywają się poza wątkiem interfejsu. Zmiany rozmiaru są przesyłane w tle: kolejka zachowuje tylko najnowszy oczekujący rozmiar, zamiast blokować układ okna i gromadzić żądania podczas przeciągania. Zamknięcie/reconnect czeka na zakończenie poprzedniego transportu.

Weryfikacja obejmuje własny serwer OpenSSH z logowaniem hasłem oraz rzeczywiste programy terminalowe. Szczegółowe wyniki i ograniczenia znajdują się w [raporcie weryfikacji](../reports/release-verification-0.28.0.md). Nie zmieniono schematu bazy, formatu poświadczeń ani zależności produkcyjnych. Aplikacja pozostaje publiczną betą MIT; [pozostałe kryteria MVP](../mvp-status.md).
