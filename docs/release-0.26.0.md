# ServerDesk 0.26.0 — wygląd i wygoda pracy

## Ekran startowy

Ekran bez otwartych sesji ma równe kafelki Nowe połączenie, Szybkie połączenie, Network Tools i Paleta poleceń. Przy węższym obszarze przechodzą do jednej kolumny, a zawartość można przewijać. Większa czcionka i szeroki panel boczny nie odcinają dostępu do akcji.

Podsumowanie pokazuje faktyczną liczbę zapisanych profili, ulubionych i używanych protokołów. Do trzech ostatnio używanych połączeń jest dostępnych bezpośrednio na starcie, w kolejności potwierdzonego połączenia. Lista aktualizuje nazwy i usunięte profile. Samo wyświetlenie nie odczytuje haseł ani nie rozpoczyna połączenia; kliknięcie korzysta ze zwykłego procesu logowania. Widok Ostatnie w panelu bocznym zachowuje własny wybór sortowania.

## Wyszukiwanie ustawień

Pole u góry ustawień przeszukuje nazwy opcji, sekcji i kategorii. Wielkość liter i polskie znaki nie blokują wyników: `polaczen` znajduje ustawienia panelu połączeń. Kilka słów zawęża wyniki; wszystkie muszą pasować do danej opcji lub jej kategorii/sekcji. Wyszukiwanie dotyczy nazw opcji, a nie ich aktualnych wartości.

Kategorie bez wyników są ukrywane; pierwszy dostępny wynik jest wybierany automatycznie. Licznik i pusty stan wyjaśniają wynik wyszukiwania. **Ctrl+F** ustawia fokus w wyszukiwarce. **Escape** w niepustym polu czyści filtr; przycisk × robi to samo. Wyczyszczenie przywraca wszystkie kategorie i opisy.

Filtrowanie zachowuje niezapisane zmiany. Zapis sprawdza cały formularz, również ukryte opcje; niepoprawny akcent zostaje ponownie pokazany. Anuluj cofa podgląd wyglądu, tak jak wcześniej. Język nadal zmienia się po restarcie, o czym informuje ustawienie i przypomnienie.

## Panele plików SSH/SFTP

Każdy panel ma osobny nagłówek: **Lokalny komputer** albo **Zdalny · nazwa serwera**. Kolor wskazuje aktywny panel, a przy dostatecznej szerokości pojawia się także tekst Aktywny panel. Kliknięcie nagłówka przenosi fokus na właściwą listę plików, bez usuwania zaznaczenia. Zmiana nazwy profilu aktualizuje podpis otwartej sesji.

Wysokość wierszy i nagłówków uwzględnia rozmiar czcionki oraz wybrane odstępy. Przy wąskim panelu pozostaje widoczna aktywna strona; przełącznik Lokalny/Zdalny nadal wybiera stronę. Niezależne sortowanie, filtry, klikalne nagłówki i operacje plików pozostają dostępne.

Zmiany są dostępne po polsku i angielsku w dotychczasowych motywach. Wydanie pozostaje publiczną betą MIT. Zakres weryfikacji i ograniczenia: [raport](release-verification-0.26.0.md), [known issues](known-issues.md).
