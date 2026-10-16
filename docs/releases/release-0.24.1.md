# ServerDesk 0.24.1 — czytelne karty i Transfery

Poprawka interfejsu po 0.24.0. Wcześniejsze paczki pozostają w osobnych katalogach wydań.

Przywrócona karta pokazuje jeden panel: nazwę serwera, protokół, adres i instrukcję rozpoczęcia sesji. Opis RDP/VNC/Serial jest w tym czasie ukryty. Tekst zawija się, a w małym oknie można przewinąć instrukcję. Przywracanie nadal nie łączy automatycznie ani nie odczytuje poświadczeń.

Widok **Transfery** pokazuje rzeczywiste zadania SFTP z otwartych kart SSH. Usunięto z niego filtry RDP/VNC/Serial, sortowanie serwerów, licznik połączeń i przyciski tworzenia folderów/połączeń. Własne wyszukiwanie obejmuje plik, serwer i ścieżki; filtr statusu pozwala zobaczyć aktywne, zakończone lub błędne/anulowane zadania. Ustawienia wyszukiwania połączeń są zachowane oddzielnie.

Każde zadanie pokazuje stan i postęp oraz pozwala anulować aktywny transfer, ponowić błąd/anulowanie lub przejść do kolejki źródłowej sesji. To te same zadania co w panelu SFTP; ponawianie zachowuje pierwotny cel. Ponawianie wymaga połączonej sesji. Usunięcie zakończonej historii działa w obu widokach. Odświeżanie postępu nie przebudowuje kart i nie odbiera fokusu.

Transfery plików w tym widoku dotyczą SFTP przez SSH. Schowek i przekierowania RDP są obsługiwane przez klienta RDP; nie są kolejką SFTP. Historia dotyczy otwartych kart: zamknięcie sesji usuwa jej historię. Nadal obowiązuje limit dwóch jednoczesnych transferów w aplikacji i do 50 zakończonych zadań na kartę.

Bez nowych zależności i zmian formatu danych. Zmiany dostępne po polsku i angielsku. Zestaw testów i ograniczenia opisano w raporcie weryfikacji dołączonym do wydania.
