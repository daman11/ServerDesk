# Pierwotne MVP — stan po etapie 0.31.1

Ta lista odnosi się do pierwotnej specyfikacji ServerDesk i jej kryteriów gotowości (§21). Nie zalicza dodatkowych Network Tools, VNC ani Serial jako zastępstwa funkcji SSH/SFTP/Server Info/RDP. Historyczny audyt 0.4.0 pozostaje zapisem tamtego stanu.

| Kryterium pierwotnego MVP | Implementacja i potwierdzony zakres | Pozostała weryfikacja |
|---|---|---|
| Uruchomienie Windows/Linux | Natywna aplikacja Avalonia; Windows x64 Setup/ZIP, Linux x64 DEB/TAR. Gotowe EXE i okno WSLg sprawdzane w procesie wydania. | Oddzielne Windows 10, Kubuntu, Fedora, Debian i fizyczne środowiska X11/Wayland. |
| Dodanie, zapis, edycja i foldery serwerów | SQLite, foldery/podfoldery, ulubione, historia, naturalne sortowanie nazw/IP, wyszukiwanie, duplikowanie i Quick Connect. | Sprawdzono import/widok 1000 profili; pozostają większe drzewa, pełna ręczna macierz gestów i wielogodzinne pomiary. |
| Połączenie SSH i interaktywny terminal | **0.28.0:** rzeczywisty OpenSSH z hasłem i kluczem/passphrase; vim/nano z zapisem UTF-8, top/htop/MC, bash/zsh/fish, 28 przypadków inputu i schowek, trzy rozmiary PTY, EOF/reconnect oraz utrata transportu. Escape Windows i kopiowanie bez zaznaczenia poprawione; otwarcie PTY i resize poza UI. Windows i Ubuntu/WSL. | Inne implementacje/wersje SSH, fizyczne układy klawiatury/AltGr/IME, mysz terminalowa i pełna macierz rozszerzonych protokołów klawiatury. |
| Kilka terminali i zakładki | Oddzielne transporty i terminale, karty, dwa panele, osobne okna, zmiana kolejności i przywracanie bez automatycznego logowania. 0.30.0: poprawione zwalnianie źródeł anulowania, pełne sprzątanie także usuniętych kart, reconnect i przenoszenie działającego terminala między oknami. | Wielogodzinne sesje i duża liczba terminali; ograniczone pomiary oraz dokładny zakres opisuje raport 0.30.0. |
| Przeglądanie plików SFTP | Dwa panele, metadane/ikony, ukryte pliki, filtrowanie/sortowanie, zaznaczanie grup, katalogi, rename, move, chmod i trwałe usuwanie z podglądem. | Sprawdzono rzeczywisty katalog 5000 wpisów i anulowanie odczytu; pozostają większe katalogi i serwery inne niż OpenSSH. |
| Upload/download i katalogi | Rzeczywiste transfery i porównanie bajtów Windows/Linux, limit dwóch aktywnych zadań, postęp/prędkość, nadpisywanie za zgodą, cancel/retry i raportowanie pominięć. | 0.29.0: sprawdzone silent loss upload/download, zerwane nadpisanie, zachowanie oryginałów i retry. Pozostają dalsze fazy/WAN i wznawianie; zdalny .part może pozostać. |
| Dowiązania symboliczne | **0.27.0:** rozpoznanie, szczegóły rzeczywistego celu, przejście do katalogu, tworzenie zdalnego dowiązania oraz osobne kopiowanie zawartości wybranego celu po potwierdzeniu. Zwykła rekurencja pomija dowiązania. | Przenoszenie samych obiektów dowiązań między platformami pozostaje poza obsługiwanym trybem; nie jest utożsamiane z kopiowaniem zawartości. |
| Server Info, rzeczywiste CPU/RAM/dyski | Zbieranie przez SSH i standardowe /proc/polecenia bez agenta/sudo; CPU z kolejnych próbek, RAM/swap, dyski, sieć, system, wykres i ograniczanie pracy nieaktywnych sesji. 0.31.0: próbka zachowuje poprawne sekcje przy braku df/ip/lscpu lub niedostępnym CPU/RAM; osobne komunikaty, walidacja liczników i nowy punkt odniesienia po przerwie. | Pozostałe dystrybucje i szersza macierz uprawnień/odmian poleceń; kontrolowane fixture nie zastępuje testu fizycznej dystrybucji. |
| Skonfigurowanie i uruchomienie RDP | Windows: osadzony natywny host i wcześniejsze rzeczywiste logowanie lokalne. Linux: zewnętrzny FreeRDP z jawnym oznaczeniem; jest to dopuszczony w specyfikacji wariant początkowy. | Osadzony framebuffer Linux, AD/Microsoft, pełny schowek/input/utrata sieci i fizyczne mieszane DPI. Test hosta EXE nie oznacza nowego testu logowania. |
| Bezpieczne przechowywanie poświadczeń | Windows DPAPI, Linux Secret Service, opcjonalny zapis, powiązanie z tożsamością profilu; JSON i SQLite bez sekretów. GNOME Keyring zweryfikowano w izolacji. | KWallet/Secret Service w rzeczywistym KDE; inne sesje keyringu. |
| Import/eksport między platformami | Wersjonowany JSON bez haseł i kluczy prywatnych, foldery/ikony/personalizacja, nowe identyfikatory importowanych profili i atomowa walidacja. | Dodatkowe rzeczywiste migracje konfiguracji użytkowników i bardzo duże importy. |
| Ciemny i jasny motyw, wygodny UI | Pięć motywów, czcionka/gęstość/akcent, ikony/grafiki, PL/EN, ustawienia z wyszukiwaniem, responsywność i poprawianie położenia okna poza ekranem. | Czytniki ekranu, zmiany motywu OS w pracy, fizyczne DPI i pozostałe kombinacje sprzętu. |

## Wykonany etap

Etap 0.31.0 przenosi przygotowanie klucza z passphrase oraz pełny odczyt monitoringu poza UI. Server Info zachowuje dostępne sekcje przy brakach narzędzi i praw odczytu, pokazuje niedostępne dane bez fikcyjnego zera oraz czyści stary wykres i parametry. Testy obejmują odczyt rzeczywistych danych przez ograniczony PATH i własne pliki z odmową dostępu, powrót danych oraz PL/EN. Zachowano poprawki cyklu życia i dokowania z 0.30.0. [Zakres](release-0.31.0.md).

## Następne prace nad pierwotnym zakresem

1. Dłuższe sesje, setki profili i większe drzewa SFTP; pomiary responsywności/pamięci i utrata sieci podczas transferów.
2. Kubuntu/KWallet oraz pozostałe docelowe systemy i sprzęt: Windows 10, fizyczny X11/Wayland, klawiatury/IME i mieszane DPI.
3. Rozszerzenie macierzy RDP oraz docelowe osadzanie Linux; obecny fallback FreeRDP pozostaje jawnie opisany.
4. Pozostałe serwery SSH/SFTP, fizyczne gesty/mysz terminalowa i szersza macierz protokołów klawiatury.

Nie ogłaszamy pełnego MVP za zakończone na podstawie samego istnienia kontrolek lub zielonej kompilacji. Wyniki tego etapu i granice testów zapisuje [raport weryfikacji](release-verification-0.31.1.md).

Poprawka 0.31.1 naprawia kodowanie instalatora oraz dodaje przypomnienia w docelowym języku i bezpieczny restart z ustawień/okna głównego. [Zakres poprawki](release-0.31.1.md).
