# Pluginy ServerDesk

W **Ustawienia → Pluginy** można niezależnie włączyć:

- **Monitoring Linux** — panel monitoringu i metryki na pasku statusu, pobierane przez osobne SSH.
- **Komendy** — Smart Cheat Sheet, własne szablony, ulubione i automatyczne wykrywanie środowiska SSH.

To pluginy wbudowane, dostarczane z aplikacją. Domyślnie oba są włączone, również przy aktualizacji starszych ustawień. Zmiany działają po **Zapisz**, bez restartu. Samo zaznaczenie pola jest podglądem; **Anuluj** zachowuje stan pracy. Domyślne ustawienia włączają oba zarejestrowane dodatki.

Wyłączenie monitoringu zatrzymuje jego pętlę i zamyka dedykowane SSH, usuwa stare metryki i wraca z otwartego panelu do terminala. Zwykły pasek statusu i licznik transferów pozostają dostępne. Wyłączenie komend zamyka panel, anuluje wykrywanie, blokuje dotychczasowe akcje i ukrywa wpisy w menu, pasku bocznym, palecie oraz sesji SSH; Ctrl+Shift+K nie otwiera panelu.

Polecenia już wysłane do terminala działają nadal; plugin nie przerywa pracy powłoki.

Oba przełączniki zachowują terminal SSH, SFTP, ustawienia modułów, własne komendy w magazynie systemowym, ulubione i zapisane profile. Nowe sesje nie uruchamiają wyłączonych dodatków. Ponowne włączenie pozwala używać tych samych danych i istniejącej sesji; panel komend otwiera się na żądanie.

Konfiguracja monitoringu pozostaje w sekcji **Monitoring** także wtedy, gdy plugin jest wyłączony. To pozwala przygotować parametry przed ponownym włączeniem. Język nadal ma oddzielny komunikat o restarcie; przełączniki pluginów go nie wymagają.

## Dodawanie kolejnego wbudowanego pluginu

1. Dodaj trwały ID i opis do `PluginCatalog.BuiltIn` w Core oraz tłumaczenia. ID nie zmienia się przy zmianie nazwy wyświetlanej.
2. Zaimplementuj `IWorkspacePlugin` i zarejestruj go w `WorkspacePlugins`. Katalog ustawień sam utworzy kartę. Nieznane, poprawne ID wyłączonych pluginów są zachowywane, ale niczego nie ładują.
3. Oznacz wszystkie punkty wejścia i akcje kontrolą `IsPluginEnabled`, również po operacjach asynchronicznych. Samo ukrycie przycisku nie wystarcza.
4. Własne operacje dodatku muszą mieć osobny token anulowania połączony z czasem życia sesji. Wyłączenie ma zakończyć tę pracę, bez anulowania terminala i transferów. Zakończone stare operacje nie mogą zmienić nowego panelu ani publikować próbek.
5. Dodaj test przełączania podczas pracy, ponownego włączenia, restartu aplikacji oraz zachowania danych. Zmiana kodu pluginu jest obecnie częścią wydania aplikacji.

Stan przełączników jest zapisywany atomowo z innymi ustawieniami w istniejącej bazie (schema3), jako lista stabilnych ID. `PluginSelection` zachowuje niezmienność i porównywanie wartości ustawień po JSON roundtrip. Zmiany cyklu życia są serializowane; nieudane zastosowanie można ponowić.

Aktualna wersja nie instaluje paczek ani zewnętrznych DLL. Registry i kontrakt dotyczą zaufanego kodu dostarczanego z ServerDesk; nie są sandboxem. Sieciowe narzędzia diagnostyczne pozostają dotychczasowym modułem. [Zakres testów](reports/plugins-2026-10-17.md).
