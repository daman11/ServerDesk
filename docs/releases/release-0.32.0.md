# ServerDesk 0.32.0 — wyszukiwanie terminala, zaufanie SSH i duże katalogi

W terminalach SSH i Serial pojawia się lokalne wyszukiwanie historii: lupa lub **Ctrl+Shift+F**, wielkość liter, całe słowa, przechodzenie do poprzedniego/następnego trafienia i powrót do bieżącego tekstu. Dopasowania przez miękkie zawinięcia liczą się raz. Nowe dane oznaczają wyniki do odświeżenia. Zapytanie pozostaje w pamięci danej karty; zwykłe Ctrl+F nadal trafia do programu zdalnego. Dostępne są teksty PL/EN oraz układ dla wąskich paneli.

**Ustawienia → Bezpieczeństwo → Zaufane klucze SSH** pozwalają wyszukać host/port/odcisk, skopiować odcisk lub usunąć konkretny wpis po potwierdzeniu. Usunięcie porównuje również oglądany odcisk, chroniąc przed zmianą wpisu w drugim oknie. Istniejące sesje działają dalej; kolejne połączenie SSH/SFTP wymaga ponownej weryfikacji. Zmieniony klucz nie jest akceptowany automatycznie.

Duże listy plików SFTP i lokalnych są filtrowane oraz sortowane poza wątkiem UI. Szybkie edycje łączą się, nieaktualne obliczenia są anulowane, a wynik nie może zastąpić nowszego katalogu. Zachowano zaznaczenie i niezależne ustawienia paneli. Sprawdzono widoki po 50 000 wpisów na panel oraz osobno rzeczywisty katalog 5000 plików OpenSSH.

Repozytorium zawiera uporządkowane przewodniki, przypięte CI Windows/Linux, blokady zależności i bezpieczne sprzątanie artefaktów. Nowa kontrola wydania wykrywa kod bez odpowiadającego tagu/paczek oraz różnice między źródłami w Git i archiwum. Wydanie obejmuje Setup/ZIP Windows, DEB/TAR.GZ Linux i źródła MIT. Paczki binarne zawierają pełną dokumentację z indeksem i powiązanymi raportami dostępnymi offline. Nie ma nowych zależności aplikacji ani migracji danych.

Wyszukiwanie obejmuje dostępną historię, nie tekst usunięty z bufora. Limit zapytania wynosi 256 znaków UTF-16; ograniczenie podświetleń może skrócić wyniki. Linux pozostaje sprawdzony pod Ubuntu/WSLg, a RDP używa tam zewnętrznego FreeRDP. Fizyczne Serial, klawiatury/IME i mieszane DPI nadal wymagają szerszej akceptacji. Beta nie ma podpisu zaufanego wydawcy ani automatycznego aktualizatora.

[Instrukcja](../getting-started.md), [weryfikacja](../reports/release-verification-0.32.0.md), [ograniczenia](../known-issues.md).
