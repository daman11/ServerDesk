# ServerDesk 0.10.1 — ładowanie i zakładki

Odtworzono problem ze zrzutu użytkownika. Fluent nadawał indeterminate ProgressBar minimalną szerokość 200 px w kolumnie 44 px. Kontrolka była wycentrowana z ujemną pozycją X (-78), a brak przycinania pozwalał rysować ją poza sesją i na tekście.

Poziomy pasek został zastąpiony małym, obracającym się wskaźnikiem 16×16 obok komunikatu operacji. Wiersz ma stałą minimalną wysokość, przycina zawartość i skraca długi tekst z pełnym opisem w tooltipie. Sesja również przycina swoje elementy. Animacja pauzuje, gdy kontrolka nie jest efektywnie widoczna; znika po zakończeniu operacji. Ostrzeżenia używają nieruchomej ikony.

Usunięto linię na całą szerokość panelu, podkreślenie Fluent pod aktywną kartą oraz kolorową linię nad kartą. Aktywna karta w skupionej grupie jest wyróżniona neutralnym tłem i pogrubioną nazwą. W split view zmiana fokusu aktualizuje oznaczenie właściwej grupy. Kolor serwera zachowano jako mały znacznik przy ikonie; edycja i usunięcie koloru aktualizują go na żywo. Terminal/SFTP/Monitoring używają subtelnego tła wyboru i pogrubienia, bez podkreślenia. Fokus klawiatury nadal otrzymuje obrys.

Weryfikacja: 120/120 xUnit, kompilacja bez błędów/ostrzeżeń i pełne UiSmoke z rzeczywistym SSH/SFTP/monitoringiem, reconnect i ciągłością terminala po dokowaniu. Wyniki: `artifacts/ui-bars-full-verification.txt`; obrazy: `artifacts/ui-bars-final`.

Osobny scenariusz regresji sprawdza kolejne klatki animacji, brak wyjścia wskaźnika poza sesję i nakładania na tekst, brak podkreślenia, fokus grupy oraz edycję/usunięcie znacznika koloru. Obejmuje Midnight/Dark/Light, split przy 900 px i renderowanie 200%. Reprodukcja: UiSmoke z `--session-chrome`; wynik: `artifacts/ui-bars-regression.txt`; obrazy: `artifacts/ui-bars-verified`. To kontrolowane oczekiwanie na poświadczenia w narzędziu testowym, bez połączenia do adresu ze zrzutu użytkownika.

Uruchomienia gotowych paczek: `artifacts/release-0.10.1-windows-smoke.txt`, `artifacts/release-0.10.1-linux-smoke.txt`. Zawartość i wersje: `artifacts/release-0.10.1-package-check.txt`. RDP i pozostałe obszary MVP: [roadmap.md](roadmap.md).
