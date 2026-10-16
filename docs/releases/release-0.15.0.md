# ServerDesk 0.15.0 — RDP w zakładce Windows

Profil RDP pozwala teraz wybrać pulpit w zakładce ServerDesk. Na Windows systemowy komponent RDP wyświetla rzeczywisty obraz wewnątrz panelu aplikacji. Sesję można przenieść do drugiego panelu, odłączyć do okna i zadokować z powrotem. Nie jest tworzona nowa sesja przy zmianie układu lub aktywnej karty. Domyślny tryb istniejących profili pozostaje zewnętrzny.

## Obsługa

1. Edytuj serwer → Ustawienia RDP → Wyświetlanie pulpitu → **W zakładce ServerDesk (Windows)**.
2. Wpisz login w profilu, zapisz i wybierz Połącz. Hasło podaje się w oknie poświadczeń ServerDesk; zapis w DPAPI jest opcjonalny.
3. Certyfikat wymagający potwierdzenia obsługuje systemowe okno Windows. NLA jest włączone, kontrola tożsamości serwera nie jest wyłączana. ServerDesk nie dodaje certyfikatów do systemowego magazynu zaufania.
4. Menu zakładki udostępnia dokowanie i odłączenie do osobnego okna. Przycisk Pulpit oddaje fokus zdalnemu pulpitowi. Ctrl+Alt+End obsługuje zdalny ekran zabezpieczeń. Skróty klawiatury w samym pulpicie obsługuje systemowy klient; lokalne polecenia dostępne są także na paskach ServerDesk.
5. Rozłącz RDP zamyka tę sesję po potwierdzeniu. Opcje → Uruchom ponownie sesję odtwarza połączenie; Poświadczenia pozwalają zmienić hasło na czas sesji.

„Połączono” i zielony stan zakładki pojawiają się dopiero po zdarzeniu OnLoginComplete. Pasek stanu pokazuje czas zalogowanej sesji. RDP nie włącza monitoringu Linux ani SFTP. W trybie zewnętrznym stan uruchomionego klienta pozostaje odrębny od potwierdzonego logowania.

Dynamiczna rozdzielczość dopasowuje pulpit do wymiarów panelu; SmartSizing zapewnia skalowanie, jeśli serwer odrzuci zmianę rozdzielczości. Wyłączenie tej opcji zachowuje rozdzielczość profilu. Pełny ekran pozostaje opcją klienta zewnętrznego. Schowek jest opcjonalny; dyski, drukarki, porty i karty inteligentne nie są automatycznie przekazywane.

## Sprawdzenie

- 162/162 testy xUnit, w tym zgodność starych profili, walidacja trybu, eksport/import i zapis ustawień RDP w SQLite.
- Rzeczywiste logowanie lokalnego konta na udostępnionym Windows przez osadzony komponent; odczytany obraz pulpitu oraz OnLoginComplete. Hasło wyłącznie w pamięci, poza argumentami, logami i plikami profilu.
- Ta sama zalogowana kontrolka zachowana w split view, osobnym oknie, po redokowaniu i przełączaniu kart. Test rozmiaru panelu, ponowne logowanie po reconnect i zwolnienie natywnego okna po potwierdzonym zamknięciu aplikacji.
- Regresje zewnętrznego klienta RDP: dostępność/NLA, start, anulowanie rozłączenia, rozłączenie, reconnect, niedostępny serwer i zamykanie aplikacji. Pełny zestaw UI: motywy, ikony, personalizacja, ustawienia, panele i przywracanie obszaru pracy.

## Zakres

Osadzenie wykorzystuje komponent RDP zainstalowany w Windows. Linux nadal otwiera FreeRDP w osobnym oknie, także dla przeniesionego profilu z wybranym trybem zakładki. Brak działającego mostu framebuffer dla Linux/Wayland. Konta AD/Microsoft, RDP Gateway, schowek w obu kierunkach, pełna macierz klawiatury, wiele monitorów, mieszane DPI i wielogodzinne sesje wymagają dalszych testów. Pełny ekran w osadzonej zakładce nie jest obsługiwany.

Weryfikację przeprowadzono na Windows 11 z systemowym komponentem RDP 10.0.26100. Budowanie mogło zgłaszać NU1900, gdy usługa audytu NuGet była niedostępna. Nie zmieniono wersji zależności.
