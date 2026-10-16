param([Parameter(Mandatory=$true)][string]$Source)
$ErrorActionPreference='Stop'
$sourcePath=(Resolve-Path -LiteralPath $Source).Path
if(-not(Test-Path -LiteralPath (Join-Path $sourcePath 'ServerDesk.App.exe'))){throw 'Source must contain a published ServerDesk release'}
$target=Join-Path $env:LOCALAPPDATA 'Programs/ServerDesk'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $sourcePath '*') -Destination $target -Recurse -Force
$shortcutPath=Join-Path ([Environment]::GetFolderPath('Programs')) 'ServerDesk.lnk'
$shell=New-Object -ComObject WScript.Shell
$shortcut=$shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath=Join-Path $target 'ServerDesk.App.exe'
$shortcut.WorkingDirectory=$target
$shortcut.Save()
Write-Output "Installed ServerDesk to $target"
