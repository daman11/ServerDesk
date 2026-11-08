# Zbiorcza edycja tagów / Batch tag editing

Funkcja deweloperska po 0.51.1. W sekcji **Połączenia**, **Ulubione** lub
**Ostatnie** kliknij prawym przyciskiem serwer albo tło listy i wybierz
**Edytuj tagi wielu połączeń**. Menu serwera zaznacza ten serwer na początek;
menu tła rozpoczyna od pustego wyboru.

1. Najpierw ustaw zwykłe filtry listy połączeń. Okno obejmie tylko pasujące
   profile, również ze zwiniętych folderów. Zamknięte sesje nie są wymagane.
2. Zaznacz konkretne połączenia. **Zaznacz widoczne** dodaje wyniki wyszukiwania
   w tym oknie. Zmiana wyszukiwania zachowuje wcześniejszy wybór; licznik
   **Wybrane** obejmuje również pozycje ukryte przez zapytanie. Wyczyść
   wyszukiwanie i włącz **Tylko wybrane**, aby przejrzeć całą partię.
3. Wpisz **Tagi do dodania** i/lub **Tagi do usunięcia**, rozdzielając nazwy
   przecinkami albo średnikami. Dopasowanie jest dokładne i ignoruje wielkość
   liter: usunięcie `prod` nie usuwa `production`. Ten sam tag w obu polach
   blokuje zapis.
4. Sprawdź podgląd **Po zmianie** przy zaznaczonych pozycjach i licznik zmian.
   Puste lub niezmieniające danych operacje mają nieaktywny przycisk **Zapisz**.
   **Anuluj** zamyka okno bez zapisywania.

Zapis dotyczy wyłącznie tagów. Kolory, pozostałe pola profilu, poświadczenia,
historia i otwarte sesje pozostają zachowane. Cała partia jest zapisywana razem;
błąd zapisu lub usunięcie/zmiana tagów jednego z docelowych profili wycofuje
wszystkie zmiany. Przy nieaktualnym podglądzie zamknij okno i otwórz je ponownie.
Po poprawnym zapisie lista i liczniki filtrów zostają odświeżone; połączenie może
zniknąć z bieżącej listy, jeśli nie pasuje już do wybranych tagów.

## English

Development feature after 0.51.1. Right-click a connection or the connection-list
background in Connections, Favorites or Recent and choose **Edit tags for multiple
connections**. A connection menu preselects that connection; the background menu
starts with no selection. Current browser filters limit the scope, including
matching entries in collapsed folders.

Select connections, enter **Tags to add** and/or **Tags to remove**, and review
each **After** preview before saving. Separate names with commas or semicolons.
Matching is exact and case insensitive; using the same tag in both fields blocks
saving. Search retains earlier selections: clear it and use **Selected only** to
review every selected connection. **Select visible** adds the current search
results. Empty/no-op plans cannot be saved; Cancel writes nothing.

The batch changes only tags, preserving colors, other profile fields, credentials,
history and active sessions. Every update commits together. A failed write or a
deleted/retagged target rolls the whole batch back. Reopen the dialog after a stale
preview. After saving, the browser reloads without resetting its filters; retagged
connections can disappear from a filter they no longer match.
