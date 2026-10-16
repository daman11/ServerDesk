# ServerDesk 0.30.0 — stabilniejsze sesje i zamykanie aplikacji

Zamknięcie karty anuluje jej operacje i ma jedno wspólne zadanie sprzątania. Czeka ono na zakończenie łączenia, odczytu katalogu, transferów, monitoringu, zegara sesji i zapisu historii, a następnie zwalnia powiązane źródła anulowania. Wyjście z programu uwzględnia również karty usunięte wcześniej z widoku. Ponowne połączenie czeka na stare transporty i transfery, zanim utworzy nowe.

Zamykanie SSH, przeglądarki SFTP i monitoringu odbywa się poza wątkiem interfejsu, z krótkim limitem oczekiwania na zamknięcie kanału. Zakończenie terminala zatrzymuje też zależne połączenia i zadania. Błąd podczas sprzątania transportu nie pozostawia jego zadania zakończenia bez wyniku.

Przenoszenie działającego terminala do osobnego okna opróżnia oczekujące przeliczenia poprzedniego okna, gdy karta jest jeszcze odłączona od drzewa kontrolek. Usuwa to wykryty wyjątek niewłaściwego menedżera układu.

Testy obejmują zwalnianie pamięci po błędnych połączeniach, równoległe sesje, powtarzany reconnect, split/floating/redock oraz zamykanie podczas cichej utraty sieci i aktywnego transferu. [Wyniki i granice](release-verification-0.30.0.md), [stan pierwotnego MVP](mvp-status.md).

Bez nowych zależności, migracji bazy ani zmian formatów poświadczeń. Publiczna beta MIT. Nie wprowadzono automatycznego wznawiania transferów: po utracie sieci zdalny plik tymczasowy może pozostać, a ponowienie kopiuje od początku. Krótki test obciążenia nie zastępuje wielogodzinnej akceptacji na fizycznym sprzęcie.
