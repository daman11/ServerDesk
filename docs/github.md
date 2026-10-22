# Git i pierwsza publikacja na GitHub

Repozytorium pracuje na `main`. Tag `v0.31.1` oznacza dokładny import 475 plików źródłowych zamrożonego wydania 0.31.1, porównanych z jego manifestem SHA-256. Porządki repozytorium i CI są osobnym, późniejszym commitem. Nie odtwarzamy fikcyjnych commitów wcześniejszych wersji. Dane autora są zapisane lokalnie w `.git/config`, poza plikami projektu.

## Codzienna praca

```sh
git status
git switch -c codex/short-description
# Wprowadź i sprawdź zmianę.
git diff
git add src tests docs
git diff --cached
git commit -m "feat: describe the resulting behavior"
```

Dodawaj tylko pliki należące do konkretnej zmiany. Dla poprawek używaj `fix:`, dla dokumentacji `docs:`, dla porządków `chore:`. Po utworzeniu zdalnego repozytorium prowadź większe zmiany przez pull request do `main`. Przed przełączeniem gałęzi rozlicz lokalne zmiany; nie używaj `reset --hard` ani czyszczenia ignorowanych plików do rutynowego porządkowania.

## Utworzenie zdalnego repozytorium

Repozytorium **[daman11/ServerDesk](https://github.com/daman11/ServerDesk)** i [wydanie 0.45.0 Public Beta](https://github.com/daman11/ServerDesk/releases/tag/v0.45.0) zostały opublikowane 2026-10-21 na polecenie maintenera. `origin` jest podłączony, a `main` i tag `v0.45.0` są na GitHub. Poniższe ogólne kroki zakładania nie wymagają powtarzania. Dostępny jest [podgląd/skrypt kolejnych publikacji](updates.md).

1. Utwórz puste repozytorium na swoim koncie GitHub. Nie dodawaj na stronie nowego README, licencji ani `.gitignore`, ponieważ istnieją już lokalnie.
2. Skopiuj adres HTTPS albo SSH tego repozytorium i wykonaj poniższe polecenia. Zastąp `URL_REPOZYTORIUM` rzeczywistym adresem. Hasła ani tokenu nie umieszczaj w URL; użyj menedżera poświadczeń Git lub klucza SSH.

```sh
git remote add origin URL_REPOZYTORIUM
git remote -v
git push -u origin main
# Po sprawdzeniu konkretnego lokalnego taga wydania:
git push origin v0.45.0
```

Jeżeli `origin` już istnieje, najpierw sprawdź jego adres zamiast dodawać drugi. Utworzone wcześniej repozytorium z własnymi commitami wymaga uzgodnienia historii; nie zastępuj jej wymuszonym pushem. Samo przygotowanie projektu lokalnie nie publikuje kodu ani paczek.

## CI i ustawienia GitHub

Workflow [CI](../.github/workflows/ci.yml) buduje rozwiązanie, uruchamia testy jednostkowe i headless UI na Windows i Ubuntu. Po locked restore wykonuje również bramkę audytu NuGet oraz testy jej decyzji. Używa dokładnej wersji SDK z `global.json` (bez roll-forward), lockfile NuGet i repozytoryjnego `NuGet.Config`. Zapisuje wyniki TRX, renderowane obrazy UI i podsumowania JSON, bez baz i logów sesji; surowy raport audytu/stderr nie jest automatycznie publikowany jako artifact CI. Akcje są przypięte do pełnych SHA; token ma `contents: read`, a checkout nie zachowuje poświadczeń. Konfiguracja korzysta z [oficjalnych akcji GitHub](https://github.com/actions) i [zaleceń bezpieczeństwa Actions](https://docs.github.com/en/actions/reference/security/secure-use).

Po pierwszym pushu sprawdź wynik zakładki Actions. Lokalny test nie zastępuje wykonania na runnerze GitHub. Następnie ustaw `main` jako domyślną gałąź oraz regułę ochrony wymagającą zielonych kontroli `Build and test (windows-2025)` i `Build and test (ubuntu-24.04)`. W repozytorium zespołowym włącz przegląd pull requestów. Włącz dostępne secret scanning i push protection. Ustal prywatny kanał zgłaszania podatności i uzupełnij [SECURITY.md](../SECURITY.md).

Dependabot przygotowuje propozycje zmian NuGet i GitHub Actions co tydzień. Zmiany zależności wymagają przeglądu, aktualizacji lockfile i testów; nie są automatycznie scalane.

Przed publikacją można powtórzyć skan historii przez `gitleaks git --redact --log-opts="--all" .`. Konfiguracja `.gitleaks.toml` zachowuje domyślne reguły i ma dwa wąskie wyjątki: dokładną publiczną stałą hasła jednorazowego fixture SNMP oraz konkretną linię konstruktora UI błędnie rozpoznaną jako klucz API. Nie wyłącza katalogów ani reguł skanowania. Raporty skanu zapisuj w ignorowanym `artifacts/`.

## Kolejne wydania

Numer aplikacji jest w `Directory.Build.props`. Bieżące wydanie to **0.46.0 Public Beta**; późniejsze commity robocze mogą pozostawać przy tej wersji, a istniejące paczki i tag opisują swój zamrożony commit. Małe zmiany kończą się lokalnym commitem, celowanymi sprawdzeniami i — jeśli dotyczą aplikacji — buildem deweloperskim do testowania z folderu. Nie wymagają bumpa, instalatora ani nowego taga. Zmiany samych narzędzi i dokumentacji nie wymagają nowego EXE.

Pełne testy i lokalne wydanie wykonuj dla większych etapów lub na wyraźne zlecenie wydania, według [procedury](releasing.md). Wtedy zmień wersję, zaktualizuj CHANGELOG i przewodniki, sprawdź paczki z końcowego commita, utwórz nowy anotowany tag i zakończ przez `scripts/check-release-state.ps1 -RequireCurrentRelease`. Do roboczego odczytu stanu używaj skryptu bez tego przełącznika; różnice względem ostatniego wydania są wtedy oczekiwane. Istniejące tagi i paczki pozostają niezmienne. [Checklista RC](rc-checklist.md) rozdziela lokalne dowody od otwartej akceptacji. Publikacja na GitHub wymaga osobnego polecenia użytkownika.

Po weryfikacji konkretnego commita utwórz nowy tag `vMAJOR.MINOR.PATCH` i wypchnij wyłącznie ten tag. Na GitHub utwórz release beta/prerelease z Setup, ZIP, DEB, TAR.GZ, źródłami i SHA-256. Gotowe paczki są załącznikami Releases, a nie plikami w historii Git. Obecne wydania w ignorowanym `artifacts/releases/` pozostają lokalne do chwili oddzielnej publikacji.
