# ServerDesk 0.29.0 — większe listy i odporność transferów

Przeglądarka połączeń wymienia wyniki filtrowania jednym zbiorczym powiadomieniem. Zapytanie jest dzielone na słowa raz na filtrowanie, a rozbudowane menu serwera powstaje przy jego pierwszym otwarciu. Zachowano sortowanie, foldery, zaznaczenie i wszystkie akcje kontekstowe.

Postęp transferu ma najwyżej jedno oczekujące wywołanie UI. Szybki transfer nie tworzy już kolejki raportów proporcjonalnej do liczby pakietów. Upload ogranicza ilość danych oczekujących na potwierdzenie do 256 KiB, zachowując przesyłanie kilku pakietów jednocześnie. Po anulowaniu zablokowany dedykowany klient jest zamykany w tle; nie rozłącza to osobnego terminala ani innych transferów. Download używa publicznego strumienia SSH.NET i ograniczonego zamykania po anulowaniu. Zamknięcie klienta transferu odbywa się poza UI.

Odczyt zdalnego katalogu i sprawdzanie istnienia ścieżki korzystają z asynchronicznych API z tokenem anulowania. Lokalne wyliczanie sprawdza token między wpisami. Usunięto dodatkowe sortowanie przed właściwym sortowaniem przeglądarki.

Testy obejmują 1000 zapisanych profili i rzeczywisty katalog 5000 plików, przerwanie uploadu podczas nadpisania, „cichą” utratę sieci w obu kierunkach, zachowanie oryginału, retry z porównaniem bajtów i zwalnianie kolejki. [Wyniki i granice](release-verification-0.29.0.md), [stan MVP](mvp-status.md).

Po zerwaniu sieci może pozostać **zdalny plik tymczasowy `.serverdesk-….part`**. Próba sprzątania ma ograniczony czas; aplikacja nie usuwa automatycznie plików znalezionych później na serwerze. Retry rozpoczyna transfer od początku. Ograniczenie okna uploadu może zmniejszyć maksymalną przepustowość łączy o dużym opóźnieniu; nie jest to implementacja wznawiania od offsetu. Bez nowych zależności, zmian bazy i formatów poświadczeń. Publiczna beta MIT.
