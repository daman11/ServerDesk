# Przegląd bezpieczeństwa ServerDesk 0.22.1

Data: 2026-10-15. Zakres: kod aplikacji i infrastruktury, formaty profili/importu, lokalne operacje SFTP, negocjacja i odbiór VNC, uruchamianie RDP, schowek terminala, poświadczenia, SQL, logowanie i zależności NuGet wraz z zależnościami przechodnimi. Testy używały własnych plików, baz oraz serwerów VNC na loopback. Nie logowano się do prywatnego serwera Windows ani nie skanowano cudzych systemów.

To przegląd kodu i ukierunkowane testy odporności, nie pełny test penetracyjny ani certyfikacja. Poziomy poniżej opisują skutki i wymagane warunki; nie nadano wyników CVSS.

## Wykryte problemy i wykonane poprawki

### SEC-01 — VNC: wyczerpanie zasobów i zapętlenie odczytu (średni)

W dokładnym tagu używanej biblioteki `MarcusW.VncClient 1.0.0-alpha4` długość nazwy pulpitu i komunikatu błędu pochodzi bezpośrednio z uint32 serwera i jest używana do alokacji. Buforowanie skompresowanej ramki także nie miało limitu aplikacji. Odczyt Raw nie obsługiwał zwrotu 0 w połowie obrazu, przez co mógł zapętlić wątek z zajętym buforem. Osobny test wykazał, że bezczynny serwer mógł również blokować ukończenie zamknięcia sesji.

Wprowadzono adapter negocjacji i inicjalizacji: limit 16 KiB na nazwę/błąd, kontrola formatu pikseli i rozmiaru pulpitu przed odczytem danych, limit 68 MiB na skompresowany blok. Raw kończy odczyt przy pierwszym EOF. Zamykanie odbiornika przerywa blokujące czytanie przez zamknięcie transportu tej sesji. Zachowano wybór jawnie ustawionej metody uwierzytelniania; nie ma przejścia z hasła na None.

Wyłączono negocjowanie ZRLE w alpha4: jego licznik długości serii nie jest ograniczony rozmiarem kafla. Pozostają Raw, CopyRect i ZLib; Tight był wyłączony już wcześniej. Serwer musi obsługiwać Raw. Niektóre połączenia mogą zużywać więcej pasma. Nie wykazano zdalnego wykonania kodu; nie przypisujemy bibliotece niepotwierdzonego CVE.

Pliki: `VncSecurity.cs`, `VncProtocol.cs`, `VncFramebuffer.cs`. Dowody: `VncSecurityTests.cs` — zbyt duże długości odrzucane bez czytania payloadu, graniczny tekst, niepoprawny format/rozmiar, urwana ramka, poprawna kompresja ZLib, negocjacja RFB 3.3/3.7/3.8 z hasłem i bez niego oraz zakończenie shutdown. Dodatkowe testy UI sprawdzają rzeczywisty obraz, klawiaturę, mysz, view-only i dokowanie.

Źródła porównawcze: [dokładny tag alpha4](https://github.com/MarcusWichelmann/MarcusW.VncClient/tree/d68bf87c36ebde37efb32827a59321a7bc2408af), [Raw](https://github.com/MarcusWichelmann/MarcusW.VncClient/blob/d68bf87c36ebde37efb32827a59321a7bc2408af/src/MarcusW.VncClient/Protocol/Implementation/EncodingTypes/Frame/RawEncodingType.cs), [inicjalizacja](https://github.com/MarcusWichelmann/MarcusW.VncClient/blob/d68bf87c36ebde37efb32827a59321a7bc2408af/src/MarcusW.VncClient/Protocol/Implementation/Services/Initialization/RfbInitializer.cs). Licencja MIT biblioteki pozostaje w paczkach.

### SEC-02 — import konfiguracji: nadmierne rozwinięcie drzewa (średni)

Limit pliku 32 MiB i 10 000 wpisów nie ograniczał głębokości folderu ani liczby wygenerowanych przodków. Poprzedni kod wielokrotnie dzielił i łączył tę samą ścieżkę, pozwalając małemu dokumentowi wymusić znacznie większą pracę i liczbę wpisów. `null` zamiast profilu lub niektórych pól powodował NullReferenceException.

Folder ma teraz do 1024 znaków i 32 poziomów; import może wygenerować najwyżej 10 000 folderów, łącznie z przodkami. Rozwinięcie używa zbioru i ograniczonych prefiksów; walidacja odbywa się przed transakcją. Błędne elementy są odrzucane z komunikatem. Limity obowiązują także zapis, przenoszenie i zmianę nazwy folderu, z kontrolą całego zmienianego poddrzewa. Przerwanie walidacji nie zapisuje części importu.

Pliki: `ProfileFolder.cs`, `Models.cs`, `ProfileRepository.cs`. Testy obejmują głębokość, długość, Unicode, null, limit wygenerowanych folderów i atomowość. Test importu 100 poziomów odtworzył brak ograniczenia przed poprawką, bez próby wyczerpania pamięci komputera.

### SEC-03 — zewnętrzny RDP: wyszukiwanie pliku wykonywalnego (średni, lokalny)

`mstsc.exe` uruchamiano po samej nazwie. Jeżeli napastnik mógł umieścić taki plik w przeszukiwanej lokalizacji, program mógł uruchomić podmienionego klienta. Atak wymaga kontroli takiej lokalizacji; nie jest to zdalna podatność serwera RDP.

Teraz klient pochodzi ze ścieżki systemowej Windows odczytanej przez `Environment.SpecialFolder.System`. Ładowanie `atl.dll` hosta ActiveX ograniczono do System32. Argumenty pozostają osobnymi elementami `ArgumentList`, bez powłoki i bez hasła w wierszu poleceń.

Pliki: `RdpClient.cs`, `WindowsRdpHost.cs`. Test potwierdza pełną ścieżkę mstsc. Dodatkowy test gotowego EXE sprawdza utworzenie i przenoszenie rzeczywistego hosta RDP bez logowania.

Zasady wyszukiwania: [CreateProcessW — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw).

### SEC-04 — SFTP: niejednoznaczne lokalne nazwy Windows (średni)

Walidacja akceptowała końcowe kropki/spacje oraz nazwy urządzeń, np. `report.`, `report `, `NUL.txt` i `COM1.log`. Windows może interpretować takie nazwy inaczej niż zdalny system i utworzyć kolizję albo odrzucić operację. Testy przed poprawką potwierdziły przyjęcie tych nazw. Nie potwierdzono zapisu poza katalogiem ani pominięcia potwierdzenia nadpisania; nie przedstawiamy tego jako wykazanego path traversal.

Wspólna walidacja odrzuca aliasy Windows, nazwy urządzeń i separatory oraz sprawdza, czy wynikowy plik jest bezpośrednim dzieckiem katalogu docelowego. Używa jej plan grupy, pobieranie rekurencyjne oraz lokalne tworzenie/zmiana nazwy. Linux zachowuje nazwy poprawne na tym systemie.

Pliki: `LocalFileName.cs`, `FileSelection.cs`, `SessionPane.cs`. Testy obejmują nazwy urządzeń z rozszerzeniami i cyframi indeksu górnego, kropki/spacje, separatory, poprawne nazwy Unicode oraz zgodność Linux. Reguły: [Microsoft — nazwy plików](https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file).

### SEC-05 — zapisane hasło po zmianie tożsamości profilu (średni)

Referencja poświadczenia bazowała na identyfikatorze profilu. Edycja hosta, portu, konta lub metody uwierzytelniania nie usuwała starego hasła. Następne połączenie mogło użyć go dla zmienionego celu. Weryfikacja klucza/certyfikatu ograniczała ryzyko, ale nie zastępowała związania poświadczenia z celem.

Przy takiej edycji usuwane są dotychczasowe referencje poświadczeń przed zapisaniem nowego celu. Jeśli magazyn jest niedostępny, nowy cel nie jest zapisywany. Zmiany nazwy, koloru i folderu zachowują hasło. Otwarte sesje zachowują swoją dotychczasową tożsamość; nowy cel wymaga nowego połączenia. Interfejs wyjaśnia potrzebę ponownego podania hasła.

Pliki: `CredentialReference.cs`, `MainWindow.cs`. Testy jednostkowe oraz UI z odizolowanym magazynem sprawdzają zachowanie hasła dla zmian wyglądu, usunięcie przy zmianie hosta i odmowę zapisu przy błędzie magazynu.

### Dodatkowe wzmocnienie — schowek terminala

Jawnie wyłączono zdalne żądania odczytu i zapisu schowka przez opcje emulatora. Ręczne kopiowanie/wklejanie i przekierowanie schowka RDP pozostają osobnymi funkcjami. Test na produkcyjnej kontrolce wysyła OSC 52 odczytu/nadpisania i potwierdza brak zdarzeń oraz zachowanie lokalnej wartości schowka. Nie traktujemy domyślnie wyłączonego odczytu biblioteki jako nowej wykrytej podatności.

## Sprawdzone zabezpieczenia, w których nie znaleziono obejścia w tym zakresie

- Windows: DPAPI `CurrentUser`, brak jawnego hasła w pliku, zerowanie tymczasowych tablic z jawnym sekretem. Linux: Secret Service, sekret przez stdin, osobne argumenty procesu i limit czasu; katalogi danych mają tryb 0700.
- SSH: domyślna odmowa zaufania, jawne potwierdzenie pierwszego klucza, blokada zmienionego klucza, zapis host+port i wykrywanie konkurencyjnej próby podmiany zaufania.
- RDP: włączone CredSSP/NLA i ostrzeżenia tożsamości Windows, brak `cert:ignore` i hasła w argumentach; diagnostyka TLS nie wysyła hasła i nie akceptuje niezaufanego certyfikatu.
- SQL: parametryzacja danych użytkownika. Import nadaje nowe GUID i nie może odziedziczyć referencji do lokalnych haseł. Eksport nie zawiera haseł ani zawartości prywatnego klucza.
- SFTP: pliki tymczasowe i publikowanie po pełnym transferze, kontrola rozmiaru, odmowa niepotwierdzonego nadpisania, brak kopiowania jawnych dowiązań, walidacja komponentów ścieżek.
- Grafiki profilu: limit pliku/rozmiaru/liczby klatek; przechowywanie znormalizowanego PNG do 128×128, kontrola struktury, CRC i ograniczonej dekompresji.
- W przejrzanych wywołaniach logowania zapisuje się typ błędu, a nie hasło ani tekst sesji.

## Zależności

`dotnet list ServerDesk.sln package --vulnerable --include-transitive --format json` zakończył się kodem 0. Źródło: `https://api.nuget.org/v3/index.json`. Wynik nie zgłosił znanych podatności dla czterech projektów rozwiązania. Dowód: `artifacts/security-dependencies-0221.json`; stderr jest pusty. Jest to wynik bazy advisory dostępnej w czasie kontroli, nie dowód braku nieznanych błędów. Własne problemy VNC znaleziono mimo czystego wyniku NuGet.

Nie dodano nowych bibliotek. Nie przeprowadzono osobnego audytu każdego natywnego komponentu Skia/SQLite, systemowego mstsc ani zainstalowanego przez użytkownika FreeRDP.

## Ograniczenia i dalsza weryfikacja

- **Klasyczne VNC nadal nie szyfruje transmisji i nie weryfikuje tożsamości serwera.** Profil hasłowy nie daje ochrony transportu równoważnej SSH/RDP. Korzystaj z niego przez VPN lub istniejący tunel SSH; interfejs już o tym informuje. TLS/VeNCrypt i tunel tworzony przez ServerDesk nie zostały zaimplementowane w tym wydaniu.
- Biblioteka VNC nadal jest wersją alpha. Potrzebny jest szerszy fuzzing dekoderów, testy z rzeczywistymi implementacjami serwerów i długimi sesjami; testy loopback nie obejmują całego protokołu.
- DPAPI/Secret Service nie chronią przed złośliwym oprogramowaniem działającym jako zalogowany użytkownik. Hasło sesji pozostaje w pamięci na potrzeby ponowienia/SFTP; łańcuchów .NET nie można wiarygodnie wyzerować. Baza, historia i eksport zawierają nazwy/adresy/login oraz ścieżki, więc same również mogą być poufne.
- Nie wykonano wyścigów z lokalnym napastnikiem podmieniającym dowiązania w trakcie operacji plikowych, pełnego fuzzingu SFTP ani testu wyczerpania dysku. Awaria sieci może pozostawić własny plik `.part` na zdalnym serwerze.
- FreeRDP i Secret Service zależą od zaufanego środowiska systemowego/PATH. Nie testowano zainfekowanego konta lokalnego ani systemu operacyjnego.
- Paczki nie mają podpisu Authenticode. Kontrola archiwów i SHA-256 wykrywa przypadkowe uszkodzenie, ale suma publikowana tym samym kanałem nie zastępuje podpisu wydawcy.
- Nie przeprowadzono niezależnego pentestu, audytu domen/AD ani pełnej macierzy systemów i urządzeń.

## Dowody testów

Przed poprawkami 13 testów regresji odtworzyło przyjmowanie nazw Windows, brak limitu głębokości/null importu oraz samą nazwę mstsc (`artifacts/security-before-0221.txt`). Po poprawkach pełny zestaw ma **319/319 testów xUnit** (262 wcześniej + 57 nowych). Build rozwiązania: 0 błędów i 0 ostrzeżeń. Testy UI poświadczeń/schowka oraz regresja VNC używają odizolowanych danych; ich wyniki i kontrole gotowych paczek zapisano w `artifacts/security-*-0221*`.

Dodatkowa weryfikacja: kontrola formatów RGB 8/16/32 bit w obu kolejnościach bajtów, niepoprawnych masek/przesunięć/głębi i granic prostokątów. Testy UI poświadczeń, schowka, rzeczywistego VNC oraz lokalizacji PL/EN przeszły także w Linux/WSLg. Gotowy EXE 0.22.1 przeszedł test hosta RDP (System32 ATL), split/floating/redock i zwolnienia HWND oraz trzy starty z izolowanymi danymi. ZIP/TAR/DEB mają poprawną wersję, biblioteki i notices; nie zawierają baz ani kluczy testowych. Nie wykonano pełnego audytu parsera sekwencji/grafiki terminala.
