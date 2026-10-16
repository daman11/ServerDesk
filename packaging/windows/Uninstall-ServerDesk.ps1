$ErrorActionPreference='Stop'
$expected=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/ServerDesk'))
$programRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
if(-not $expected.StartsWith($programRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected uninstall path'}
if(Test-Path -LiteralPath $expected){Remove-Item -LiteralPath $expected -Recurse -Force}
$shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'ServerDesk.lnk'
if(Test-Path -LiteralPath $shortcut){Remove-Item -LiteralPath $shortcut}
Write-Output 'Removed application. User configuration was preserved.'
