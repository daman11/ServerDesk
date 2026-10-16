# ServerDesk 0.24.1 — weryfikacja 2026-10-15

## Zakres poprawki

Przywrócone karty mają pojedynczy, przewijany panel z zawijanym tekstem. Widok Transfery ma własne wyszukiwanie i filtr statusu oraz pokazuje rzeczywiste zadania SFTP z otwartych kart SSH. Nie zmieniono formatu danych ani zależności wykonawczych. Poprzednie wydania 0.23.0 i 0.24.0 pozostają w osobnych katalogach.

## Potwierdzone wyniki

- Kompilacja rozwiązania i narzędzia UI: 0 błędów, 0 ostrzeżeń. Testy jednostkowe: **357/357**, 0 pominiętych.
- NuGet `list package --vulnerable --include-transitive --format json`: poprawne zakończenie, brak błędów źródła i brak zgłoszonych podatnych pakietów w czterech projektach rozwiązania. Raport: `artifacts/ui-fixes-0241-dependency-audit.json`. Wynik bazy ostrzeżeń nie jest gwarancją braku podatności.
- Windows i Linux/WSL: przywrócone SSH/RDP/VNC/Serial w PL/EN, 1000×580 i 440×300, czcionka 16. Sprawdzenie geometrii tekstów, zawijania i przewijania do końca instrukcji; brak równocześnie widocznego opisu protokołu. Ręczna diagnostyka RDP na własnym odrzucającym serwerze TCP zastępuje komunikat przywrócenia, pokazuje wynik i nie rozpoczyna sesji.
- Odtwarzanie obszaru pracy: duplikaty kart, usunięte profile, bieżące metadane, odpięte okna, zapis panelu, wyłączanie przywracania, uszkodzona konfiguracja i przełącznik sesji. Zero automatycznych połączeń i odczytów poświadczeń.
- Oddzielny panel Transferów: brak kontrolek przeglądarki połączeń, zachowanie filtrów połączeń i transferów po nawigacji, prawidłowy pusty stan oraz cztery filtry statusu. Sesja SSH bez zadań nie jest wyświetlana jako transfer.
- Rzeczywisty OpenSSH/SFTP, Windows oraz Linux/WSL, własny port 22222 i losowe katalogi testowe: oczekiwanie w kolejce, anulowanie z panelu bocznego, ponowienie tego samego zadania, sprawdzenie przesłanej treści UTF-8, błędy, zadania zakończone, wyszukiwanie wielowyrazowe, przejście do źródłowej kolejki, wspólne czyszczenie historii. Kontrolki i fokus zachowane po odświeżeniu. Usunięcie karty zwalnia jej kontrolki również przy ukrytym widoku Transferów.
- Transfery: rzeczywista szerokość panelu 220 px, Graphite/Light i czcionka 16; brak poziomego wyjścia tekstu kart poza ich obrys.
- Regresja istniejącego SFTP na Windows: 1 MiB w obie strony i porównanie danych, pliki/katalogi, odmowa nadpisania, anulowanie podczas pytania, pierwotny cel ponowienia po zmianie ścieżek, odzyskanie zadania po błędzie, limit dwóch transferów, zwinięcie/czyszczenie kolejki i limit 50 zakończonych zadań. Układ 650/1200 px, czcionka 13/16.
- Regresja lokalizacji: PL/EN, zapis/anulowanie/ponowne otwarcie ustawień i semantyka restartu, edytor, historia, polecenia i panele protokołów; zachowane dane użytkownika.
- Gotowy EXE Windows 0.24.1: osadzony manifest Windows 10/11, asInvoker, uiAccess=false; natywne osadzenie kontrolera RDP, split, odpięcie, ponowne dokowanie, zmiana rozmiaru i zwolnienie HWND. Bez logowania do prywatnego serwera i bez poświadczeń.
- Gotowy EXE Windows: trzy procesy z izolowanymi ustawieniami — korekta położenia poza ekranem, przywrócenie maksymalizacji, zmieniona konfiguracja monitorów i czyste zamknięcie.
- Instalator: świeża instalacja w ścieżce ze spacjami i polskimi znakami, start z własną bazą, odmowa aktualizacji/deinstalacji przy zablokowanym pliku, zachowanie danych i obcych plików, zgodność wszystkich bajtów, usunięcie wyłącznie plików aplikacji. Test domyślnej rejestracji pominięto, aby zachować istniejącą rejestrację/skróty użytkownika; pozostałe testy używały `/NOREGISTER`.
- Linux/WSLg: rzeczywisty proces i nowe natywne okno z osobnym katalogiem danych. TAR i DEB: zgodność wszystkich 324 plików, wersja 0.24.1, amd64, prawa wykonania, właściciel root w DEB, launcher i wpis desktop.

Szczegółowe logi lokalnej sesji: `artifacts/ui-fixes-0241-*.txt`, raport natywnego RDP `artifacts/ui-fixes-0241-native-rdp.json` i zrzuty testów w odpowiadających katalogach. Użyto wyłącznie danych/kluczy testowych; serwer testowy zatrzymano po weryfikacji.

## Granice weryfikacji

To poprawka interfejsu, nie ponowne wykonanie całej fizycznej macierzy RDP, klawiatur, monitorów DPI i urządzeń szeregowych. Test Linux wykonano pod WSLg, nie na wszystkich dystrybucjach. Obowiązują ograniczenia w `known-issues.md`. Historia Transferów dotyczy otwartych kart i nie jest trwałym dziennikiem. Schowek/przekierowania RDP nie są zadaniami SFTP. Paczki beta nie mają podpisu zaufanego wydawcy ani automatycznego aktualizatora.
