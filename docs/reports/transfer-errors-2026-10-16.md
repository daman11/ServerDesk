# Błędy transferów SFTP przed 1.0

Wersja rozwojowa po 0.36.2. Kolejka podaje komunikaty PL/EN na podstawie typów wyjątków i kodów SFTP/socket, z instrukcjami przed ręcznym ponowieniem. Opis zawija się do trzech linii; pełny tekst pozostaje w podpowiedzi i widoku Transfery. Zachowane są ostrzeżenia o kluczu hosta, fingerprinty i osłony integralności/typu celu. Nie dodano automatycznego ponawiania ani nowych zależności.

Wykryto i poprawiono podwójną konwersję chmod: SSH.NET wymaga cyfr ósemkowych jako liczby, np. 644, a UI wcześniej przekazywało ich wartość po konwersji z podstawy 8. Zniknięcie źródła transferu dostaje komunikat o ścieżce; usuwanie zachowuje swoją dotychczasową kontrolę nieaktualnego podglądu oraz walidację rodzica/dowiązań.

## Celowana weryfikacja

- 66/66 testów `TransferErrorMessagesTests`, `LocalizationTests`, `FileDeletionTests`, `TransferTraversalTests`, bez pominięć. Obejmują m.in. typy/statusy błędów, brak surowej odpowiedzi serwera w nowych komunikatach, zachowanie fingerprintów i konkretnych komunikatów integralności. Kody Windows oznaczające pełny dysk są wstrzyknięte w teście; nie zapełniano rzeczywistego woluminu.
- Własny OpenSSH w Ubuntu WSL na loopback:22222, klient Windows i izolowana baza; żadnych zapisanych serwerów ani poświadczeń użytkownika.
- `--transfer-errors` PL/EN: produkcyjny dialog chmod ustawia dokładnie 640 i 0644; katalog 500 odmawia uploadu; oryginał zostaje bajt w bajt bez zmian i bez pozostałego `.part`. Po przywróceniu 700 ręczne Ponów kończy się zgodnym plikiem.
- Ten sam scenariusz: zniknięcie lokalnego i zdalnego źródła daje wskazówkę o ścieżce, zachowuje istniejący lokalny cel i oddaje oba sloty. Zawijanie opisu i pełna podpowiedź sprawdzone w PL/EN przy szerokościach 1000 i 640 px.
- `--transfer-queue-recovery`: PASS — zakończone kopie nie blokują kolejki podczas zatrzymanego odświeżania, anulowanie i retry po reconnect zachowują cel.
- `--file-deletion-network`: PASS — usuwanie rekurencyjne/ukryte, zachowanie celów dowiązań, odmowa chmod na dowiązaniu, anulowanie i utrata połączenia oraz zakres zaznaczenia w panelu.

Dowody lokalne: `artifacts/reports/dev-transfer-errors/` — TRX, logi i zrzuty kolejki PL/EN. Własne zdalne katalogi oraz serwer uruchomiony do próby są sprzątane po sprawdzeniu.

Ogólnego statusu Failure z SFTP nie interpretujemy jako pewnego braku miejsca. Pełny zdalny dysk, quota, WAN, fizyczne gesty i długa praca wymagają dalszej akceptacji. Nie wykonywano pełnej macierzy ani wydania; dostarczany jest przyrostowy build Windows do testów z folderu.
