# ServerDesk 0.25.0 — Network Tools

MVP rozszerzono o dziewięć rzeczywistych narzędzi: Ping, Traceroute/MTR, DNS Lookup, Port Scanner, Wake-on-LAN, HTTP Client, SSL/TLS Inspector, Subnet Calculator i SNMP Browser v2c/v3.

Każde działa bez zapisanych serwerów i aktywnych połączeń. Dostęp: lewy pasek lub Menu → Network Tools; menu kontekstowe serwera przekazuje edytowalny adres. Karty korzystają z istniejącego split view/dokowania, mogą działać równolegle i anulują pracę przy zamknięciu. Serial Console zachowuje dotychczasowe działanie.

Dodano lokalną historię bez sekretów, własne resolvery, ulubione WoL/OID, kolekcje przechowywane przez systemowy magazyn poświadczeń, eksport TXT/CSV/JSON/PEM oraz polski/angielski interfejs. Windows ICMP używa .NET; Linux bez surowych gniazd używa systemowego iputils-ping. DNS: DnsClient 1.8.0 (Apache-2.0); SNMP: SharpSnmpLib 12.5.7 (MIT), SHA-2/AES-128.

Instrukcja, pełny zakres, główne pliki i limity: **docs/network-tools.md**. Testy i granice platform: raport weryfikacji dołączony do paczek. To nadal publiczna beta bez podpisu zaufanego wydawcy i automatycznego aktualizatora. Poprzednie wydania pozostają osobno.
