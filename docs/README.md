# Dokumentacja / Documentation

Bieżące wydanie: [0.43.0 Public Beta](releases/release-0.43.0.md) — spójność profili i haseł, równoległy zapis pluginów, schemat 4 z kopią przed migracją i odzyskiwanie SSH/SFTP po ciszy na łączu. Obejmuje też reset fabryczny i dokładniejsze błędy SSH. [Weryfikacja i ograniczenia](reports/release-verification-0.43.0.md).

| Temat / Topic | Dokument |
|---|---|
| Instalacja i pierwsze połączenie | [Instrukcja PL](getting-started.md), [English guide](getting-started.en.md) |
| Obsługa aplikacji | [Podręcznik](user-manual.md), [Network Tools](network-tools.md) |
| Ściągawka i własne polecenia | [Commands: obsługa, bezpieczeństwo, architektura i rozszerzanie](commands.md) |
| Ograniczenia i prywatność | [Known issues](known-issues.md), [Privacy](privacy.md), [Security policy](../SECURITY.md) |
| Kopie danych i odtwarzanie | [Kopie bazy danych / Database backups](database-backups.md) |
| Architektura i rozwój | [Architecture](architecture.md), [Development and tests](development.md), [Contributing](../CONTRIBUTING.md) |
| Repozytorium i GitHub | [Git workflow and first upload](github.md) |
| Pliki robocze i miejsce na dysku | [Artifact retention and cleanup](artifacts.md) |
| Wydania | [Release procedure](releasing.md), [Release notes](releases/README.md), [Verification reports](reports/README.md) |
| Przygotowanie wersji stabilnej | [Kolejność prac i warunki gotowości 1.0](stable-release-plan.md), [Checklista konkretnego RC](rc-checklist.md) |
| Zakres projektu | [Droga do 1.0](stable-release-plan.md), [Stan MVP](mvp-status.md), [Roadmap](roadmap.md), [UI](ui-redesign.md), [Icons](icons.md) |
| RDP i zależności | [RDP integration](rdp-integration.md), [RDP matrix](rdp-test-matrix.md), [Dependencies](dependencies.md) |
| Systemy, monitory i dostępność | [Macierz Windows/Linux, Kali/KDE/Wayland i checklista akceptacji](platform-support.md) |

`releases/` zawiera opis zmian poszczególnych wersji, a `reports/` — historyczne wyniki testów i audytów. Bieżące instrukcje są bezpośrednio w `docs/`. Starsze raporty dokumentują stan w dniu testu; nie potwierdzają automatycznie obecnego działania.

Plugins: [enabling built-in modules and extension contract](plugins.md).
