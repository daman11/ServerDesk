# Bitwarden / Vaultwarden

Wbudowany plugin odczytuje hasła wpisów **Login** przez oficjalny **Bitwarden Password Manager CLI**. Działa z kontem Bitwarden US/EU albo serwerem Vaultwarden skonfigurowanym w CLI. Nie wymaga publicznego API do odszyfrowanych haseł ani `bw serve`.

## Konfiguracja

1. Zainstaluj samodzielny program `bw.exe` na Windows lub `bw` na Linux z [oficjalnej strony CLI](https://bitwarden.com/help/cli/). Na Windows wymagany jest plik EXE; wrapper npm `bw.cmd` nie jest uruchamiany.
2. W swoim terminalu skonfiguruj serwer: `bw config server https://vault.bitwarden.com`, `bw config server https://vault.bitwarden.eu` albo `bw config server https://adres-twojego-sejfu`. Następnie wykonaj `bw login` i dokończ wymagane MFA. Nie wpisuj hasła w argumentach polecenia.
3. W **Ustawienia → Pluginy → Bitwarden / Vaultwarden** wskaż pełną ścieżkę do programu. Opcjonalny katalog profilu odpowiada `BITWARDENCLI_APPDATA_DIR`; podczas konfiguracji i logowania w terminalu musi być ten sam. Puste pole oznacza domyślny profil CLI.
4. Zapisz ustawienia. Odblokowanie aplikacji desktopowej lub rozszerzenia Bitwarden nie zastępuje logowania konta CLI. Plugin nie instaluje CLI i nie loguje konta automatycznie.

CLI musi podawać jawny `serverUrl` w `bw status`. Niektóre domyślne konfiguracje chmurowe zwracają `null`; nie zgadujemy wtedy regionu. Skonfiguruj jawny adres przed `bw login`, najlepiej w nowym, osobnym katalogu profilu CLI. Zmiana adresu już zalogowanego profilu CLI wymaga wylogowania z niego — nie wykonujemy tego automatycznie i nie zalecamy naruszania współdzielonej konfiguracji. [Implementacja statusu upstream](https://github.com/bitwarden/clients/blob/main/apps/cli/src/commands/status.command.ts).

## Użycie

- **Jednorazowo:** w oknie poświadczeń wybierz **Hasło z Bitwarden / Vaultwarden**, odblokuj sejf hasłem głównym, wyszukaj nazwę/adres i wybierz **Użyj hasła**. Wyszukiwanie wymaga co najmniej dwóch znaków, pokazuje do 100 wpisów i nie kopiuje niczego do schowka. Zapis do magazynu systemowego wymaga osobnego świadomego zaznaczenia jego opcji.
- **Wybór wpisu:** po odblokowaniu pole hasła głównego znika, a adres połączenia jest automatycznie wyszukiwany, jeśli stanowi poprawne zapytanie. Podgląd pokazuje nazwę i login wybranego wpisu; hasło jest pobierane dopiero przy **Użyj hasła**. Zmiana lub wyczyszczenie zapytania usuwa poprzedni wybór. Pusty wynik i błąd wyszukiwania mają osobne komunikaty; sprawdzenie stanu CLI można ponowić bez zamykania okna.
- **Klawiatura i pomoc:** Enter w polu hasła odblokowuje sejf, Enter w wyszukiwaniu uruchamia szukanie, strzałka w dół przenosi fokus do wyników, a Enter na liście zatwierdza wybór. Ctrl+F wraca do wyszukiwania; Esc zamyka okno. Rozwijana **Instrukcja konfiguracji** działa offline także wtedy, gdy CLI nie jest jeszcze skonfigurowany. W wąskim oknie pola i przyciski ustawiają się pionowo.
- **Trwale:** w edytorze połączenia wybierz **Przypisz wpis sejfu**, wybierz wpis i zapisz profil. Login zostanie uzupełniony w formularzu; zweryfikuj go przed zapisem. Profil zawiera tylko UUID wpisu i konta, adres sejfu oraz nazwę. Przy każdym pobieraniu hasła wymagane jest odblokowanie; połączenie używa aktualnego hasła wpisu z lokalnie zsynchronizowanego CLI.
- **SSH z kluczem:** hasło wpisu jest passphrase lokalnego klucza wybranego w profilu. Sam prywatny klucz pozostaje w pliku; plugin go nie pobiera.
- **RDP na Windows:** hasła sejfu są używane w trybie **W zakładce ServerDesk**. Osobny klient `mstsc` ma własne okno poświadczeń i nie otrzymuje hasła z pluginu. Edytor wyjaśnia to ograniczenie, zachowując istniejące przypisanie na powrót do trybu osadzonego.
- **Login:** hasło trafia do loginu zapisanego w profilu. Różny login we wpisie sejfu wymaga potwierdzenia. Plugin nie zmienia adresu docelowego przy pobieraniu hasła.
- **Powrót:** **Usuń przypisanie sejfu** w edytorze usuwa samą referencję. Podczas łączenia przypisanego profilu można wybrać **Podaj hasło ręcznie**. Wyłączony plugin również umożliwia ręczne podanie hasła bez zapisu; zachowuje przypisania na później.

## Ochrona danych i ograniczenia

Hasło główne i klucz sesji są przekazywane tylko w środowisku uruchomionego procesu CLI, nigdy w argumentach. Nie są umieszczane w bazie, logach, schowku ani konfiguracji. Surowe stdout/stderr CLI nie trafia do logów; widoczne są kontrolowane kategorie błędów. Operacje mają limit czasu 60 s, stdout 8 MiB i stderr 64 KiB; zamknięcie okna lub wyłączenie pluginu anuluje i kończy jego procesy.

CLI zwraca odszyfrowane wyniki wyszukiwania, także pola haseł. Plugin zachowuje z nich tylko metadane; hasło odczytuje ponownie dla wybranego dokładnego UUID. Bufory wyjściowe są czyszczone po odczycie. Odszyfrowane dane i niemodyfikowalne ciągi .NET istnieją przejściowo w pamięci. Nie gwarantujemy wyzerowania każdej kopii ani ochrony przed innym złośliwym procesem działającym z uprawnieniami użytkownika.

Zamknięcie okna usuwa lokalny klucz sesji ServerDesk. Nie wykonujemy `bw lock`/`bw logout`, które mogłyby wpłynąć na inne zastosowania tego samego profilu CLI. Aby unieważnić sesje całego profilu, wykonaj `bw lock` samodzielnie. Konto i zaszyfrowany cache pozostają pod kontrolą CLI. Synchronizuj go jawnie poleceniem `bw sync`.

Przypisanie jest sprawdzane względem UUID konta i kanonicznego adresu sejfu przed użyciem. Zmiana konta nie powoduje automatycznego odczytu z nowego sejfu. Kopiowanie profilu i eksport/import konfiguracji pomijają referencje; kopie bazy je zachowują jako metadane. Zmiana lub usunięcie referencji obraca lokalne przypisanie poświadczeń, aby stare hasło systemowe nie wróciło po zmianie źródła.

Pierwsza wersja nie edytuje sejfu i nie pobiera TOTP, załączników ani wpisów SSH Key. Wymaga zgodnego CLI/serwera oraz poprawnego TLS; nie wyłącza jego weryfikacji. Testy automatyczne używają wyłącznie syntetycznych sejfów i atrap CLI. Akceptacja rzeczywistego konta Bitwarden/Vaultwarden pozostaje do wykonania na osobnym koncie testowym.

Ta sama instrukcja jest dostępna offline w **Pomoc → Bitwarden / Vaultwarden**, po polsku i angielsku.
