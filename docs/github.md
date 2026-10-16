# Git i pierwsza publikacja na GitHub

Repozytorium pracuje na `main`. Tag `v0.31.1` oznacza dokładny import 475 plików źródłowych zamrożonego wydania 0.31.1, porównanych z jego manifestem SHA-256. Porządki repozytorium i CI są osobnym, późniejszym commitem. Nie odtwarzamy fikcyjnych commitów wcześniejszych wersji. Dane autora są zapisane lokalnie w `.git/config`, poza plikami projektu.

## Codzienna praca

```sh
git status
git switch -c feat/short-description
# Wprowadź i sprawdź zmianę.
git diff
git add src tests docs
git diff --cached
git commit -m "feat: describe the resulting behavior"
```

Dodawaj tylko pliki należące do konkretnej zmiany. Dla poprawek używaj `fix:`, dla dokumentacji `docs:`, dla porządków `chore:`. Po utworzeniu zdalnego repozytorium prowadź większe zmiany przez pull request do `main`. Przed przełączeniem gałęzi rozlicz lokalne zmiany; nie używaj `reset --hard` ani czyszczenia ignorowanych plików do rutynowego porządkowania.

## Utworzenie zdalnego repozytorium

1. Utwórz puste repozytorium na swoim koncie GitHub. Nie dodawaj na stronie nowego README, licencji ani `.gitignore`, ponieważ istnieją już lokalnie.
2. Skopiuj adres HTTPS albo SSH tego repozytorium i wykonaj poniższe polecenia. Zastąp `URL_REPOZYTORIUM` rzeczywistym adresem. Hasła ani tokenu nie umieszczaj w URL; użyj menedżera poświadczeń Git lub klucza SSH.

```sh
git remote add origin URL_REPOZYTORIUM
git remote -v
git push -u origin main
git push origin v0.31.1 v0.32.0 v0.33.0 v0.34.0 v0.35.0 v0.36.0 v0.36.1
```

Jeżeli `origin` już istnieje, najpierw sprawdź jego adres zamiast dodawać drugi. Utworzone wcześniej repozytorium z własnymi commitami wymaga uzgodnienia historii; nie zastępuj jej wymuszonym pushem. Samo przygotowanie projektu lokalnie nie publikuje kodu ani paczek.

## CI i ustawienia GitHub

Workflow [CI](../.github/workflows/ci.yml) buduje rozwiązanie, uruchamia testy jednostkowe i headless UI na Windows i Ubuntu. Używa SDK z `global.json`, lockfile NuGet i repozytoryjnego `NuGet.Config`. Zapisuje tylko wyniki TRX oraz renderowane obrazy UI, bez baz i logów sesji. Akcje są przypięte do pełnych SHA; token ma `contents: read`, a checkout nie zachowuje poświadczeń. Konfiguracja korzysta z [oficjalnych akcji GitHub](https://github.com/actions) i [zaleceń bezpieczeństwa Actions](https://docs.github.com/en/actions/reference/security/secure-use).

Po pierwszym pushu sprawdź wynik zakładki Actions. Lokalny test nie zastępuje wykonania na runnerze GitHub. Następnie ustaw `main` jako domyślną gałąź oraz regułę ochrony wymagającą zielonych kontroli `Build and test (windows-2025)` i `Build and test (ubuntu-24.04)`. W repozytorium zespołowym włącz przegląd pull requestów. Włącz dostępne secret scanning i push protection. Ustal prywatny kanał zgłaszania podatności i uzupełnij [SECURITY.md](../SECURITY.md).

Dependabot przygotowuje propozycje zmian NuGet i GitHub Actions co tydzień. Zmiany zależności wymagają przeglądu, aktualizacji lockfile i testów; nie są automatycznie scalane.

Przed publikacją można powtórzyć skan historii przez `gitleaks git --redact --log-opts="--all" .`. Konfiguracja `.gitleaks.toml` zachowuje domyślne reguły i ma dwa wąskie wyjątki: dokładną publiczną stałą hasła jednorazowego fixture SNMP oraz konkretną linię konstruktora UI błędnie rozpoznaną jako klucz API. Nie wyłącza katalogów ani reguł skanowania. Raporty skanu zapisuj w ignorowanym `artifacts/`.

## Kolejne wydania

Numer aplikacji jest w `Directory.Build.props`. Bump dotyczy nowego wydania, nie każdego pośredniego commita. Po zakończeniu zamówionego etapu zmian użytkowych przygotuj również lokalne paczki i tag; sam commit nie kończy wydania. Bieżące wydanie to 0.36.1; następne poprawki mogą otrzymać 0.36.2, a większy etap 0.37.0. Postępuj według [procedury wydania](releasing.md), zapisz zmiany w CHANGELOG i dodaj nowe notatki/raport. Przed zakończeniem uruchom `scripts/check-release-state.ps1 -RequireCurrentRelease`. Publikacja na GitHub nadal wymaga polecenia użytkownika.

Po weryfikacji konkretnego commita utwórz nowy tag `vMAJOR.MINOR.PATCH` i wypchnij wyłącznie ten tag. Na GitHub utwórz release beta/prerelease z Setup, ZIP, DEB, TAR.GZ, źródłami i SHA-256. Gotowe paczki są załącznikami Releases, a nie plikami w historii Git. Obecne wydania w ignorowanym `artifacts/releases/` pozostają lokalne do chwili oddzielnej publikacji.
