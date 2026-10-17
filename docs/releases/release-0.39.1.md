# ServerDesk 0.39.1 — Public Beta

Wydanie skupia się na ochronie danych i stabilności istniejących funkcji. Eksport konfiguracji, Komend i wyników Network Tools zastępuje wybrany plik dopiero po przygotowaniu, zapisie i zamknięciu nowej zawartości. Błąd lub anulowanie zachowuje poprzedni plik. Wymagana jest ścieżka systemu plików; nowe pliki Unix są dostępne do odczytu/zapisu tylko dla właściciela.

Import konfiguracji działa poza wątkiem UI i odrzuca powtórzone pola JSON. Transakcje profili/folderów, spójny obraz eksportu i rollback importu obejmują wcześniejsze poprawki robocze po 0.39.0.

Uszkodzona historia, indeks kolekcji lub zapamiętane wartości Network Tools są zgłaszane bez automatycznego resetu. Kolekcje i urządzenia WoL podlegają walidacji przed zastosowaniem. Awaria opcjonalnego zapisu historii/ustawień nie blokuje diagnostyki ani nie usuwa jej wyników; pojawia się ostrzeżenie.

Ustawienia bezpieczeństwa pokazują 20 najnowszych kopii bazy. Komendy prezentują opis i szablon na kartach, z pełnymi szczegółami po otwarciu. Ujednolicono nagłówki ustawień, ikony i zachowanie w wąskich panelach. Opcja pełnego ekranu RDP wyraźnie dotyczy klienta w osobnym oknie; pulpit osadzony dopasowuje się do panelu.

Bez migracji schema3 i nowych zależności NuGet. Windows x64 Setup/ZIP, Linux x64 DEB/TAR.GZ i źródła ZIP, MIT. Nadal Public Beta bez podpisu Authenticode. Przed aktualizacją zamknij aplikację i zabezpiecz dane. [Weryfikacja i ograniczenia](../reports/release-verification-0.39.1.md), [plan 1.0](../stable-release-plan.md), [znane ograniczenia](../known-issues.md).
