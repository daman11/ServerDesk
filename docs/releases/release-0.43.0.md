# ServerDesk 0.43.0 — Public Beta

Wydanie wzmacnia spójność danych i odzyskiwanie SSH/SFTP po problemach sieciowych.

- Zmiana adresu, portu, loginu lub metody logowania zapisuje nowe powiązanie magazynu haseł razem z profilem. Błąd bazy zachowuje poprzedni profil i hasło; błąd późniejszego sprzątania nie pozwala użyć starego hasła dla nowego celu.
- Usunięcie profilu jest najpierw zatwierdzane w bazie. Jeśli zapis się nie powiedzie, zapisane hasło pozostaje. Niedostępny magazyn po udanym usunięciu powoduje osobny komunikat o niepotwierdzonym sprzątaniu.
- Równoległy zapis Komend odrzuca nieaktualny formularz zamiast nadpisywać nowszą bibliotekę. Indeksy i historia Network Tools są aktualizowane transakcyjnie także między oknami/procesami.
- Cichy zastój transferu SFTP kończy się limitem bezczynności (domyślnie 30 s bez postępu). Krótsza przerwa może minąć bez przerwania zadania. Zachowane są zabezpieczenia oryginałów i ostrzeżenia o możliwych plikach `.part`.
- Po 30 s ciszy SSH sprawdza transport osobnym kanałem tylko do odczytu (`echo`, bez wstawiania do terminala), z limitem odpowiedzi 12 s. Szybka odmowa kanału exec na ograniczonym serwerze nie oznacza awarii sprawnego terminala. Utrata łącza pozwala na jawne ponowne połączenie.
- Reconnect zachowuje panel i katalogi SFTP, filtry, zaznaczenie oraz historię nawigacji. Zadania przerwane utratą SSH są błędami transportu, a nie anulowaniem użytkownika. Wybierz ręcznie **Ponów**; plik jest przesyłany od początku, z ponowną zgodą na nadpisanie.
- Wydanie zawiera też domyślne preferencje i pełny reset fabryczny z kopiami bazy oraz szczegółowe komunikaty błędów SSH rozwijane po 0.42.0.

## Aktualizacja i dane

Pierwszy start migruje bazę do **schematu 4**, po sprawdzonej kopii sprzed migracji. Istniejące profile i hasła nie są przepisywane. Aplikacje do 0.42.0 odrzucają schemat 4, aby nie zignorować nowych powiązań haseł. Import/eksport i duplikowanie nie przenoszą lokalnych powiązań poświadczeń. Kopie SQLite pozostają kopiami metadanych; nie są przenośnym eksportem DPAPI/Secret Service. [Odtwarzanie i ograniczenia](../database-backups.md).

Windows: Setup EXE i ZIP x64. Linux: DEB i TAR.GZ x64. MIT, samodzielne paczki .NET, ręczna aktualizacja, brak zaufanego podpisu wydawcy Windows. Zachowano niezmienione wcześniejsze wydania.

Natywne VM/KDE/KWallet, fizyczne uśpienie i wznowienie, mieszane DPI, sprzęt Serial i wielogodzinny pilotaż pozostają niezaliczone. Próba cichego łącza/symulowanego wieku sesji nie jest fizycznym testem suspend/resume. Status pozostaje Public Beta.

[Weryfikacja](../reports/release-verification-0.43.0.md) · [Plan 1.0](../stable-release-plan.md) · [Instrukcja PL](../getting-started.md) · [English guide](../getting-started.en.md)
