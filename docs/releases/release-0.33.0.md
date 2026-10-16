# ServerDesk 0.33.0 — wygodniejsza i bezpieczniejsza nawigacja SFTP

Oba panele plików mają przyciski **Wstecz**, **Dalej**, **Katalog nadrzędny** i **Katalog domowy**. Skróty **Alt+← / Alt+→ / Alt+↑ / Alt+Home** działają w panelu SFTP, a **Ctrl+L** wybiera jego ścieżkę. Pełna szerokość pola ścieżki i przewijane okruszki ułatwiają pracę w wąskich panelach. Ikony pochodzą z dotychczas przypiętej wersji Lucide; teksty są dostępne po polsku i angielsku.

Historia jest osobna dla panelu lokalnego i zdalnego, obejmuje do 100 poprawnie wczytanych katalogów i istnieje tylko w pamięci danej karty. Odświeżenie ani nieudana próba nie usuwają możliwości przejścia Dalej. Wejście w nowy katalog po cofnięciu tworzy nową gałąź historii. Ponowne połączenie i zamknięcie czyszczą historię. Zdalny przycisk domowy wraca do katalogu początkowego po zalogowaniu.

Wpisaną ścieżkę zatwierdza **Enter**, a **Escape** przywraca otwarty katalog i fokus listy. Niedokończona ścieżka od razu blokuje stare wiersze oraz operacje na nich. Transfery, nowy katalog i cele kopiowania dowiązań korzystają z potwierdzonych katalogów. Błędny odczyt przywraca ostatnie poprawnie wczytane ścieżki; nie przesuwa historii. Wynik starszego żądania nie zastępuje nowszej nawigacji.

W tym wydaniu pozostaje wspólny odczyt obu paneli: nieudane odczytanie jednego katalogu wycofuje niezakończoną zmianę obu ścieżek. Nawigacja nie zapewnia wznawiania transferu ani ochrony przed każdą równoczesną zmianą zdalnego systemu plików.

Publiczna beta MIT, bez nowych zależności aplikacji, zmian formatu profili i migracji danych. [Instrukcja](../getting-started.md), [zakres weryfikacji](../reports/release-verification-0.33.0.md), [ograniczenia](../known-issues.md).
