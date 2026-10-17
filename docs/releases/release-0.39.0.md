# ServerDesk 0.39.0 — Public Beta

**Ustawienia → Pluginy** pozwalają niezależnie włączać Monitoring Linux oraz Komendy (Smart Cheat Sheet i własne szablony). To pluginy wbudowane; domyślnie oba działają również po aktualizacji. Zmiana działa po zapisie bez restartu, a Anuluj zachowuje stan pracy.

Wyłączenie zatrzymuje pracę dodatku, ukrywa jego akcje i zamyka jego panel. Zachowane są terminal SSH, SFTP, profile, ustawienia monitoringu, własne szablony i ulubione. Ponowne włączenie działa na istniejącym połączeniu. Zwykły pasek statusu i licznik transferów są funkcjami rdzenia.

Katalog stabilnych ID i osobny kontrakt cyklu życia stanowią podstawę kolejnych wbudowanych dodatków. Nie dodano loadera zewnętrznych DLL, instalatora pluginów ani publicznego SDK. [Instrukcja i rozszerzanie](../plugins.md).

Ustawienia mają siedem kategorii; pluginy uczestniczą w wyszukiwaniu, domyślnych ustawieniach i PL/EN. Zachowano starszy JSON preferencji i schema3 bazy; bez nowych zależności NuGet.

Paczki Windows x64 Setup/ZIP, Linux x64 DEB/TAR.GZ i źródła ZIP, MIT, nadal Public Beta bez podpisu Authenticode. Przed aktualizacją zamknij aplikację i zabezpiecz dane. [Weryfikacja](../reports/release-verification-0.39.0.md), [ograniczenia](../known-issues.md), [plan 1.0](../stable-release-plan.md).
