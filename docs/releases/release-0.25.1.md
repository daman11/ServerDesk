# ServerDesk 0.25.1 — poprawki ergonomii i stabilności

Wydanie dopracowuje istniejące funkcje i wspólne elementy interfejsu. Nie dodaje zależności ani nowych protokołów.

- **Ustawienia:** poniżej 820 px boczne kategorie używają ikon z nazwami dostępnymi dla czytnika ekranu i w podpowiedzi. Przy większej szerokości wracają pełne etykiety. Formularze i przyciski są zablokowane podczas zapisu/stosowania; okna nie można wtedy zamknąć w połowie operacji.
- **Dialogi:** przewijana treść i osobne, stale dostępne akcje. Długie komunikaty można zaznaczyć i skopiować. Escape zamyka okno informacyjne. Rozmiary są ograniczane do obszaru roboczego monitora właściciela, z uwzględnieniem jego skali; dotyczy także ustawień, edytora serwera i usuwania plików.
- **Nawigacja:** Network Tools na ekranie startowym i dziewięć bezpośrednich akcji w palecie poleceń. Paleta obsługuje kilka słów, informuje o braku wyników, zachowuje fokus wyszukiwania przy ↑/↓ i wykonuje zaznaczenie przez Enter.
- **Cele narzędzi:** wybór zapisanego hosta WoL uzupełnia adres wysyłki IPv4, zachowując MAC. Nazwa DNS nie jest zgadywana jako MAC/broadcast. W kalkulatorze IP otrzymuje /32 lub /128; HTTP poprawnie obejmuje IPv6 nawiasami.
- **Wyniki:** po zaznaczeniu wiersza rozwiń „Szczegóły zaznaczonego wyniku”, aby przeczytać i skopiować całą wartość, także długie TXT/OID/SAN. Są osobne komunikaty przed startem, przy oczekiwaniu i braku wyników. Parametry operacji sieciowych można edytować po Stop lub zakończeniu; automatyczny kalkulator nadal reaguje na wpisywanie.
- **Karty i status:** aktualne podpowiedzi celu, wspólne ustawienie szerokości nazwy, pasek statusu aktywnego narzędzia i nazwy HTTP ograniczone do origin bez loginu/ścieżki/query. Zamknięcie aplikacji pyta także o zatrzymanie trwających narzędzi.

Instalacja jest aktualizacją publicznej bety MIT. Poprzednie wersje w `artifacts/releases` są zachowane. Wersja nie jest podpisana i nie ma automatycznej aktualizacji. Zakres platform i protokołów pozostaje opisany w [known-issues.md](../known-issues.md). Nie oznacza to zakończenia całego MVP ani pełnej macierzy sprzętowej.
