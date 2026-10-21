# ServerDesk 0.45.1 Public Beta

MIT; Windows x64 i Linux x64. Schemat bazy pozostaje 4.

- Instalator Windows ma osobny krok **Skróty** z opcją **Utwórz skrót na pulpicie**. Wybór jest zapamiętywany przy aktualizacji z programu. Domyślnie nowa instalacja nie dodaje skrótu na pulpicie; menu Start działa jak wcześniej.
- Sprawdzanie aktualizacji przy starcie jest domyślnie włączone, z zachowaniem wcześniej zapisanego wyłączenia. Nie blokuje uruchomienia ani sesji przy braku sieci.
- Powiadomienie oferuje **Aktualizuj**, **Pomiń tę wersję** i **Przypomnij przy następnym uruchomieniu**. Pominięcie dotyczy jednego numeru; ręczne sprawdzanie pozostaje dostępne.
- Poprawki pierwszej publikacji: dokładny SDK dla locked restore, obsługa ukrytych plików na Linux, bezpieczne argumenty prerelease, weryfikacja szkiców i poprawne odnośniki dokumentacji w opisie wydania.

**Test z 0.45.0:** w Ustawienia → Bezpieczeństwo włącz sprawdzanie przy starcie, zapisz i uruchom program ponownie albo wybierz Sprawdź aktualizacje. Instalacja z programu wymaga wersji Windows z Setup. Trzy nowe decyzje pojawią się dopiero po zainstalowaniu 0.45.1, gdy będzie dostępne kolejne wydanie. ZIP i Linux aktualizuj ręcznie.

[Weryfikacja](../reports/release-verification-0.45.1.md), [aktualizacje i model zaufania](../updates.md), [znane ograniczenia](../known-issues.md).

Wydanie pozostaje betą bez Authenticode i osobno podpisanego manifestu. Dotychczasowe ograniczenia RDP, fizycznych gestów, DPI i natywnej akceptacji pozostają otwarte. Aktualizator sprawdza SHA-256 i rozmiar z publicznego API GitHuba; nie wysyła profili, haseł ani logów.
