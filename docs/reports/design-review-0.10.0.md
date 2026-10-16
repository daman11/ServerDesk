# Przegląd designu po 0.9.0

Punkt odniesienia: `artifacts/ui-reliability-final/main-dark.png` i `live-responsive-sessions.png`, instrukcja redesignu użytkownika oraz działający kod Avalonia.

Największe problemy tego etapu:

- Przyciski narzędziowe mają ramki i podobną wagę do akcji głównej; wybrany panel i zakładka są jednocześnie mocno niebieskie.
- Działające połączenie jest prezentowane jako wyszarzony, nieaktywny przycisk, przez co trudno odczytać stan.
- Ekran startowy ma przypadkowe wyrównanie i małe przyciski w dużej pustej przestrzeni; skróty nie są powiązane z konkretnymi akcjami.
- Wyszukiwarka nie ma szybkiego czyszczenia, sortowanie konkuruje wizualnie z wyszukiwaniem, nazwy serwerów w zakładkach są wcześnie skracane.

Kierunek: spokojny, płaski interfejs IDE, ikony Lucide, kompaktowe listy i wyraźna hierarchia. Ten etap dotyczy głównego okna, pasków narzędziowych, zakładek i ekranu startowego. Kolejne etapy mogą objąć pełne uporządkowanie formularzy profili, ustawień i tabel SFTP. Nie oznacza to zakończenia wszystkich kryteriów redesignu.
