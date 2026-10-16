# ServerDesk 0.24.0 — operacje na plikach

Kolejny etap MVP po publicznej becie 0.23.0: usuwanie zaznaczonych plików oraz niepustych katalogów w obu panelach SFTP. Zamrożone wydanie 0.23.0 pozostaje oddzielnie w `artifacts/releases/0.23.0`.

## Obsługa

Zaznacz pliki/katalogi przez Ctrl/Shift lub Ctrl+A, następnie wybierz **Usuń zaznaczone…** w menu albo naciśnij **Delete** na liście plików. Delete w filtrze, ścieżce i terminalu zachowuje dotychczasowe działanie.

Dialog najpierw przygotowuje podgląd, bez zmieniania plików. Pokazuje komputer/serwer, pełne wybrane ścieżki oraz liczbę plików, katalogów i dowiązań. Obejmuje także ukrytą zawartość katalogów. Usuwanie wymaga kliknięcia **Usuń trwale**; ten przycisk nie jest domyślną akcją Enter. Anulowanie podglądu niczego nie usuwa.

Po zatwierdzeniu operacja ma postęp i możliwość przerwania. Końcowy raport pozostaje widoczny również po przerwaniu lub błędzie. Licznik obejmuje potwierdzone usunięcia. Przy utracie połączenia serwer mógł dokończyć ostatnie żądanie bez potwierdzenia — trzeba odświeżyć listę. Nie ma automatycznego ponawiania usuwania ani możliwości cofnięcia wykonanych operacji.

## Ochrona zakresu

- Zamrożony wybór plików i sesji; zmiana aktywnego panelu/ścieżki nie przekierowuje zatwierdzonej operacji.
- Odrzucenie korzenia systemu, wpisu nadrzędnego, ścieżek z `.`/`..`, duplikatów oraz wpisów spoza drzewa.
- Podgląd do 20 000 wpisów i 64 poziomów. Większe zestawy wymagają podziału na mniejsze grupy; przekroczenie limitu nie rozpoczyna usuwania.
- Sprawdzenie typów i metadanych plików przed pierwszym usunięciem oraz przy wykonywaniu żądań. Zmiana celu na dowiązanie zatrzymuje operację.
- Katalogi usuwane od liści; usuwanie pojedynczego katalogu jest nierekurencyjne. Nowe pliki, które pojawią się po podglądzie, nie są dodawane do planu i mogą zatrzymać usunięcie katalogu nadrzędnego.
- Dowiązanie usuwa się jako wpis, bez odczytywania zawartości celu. Katalog będący dowiązaniem nie jest przeglądany. Dowiązania w ścieżce nadrzędnej są odrzucane; otwórz rzeczywistą lokalizację.
- SFTP: zmiana nazwy/przeniesienie dowiązania działa na jego wpisie. Chmod na dowiązaniu jest odrzucany; uprawnienia należy zmieniać w rzeczywistej lokalizacji.

Przy przeglądzie sprawdzono kod używanej wersji SSH.NET: operacje na ścieżkach wykonują kanonizację, natomiast operacje na `ISftpFile` mogą zachować dokładny wpis. Adapter korzysta z tego drugiego wariantu przy usuwaniu i zmianie nazwy. Źródła: [SftpClient](https://github.com/sshnet/SSH.NET/blob/7b2fd3dbf2c86a80a7b06cea020aa5f821c9902e/src/Renci.SshNet/SftpClient.cs), [SftpFile](https://github.com/sshnet/SSH.NET/blob/7b2fd3dbf2c86a80a7b06cea020aa5f821c9902e/src/Renci.SshNet/Sftp/SftpFile.cs).

## Ograniczenia

To trwałe usuwanie bez kosza i bez transakcyjnego cofania. Sprawdzenie typu/rozmiaru/czasu modyfikacji nie zapewnia izolacji od równoległych zmian innymi programami. Pomiędzy sprawdzeniem a żądaniem system/serwer może zmienić ścieżkę; nie jest to gwarancja odporności na wrogi proces jednocześnie modyfikujący drzewo. SFTP obsługuje zwykłe pliki, katalogi i dowiązania; wpisy specjalne są odrzucane. Zachowano ograniczenia platform, RDP i VNC z poprzedniej bety. Nie dodano zależności runtime.

## English

Batch and recursive deletion in both file panes, with a read-only preview, explicit permanent-delete confirmation, progress/cancel and acknowledged partial results. Hidden descendants are included. Links are unlinked without traversing their targets. The operation is bound to its original session/selection, validates scope and is limited to 20,000 items/64 levels. New files discovered after preview are not added to the approved plan. Rename preserves the link itself; chmod on a link is refused. Deletion is irreversible and is not a filesystem transaction or a guarantee against concurrent hostile filesystem mutation. See the verification report for executed tests and remaining limits.
