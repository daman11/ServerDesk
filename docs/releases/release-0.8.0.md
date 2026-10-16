# ServerDesk 0.8.0 — powrót do pracy i nawigacja między sesjami

Dodano wyszukiwarkę otwartych sesji pod Ctrl+Shift+L oraz ikoną lupy w nagłówku. Obsługuje karty obu paneli i osobne okna. Zapytanie może łączyć fragmenty nazwy, adresu, loginu, protokołu, panelu i stanu. Enter aktywuje wybraną sesję; strzałki zmieniają wybór, Esc zamyka okno. Wiersz pokazuje bieżącą ikonę stanu, a wybór przechodzi także do odpiętego okna. Wyszukiwarka przenosi fokus, bez uruchamiania połączenia.

Przy zamykaniu aplikacji zapisywana jest lista do 128 kart odnoszących się do zapisanych profili, ich kolejność/grupa, wybór, kierunek/proporcje podziału oraz otwarty panel Terminal/SFTP/Server Info. Duplikaty jednego profilu pozostają osobnymi kartami. Odpięte okna otrzymują zapamiętany normalny rozmiar/pozycję; odtwarzanie dopasowuje je do bieżących ekranów, także po usunięciu monitora. Testy geometrii obejmują ujemne współrzędne i różne skale, bez deklaracji pełnej weryfikacji systemowej mieszanego DPI.

Przywrócone karty czekają na „Połącz”. Nie uruchamiają SSH/RDP, nie odczytują poświadczeń i nie pokazują dawnego terminala. Zapis zawiera tylko identyfikatory i układ, bez adresów, danych logowania, ścieżek, sekretów oraz treści terminala. Karty Quick Connect bez zapisanego profilu są pomijane. Używana jest najnowsza konfiguracja/kolor/grafika profilu; usunięty profil jest pomijany z informacją w nagłówku. Uszkodzony zapis nie blokuje startu. Opcję przywracania można wyłączyć w Ustawieniach, co usuwa zapamiętaną listę kart.

Pasek sesji zawija akcje w wąskich panelach. Nowa lub przywrócona karta ma „Połącz”, zakończona „Połącz ponownie”, a działające SSH „Połączono”. Przy aktywnym połączeniu główny przycisk jest nieaktywny. Wymuszony restart jest w Opcje i wymaga potwierdzenia przerwania bieżących poleceń/transferów. Anulowanie zachowuje transport i terminal. Nieaktywne panele SFTP/Server Info nie próbują logować się przed rozpoczęciem SSH. Własna grafika ma oddzielny podgląd wyświetlany tylko po imporcie.

## Weryfikacja

116 testów xUnit zaliczonych, build Release bez błędów i ostrzeżeń. Nowe testy obejmują zapis referencji i duplikatów, usunięte profile/wybór, walidację uszkodzonego zapisu, dopasowanie geometrii i wyszukiwanie po wielu polach.

UiSmoke obejmuje pełny cykl zamknięcia/odtworzenia, aktualną personalizację, Quick Connect, pomijanie usuniętych profili, odpięte okna, zapamiętany panel, Ctrl+Shift+L/Enter i puste wyniki. Licznik testowego magazynu potwierdza zero odczytów poświadczeń podczas odtwarzania; wszystkie przywrócone sesje są niepołączone. Sprawdzono wyłączenie funkcji i uszkodzony JSON. Wyniki: `artifacts/ui-sessions-verification.txt`; obrazy: `artifacts/ui-sessions`.

Rzeczywiste SSH Ubuntu/WSL sprawdza ręczne połączenie odtworzonej karty, ochronę przycisku, anulowanie wymuszonego restartu z zachowaniem tego samego identyfikatora terminala oraz widoczność akcji w split view. Zachowano rzeczywiste terminal/nano, reconnect, SFTP/monitoring, dokowanie i skróty głównego okna.

## Ograniczenia

Lista kart zapisuje się przy prawidłowym zamknięciu, więc awaria procesu nie tworzy nowego zapisu. Zapamiętane są do 128 kart zapisanych profili. Minimizacja/maksymalizacja odpiętych okien oraz ich pozycja w kolejności okien OS nie są odtwarzane. Geometria jest sprawdzona w czystej logice i headless; wielomonitorowość/mieszane DPI wymagają testów na sprzęcie. Gesty D&D, czytniki ekranu, KWallet i pozostałe systemy pozostają w macierzy testów. RDP nadal korzysta z zewnętrznego klienta, bez osadzonego obrazu i rzeczywistego testu serwera Windows. Aktualny zakres: [roadmap.md](../roadmap.md).
