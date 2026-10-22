# Bitwarden / Vaultwarden

Wbudowany plugin odczytuje hasła wpisów **Login** oraz klucze prywatne wpisów **SSH Key** przez oficjalny **Bitwarden Password Manager CLI**. Działa z kontem Bitwarden US/EU albo serwerem Vaultwarden obsługującym dany typ wpisu i skonfigurowanym w CLI. Nie wymaga `bw serve` ani włączania agenta SSH Bitwarden. [Typy wpisów CLI](https://bitwarden.com/help/cli/#enums), [klucze SSH w Bitwarden](https://bitwarden.com/help/about-ssh/).

## Konfiguracja

1. Zainstaluj samodzielny program `bw.exe` na Windows lub `bw` na Linux z [oficjalnej strony CLI](https://bitwarden.com/help/cli/). Na Windows wymagany jest plik EXE; wrapper npm `bw.cmd` nie jest uruchamiany.
2. W swoim terminalu skonfiguruj serwer: `bw config server https://vault.bitwarden.com`, `bw config server https://vault.bitwarden.eu` albo `bw config server https://adres-twojego-sejfu`. Następnie wykonaj `bw login` i dokończ wymagane MFA. Nie wpisuj hasła w argumentach polecenia.
3. W **Ustawienia → Pluginy → Bitwarden / Vaultwarden** wskaż pełną ścieżkę do programu. Opcjonalny katalog profilu odpowiada `BITWARDENCLI_APPDATA_DIR`; podczas konfiguracji i logowania w terminalu musi być ten sam. Puste pole oznacza domyślny profil CLI.
4. Zapisz ustawienia. Odblokowanie aplikacji desktopowej lub rozszerzenia Bitwarden nie zastępuje logowania konta CLI. Plugin nie instaluje CLI i nie loguje konta automatycznie.

CLI musi podawać jawny `serverUrl` w `bw status`. Niektóre domyślne konfiguracje chmurowe zwracają `null`; nie zgadujemy wtedy regionu. Skonfiguruj jawny adres przed `bw login`, najlepiej w nowym, osobnym katalogu profilu CLI. Zmiana adresu już zalogowanego profilu CLI wymaga wylogowania z niego — nie wykonujemy tego automatycznie i nie zalecamy naruszania współdzielonej konfiguracji. [Implementacja statusu upstream](https://github.com/bitwarden/clients/blob/main/apps/cli/src/commands/status.command.ts).

## Użycie

- **Jednorazowo:** w oknie poświadczeń wybierz **Hasło z Bitwarden / Vaultwarden**, odblokuj sejf hasłem głównym, wyszukaj nazwę/adres i wybierz **Użyj hasła**. Wyszukiwanie wymaga co najmniej dwóch znaków, pokazuje do 100 wpisów i nie kopiuje niczego do schowka. Zapis do magazynu systemowego wymaga osobnego świadomego zaznaczenia jego opcji.
- **Wybór wpisu:** okno pokazuje dwa kroki: **Odblokuj sejf**, a następnie **Wybierz wpis**. Wyszukiwarka i zatwierdzanie wyboru pojawiają się po odblokowaniu; adres połączenia jest automatycznie wyszukiwany, jeśli stanowi poprawne zapytanie. Wyniki pokazują nazwę i podpisany login. Hasło jest pobierane przy **Użyj hasła**. Zmiana lub wyczyszczenie zapytania usuwa poprzedni wybór. Pusty wynik i błąd wyszukiwania mają osobne komunikaty; sprawdzenie stanu CLI można ponowić bez zamykania okna.
- **Klawiatura i pomoc:** Enter w polu hasła odblokowuje sejf, Enter w wyszukiwaniu uruchamia szukanie, strzałka w dół przenosi fokus do wyników, a Enter na liście zatwierdza wybór. Ctrl+F wraca do wyszukiwania; Esc zamyka okno. **Pierwsza konfiguracja sejfu** w ustawieniach i oknie sejfu wyjaśnia trzy kroki przygotowania. Szczegóły są w rozwinięciu **Polecenia i ustawienia CLI**. Pomoc działa offline. W wąskim oknie pola i przyciski ustawiają się pionowo.
- **Trwale:** w edytorze połączenia wybierz **Przypisz wpis sejfu**, wybierz wpis i zapisz profil. Login zostanie uzupełniony w formularzu; zweryfikuj go przed zapisem. Profil zawiera tylko UUID wpisu i konta, adres sejfu oraz nazwę. Przy każdym pobieraniu hasła wymagane jest odblokowanie; połączenie używa aktualnego hasła wpisu z lokalnie zsynchronizowanego CLI.
- **SSH z kluczem:** hasło wpisu jest passphrase lokalnego klucza wybranego w profilu. Sam prywatny klucz pozostaje w pliku; plugin go nie pobiera.
- **RDP na Windows:** hasła sejfu są używane w trybie **W zakładce ServerDesk**. Osobny klient `mstsc` ma własne okno poświadczeń i nie otrzymuje hasła z pluginu. Edytor wyjaśnia to ograniczenie, zachowując istniejące przypisanie na powrót do trybu osadzonego.
- **Login:** hasło trafia do loginu zapisanego w profilu. Różny login we wpisie sejfu wymaga potwierdzenia. Plugin nie zmienia adresu docelowego przy pobieraniu hasła.
- **Zmiana i powrót:** przypisane połączenie pokazuje przyciski **Zmień wpis sejfu** i **Usuń przypisanie sejfu**. Usunięcie dotyczy samego przypisania po zapisaniu formularza; wpis w sejfie pozostaje zachowany. Bez przypisania widoczny jest przycisk **Przypisz wpis sejfu**. Podczas łączenia przypisanego profilu można wybrać **Podaj hasło ręcznie**. Wyłączony plugin również umożliwia ręczne podanie hasła bez zapisu; zachowuje przypisania na później.

## Klucz SSH przechowywany w sejfie

1. W Bitwarden/Vaultwarden zapisz lub zaimportuj klucz jako wpis **SSH Key**, a następnie zsynchronizuj ten sam profil CLI poleceniem `bw sync`.
2. W edytorze połączenia SSH ustaw **Uwierzytelnianie SSH → Klucz SSH z sejfu**. Podaj login serwera; wpis SSH Key nie ustala tego loginu.
3. Wybierz **Przypisz wpis sejfu**, odblokuj sejf i wybierz **Przypisz klucz**. Zapisz połączenie.
4. Przy łączeniu odblokuj sejf i wybierz **Użyj klucza**. W tym trybie nie wskazujesz lokalnego pliku i nie zapisujesz klucza w magazynie haseł systemu.

Obsługiwane są RSA 2048–8192 bitów i Ed25519, w nieszyfrowanym dodatkowo formacie OpenSSH lub PEM/PKCS#8 zgodnym z SSH.NET. Sam sejf nadal chroni dane swoim szyfrowaniem. Jeśli pole klucza zawiera klucz zabezpieczony dodatkowym passphrase, użyj funkcji importu klucza Bitwarden i podaj hasło podczas importu; plugin wyjaśni tę sytuację zamiast zapisywać tymczasowy plik. Odcisk i klucz publiczny, jeśli CLI je podaje, muszą odpowiadać pobranemu kluczowi prywatnemu. Zaufanie do **klucza hosta serwera** pozostaje osobną kontrolą.

Prywatny materiał jest przechowywany wyłącznie w pamięci aktywnej sesji i służy także jej nowym połączeniom SFTP, transferom oraz monitoringowi. Koniec sesji, błąd łączenia, anulowanie lub zamknięcie czyści własne bufory. Ponowne połączenie ponownie odblokowuje sejf i pobiera aktualny wpis. Wyłączenie opcjonalnego pluginu zamyka jego okna i procesy CLI, zachowując pracujący terminal/SFTP oraz należący do niego klucz do końca tej sesji. Nowe połączenie wymaga ponownego włączenia pluginu.

Profil zapisuje wyłącznie metadane referencji, w tym typ wpisu. Eksport/import konfiguracji pomija przypisanie; metoda **Klucz SSH z sejfu** pozostaje i podczas łączenia poprosi o wybór wpisu. Duplikowanie otwiera edytor bez dotychczasowego przypisania — przed zapisaniem kopii wskaż klucz ponownie. Kopie bazy zachowują referencję. Wyłączony plugin nie zastępuje klucza zapisanym hasłem ani plikiem; inną metodę wybierz świadomie w edytorze. Wersje sprzed 0.47.0 nie obsługują nowej metody logowania — przed powrotem do starszej wersji przywróć zgodną kopię bazy.

## Ochrona danych i ograniczenia

Hasło główne i klucz sesji są przekazywane tylko w środowisku uruchomionego procesu CLI, nigdy w argumentach. Nie są umieszczane w bazie, logach, schowku ani konfiguracji. Surowe stdout/stderr CLI nie trafia do logów; widoczne są kontrolowane kategorie błędów. Operacje mają limit czasu 60 s, stdout 8 MiB i stderr 64 KiB; zamknięcie okna lub wyłączenie pluginu anuluje i kończy jego procesy.

CLI zwraca odszyfrowane wyniki wyszukiwania, także pola haseł i kluczy. Plugin zachowuje z nich tylko metadane; sekret odczytuje ponownie dla wybranego dokładnego UUID i typu wpisu. Bufory wyjściowe i własne bufory klucza są czyszczone po użyciu. Odszyfrowane dane, obiekty biblioteki kryptograficznej i niemodyfikowalne ciągi .NET istnieją przejściowo w pamięci. Nie gwarantujemy wyzerowania każdej kopii ani ochrony przed innym złośliwym procesem działającym z uprawnieniami użytkownika.

Zamknięcie okna usuwa lokalny klucz sesji ServerDesk. Nie wykonujemy `bw lock`/`bw logout`, które mogłyby wpłynąć na inne zastosowania tego samego profilu CLI. Aby unieważnić sesje całego profilu, wykonaj `bw lock` samodzielnie. Konto i zaszyfrowany cache pozostają pod kontrolą CLI. Synchronizuj go jawnie poleceniem `bw sync`.

Przypisanie jest sprawdzane względem UUID konta i kanonicznego adresu sejfu przed użyciem. Zmiana konta nie powoduje automatycznego odczytu z nowego sejfu. Kopiowanie profilu i eksport/import konfiguracji pomijają referencje; kopie bazy je zachowują jako metadane. Zmiana lub usunięcie referencji obraca lokalne przypisanie poświadczeń, aby stare hasło systemowe nie wróciło po zmianie źródła.

Plugin nie edytuje sejfu i nie pobiera TOTP, załączników ani certyfikatów SSH. Wymaga zgodnego CLI/serwera oraz poprawnego TLS; nie wyłącza jego weryfikacji. Testy używają syntetycznych sejfów, atrap CLI i osobnego testowego OpenSSH. Akceptacja rzeczywistego konta Bitwarden/Vaultwarden pozostaje do wykonania na osobnym koncie testowym.

Ta sama instrukcja jest dostępna offline w **Pomoc → Bitwarden / Vaultwarden**, po polsku i angielsku.
