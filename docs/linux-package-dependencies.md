# Linux DEB: ICU dependencies / Zależności ICU

Paczka 0.51.1 wymieniała tylko `libicu76 | libicu74 | libicu72 | libicu70`.
APT odrzucał instalację na systemie z nowszym ICU, mimo że biblioteka była
dostępna. Nowe metadane dopuszczają ICU 78, 77, 76, 75, 74, 73, 72 lub 70.
ICU pozostaje wymagane; aplikacja zachowuje obsługę kultur PL/EN.

[Microsoft opisuje wybór zainstalowanego ICU przez .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu).
[Debian udostępnia bibliotekę wykonawczą libicu78](https://packages.debian.org/sid/libicu78).
To nie jest deklaracja obsługi każdej dystrybucji ani przyszłej wersji ICU.

## Naprawiona paczka 0.51.1-1

Rewizja Debiana `0.51.1-1` zawiera ten sam program i te same pliki instalowane
co opublikowane 0.51.1. Zmienia jedynie wersję paczki i deklarację zależności.
Po przeniesieniu pliku do Linuxa uruchom w jego katalogu:

```bash
sudo apt install ./serverdesk_0.51.1-1_amd64.deb
```

Nie trzeba odinstalowywać poprzedniej wersji. Rewizja jest nowsza dla dpkg,
natomiast okno „O aplikacji” nadal pokazuje wersję programu 0.51.1.
Jej przygotowanie lokalnie nie zmienia plików wydania GitHub.

## Packaging and verification

The former ICU whitelist rejected a machine whose available provider was ICU 78.
The corrected list accepts the maintained runtime package names above without
disabling globalization or requiring development headers. Recommends such as
FreeRDP are separate from the mandatory ICU dependency.

The packager keeps its existing arguments/default output. Packaging-only repairs
can use a numeric Debian revision and a separate output directory:

```bash
bash scripts/package-deb.sh 0.51.1 /path/to/extracted/opt/serverdesk \
  --revision 1 --output-dir artifacts/fixes/linux-icu-0.51.1-1 --deb-only
python3 scripts/test-deb-dependencies.py --fixture
python3 scripts/test-deb-dependencies.py /path/to/repaired.deb --previous /path/to/original.deb
```

The payload RELEASE.json retains its upstream application version. Frozen
`artifacts/releases/` is forbidden as a direct packaging destination. Never
replace an existing public DEB or alter its checksums under the old filename.

The test uses APT simulation with isolated synthetic installed-package state and
empty source lists. It neither installs packages nor uses the host's installed
ICU to mask a missing dependency. Its `--previous` mode also checks every
installed file's SHA-256, mode and ownership and reproduces the original solver
failure. These checks do not certify GUI startup or a native Kali desktop.
