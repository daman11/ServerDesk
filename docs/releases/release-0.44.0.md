# ServerDesk 0.44.0 — Public Beta

Wydanie zbiera usprawnienia codziennej pracy od 0.43.0. Status pozostaje Public Beta; nie deklarujemy zakończenia pełnej akceptacji 1.0.

- **Pomoc offline:** 14 tematów PL/EN, wyszukiwanie całej treści, kopiowanie, wyraźne sekcje i skróty w osobnych wierszach. Otwieranie z menu, bocznej nawigacji i F1 poza wejściem zdalnej sesji; dokowanie bez tworzenia zapisanego połączenia.
- **SFTP:** kopiowanie nazw, ścieżek i bieżącego katalogu jako tekst; Ctrl+Shift+C, filtrowanie Ctrl+F i powrót Escape. Pełne szczegóły transferu z kopiowaniem, krótsze karty oraz czyszczenie udanych lub zakończonych zadań bez usuwania plików i aktywnych operacji.
- **Interfejs:** menu kontekstowe nagłówka i pustego obszaru Połączeń, poprawki tworzenia profilu w folderze, wyrównane ikony ekranu startowego i spójne kolory akcentu.
- **Diagnostyka i bezpieczeństwo:** identyfikatory błędów, ograniczenie równoczesnych okien błędu, bezpieczne stosy wyjątków, kontekst wersji/procesu oraz rotacja logów dziennie lub przy 4 MiB, do 14 plików. Maskowanie odbitych sekretów HTTP i poprawki kopii/resetu w długich ścieżkach Windows.
- **RDP Windows:** nowe profile domyślnie w ServerDesk; jawnie zapisane preferencje zewnętrzne pozostają zachowane. Lokalny zrzut na zdalny pulpit przygotowuje schowek i wysyła jedno wklejenie do wskazanej sesji; zdalny Windows obsługuje konflikty i zakończenie kopiowania.
- **Eksperymentalne Pliki z RDP:** zdalne Ctrl+C → podgląd listy → lokalny uchwyt przeciągania. Lokalne testy opisów i strumieni nie potwierdzają rzeczywistego zrzutu do Eksploratora; próba Shell zakończyła się timeoutem. Alternatywą jest Ctrl+V w lokalnym Eksploratorze. Ciągły gest rozpoczynany w zdalnym Eksploratorze nie jest wdrożony; pomocnik nie jest dołączony.

## Aktualizacja i dane

Schemat bazy pozostaje **4**, zgodny z 0.43.0. Przy aktualizacji ze starszych wydań obowiązuje sprawdzona kopia przed migracją oraz blokada otwierania bazy przez klienty do 0.42.0. Zamknij sesje przed aktualizacją. Setup zachowuje dane użytkownika; ZIP/TAR rozpakuj do nowego katalogu. [Kopie i odtwarzanie](../database-backups.md).

Windows x64: Setup EXE i portable ZIP. Linux x64: DEB i TAR.GZ. MIT, samodzielne paczki .NET, aktualizacja ręczna. Wydanie nie ma zaufanego podpisu wydawcy Windows; sumy SHA-256 potwierdzają integralność, nie tożsamość wydawcy.

Pełna macierz RDP, fizyczne AltGr/IME w ServerDesk, mieszane DPI, awarie rzeczywistej sieci, sprzęt Serial, wszystkie dystrybucje i długie sesje pozostają otwarte. VMware nie jest uruchamiany w tym etapie zgodnie z decyzją maintenera.

[Weryfikacja](../reports/release-verification-0.44.0.md) · [Znane ograniczenia](../known-issues.md) · [Plan 1.0](../stable-release-plan.md) · [Instrukcja PL](../getting-started.md) · [English guide](../getting-started.en.md)
