# ServerDesk 0.3.0 — RDP i poświadczenia Linux

Profile zapisują rozdzielczość, pełny ekran, schowek i skalowanie RDP. Import/eksport przenosi te ustawienia bez sekretów, a stare profile dostają bezpieczne wartości domyślne. Obsługiwane są login lokalny, UPN i DOMENA\użytkownik, ze sprawdzaniem sprzecznej domeny. FreeRDP używa NLA i sprawdzania certyfikatów bez opcji ignorowania certyfikatu. Hasło jest wpisywane w konsoli klienta; nie jest argumentem procesu ani polem .rdp.

Opcjonalny pojedynczy folder może być przekazany przez FreeRDP/Linux po włączeniu opcji i wskazaniu istniejącej ścieżki absolutnej. Domyślnie nie przekazuje się folderów ani dysków. mstsc nie obsługuje tego modelu pojedynczego folderu; opcja jest oznaczona jako Linux/FreeRDP. Na Windows importowany profil z włączoną opcją wymaga jej wyłączenia przed startem.

Proces zewnętrznego klienta ma własny cykl życia. Zakładka pokazuje zakończenie i kod procesu, usuwa plik .rdp po zakończeniu, a zamknięcie/reconnect zamyka wyłącznie proces uruchomiony przez tę sesję. Uruchomienie procesu nie oznacza potwierdzonego połączenia RDP. Konsola FreeRDP nie jest zasłaniana informacją o osobnym oknie.

Poświadczenia Linux obsługuje wydzielony SecretServiceCredentialStore. stdout ze secret-tool jest zwracane dokładnie, bez TrimEnd, dzięki czemu zachowuje białe znaki i końce linii. Timeout lub brak magazynu przy odczycie uruchamia ścieżkę wpisania hasła bez zapisu; jawne anulowanie pozostaje anulowaniem. Błąd zapisu/usunięcia jest zgłaszany, nie udaje sukcesu. Usuwanie profilu przy niedostępnym magazynie informuje o możliwym pozostawieniu poświadczenia i pozwala wybrać usunięcie samego profilu.

## Weryfikacja

59 testów xUnit obejmujących konfigurację, domeny, rozdzielczość, opcjonalne foldery, migrację starych profili przez wartości domyślne, zapis/import/eksport RDP, zakończenie i zatrzymywanie własnego procesu oraz sprzątanie konfiguracji po błędzie startu. Renderowanie Avalonia sprawdziło status zakładki po zakończeniu kontrolowanego procesu testowego — nie jest to test protokołu RDP.

Rzeczywisty Secret Service/libsecret 0.21.4 i GNOME Keyring 46.1 testowano w izolowanej sesji D-Bus, z jednorazowym HOME, XDG i niepustym hasłem keyringu. Sprawdzono zapis/odczyt/aktualizację/usunięcie, izolację GUID, Unicode, końce linii, timeout, anulowanie i brak backendu. Pliki keyringu przy istniejącym poświadczeniu nie zawierały jawnego sekretu testowego. Pierwszy test ujawnił niewłaściwe przycinanie oraz nieidempotentne clear — poprawione i ponownie zweryfikowane.

Testowy FreeRDP 2.11.5 z Ubuntu 24.04 zaakceptował argumenty size/sec/from-stdin/dynamic-resolution/clipboard i doszedł do oczekiwanego błędu transportu na nieużywanym porcie loopback. Weryfikację parsera argumentów oddzielono od połączenia.

Brak serwera Windows został potwierdzony przez użytkownika. Prawdziwe RDP, AD, certyfikaty, schowek, transfer folderu i obraz pulpitu pozostają niewykonane. Wbudowany framebuffer FreeRDP i KWallet pozostają do realizacji/weryfikacji. Pełne MVP nie jest zakończone.

Źródła integracji: oficjalny [secret-tool.c](https://github.com/GNOME/libsecret/blob/main/tool/secret-tool.c) (różne zachowanie stdout dla pipe i TTY), pomoc z zainstalowanego testowego FreeRDP (`.tools/freerdp-help.txt`). Testowe binaria GNOME/FreeRDP nie są częścią dystrybucji aplikacji.

Gotowe paczki 0.3.0 uruchomiono na Windows 11 oraz Ubuntu 24.04/WSLg: proces Windows odpowiadał i miał okno, Linux utworzył okno ServerDesk i pozostał aktywny. ZIP sprawdzono pod kątem wykluczenia danych developerskich i kluczy testowych; sumy SHA-256 paczek znajdują się w artifacts/SHA256SUMS.txt.
