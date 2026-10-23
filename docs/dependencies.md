# Zależności

Przed dodaniem bibliotek sprawdzono opublikowane pakiety NuGet i źródła producentów. Restore i kompilacja zweryfikowały rzeczywistą dostępność oraz zgodność .NET 10; testy runtime opisano oddzielnie. Wersje są przypięte, a `packages.lock.json` umożliwia odtwarzalny restore po wygenerowaniu.

| Biblioteka | Wersja | Licencja | Zakres |
|---|---|---|---|
| Avalonia Desktop / Fluent / Inter | 12.0.4 | MIT (licencje czcionek należy zachować) | Windows/Linux UI |
| SkiaSharp | 3.119.4 | MIT (natywne Skia ma własne notices) | dekodowanie i normalizacja grafik; istniejący backend Avalonia |
| Iciclecreek.Avalonia.Terminal | 4.0.8 | MIT | xterm na Avalonia 12/.NET10 |
| Porta.Pty | 2.2.3 | MIT | PTY adapter / procesy RDP Linux |
| SSH.NET | 2026.0.0 | MIT | SSH/SFTP Windows/Linux |
| MarcusW.VncClient (core) | 1.0.0-alpha4 | MIT | RFB/VNC; własny adapter Avalonia 12, bez Tight/TurboJPEG |
| System.IO.Ports | 10.0.12 | MIT | porty COM i urządzenia szeregowe Linux |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | MIT | interfejs logowania biblioteki VNC; używany NullLoggerFactory |
| Microsoft.Data.Sqlite | 10.0.12 | MIT | SQLite + natywne runtime |
| ProtectedData | 10.0.12 | MIT | DPAPI tylko Windows, branch OS |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | MVVM |
| Microsoft.Extensions.DependencyInjection | 10.0.12 | MIT | DI |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | logi rotowane |
| Lucide SVG (lokalny podzbiór 66 ikon) | 1.55.0 | ISC + MIT dla części pochodzącej z Feather | wektorowe ikony UI; bez pakietu NuGet |
| xUnit / runner / Test SDK | 2.9.3 / 3.1.4 / 17.14.1 | Apache-2.0 / MIT zależnie od pakietu | tylko testy |

Źródła:

- [MarcusW.VncClient](https://github.com/MarcusWichelmann/MarcusW.VncClient), [pakiet 1.0.0-alpha4](https://www.nuget.org/packages/MarcusW.VncClient/1.0.0-alpha4). Wersja alpha i zakres weryfikacji opisane w wydaniu 0.18.0. Pełna licencja lokalnie: `third-party/marcusw-vnc/LICENSE` i w dystrybucji: `MARCUSW-VNC-LICENSE.txt`.
- [System.IO.Ports.SerialPort — dokumentacja .NET 10](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport?view=net-10.0).
- https://www.nuget.org/packages/Avalonia.Desktop/12.0.4
- https://github.com/tomlm/Iciclecreek.Avalonia.Terminal (sprawdzono dokładne AttachConnection i IPtyConnection w źródłach)
- https://github.com/tomlm/Porta.Pty
- https://www.nuget.org/packages/SSH.NET/2026.0.0
- https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12
- https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2
- https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0
- https://github.com/FreeRDP/FreeRDP/wiki/CommandLineInterface
- https://pub.freerdp.com/api/structrdp__gdi.html

Zewnętrzny FreeRDP ma licencję Apache-2.0. Nie jest dystrybuowany z aplikacją. Secret Service należy zapewnić w środowisku użytkownika, kompatybilność KWallet zależy od udostępnienia Secret Service i nie jest założeniem gwarantowanym.

Lucide pochodzi z przypiętego commitu `70562c1ee1c4fdcf736fe97bc893fb8511927934`. Zachowano oryginalne pliki SVG, SHA-256 w `third-party/lucide/manifest.json` oraz pełny upstream LICENSE. Paczki zawierają `LUCIDE-LICENSE.txt` i `LUCIDE-MANIFEST.json`. Źródło i warunki: [licencja Lucide](https://lucide.dev/license); zasady użycia w aplikacji: [icons.md](icons.md).

Od 0.23.0 każda paczka zawiera `DEPENDENCIES.json`, `THIRD-PARTY-NOTICES.md` i pełne teksty w `licenses/`, także notices natywnych Skia/HarfBuzz/ANGLE, licencję Inter OFL oraz rzeczywistego runtime .NET. Teksty brakujące w NuGet pobrano z repozytoriów upstream; źródła i SHA-256 zapisano w `third-party/licenses/manifest.json`. Generator zatrzymuje pakowanie przy brakującej licencji. Sprawdzono metadane i teksty; nie jest to certyfikacja prawna ani gwarancja kompletności metadanych upstream.

Instalator Windows: [NSIS 3.13](https://nsis.sourceforge.io/Download), kompresja zlib, [warunki upstream](https://nsis.sourceforge.io/Docs/AppendixI.html). Wyłącznie narzędzie pakowania; pełne notices w `third-party/licenses/nsis-COPYING.txt`. Nie dodano zależności runtime aplikacji.


Bitwarden CLI OSS 2026.10.0 jest dołączony jako osobny proces GPL v3, bez komercyjnego poddrzewa. Kod ServerDesk pozostaje MIT. Manifest przypina archiwa i pliki Windows/Linux x64 oraz źródła OSS; paczki zawierają pełne notices i SOURCE.tar.gz. [Pochodzenie i źródła](../third-party/bitwarden/README.md). Komponent nie jest pakietem NuGet; audyt NuGet nie obejmuje zależności w jego binarium. Linux wymaga też libstdc++6 i libgcc-s1.
