# ServerDesk 0.15.1 — poprawka osadzonego RDP

W wydaniu 0.15.0 uruchomienie EXE mogło zakończyć się komunikatem „Unable to create child window for native control host”. Serwer i negocjacja NLA były dostępne; błąd dotyczył utworzenia lokalnego okna pulpitu. W EXE brakowało manifestu zgodności z Windows 10/11. Poprzedni test uruchamiany przez dotnet.exe korzystał z manifestu hosta .NET i nie ujawniał różnicy względem paczki.

Dodano app.manifest do aplikacji oraz narzędzia UiSmoke. Jest osadzony w EXE, z deklaracją Windows 10/11 oraz asInvoker — uruchomienie nie wymaga podwyższania uprawnień. Publikacja Windows odczytuje manifest z zasobów rzeczywistego EXE i odrzuca paczkę, jeżeli brakuje wymaganej deklaracji. NLA i weryfikacja certyfikatu pozostają włączone.

## Obsługa błędu i odzyskiwanie

Niepowodzenie osadzenia daje komunikat po polsku w karcie sesji, bez powielającego go okna błędu. Przycisk **Otwórz w osobnym oknie** uruchamia systemowy klient RDP dla tej samej karty. Wybór ten dotyczy bieżącej instancji; zapisany profil zachowuje tryb osadzony. Pasek narzędzi aktualizuje przyciski, ustawienie pełnego ekranu, opis i stan klienta zewnętrznego. Dane logowania zmienia się wtedy w jego oknie.

Ikona pulpitu jest wyrównana do nagłówka karty. Puste miejsce certyfikatu jest ukryte, a dolny komunikat rozróżnia błąd lokalnego osadzenia od problemów z serwerem.

Aby korzystać z naprawy, rozpakuj całe wydanie 0.15.1 i uruchom znajdujący się w nim ServerDesk.App.exe. Uruchomiony wcześniej proces 0.15.0 nie otrzymuje poprawki automatycznie. Profile i ustawienia korzystają z dotychczasowego magazynu danych.

## Weryfikacja

Błąd odtworzono w UiSmoke.exe bez manifestu. Test tej samej kontrolki po poprawce uruchomiono ponownie jako EXE: tworzenie, opóźnione odpięcie i ponowne dołączenie oraz zwalnianie HWND. Odczyt zasobów starego opublikowanego ServerDesk.App.exe potwierdził brak supportedOS.

162/162 testy jednostkowe. Testy UI obejmują regresje klienta zewnętrznego, edytor profilu oraz rzeczywistą ścieżkę niedostępnego hosta na platformie Headless: stan błędu, brak pozostałego ładowania, polski komunikat, brak duplikatu modalnego i działający przycisk odzyskiwania. Klient procesu testowego nie jest dowodem uwierzytelnienia.

Dodano opt-in `--check-rdp-host REPORT` do gotowego EXE. Tworzy kontrolkę w pełnym oknie ServerDesk, sprawdza dokowanie i zwolnienie zasobów, bez sieci i na osobnych danych. Wariant z serwerem, loginem oraz jednorazową nazwą pipe służy testom rzeczywistego logowania i reconnect. Narzędzie AppProbe przekazuje hasło wyłącznie w pamięci przez pipe dostępny dla bieżącego użytkownika. Wyniki tego sprawdzenia zapisuje do osobnego JSON, poza paczką i magazynem profili.

Gotowy ServerDesk.App.exe 0.15.1 przeszedł test tworzenia kontrolki bez sieci oraz rzeczywistego logowania lokalnego konta na zatwierdzonym serwerze. OnLoginComplete potwierdziło logowanie i reconnect; podział widoku, odłączenie do okna i powrót zachowały HWND oraz stan zalogowania. Wcześniejszy HWND po reconnect oraz ostatni HWND przy zamknięciu zostały zwolnione. Odcisk TLS był zgodny z wcześniej zatwierdzonym certyfikatem. Wyniki: `artifacts/rdp-0151-published-host.json`, `artifacts/rdp-0151-published-login.json`.

## Pozostały zakres

Osadzony pulpit działa na Windows. Linux korzysta z osobnego klienta FreeRDP. Konta domenowe/Microsoft, pełna macierz schowka i klawiatury, utrata sieci, wielogodzinne sesje oraz mieszane DPI pozostają do szerszej weryfikacji. Zmiana manifestu nie zastępuje tych testów.

Tło techniczne: [manifest Windows i supportedOS](https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests), [tworzenie natywnego hosta w Avalonia 12.0.4](https://github.com/AvaloniaUI/Avalonia/blob/12.0.4/src/Windows/Avalonia.Win32/Win32NativeControlHost.cs).
