# ServerDesk 0.35.0 — zakres weryfikacji, 2026-10-16

Wydanie obejmuje trzy/cztery panele i preferencje obszaru pracy opisane w [raporcie funkcji](four-panel-workspace-2026-10-16.md). Testy jednostkowe: 535/535; scenariusze UI, zgodności zapisów i czterech rzeczywistych sesji OpenSSH/SFTP korzystają wyłącznie z izolowanych danych. Bez nowych zależności aplikacji i migracji SQLite.

Przed zamrożeniem obowiązują locked restore/Release build, jednostkowe i macierz UI Windows/Linux, regresja poprzednich paneli, advisory NuGet, manifest i natywny host RDP w opublikowanym EXE (także Three/Grid) bez logowania, położenie okna, rzeczywisty restart, teksty Setup, izolowana instalacja/upgrade/uninstall, porównanie payloadów ZIP/DEB/TAR oraz kompilacja/testy źródeł rozpakowanych z ZIP. Rejestracja Apps/Start menu jest pomijana przy istniejącej instalacji użytkownika.

Payloady powstają z czystego commita. EXE ma wersję 0.35.0.0 i SHA tego commita w ProductVersion. Dziesięć plików zestawu obejmuje Windows Setup/ZIP, Linux DEB/TAR, źródła MIT, notatki, weryfikację, metadane, licencję i SHA256SUMS. Końcowe wyniki bramek są dopisywane po wykonaniu do VERIFICATION.md zestawu. Szczegółowe dowody: artifacts/reports/release-0.35.0/. Tag v0.35.0 i wydanie są lokalne; poprzednie paczki pozostają bez zmian.

Nie jest to pełne zamknięcie pierwotnego MVP. Fizyczne drag and drop, Serial/IME/mieszane DPI, szersza macierz systemów i logowania RDP oraz wielogodzinne pomiary wymagają dalszej weryfikacji. Bez podpisu zaufanego wydawcy i automatycznej aktualizacji. [Ograniczenia](../known-issues.md).
