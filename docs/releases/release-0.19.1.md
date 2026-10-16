# ServerDesk 0.19.1 — poprawne położenie okien

Naprawiono uruchamianie częściowo poza ekranem. Wcześniejsza kontrola sprawdzała tylko lewy górny róg, a domyślny rozmiar 1320×840 mógł przekraczać obszar roboczy monitora. Teraz cała pozycja i rozmiar są dopasowywane do dostępnego miejsca, z uwzględnieniem skalowania, obramowania i paska zadań. Na mniejszym ekranie minimalny rozmiar okna również ustępuje dostępnemu miejscu. Nie trzeba usuwać własnej konfiguracji.

Nowa instalacja otwiera okno wyśrodkowane. Poprawna zapamiętana pozycja pozostaje zachowana, w tym na monitorze o ujemnych współrzędnych. Przy rozłączonym monitorze okno wraca na ekran główny. Gdy okno częściowo przecina kilka ekranów, wybierany jest ekran o największym wspólnym obszarze. Zmiana konfiguracji ekranów ponownie dopasowuje normalne okna. Na Wayland ostateczną pozycję może kontrolować menedżer okien.

Rozmiar i pozycja normalnego okna są zapisywane oddzielnie od maksymalizacji. Po restarcie zmaksymalizowanego programu przywrócenie daje wcześniejszy normalny rozmiar. Zamknięcie podczas minimalizacji nie zapisuje systemowej pozycji -32000/-32000 i nie uruchamia aplikacji jako zminimalizowanej. Nowy zapis geometrii jest pojedynczym ustawieniem JSON; istniejące klucze x/y/width/height są odczytywane jako migracja. Błędne, niefinitywne lub uszkodzone wartości nie blokują uruchomienia.

Odłączone okna sesji także mieszczą się w obszarze roboczym, a miejsce na ich pasek tytułu uwzględnia skalowanie DPI. Nie zmienia to sesji ani zawartości terminala. Ponowne dokowanie zachowuje dotychczasowe działanie.

## Weryfikacja

Błąd odtworzono w rzeczywistym oknie Windows: widoczny prostokąt [1667,872,2989,1714] przy obszarze roboczym [0,0,1920,1032]. Test po poprawce sprawdza korektę pozycji, ponowne otwarcie po maksymalizacji, powrót do rozmiaru 950×640, ponowne otwarcie po minimalizacji oraz odłączone okno.

241/241 xUnit. Nowe przypadki: cały prostokąt na ekranie przy 100/125/150/175/200%, monitory o ujemnych współrzędnych i różnym skalowaniu, przerwy między ekranami, odłączenie monitora, skalowanie ramki okna, stabilność dopasowania, zapis i zgodność starych ustawień oraz NaN/Infinity/uszkodzony JSON.

Testy skalowania i kilku ekranów są modelowe. Fizycznego przełączania monitorów z różnym DPI nie wykonano. Nowe natywne próby uruchamiania dotyczą Windows przy dostępnej skali 100%. W tym wydaniu nie powtarzano zdalnego logowania RDP ani testu fizycznego adaptera Serial.

Wyniki i paczki: `artifacts/release-0191-verification.md`. Przed testami używano osobnych katalogów danych; nie resetowano profili ani poświadczeń użytkownika.
