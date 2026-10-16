# ServerDesk 0.17.0 — wygodniejsza edycja i przeglądanie serwerów

Edytor serwera ma cztery sekcje: Połączenie, Wygląd, Organizacja i Pulpit RDP. Sekcja RDP pojawia się przy właściwym protokole. Zamiast jednej długiej listy pól są grupy ustawień, podgląd nazwy, adresu, ikony i koloru oraz stale widoczne przyciski zapisu i anulowania. Okno można skalować; zawartość przewija się niezależnie od przycisków. Formularz wskazuje błędy przy polach, przechodzi do pierwszej błędnej sekcji i usuwa komunikat po poprawieniu wartości.

Klucz SSH można wybrać z pliku, a folder — z listy istniejących folderów. Zmiana SSH/RDP dobiera domyślny port, zachowując port niestandardowy. Niewykorzystana robocza wartość rozdzielczości RDP nie blokuje zapisu SSH. Grafika może zostać usunięta lub zmieniona także podczas wpisywania niepoprawnego koloru; zapis nadal wymaga poprawnego koloru. Hasła pozostają poza formularzem profilu.

Panel połączeń ma filtry Wszystkie / SSH / RDP, licznik wyników i liczbę serwerów w folderach. Wyszukiwanie łączy kilka słów z nazwy, adresu, loginu, domeny, portu, tagów, opisu i folderu. Wszystkie słowa muszą pasować; wielkość liter nie ma znaczenia. Foldery pasujących wyników rozwijają się podczas wyszukiwania. Pusta lista wyjaśnia przyczynę i oferuje wyczyszczenie filtrów lub dodanie serwera. Escape czyści zapytanie, zachowując wybrany filtr protokołu.

Po przeładowaniu listy zaznaczenie wskazuje aktualne dane profilu. Sortowanie modelu listy jest zgodne z wybraną kolejnością. Polecenie przeniesienia do folderu zmienia wyłącznie folder, zachowując pozostałe aktualne dane serwera. Opis aplikacji poprawnie rozróżnia osadzony RDP Windows i zewnętrzny FreeRDP Linux.

Weryfikacja: 193/193 testów xUnit; pełna dotychczasowa regresja headless UI; osobny scenariusz `--polish` sprawdzający walidację, zapis/anulowanie, podgląd, zmianę protokołu, filtry i odświeżanie zaznaczenia. Formularz wyrenderowano w jasnym/ciemnym motywie przy 780×720 i 600×520 oraz tekście 13/16. Wąski panel połączeń sprawdzono przy szerokości 220 i tekście 16. Wyniki: `artifacts/polish-0170-unit-checks.txt`, `artifacts/polish-0170-regression-checks.txt`, `artifacts/polish-0170-ui-checks.txt`; zrzuty: `artifacts/ui-0170-polish`.

To wydanie dopracowuje istniejące funkcje. Nie zamyka pozostałej macierzy RDP, sprzętowych testów mieszanego DPI ani testów wydajności dużych zbiorów. Zakres wcześniejszych testów rzeczywistego RDP pozostaje opisany w [rdp-test-matrix.md](../rdp-test-matrix.md). Picker systemowy ma obsługę błędów, ale jego ręcznego wyboru pliku nie zaliczamy do testów headless.
