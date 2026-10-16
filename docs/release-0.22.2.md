# ServerDesk 0.22.2 — bezpieczeństwo i stabilność

Opis zmian i dowody: [security-stability-0.22.2.md](security-stability-0.22.2.md).

- VNC nie blokuje interfejsu, gdy serwer zatrzyma się w połowie ramki. Zachowuje ostatni wyświetlony obraz; zmiana rozmiaru i rozłączanie nadal działają.
- Błąd pakietu lub transportu VNC daje stan awarii, zamiast wyglądać jak zwykłe zamknięcie. Naprawiono też kolejność prezentacji stanu przy natychmiastowym zakończeniu VNC/Serial.
- Windows: hasło jest szyfrowane DPAPI poza wątkiem UI, zapisywane do osobnego zaszyfrowanego pliku i podmieniane po ukończeniu. Nieudany zapis zachowuje poprzedni wpis. Operacje magazynu są kolejkowane w instancji aplikacji.
- Uszkodzonego lub nieczytelnego wpisu poświadczenia nie trzeba usuwać, żeby nawiązać połączenie. Dialog PL/EN wyjaśnia błąd i pozwala podać hasło do bieżącej sesji; zastąpienie zapisanego hasła wymaga zaznaczenia Zapamiętaj.
- Magazyn poświadczeń ma limity wielkości. Linux: ograniczone stdout/stderr, odczyt obu strumieni równolegle ze stdin, przerwanie procesu przy przekroczeniu limitu oraz obsługa zablokowanego stdin.

335/335 testów xUnit, 0 błędów i ostrzeżeń kompilacji. Rzeczywisty VNC na loopback oraz odzyskiwanie poświadczeń PL/EN sprawdzono w UI Windows i Linux/WSLg. Systemowy Secret Service sprawdzono w osobnej sesji D-Bus z jednorazowym magazynem, także przy awariach procesów pomocniczych. Nowych zależności nie dodano.

VNC nadal nie obsługuje TLS/VeNCrypt ani własnego tunelu SSH. Do ochrony transmisji potrzebny jest VPN lub istniejący tunel. Paczki pozostają bez podpisu wydawcy; pozostałe ograniczenia audytu 0.22.1 nadal obowiązują.
