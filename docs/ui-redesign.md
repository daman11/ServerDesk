# Audyt i redesign ServerDesk

Interfejs 0.3 używa programatycznych widoków C#, domyślnych kontrolek Fluent i dwóch lokalnych kolorów. Główne problemy: toolbar pełen równorzędnych akcji, płaska wielowierszowa lista zamiast drzewa, brak przycisku zamknięcia w zakładce, niejednolita typografia i ikony znakowe, stałe kolory terminala i monitoringu, ustawienia jako seria promptów. SFTP wyświetla zlepione kolumny, a nie wyrównaną tabelę. Zapis szerokości panelu istnieje, ale brakuje ukrywania i podziału sekcji.

Kierunek: kompaktowe środowisko administratora, activity bar 48 px, drzewo połączeń, przewijane zakładki, spokojne powierzchnie i cienki pasek statusu. Logika transmisji i przechowywanie profili pozostają istniejące. Nowe zasoby kolorystyczne i geometria ikon są wspólne dla całej aplikacji.

Implementacja i wyniki weryfikacji: [wydanie 0.4.0](release-0.4.0.md). Zrzuty przed i po przechowuje katalog artifacts; narzędzie tools/ServerDesk.UiSmoke odtwarza scenariusze.
