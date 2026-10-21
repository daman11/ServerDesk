param([string]$Installer,[string]$Payload,[string]$PreviousInstaller,[string]$Report,[switch]$SkipRegistration)
$ErrorActionPreference='Stop'
if(-not $IsWindows){throw 'This installer check requires Windows'}
$repo=Split-Path -Parent $PSScriptRoot
$version=([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
if(-not $Installer){$Installer=Join-Path $repo "artifacts/ServerDesk-$version-win-x64-Setup.exe"}
if(-not $Payload){$Payload=Join-Path $repo "artifacts/win-x64-$version"}
$Installer=(Resolve-Path -LiteralPath $Installer).Path; $Payload=(Resolve-Path -LiteralPath $Payload).Path
if([Diagnostics.FileVersionInfo]::GetVersionInfo($Installer).FileVersion -ne $version){throw 'Installer version differs from source version'}
if((Get-Content (Join-Path $Payload 'RELEASE.json') -Raw | ConvertFrom-Json).version -ne $version){throw 'Payload version differs from source version'}
if($PreviousInstaller){
    $PreviousInstaller=(Resolve-Path -LiteralPath $PreviousInstaller).Path
    if([version][Diagnostics.FileVersionInfo]::GetVersionInfo($PreviousInstaller).FileVersion -ge [version]$version){throw 'Previous installer must have an older version'}
}
$payloadHashes=@{}; foreach($file in (Get-ChildItem -LiteralPath $Payload -Recurse -File)){$payloadHashes[[IO.Path]::GetRelativePath($Payload,$file.FullName)]=(Get-FileHash -LiteralPath $file.FullName).Hash}
if(-not $payloadHashes.ContainsKey('ServerDesk.App.exe') -or -not $payloadHashes.ContainsKey('ServerDesk.App.dll')){throw 'Incomplete expected payload'}
$testRoot=Join-Path $repo ('artifacts/installer-test-'+[Guid]::NewGuid().ToString('N'))
$target=Join-Path $testRoot 'aplikacja żółć'
$data=Join-Path $testRoot 'isolated-data'
New-Item -ItemType Directory -Force $target,$data | Out-Null
function Owned-File([string]$Name) {
    $full=[IO.Path]::GetFullPath((Join-Path $target $Name))
    $prefix=[IO.Path]::GetFullPath($testRoot)+[IO.Path]::DirectorySeparatorChar
    if(-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or -not $full.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture file is outside the owned install directory'}
    $entry=Get-Item -LiteralPath $full -Force
    if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Fixture mutation cannot target a link'}
    $parent=$entry.Directory
    while($parent -and $parent.FullName.StartsWith($testRoot,[StringComparison]::OrdinalIgnoreCase)){
        if(($parent.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Fixture mutation cannot traverse a link'}
        $parent=$parent.Parent
    }
    return $full
}
function Assert-Payload {
    foreach($relative in $payloadHashes.Keys){
        $path=Join-Path $target $relative
        if(-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path).Hash -ne $payloadHashes[$relative]){throw "Installed payload mismatch: $relative"}
    }
}
function Assert-Uninstalled {
    foreach($relative in $payloadHashes.Keys){if(Test-Path -LiteralPath (Join-Path $target $relative)){throw "Uninstaller left an owned payload file: $relative"}}
    Assert-Preserved
}
function Capture-Data {
    $snapshot=@{}; foreach($file in (Get-ChildItem -LiteralPath $data -Recurse -File)){$snapshot[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash}; return $snapshot
}
function Assert-Preserved {
    $current=Capture-Data
    if($current.Count -ne $hashes.Count){throw 'Setup changed the set of user-data files'}
    foreach($path in $hashes.Keys){if($current[$path] -ne $hashes[$path]){throw 'Setup changed user data'}}
    if(-not (Test-Path -LiteralPath $unknown) -or (Get-FileHash -LiteralPath $unknown).Hash -ne $unknownHash){throw 'Setup removed or changed an unknown install file'}
}
function Invoke-Setup([string]$File,[string]$Arguments) {
    $start=[Diagnostics.ProcessStartInfo]::new($File,$Arguments); $start.UseShellExecute=$false; $start.WindowStyle='Hidden'
    $process=[Diagnostics.Process]::Start($start)
    try {if(-not $process.WaitForExit(60000)){$process.Kill($true); throw 'Owned installer process timed out'}; return $process.ExitCode}
    finally {$process.Dispose()}
}
function Start-TestApp {
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $target 'ServerDesk.App.exe')); $start.UseShellExecute=$false; $start.WorkingDirectory=$target; $start.WindowStyle='Hidden'; $start.Environment['SERVERDESK_DATA_HOME']=$data
    $process=[Diagnostics.Process]::Start($start)
    $deadline=[DateTime]::UtcNow.AddSeconds(25)
    try {while($process.MainWindowHandle -eq 0) {if($process.HasExited -or [DateTime]::UtcNow -gt $deadline){throw 'Installed app did not open'}; Start-Sleep -Milliseconds 100; $process.Refresh()}}
    catch {if(-not $process.HasExited){$process.Kill($true); $process.WaitForExit()}; $process.Dispose(); throw}
    return $process
}
$steps=[Collections.Generic.List[string]]::new()
$app=$null
try {
    if($PreviousInstaller){
        $currentTarget=$target; $currentData=$data
        $target=Join-Path $testRoot 'starsza wersja żółć'; $data=Join-Path $testRoot 'upgrade-data'; New-Item -ItemType Directory -Force $target,$data | Out-Null
        if((Invoke-Setup $PreviousInstaller "/S /NOREGISTER /D=$target") -ne 0){throw 'Previous-version install failed'}
        $app=Start-TestApp
        if(-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000) -or $app.ExitCode -ne 0){throw 'Previous-version app did not close cleanly'}
        $app.Dispose(); $app=$null
        if(-not (Get-ChildItem -LiteralPath $data -Recurse -File -Filter '*.db')){throw 'Previous version did not create its isolated database'}
        $unknown=Join-Path $target 'fixture-user-note.txt'; [IO.File]::WriteAllText($unknown,'Preserve this file through an older-version upgrade'); $unknownHash=(Get-FileHash -LiteralPath $unknown).Hash; $hashes=Capture-Data
        if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Previous-version upgrade failed'}
        Assert-Payload; Assert-Preserved
        $app=Start-TestApp
        if(-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000) -or $app.ExitCode -ne 0){throw 'Upgraded app did not close cleanly'}
        $app.Dispose(); $app=$null; $hashes=Capture-Data
        if((Invoke-Setup (Join-Path $target 'Uninstall.exe') "/S _?=$target") -ne 0){throw 'Upgraded uninstall failed'}
        Assert-Uninstalled
        $steps.Add('Older-version install/start, upgrade with byte-identical data and unknown file preservation, complete new payload, new start and uninstall')
        $target=$currentTarget; $data=$currentData
    }
    if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Fresh install failed'}
    $steps.Add('Fresh silent installation into a path containing spaces and Polish characters')
    $app=Start-TestApp
    if(-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000) -or $app.ExitCode -ne 0){throw 'Installed app did not close cleanly'}
    $app.Dispose(); $app=$null
    $dataFiles=@(Get-ChildItem -LiteralPath $data -Recurse -File)
    if(-not ($dataFiles | Where-Object Extension -eq '.db')){throw 'No isolated application database created'}
    $hashes=@{}; foreach($file in $dataFiles){$hashes[$file.FullName]=(Get-FileHash $file.FullName).Hash}
    $steps.Add('Installed self-contained app starts and shuts down with its own real database')
    $unknown=Join-Path $target 'fixture-user-note.txt'; [IO.File]::WriteAllText($unknown,'User-created file must survive uninstall'); $unknownHash=(Get-FileHash -LiteralPath $unknown).Hash
    $before=(Get-FileHash "$target/ServerDesk.App.dll").Hash
    $app=Start-TestApp
    $code=Invoke-Setup $Installer "/S /NOREGISTER /D=$target"
    if($code -ne 4 -or $app.HasExited -or (Get-FileHash "$target/ServerDesk.App.dll").Hash -ne $before){throw 'Busy upgrade must refuse without stopping/replacing the app'}
    $steps.Add('Upgrade refuses a running application without killing its sessions')
    $code=Invoke-Setup (Join-Path $target 'Uninstall.exe') "/S _?=$target"
    if($code -ne 4 -or $app.HasExited -or -not (Test-Path "$target/ServerDesk.App.exe")){throw 'Busy uninstall must refuse without stopping/removing the app'}
    $steps.Add('Uninstall refuses a running application without killing its sessions')
    if(-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000)){throw 'Cannot close owned app'}
    $app.Dispose(); $app=$null
    # Capture post-shutdown hashes: startup legitimately changes logs/window state.
    $hashes=Capture-Data
    if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Upgrade/reinstall failed'}
    Assert-Preserved
    $steps.Add('Upgrade/reinstall preserves the entire isolated database/settings/log directory and extra install files')
    Assert-Payload
    $steps.Add('Every installed payload byte matches the published package')
    if((Invoke-Setup $Installer "/S /NOREGISTER /DESKTOPSHORTCUT=1 /D=$target") -ne 0){throw 'Shortcut opt-in installation failed'}
    if(-not ([IO.File]::ReadAllText((Join-Path $target 'install-state.ini')) -match '(?m)^DesktopShortcut=1\r?$')){throw 'Shortcut preference was not saved'}
    if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Shortcut-preserving reinstall failed'}
    if(-not ([IO.File]::ReadAllText((Join-Path $target 'install-state.ini')) -match '(?m)^DesktopShortcut=1\r?$')){throw 'Silent update lost the desktop shortcut preference'}
    if((Invoke-Setup $Installer "/S /NOREGISTER /DESKTOPSHORTCUT=0 /D=$target") -ne 0){throw 'Shortcut opt-out installation failed'}
    if(-not ([IO.File]::ReadAllText((Join-Path $target 'install-state.ini')) -match '(?m)^DesktopShortcut=0\r?$')){throw 'Shortcut preference could not be disabled'}
    Assert-Payload; Assert-Preserved
    $steps.Add('Desktop shortcut opt-in, silent-upgrade preference preservation and opt-out with registration disabled')
    foreach($name in @('ServerDesk.App.dll','ServerDesk.App.runtimeconfig.json')){Remove-Item -LiteralPath (Owned-File $name)}
    if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Repair of missing files failed'}
    Assert-Payload; Assert-Preserved
    $steps.Add('Rerunning Setup repairs missing application DLL/runtime configuration; every payload byte and all user data remain correct')
    foreach($name in @('ServerDesk.Core.dll','ServerDesk.Infrastructure.dll')){[IO.File]::WriteAllBytes((Owned-File $name),[byte[]](1,2,3,4))}
    if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Repair of corrupt files failed'}
    Assert-Payload; Assert-Preserved
    $steps.Add('Rerunning Setup replaces corrupted owned DLLs without changing data or unknown files')
    $uninstallerHash=(Get-FileHash -LiteralPath (Join-Path $target 'Uninstall.exe')).Hash
    Remove-Item -LiteralPath (Owned-File 'Uninstall.exe')
    if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Repair of missing uninstaller failed'}
    Assert-Payload; Assert-Preserved
    if((Get-FileHash -LiteralPath (Join-Path $target 'Uninstall.exe')).Hash -ne $uninstallerHash){throw 'Recovered uninstaller differs from the installer-generated original'}
    $steps.Add('Rerunning Setup recreates a missing uninstaller with identical bytes')
    $app=Start-TestApp
    if(-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000) -or $app.ExitCode -ne 0){throw 'Repaired app did not close cleanly'}
    $app.Dispose(); $app=$null; $hashes=Capture-Data
    $steps.Add('Repaired self-contained application starts and shuts down cleanly')
    # _?= executes the owned uninstaller in place and allows waiting for actual completion.
    if((Invoke-Setup (Join-Path $target 'Uninstall.exe') "/S _?=$target") -ne 0){throw 'Uninstall failed'}
    Assert-Uninstalled
    if(-not (Test-Path -LiteralPath $unknown) -or (Test-Path "$target/ServerDesk.App.exe") -or (Test-Path "$target/ServerDesk.App.dll")){throw 'Uninstaller removed extra files or left app binaries'}
    $steps.Add('Uninstall removes owned application binaries and preserves user data plus unknown files')
    $registry='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ServerDesk'
    $shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'ServerDesk.lnk'
    $desktopShortcut=Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'ServerDesk.lnk'
    if(-not $SkipRegistration -and -not (Test-Path -LiteralPath $registry) -and -not (Test-Path -LiteralPath $shortcut) -and -not (Test-Path -LiteralPath $desktopShortcut)) {
        if((Invoke-Setup $Installer "/S /DESKTOPSHORTCUT=1 /D=$target") -ne 0){throw 'Registered install failed'}
        $registered=Get-ItemProperty -LiteralPath $registry
        $link=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
        if($registered.InstallLocation -ne $target -or $registered.DisplayVersion -ne $version -or $link.TargetPath -ne (Join-Path $target 'ServerDesk.App.exe')){throw 'Incorrect Apps registration or Start menu shortcut'}
        $desktopLink=(New-Object -ComObject WScript.Shell).CreateShortcut($desktopShortcut)
        if($desktopLink.TargetPath -ne (Join-Path $target 'ServerDesk.App.exe')){throw 'Incorrect desktop shortcut'}
        if((Invoke-Setup (Join-Path $target 'Uninstall.exe') "/S _?=$target") -ne 0){throw 'Registered uninstall failed'}
        if((Test-Path -LiteralPath $registry) -or (Test-Path -LiteralPath $shortcut) -or (Test-Path -LiteralPath $desktopShortcut)){throw 'Owned registration or shortcut remains after uninstall'}
        $steps.Add('Default per-user Apps registration and Start menu shortcut are created and removed correctly')
    } else {$steps.Add('SKIP default registration test: explicitly skipped or pre-existing user registration/shortcut preserved')}
    if(-not $Report){$Report=Join-Path $testRoot 'result.json'}
    [ordered]@{success=$true;version=$version;installer=$Installer;installerSha256=(Get-FileHash -LiteralPath $Installer).Hash.ToLowerInvariant();payload=$Payload;payloadFileCount=$payloadHashes.Count;previousInstaller=$PreviousInstaller;registrationTested=($steps -contains 'Default per-user Apps registration and Start menu shortcut are created and removed correctly');isolatedRoot=$testRoot;steps=@($steps)} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Report -Encoding utf8
    $steps | ForEach-Object {if($_.StartsWith('SKIP ')){Write-Output $_}else{Write-Output "PASS $_"}}
    Write-Output "Report: $Report"
} finally {if($app){if(-not $app.HasExited){$app.Kill($true); $app.WaitForExit()}; $app.Dispose()}}
