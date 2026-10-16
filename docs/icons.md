# Ikony ServerDesk

Interfejs używa lokalnego podzbioru 68 ikon Lucide 1.55.0 z commitu `70562c1ee1c4fdcf736fe97bc893fb8511927934`. Oryginalne SVG znajdują się w `src/ServerDesk.App/Assets/Icons`; źródło i skróty SHA-256 w `third-party/lucide/manifest.json`. Pełna licencja ISC oraz wymagane notice MIT dla ikon pochodzących z Feather znajdują się w `third-party/lucide/LICENSE` i paczkach dystrybucyjnych. [Upstream](https://github.com/lucide-icons/lucide), [licencja](https://lucide.dev/license).

DeskIcon rysuje geometrię na siatce 24×24, z obrysem 2 jednostki i zaokrąglonymi końcami/łączeniami. Geometrie są buforowane. Podstawowe rozmiary to 16 px w akcjach/wierszach oraz 20–24 px w nawigacji i podglądzie. Kolor wynika z Foreground kontrolki lub dynamicznego zasobu motywu, więc ikona reaguje na hover, aktywację i disabled. Renderer nie korzysta z czcionki ikon ani pobierania z sieci. Akceptuje wyłącznie osadzone SVG i ograniczony zestaw prymitywów; wyklucza DTD, resolver oraz transformacje.

Przyciski z samą ikoną mają tooltip i nazwę dostępności. Ikony akcji są także używane w menu i przyciskach z tekstem. Stan sesji otrzymuje osobny kształt oraz kolor: łączenie, połączenie, zamknięcie, błąd i zewnętrzny klient RDP. Uruchomienie klienta RDP nie oznacza potwierdzonego połączenia.

Profile mają pole `IconId`. Edytor zawiera 15 wyborów: automatyczny oraz 14 ról serwera. Wybór trafia do SQLite i eksportu JSON, przechodzi przez import i duplikowanie. Stary profil bez pola otrzymuje `auto`; nieznany poprawny identyfikator z nowszego eksportu jest zachowywany i wyświetlany jako ikona protokołu. Błędne identyfikatory, ścieżki i adresy są odrzucane. Zmiana ikony aktualizuje otwarte zakładki, a ponowne połączenie nie nadpisuje edycji starszym snapshotem profilu.

Od 0.7.0 można importować własne PNG/JPEG/WebP. Źródło do 4 MiB, 4096 px na bok i 16 mln pikseli jest dekodowane w tle i normalizowane do statycznego PNG 128×128 z zachowaniem proporcji i przezroczystości. Pole `CustomIconPng` zawiera base64 tego PNG, bez ścieżki źródła oraz metadanych zdjęcia. Ikona podróżuje z eksportem JSON (limit konfiguracji 32 MiB). Walidacja sprawdza limit 96 KiB, nagłówek, CRC/chunki PNG i ograniczone rozpakowane wiersze. Duże lub uszkodzone dane są odrzucane przed zapisem/importem. ProfileVisual zwalnia bitmapę po odłączeniu kontrolki i przy zmianie grafiki.

Własna grafika ma pierwszeństwo przed IconId. Jej usunięcie przywraca ikonę z listy. Kolor `#RRGGBB` jest znacznikiem drzewa i górną linią zakładki; pusty kolor usuwa znacznik. Tekst i ikona stanu zachowują kolory motywu. Edycja prezentacji działa w panelach i odpiętych oknach. Import własnych SVG i animacji nie jest obsługiwany.

SFTP rozróżnia katalogi, dowiązania, kod, konfiguracje, JSON, klucze, tekst, archiwa i obrazy. Klasyfikacja korzysta z nazwy pliku i metadanych; nie odczytuje zawartości.

Znak aplikacji jest własnym rysunkiem ServerDesk: serwer i terminal na granatowym tle. Wersja wektorowa znajduje się w `Assets/Brand/serverdesk.svg`, PNG służy oknom Avalonia, a ICO zawiera 16/24/32/48/64/128/256 px dla Windows. Linux DEB instaluje SVG w hicolor. Źródłem generowania PNG/ICO jest BrandMark oraz `tools/ServerDesk.UiSmoke/BrandAssets.cs`.

Dodając ikonę, zachowaj oryginalny plik z tego samego commitu, wpis w manifeście oraz notice. Dodanie nowej roli serwera wymaga wpisu ProfileIcons i polskiej etykiety. Uruchomienie UiSmoke sprawdza skróty osadzonych assetów, geometrię, podgląd, zapis oraz dark/light w 16/24 px.
