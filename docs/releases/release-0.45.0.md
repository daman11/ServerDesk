# ServerDesk 0.45.0 Public Beta

MIT; Windows x64 i Linux x64. Schemat bazy pozostaje 4.

- Aktualizacje GitHub w Ustawienia → Bezpieczeństwo oraz „O programie”. Opcjonalne sprawdzanie przy starcie jest domyślnie wyłączone; dostępna wersja pokazuje pasek powiadomienia.
- Pobieranie instalatora z postępem/anulowaniem, kontrolą rozmiaru i SHA-256 z API GitHuba. Instalacja Windows Setup tworzy kopię bazy i po potwierdzeniu zamyka sesje. Oddzielny pomocnik czeka na wyjście aplikacji, ponownie weryfikuje wydanie, instaluje i uruchamia program. ZIP/Linux aktualizowane ręcznie. Pierwsze przejście do wersji z aktualizatorem wymaga nowego Setupu.
- Sprawdzanie kopii kontroluje strukturę bazy i zapisane profile bez migracji lub zapisywania do kopii.
- Osadzone RDP Windows odświeża obraz po aktywacji oraz okresowo; przycisk Pulpit może ponowić przerysowanie i parametry ekranu bez nowej sesji.
- Komendy: Poprzednia/Następna, licznik i Alt+strzałki, zachowanie formularza po zmianie ulubionych w tym samym kontekście.
- Repozytorium GitHub `daman11/ServerDesk`, instrukcje i skrypt publikacji ze sprawdzaniem paczek oraz bez domyślnego wysyłania.

[Weryfikacja](../reports/release-verification-0.45.0.md), [aktualizacje i model zaufania](../updates.md), [znane ograniczenia](../known-issues.md).

Instalator nie ma Authenticode. Zaufanie aktualizacji opiera się na HTTPS i zabezpieczeniu konta repozytorium, nie osobnym podpisie manifestu. Publiczny feed będzie działał po publikacji kompletnego wydania. Lokalny test nie jest próbą aktualizacji pomiędzy dwoma opublikowanymi wydaniami. Ochrona Setupu przed zajętymi plikami jest zachowana; kopiowanie nie jest transakcyjne. Dotychczasowe ograniczenia RDP, fizycznych gestów, DPI i akceptacji natywnych systemów pozostają otwarte.
