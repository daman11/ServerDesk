# ServerDesk 0.2.0 — stabilność sesji i Server Info

## Zmiany

„Połącz ponownie” odnawia połączenie w tej samej zakładce. Nieudana próba nie niszczy kontrolki: można ponowić ją lub podać inne hasło/passphrase. Poprzedni terminal, SFTP, monitoring i transfery należące do poprzedniego połączenia są anulowane i zwalniane. Nowe połączenie korzysta z osobnego tokenu; zakończenie starej operacji nie aktualizuje nowego widoku.

SshTerminalSession udostępnia Completion rozróżniające zamknięcie zdalnego shella, błąd transportu i lokalne zamknięcie. Błędy transportu przerywają oczekujący odczyt. Status zakładki i czas sesji aktualizują się na UI. Czytelny status błędu nie zawiera treści sekretów ani terminala. Terminarz odświeżania kończy się wraz ze swoją sesją; reconnect nie tworzy dodatkowych pętli monitoringu.

Server Info prezentuje nazwę hosta, dystrybucję, kernel, architekturę, uptime, użytkownika, model CPU, gniazda/rdzenie/wątki, indywidualne dyski z paskami i wyróżnieniem >=90%, IPv4/IPv6/MAC/stan interfejsów, bramy i DNS. Dane pochodzą z rzeczywistych poleceń Linux. Brak ip/lscpu zachowuje resztę monitoringu i pokazuje niedostępne dane. Parser df zachowuje spacje w mountpointach i odrzuca przepełnienia liczników.

## Weryfikacja

38 testów xUnit. Rzeczywiste testy Ubuntu WSL/OpenSSH obejmują EOF, wymuszone zerwanie pojedynczego połączenia, disposal przy zablokowanym buforze, dwie równoczesne sesje, reconnect, monitoring i SFTP. Test kontrolki Avalonia sprawdził reconnect po EOF, odzyskanie po złym passphrase, ponawianie przy otwartym SFTP/monitoringu i zamknięcie w trakcie zdalnej komendy. Obrazy są w artifacts/ui-smoke; wyniki protokołów w artifacts/network-probe/reliability.txt.

RDP wciąż używa osobnego okna. Osadzanie FreeRDP, połączenie z Windows/AD, KWallet, inne dystrybucje oraz długotrwałe testy pozostają niewykonane. Pełne MVP nie jest jeszcze ukończone.
