# ServerDesk 0.5.0 — poprawki po audycie wymagań

Naprawiono wyjątek po wyczyszczeniu zdalnej ścieżki SFTP. Breadcrumby obsługują puste i niepełne wejście, ścieżki względne, Unicode i wielokrotne separatory; docelowe otwarcie pustego katalogu daje czytelny błąd. Wprowadzono testy czystej logiki PathNavigation i regresję rzeczywistego widoku Avalonia.

Usunięto ukrywanie błędów wczytywania wyglądu. Odczyt jest anulowalny i serializowany, nieprawidłowości mają komunikat w sesji oraz log typu/stosu bez treści poświadczeń. Ujawniony błąd nakładania palety przed załadowaniem kontrolki został poprawiony: nakładanie ANSI następuje po Loaded i pomija nieaktywną, ukrytą konsolę RDP. Pierwsze uruchomienie inicjalizuje SQLite przed odczytem ustawień.

Czas SSH jest liczony zegarem monotonicznym i udostępniany przy odczycie statusu. Zmiany stanu i czasu są zgłaszane do głównego okna zdarzeniem sesji. Pasek głównego okna otrzymuje aktualną wartość, bez zależności od tego, czy lokalny tekst sesji został właśnie odświeżony. Godziny nie zerują się po pierwszej dobie.

Dodano komunikaty oczekiwania dla połączenia, otwierania SFTP, listowania katalogów i pierwszej próbki monitoringu. Zagnieżdżone operacje mają własne zakresy, więc zakończenie jednej nie ukrywa trwającej operacji. Zwykłe automatyczne próbkowanie po pierwszym odczycie nie zmienia wysokości layoutu.

DesignTokens stanowi wspólne źródło podstawowych wymiarów, odstępów i typografii. Więcej kontrolek korzysta bezpośrednio z dynamicznych zasobów FontBody/Caption/Heading/Title, ControlHeight, ButtonPadding, InputPadding, BorderWidth i odstępów. Dodano stany interakcji pól, przycisków, segmentów sesji, menu, checkboxów, przełączników i scrollbarów. Zastąpiono lokalne przypisanie tła aktywnego przycisku klasą, aby nie blokować stylów hover. Nie oznacza to jeszcze potwierdzenia pełnej dostępności każdego szablonu Fluent/DataGrid.

Uproszczono toolbar terminala: kopiowanie, wklejanie, czcionka i poświadczenia są w Opcje. SFTP ma główne akcje oraz menu Operacje. Wiersze mają responsywne kolumny typu/uprawnień/dat, osobne oznaczenie dowiązania i szczegóły dla ukrytych kolumn. Przenoszenie do katalogu działa lokalnie lub w obrębie tego samego serwera, bez nadpisywania celu. Transfer samych dowiązań pozostaje niedostępny.

Dodano zapisywany wybór sortowania serwerów: nazwa, adres, protokół, ostatnie połączenie. Sekcja Ostatnie zachowuje chronologię. Historia udanych SSH ma paginację i filtr dla konkretnego profilu. Usunięty profil pozostawia wpis historyczny, z którego nie można ponownie połączyć się z nieistniejącą konfiguracją. Uruchomienie zewnętrznego RDP nie jest przedstawiane jako udane logowanie.

Monitoring RAM prezentuje oddzielnie używane, całkowite i dostępne zasoby, a także wykorzystany/całkowity swap, z czytelnymi jednostkami.

## Weryfikacja

69 testów xUnit: istniejące 59 oraz regresje nawigacji ścieżek, sortowanie i stronicowanie historii, filtrowanie oraz usunięte profile. Kompilacja bez ostrzeżeń i błędów.

UiSmoke obejmuje pięć motywów, 150/200% render scaling, małe okna, dynamiczną zmianę wspólnych zasobów w istniejących kontrolkach, zapis sortowania, rzeczywisty odczyt historii SQLite, widoczny błąd backendu ustawień, pierwszy start bez bazy oraz edycję ścieżki SFTP. Rzeczywiste SSH/SFTP/monitoring sprawdzane są na izolowanym serwerze Ubuntu/WSL; wynik uruchomienia znajduje się w artifacts/ui-fixes-verification.txt, zrzuty w artifacts/ui-fixes.

## Pozostałe ograniczenia

Pełne MVP i wszystkie kryteria redesignu nadal nie są zamknięte. Brakuje wbudowanego PoC RDP i rzeczywistych testów RDP, KWallet, innych docelowych systemów, czytników ekranu, natywnej zmiany Follow System oraz mieszanego DPI. Pozostają testy gestów D&D, obszerne katalogi/bazy, długotrwałe sesje i dalsze dopracowanie formularzy. Własne ikony/kolory serwerów, dokowanie/split view i pełne operacje na dowiązaniach nie zostały zrealizowane w tym wydaniu. Opis testów procesu RDP nadal nie jest dowodem połączenia z Windows.

