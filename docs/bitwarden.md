# Bitwarden / Vaultwarden

Wbudowany plugin odczytuje hasła wpisów **Login** oraz klucze prywatne wpisów **SSH Key** przez oficjalny **Bitwarden Password Manager CLI**. Działa z kontem Bitwarden US/EU albo serwerem Vaultwarden obsługującym dany typ wpisu. Nie wymaga `bw serve` ani włączania agenta SSH Bitwarden. [Typy wpisów CLI](https://bitwarden.com/help/cli/#enums), [klucze SSH w Bitwarden](https://bitwarden.com/help/about-ssh/).

## Konfiguracja w oknie aplikacji

1. Włącz plugin w **Ustawienia → Pluginy → Bitwarden / Vaultwarden**.
2. Wybierz **Bitwarden USA**, **Bitwarden EU** lub **Własny serwer**. Dla Vaultwarden podaj adres HTTPS z zaufanym certyfikatem. Możesz zapisać e-mail konta.
3. Wybierz **Zapisz i połącz z sejfem**. Najpierw zapisujemy ustawienia, następnie otwieramy logowanie w GUI. Anulowanie ustawień nie uruchamia logowania.
4. Podaj e-mail i hasło główne. Wybierz metodę kodu skonfigurowaną na koncie: aplikacja uwierzytelniająca, e-mail lub YubiKey OTP. Kod 2FA, weryfikacja nowego urządzenia i ewentualny dodatkowy client_secret mają osobne zamaskowane okna.
5. Odblokuj sejf raz na czas działania ServerDesk. Zamknięcie okna sejfu zachowuje odblokowanie. Przy kolejnym uruchomieniu aplikacji pojawi się zamaskowane pole hasła; **Pomiń** pozwala pracować bez sejfu i odblokować go później. Opcja **Odblokuj sejf przy uruchamianiu ServerDesk** jest w ustawieniach pluginu. Nowa, nieskonfigurowana instalacja nie wyświetla tego okna.
6. **Zablokuj sejf** usuwa klucz sesji z pamięci ServerDesk bez wylogowania konta. **Synchronizuj sejf** odświeża lokalny cache oficjalnego klienta, a **Wyloguj konto sejfu** usuwa logowanie własnego profilu. Zamknięcie programu, wyłączenie pluginu lub zmiana serwera/e-maila/profilu CLI również usuwa odblokowanie. Hasło główne nie jest przechowywane na przyszłe operacje.

Pełna paczka zawiera oficjalny klient **Bitwarden CLI OSS 2026.10.0** dla Windows/Linux x64. Nie instalujesz go osobno i nie wskazujesz ścieżki. SHA-256 pliku jest sprawdzany przed każdym uruchomieniem. Brak lub uszkodzenie wymaga ponownej instalacji kompletnej paczki. Komponent jest odrębnym procesem GPL v3; kod ServerDesk pozostaje MIT. Paczka zawiera notices i źródła OSS. [Pochodzenie i budowanie](../third-party/bitwarden/README.md).

ServerDesk tworzy własny katalog konta według kanonicznego serwera i e-maila w bitwarden-accounts pod katalogiem danych aplikacji. Nie używa domyślnego profilu CLI ani konta rozszerzenia. Zmiana serwera/e-maila wybiera inny katalog. Stare przypisania konta/serwera nadal podlegają weryfikacji. Wyloguj poprzednie konto przed zmianą konfiguracji, jeśli chcesz usunąć jego aktywne logowanie z lokalnego cache.

### Zgodność ze starszą konfiguracją

Rozwinięcie **Zewnętrzny klient i zgodność ze starszą konfiguracją** zachowuje możliwość użycia własnego natywnego bw.exe/bw i katalogu BITWARDENCLI_APPDATA_DIR. Dotychczasowe ścieżki pozostają zapisane. Ten profil musi być wcześniej skonfigurowany i zalogowany samodzielnie; ServerDesk nie zmienia jego serwera ani go nie wylogowuje. Jawny serverUrl jest wymagany dla bezpiecznej identyfikacji konta. Wrapper npm bw.cmd nie jest obsługiwany.

GUI obsługuje logowanie e-mailem i hasłem głównym z kodami, bez pierwotnego logowania SSO/FIDO2 ani osobistym kluczem API. Nie ma opcji pomijania weryfikacji TLS. Dodatkowy client_secret przy ochronie logowania e-mailem jest przekazywany na stdin, nie jako trwała konfiguracja konta.

## Użycie

- **Jednorazowo:** w oknie poświadczeń wybierz **Hasło z Bitwarden / Vaultwarden**, odblokuj sejf hasłem głównym, wyszukaj nazwę/adres i wybierz **Użyj hasła**. Wyszukiwanie wymaga co najmniej dwóch znaków, pokazuje do 100 wpisów i nie kopiuje niczego do schowka. Zapis do magazynu systemowego wymaga osobnego świadomego zaznaczenia jego opcji.
- **Wybór wpisu:** okno pokazuje dwa kroki: **Odblokuj sejf**, a następnie **Wybierz wpis**. Wyszukiwarka i zatwierdzanie wyboru pojawiają się po odblokowaniu; adres połączenia jest automatycznie wyszukiwany, jeśli stanowi poprawne zapytanie. Wyniki pokazują nazwę i podpisany login. Hasło jest pobierane przy **Użyj hasła**. Zmiana lub wyczyszczenie zapytania usuwa poprzedni wybór. Pusty wynik i błąd wyszukiwania mają osobne komunikaty; sprawdzenie stanu CLI można ponowić bez zamykania okna.
- **Klawiatura i pomoc:** Enter w polu hasła odblokowuje sejf, Enter w wyszukiwaniu uruchamia szukanie, strzałka w dół przenosi fokus do wyników, a Enter na liście zatwierdza wybór. Ctrl+F wraca do wyszukiwania; Esc zamyka okno. **Pierwsza konfiguracja sejfu** w ustawieniach i oknie sejfu wyjaśnia trzy kroki przygotowania. Szczegóły są w rozwinięciu **Szczegóły konfiguracji sejfu**. Pomoc działa offline. W wąskim oknie pola i przyciski ustawiają się pionowo.
- **Trwale:** w edytorze połączenia wybierz **Przypisz wpis sejfu**, wybierz wpis i zapisz profil. Login zostanie uzupełniony w formularzu; zweryfikuj go przed zapisem. Profil zawiera tylko UUID wpisu i konta, adres sejfu oraz nazwę. Kolejne połączenia korzystają z istniejącego odblokowania; hasło wybranego wpisu jest pobierane ponownie z lokalnie zsynchronizowanego CLI. Odblokowanie jest wymagane ponownie po ręcznej blokadzie albo zakończeniu pracy programu.
- **SSH z kluczem:** hasło wpisu jest passphrase lokalnego klucza wybranego w profilu. Sam prywatny klucz pozostaje w pliku; plugin go nie pobiera.
- **RDP na Windows:** hasła sejfu są używane w trybie **W zakładce ServerDesk**. Osobny klient `mstsc` ma własne okno poświadczeń i nie otrzymuje hasła z pluginu. Edytor wyjaśnia to ograniczenie, zachowując istniejące przypisanie na powrót do trybu osadzonego.
- **Login:** hasło trafia do loginu zapisanego w profilu. Różny login we wpisie sejfu wymaga potwierdzenia. Plugin nie zmienia adresu docelowego przy pobieraniu hasła.
- **Zmiana i powrót:** przypisane połączenie pokazuje przyciski **Zmień wpis sejfu** i **Usuń przypisanie sejfu**. Usunięcie dotyczy samego przypisania po zapisaniu formularza; wpis w sejfie pozostaje zachowany. Bez przypisania widoczny jest przycisk **Przypisz wpis sejfu**. Podczas łączenia przypisanego profilu można wybrać **Podaj hasło ręcznie**. Wyłączony plugin również umożliwia ręczne podanie hasła bez zapisu; zachowuje przypisania na później.

## Klucz SSH przechowywany w sejfie

1. W Bitwarden/Vaultwarden zapisz lub zaimportuj klucz jako wpis **SSH Key**, a następnie wybierz **Synchronizuj sejf** w oknie sejfu.
2. W edytorze połączenia SSH ustaw **Uwierzytelnianie SSH → Klucz SSH z sejfu**. Podaj login serwera; wpis SSH Key nie ustala tego loginu.
3. Wybierz **Przypisz wpis sejfu**, odblokuj sejf i wybierz **Przypisz klucz**. Zapisz połączenie.
4. Przy łączeniu odblokuj sejf i wybierz **Użyj klucza**. W tym trybie nie wskazujesz lokalnego pliku i nie zapisujesz klucza w magazynie haseł systemu.

Obsługiwane są RSA 2048–8192 bitów i Ed25519, w nieszyfrowanym dodatkowo formacie OpenSSH lub PEM/PKCS#8 zgodnym z SSH.NET. Sam sejf nadal chroni dane swoim szyfrowaniem. Jeśli pole klucza zawiera klucz zabezpieczony dodatkowym passphrase, użyj funkcji importu klucza Bitwarden i podaj hasło podczas importu; plugin wyjaśni tę sytuację zamiast zapisywać tymczasowy plik. Odcisk i klucz publiczny, jeśli CLI je podaje, muszą odpowiadać pobranemu kluczowi prywatnemu. Zaufanie do **klucza hosta serwera** pozostaje osobną kontrolą.

Prywatny materiał jest przechowywany wyłącznie w pamięci aktywnej sesji i służy także jej nowym połączeniom SFTP, transferom oraz monitoringowi. Koniec sesji, błąd łączenia, anulowanie lub zamknięcie czyści własne bufory. Ponowne połączenie pobiera aktualny wpis; jeśli sejf pozostaje odblokowany w ServerDesk, nie wymaga ponownego hasła głównego. Wyłączenie opcjonalnego pluginu zamyka jego okna i procesy CLI, zachowując pracujący terminal/SFTP oraz należący do niego klucz do końca tej sesji. Nowe połączenie wymaga ponownego włączenia pluginu.

Profil zapisuje wyłącznie metadane referencji, w tym typ wpisu. Eksport/import konfiguracji pomija przypisanie; metoda **Klucz SSH z sejfu** pozostaje i podczas łączenia poprosi o wybór wpisu. Duplikowanie otwiera edytor bez dotychczasowego przypisania — przed zapisaniem kopii wskaż klucz ponownie. Kopie bazy zachowują referencję. Wyłączony plugin nie zastępuje klucza zapisanym hasłem ani plikiem; inną metodę wybierz świadomie w edytorze. Wersje sprzed 0.47.0 nie obsługują nowej metody logowania — przed powrotem do starszej wersji przywróć zgodną kopię bazy.

## Ochrona danych i ograniczenia

Hasło główne i klucz sesji są przekazywane tylko w środowisku uruchomionego procesu CLI, nigdy w argumentach. Kody 2FA idą wyłącznie na stdin procesu. Hasło główne, kody i klucz sesji nie są umieszczane przez ServerDesk w bazie, logach, schowku ani konfiguracji. Surowe stdout/stderr CLI nie trafia do logów; widoczne są kontrolowane kategorie błędów. Logowanie ma limit 180 s, pozostałe operacje 60 s, stdout 8 MiB i stderr 64 KiB; zamknięcie okna lub wyłączenie pluginu anuluje i kończy jego procesy.

CLI zwraca odszyfrowane wyniki wyszukiwania, także pola haseł i kluczy. Plugin zachowuje z nich tylko metadane; sekret odczytuje ponownie dla wybranego dokładnego UUID i typu wpisu. Bufory wyjściowe i własne bufory klucza są czyszczone po użyciu. Odszyfrowane dane, obiekty biblioteki kryptograficznej i niemodyfikowalne ciągi .NET istnieją przejściowo w pamięci. Nie gwarantujemy wyzerowania każdej kopii ani ochrony przed innym złośliwym procesem działającym z uprawnieniami użytkownika.

Klucz odblokowania pozostaje wyłącznie w pamięci pluginu do zamknięcia ServerDesk, ręcznej blokady, wylogowania, zmiany konfiguracji konta lub wyłączenia pluginu. Zamknięcie samego okna anuluje jego bieżącą operację, a kolejne okno korzysta z tej samej sesji. Operacje są wykonywane kolejno, aby nie uruchamiać równocześnie kilku procesów CLI dla jednego profilu. Własny profil zachowuje logowanie, tokeny konta i zaszyfrowany cache zgodnie z zachowaniem oficjalnego klienta. **Wyloguj konto sejfu** usuwa logowanie tylko w tym profilu. Profil zewnętrzny nie jest wylogowywany. Synchronizacja jest jawna. Blokada pliku zapobiega równoległemu zapisywaniu jednego własnego profilu przez ServerDesk. Nie uruchamiaj innych narzędzi bezpośrednio na tych katalogach.

Ponowne otwarcie odblokowanego okna z przypisanym wpisem nie uruchamia kontroli stanu CLI. Użycie hasła lub klucza sprawdza konto przed i po odczycie dokładnego UUID. Dla dołączonego klienta i niezmienionego własnego profilu wystarcza jeden proces pobierający wpis: lokalny odcisk danych potwierdza aktualność wcześniejszej kontroli konta. Odcisk obejmuje cały plik, a klucz automatycznego odblokowania jest uwzględniany po uwierzytelnieniu HMAC i przejściowym odszyfrowaniu w pamięci; kolejne szyfrowanie tego samego klucza przez CLI nie wymusza ponownej kontroli. Własne bufory są czyszczone. Hasła i klucze wpisów nadal pobiera oficjalny CLI, bez dodatkowego cache’u sekretów.

Zmiana danych uruchamia ponowną kontrolę stanu. Zewnętrzny CLI, nieobsługiwany format klucza, brak dostępu, plik większy niż 64 MiB lub katalog przenośny `bw-data` zachowują pełne kontrole CLI. Podczas ponownego otwarcia wyszukiwarki można wykorzystać metadane do 8 ostatnich zapytań z ostatnich 30 sekund; cache nie zawiera haseł ani kluczy prywatnych. Jawne **Szukaj** zawsze odświeża wyniki, a synchronizacja lub blokada czyści metadane. Weryfikacja SHA-256 programu CLI pozostaje aktywna przy każdym jego uruchomieniu. Sam CLI oraz sieć nadal mogą wpływać na opóźnienia.

Reset fabryczny zachowuje cache kont i kopie do odzyskania danych; nie jest bezpiecznym wymazaniem. Wyloguj konto przed usunięciem konfiguracji. Profile i tokeny klienta są poza kopią samej bazy SQLite. Uprawnienia konta systemowego chronią katalog; aplikacja nie izoluje go przed innym procesem tego samego użytkownika.

Oficjalny klient może łączyć się z serwerem przy sprawdzaniu/synchronizacji oraz pobierać publiczną konfigurację Bitwarden podczas pierwszego uruchomienia przed zmianą domyślnego serwera. Nie wysyłamy zawartości sejfu do innej usługi ServerDesk. Rzeczywiste konto testowe wymaga odrębnej akceptacji.

Przypisanie jest sprawdzane względem UUID konta i kanonicznego adresu sejfu przed użyciem. Zmiana konta nie powoduje automatycznego odczytu z nowego sejfu. Kopiowanie profilu i eksport/import konfiguracji pomijają referencje; kopie bazy je zachowują jako metadane. Zmiana lub usunięcie referencji obraca lokalne przypisanie poświadczeń, aby stare hasło systemowe nie wróciło po zmianie źródła.

Plugin nie edytuje sejfu i nie pobiera TOTP, załączników ani certyfikatów SSH. Wymaga zgodnego CLI/serwera oraz poprawnego TLS; nie wyłącza jego weryfikacji. Testy używają syntetycznych sejfów, atrap CLI i osobnego testowego OpenSSH. Akceptacja rzeczywistego konta Bitwarden/Vaultwarden pozostaje do wykonania na osobnym koncie testowym.

Ta sama instrukcja jest dostępna offline w **Pomoc → Bitwarden / Vaultwarden**, po polsku i angielsku.
