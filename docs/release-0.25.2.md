# ServerDesk 0.25.2 — sortowanie połączeń i SFTP

Wydanie skupia się na pierwotnych funkcjach ServerDesk: menedżerze połączeń, folderach oraz lokalnym/zdalnym panelu plików SSH/SFTP.

## Połączenia

Wybierz **Nazwa**, **Adres**, **Protokół** lub **Ostatnie połączenie** w panelu bocznym. Przycisk **↑ / ↓** obok listy zmienia kierunek. Oba ustawienia są zapamiętywane; zapis jest atomowy, ostatni wybór wygrywa przy szybkich zmianach, a zamknięcie czeka na zapis. Starsze ustawienie samego pola jest nadal odczytywane. Dla daty domyślny kierunek to najnowsze najpierw.

- `server1`, `server2`, `server10` mają naturalną kolejność. Wielkie/małe litery nie zmieniają głównego porządku. Przy identycznej liczbie krótszy zapis jest pierwszy, np. `server2` przed `server02`.
- Adresy IPv4 porównują wszystkie oktety liczbowo: `10.0.0.2`, `10.0.0.20`, `10.0.0.100`. IPv6 porównuje pełne bajty adresu, a zapis skrócony i rozwinięty oznacza ten sam adres. IPv4-mapped IPv6 jest normalizowany do IPv4.
- Rosnąco grupy to IPv4, IPv6, nazwy DNS, urządzenia Serial; malejąco porządek jest odwrócony. W obrębie jednego adresu port jest liczbowy, np. 22 przed 3389. Nazwy DNS i urządzeń mają naturalne liczby; Serial korzysta z faktycznego portu, np. COM2 przed COM10.
- Protokoły sortują się według widocznej nazwy: RDP, Serial, SSH, VNC. Daty bez potwierdzonego połączenia pozostają na końcu w obu kierunkach. Remisy rozstrzyga nazwa i ID, aby wpisy nie przeskakiwały po odświeżeniu.
- **Ostatnie** i **Ulubione** respektują widoczny wybór sortowania. Ostatnie filtruje wpisy z historią; aby zobaczyć najnowsze na początku, wybierz **Ostatnie połączenie**. Wcześniej ten widok ignorował wybór nazwy/adresu.
- Foldery pozostają przed serwerami i mają własny naturalny porządek także w podfolderach. Hierarchia nie jest spłaszczana: wybrane kryterium układa serwery wewnątrz ich folderów. Przy adresie/dacie dodatkowy wiersz pokazuje wartość używaną do sortowania; pełny endpoint jest także w podpowiedzi. IPv6 jest przedstawiony jako `[adres]:port`.

## Pliki lokalne i SFTP

Kliknij nagłówek **Nazwa**, **Rozmiar**, **Typ** lub **Modyfikacja**. Pierwsze kliknięcie innej kolumny wybiera rosnący porządek, kolejne odwraca kierunek. Można nadal używać listy kryteriów i przycisku kierunku obok filtra. Strzałka nagłówka pokazuje aktywne pole i kierunek. Każdy panel ma niezależny stan; fokus nagłówka wskazuje właściwy panel dla skrótów i operacji. Enter aktywuje zaznaczony nagłówek.

Nazwy są naturalne, np. `file2.log` przed `file10.log`. `..` i katalogi pozostają przed plikami również malejąco. Rozmiar katalogu nie jest sumowany ani porównywany z systemową wartością metadanych; przy sortowaniu rozmiaru katalogi mają kolejność nazw. Brak daty pozostaje na końcu. Typ pliku jest prezentowany jako rozszerzenie (np. TXT/LOG), zgodnie z kryterium sortowania; katalogi i dowiązania zachowują swoje oznaczenia.

Zmiana sortowania/odświeżenie zachowuje widoczne zaznaczone pliki. Filtr może usunąć niewidoczne wpisy z zaznaczenia. Parametry SFTP należą do danej sesji, a ustawienia sortowania połączeń są zapamiętywane między uruchomieniami.

Nie zmieniono sposobu uwierzytelniania, działania terminala ani protokołów RDP/VNC/Serial. Nadal obowiązują [ograniczenia bety](known-issues.md). Wydanie pozostaje MIT, bez podpisu zaufanego wydawcy i automatycznego aktualizatora.
