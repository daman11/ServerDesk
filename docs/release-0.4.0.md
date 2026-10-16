# ServerDesk 0.4.0 — redesign UI/UX

Nowy układ aplikacji: Activity Bar 48 px, drzewo folderów i połączeń, osobne Ulubione/Ostatnie/Transfery, wyszukiwarka, kompaktowe przewijane zakładki z zamykaniem oraz pasek statusu aktywnej sesji. Sidebar można ukryć, jego szerokość i zwinięte foldery są zapisywane. Menu kontekstowe działa na wskazanym serwerze/folderze; istniejące operacje, import, eksport i przeciąganie zachowano. Zamknięcie zakładki w tle zachowuje dotychczasowy wybór, a zamknięcie aktywnej wybiera sąsiednią.

Wspólny design system (`DesignSystem.cs`) dostarcza dynamiczne tokeny powierzchni, tekstu, akcentów, statusów, typografii, odstępów i kontrolek. Motywy: ServerDesk Dark, Midnight, Graphite, Nord, ServerDesk Light, Follow System. Zmiana jest natychmiastowa; zapis w SQLite przez istniejący magazyn ustawień. Follow System używa ThemeVariant.Default i dynamicznych słowników Dark/Light; automatyczna reakcja zależy od wsparcia platformy Avalonia.

Ikony ServerDesk to własny jednolity zestaw geometrii wektorowych, bez emoji i zależności od czcionek ikon. Powierzchnie oraz focus korzystają z wspólnych zasobów i mechanizmów Fluent. Nie wprowadzono nowej biblioteki stylowania ani zależności tylko dla Windows.

Ustawienia wyglądu, palety, czcionki i monitoringu są w jednym oknie z próbkami kolorów. Uwierzytelnianie SSH łączy hasło/passphrase i opcjonalny zapis poświadczenia w jednym formularzu. Formularz serwera grupuje połączenie, uwierzytelnianie i organizację. Błędy mają czytelny komunikat oraz oznaczenie niepoprawnych pól podstawowych. Okna informacyjne nie pytają o potwierdzanie błędu.

Terminal korzysta z niezależnych palet: ServerDesk Default, VS Code Dark, Nord, One Dark, Solarized Dark. Palety aktualizują kolory ANSI przez OSC w istniejącym emulatorze; zmiana motywu UI nie resetuje terminala ani jego kolorów. Czcionka ma systemowe fallbacki monospace. Zachowano istniejącą transmisję SSH.

SFTP ma wyrównane kolumny nazwy, rozmiaru i czasu, ikony, edytowalną ścieżkę i klikalne segmenty, przyciski katalogu nadrzędnego, podział szerokości lokalny/zdalny i zapis proporcji. Przy małej szerokości selektor Lokalny/Zdalny wybiera widoczny eksplorator, a data znika w wąskich wierszach. Transfery mają indywidualne paski postępu i anulowanie; kolejka nie zajmuje miejsca przed pierwszym transferem. Monitoring zawiera Overview/System, CPU z historią, Memory, Storage i Network. Kolory ostrzeżeń dysku pojawiają się od 85%, krytyczne od 95%.

Skróty: Ctrl+Shift+N, Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+B, Ctrl+W, Ctrl+Shift+P. Gdy fokus jest w terminalu, Ctrl+B i Ctrl+W przechodzą do zdalnej aplikacji; Ctrl+Shift+B / Ctrl+Shift+W sterują ServerDesk. Dostępne są także przyciski zamykania zakładek i ukrywania panelu.

## Weryfikacja i granice

59 testów xUnit przeszło; kompilacja Release bez ostrzeżeń i błędów. Headless/Skia wyrenderował pięć motywów, formularze, palety, rozmiar 900×600 oraz skale 150% i 200%. Sprawdzono filtry, skróty sidebaru, zapis ustawień i niezależność terminala. Obliczono kontrast podstawowego i drugorzędnego tekstu na standardowych powierzchniach i stanach hover/selected: minimum 4,5:1. To kontrola tokenów, nie certyfikacja całej aplikacji WCAG ani audyt czytnika ekranu.

Rzeczywiste SSH w izolowanym Ubuntu/WSL: terminal, UTF-8 i nano, zakończenie shella, ponowne połączenie, korekta błędnej passphrase, SFTP i dane monitoringu. Dwie rzeczywiste sesje w głównym oknie potwierdziły niezależne zakładki, Ctrl+Tab / Ctrl+Shift+Tab, przepuszczanie Ctrl+W do terminala i zamykanie przez Ctrl+Shift+W. Zrzuty przed zmianami: artifacts/ui-before; po zmianach: artifacts/ui-redesign. Profile przykładowe są tworzone wyłącznie przez narzędzie testowe, nie przez aplikację.

RDP pozostaje klientem zewnętrznym. Test cyklu życia używa kontrolowanego procesu i nie potwierdza protokołu RDP; serwer Windows jest obecnie niedostępny. Do dalszych testów: rzeczywista zmiana ustawienia systemowego jasny/ciemny na Windows i różnych środowiskach Linux, czytniki ekranu, obszerne katalogi SFTP/bazy serwerów oraz monitor o rzeczywistym mieszanym DPI. Osadzenie obrazu RDP i KWallet pozostają osobnymi zadaniami.

Dodatkowa regresja sieciowa potwierdziła transfery upload/download z porównaniem zawartości, nazwy Unicode, chmod, rename/delete, odmowę niepotwierdzonego nadpisania, zachowanie oryginalnego pliku po anulowaniu, izolację dwóch sesji, EOF/utratę transportu oraz blokowanie zmienionego klucza hosta.
