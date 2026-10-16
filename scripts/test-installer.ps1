param([string]$Installer)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$version=([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
if(-not $Installer){$Installer=Join-Path $repo "artifacts/ServerDesk-$version-win-x64-Setup.exe"}
$testRoot=Join-Path $repo ('artifacts/installer-test-'+[Guid]::NewGuid().ToString('N'))
$target=Join-Path $testRoot 'aplikacja żółć'
$data=Join-Path $testRoot 'isolated-data'
New-Item -ItemType Directory -Force $target,$data | Out-Null
function Invoke-Setup([string]$File,[string]$Arguments) {
    $start=[Diagnostics.ProcessStartInfo]::new($File,$Arguments); $start.UseShellExecute=$false; $start.WindowStyle='Hidden'
    $process=[Diagnostics.Process]::Start($start)
    if(-not $process.WaitForExit(60000)){$process.Kill($true); throw 'Owned installer process timed out'}
    $code=$process.ExitCode; $process.Dispose(); return $code
}
function Start-TestApp {
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $target 'ServerDesk.App.exe')); $start.UseShellExecute=$false; $start.WorkingDirectory=$target; $start.WindowStyle='Hidden'; $start.Environment['SERVERDESK_DATA_HOME']=$data
    $process=[Diagnostics.Process]::Start($start)
    $deadline=[DateTime]::UtcNow.AddSeconds(25)
    while($process.MainWindowHandle -eq 0) {if($process.HasExited -or [DateTime]::UtcNow -gt $deadline){throw 'Installed app did not open'}; Start-Sleep -Milliseconds 100; $process.Refresh()}
    return $process
}
$steps=[Collections.Generic.List[string]]::new()
$app=$null
try {
    if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Fresh install failed'}
    $steps.Add('Fresh silent installation into a path containing spaces and Polish characters')
    $app=Start-TestApp
    if(-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000) -or $app.ExitCode -ne 0){throw 'Installed app did not close cleanly'}
    $app.Dispose(); $app=$null
    $dataFiles=@(Get-ChildItem -LiteralPath $data -Recurse -File)
    if(-not ($dataFiles | Where-Object Extension -eq '.db')){throw 'No isolated application database created'}
    $hashes=@{}; foreach($file in $dataFiles){$hashes[$file.FullName]=(Get-FileHash $file.FullName).Hash}
    $steps.Add('Installed self-contained app starts and shuts down with its own real database')
    $unknown=Join-Path $target 'user-note.txt'; [IO.File]::WriteAllText($unknown,'User-created file must survive uninstall')
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
    $hashes=@{}; foreach($file in (Get-ChildItem $data -Recurse -File)){$hashes[$file.FullName]=(Get-FileHash $file.FullName).Hash}
    if((Invoke-Setup $Installer "/S /NOREGISTER /D=$target") -ne 0){throw 'Upgrade/reinstall failed'}
    foreach($file in $hashes.Keys){if((Get-FileHash -LiteralPath $file).Hash -ne $hashes[$file]){throw 'Setup modified user data'}}
    $steps.Add('Upgrade/reinstall preserves the entire isolated database/settings/log directory and extra install files')
    $source=Join-Path $repo "artifacts/win-x64-$version"
    foreach($file in (Get-ChildItem $source -Recurse -File)){$relative=[IO.Path]::GetRelativePath($source,$file.FullName); if((Get-FileHash (Join-Path $target $relative)).Hash -ne (Get-FileHash $file.FullName).Hash){throw "Installed payload mismatch: $relative"}}
    $steps.Add('Every installed payload byte matches the published package')
    # _?= executes the owned uninstaller in place and allows waiting for actual completion.
    if((Invoke-Setup (Join-Path $target 'Uninstall.exe') "/S _?=$target") -ne 0){throw 'Uninstall failed'}
    foreach($file in $hashes.Keys){if((Get-FileHash -LiteralPath $file).Hash -ne $hashes[$file]){throw 'Uninstaller modified user data'}}
    if(-not (Test-Path -LiteralPath $unknown) -or (Test-Path "$target/ServerDesk.App.exe") -or (Test-Path "$target/ServerDesk.App.dll")){throw 'Uninstaller removed extra files or left app binaries'}
    $steps.Add('Uninstall removes owned application binaries and preserves user data plus unknown files')
    $registry='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ServerDesk'
    $shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'ServerDesk.lnk'
    if(-not (Test-Path -LiteralPath $registry) -and -not (Test-Path -LiteralPath $shortcut)) {
        if((Invoke-Setup $Installer "/S /D=$target") -ne 0){throw 'Registered install failed'}
        $registered=Get-ItemProperty -LiteralPath $registry
        $link=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
        if($registered.InstallLocation -ne $target -or $registered.DisplayVersion -ne $version -or $link.TargetPath -ne (Join-Path $target 'ServerDesk.App.exe')){throw 'Incorrect Apps registration or Start menu shortcut'}
        if((Invoke-Setup (Join-Path $target 'Uninstall.exe') "/S _?=$target") -ne 0){throw 'Registered uninstall failed'}
        if((Test-Path -LiteralPath $registry) -or (Test-Path -LiteralPath $shortcut)){throw 'Owned registration or shortcut remains after uninstall'}
        $steps.Add('Default per-user Apps registration and Start menu shortcut are created and removed correctly')
    } else {$steps.Add('SKIP default registration test: pre-existing user registration/shortcut preserved')}
    [ordered]@{success=$true;installer=$Installer;isolatedRoot=$testRoot;steps=@($steps)} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $testRoot 'result.json') -Encoding utf8
    $steps | ForEach-Object {Write-Output "PASS $_"}
    Write-Output "Report: $testRoot/result.json"
} finally {if($app){if(-not $app.HasExited){$app.Kill($true); $app.WaitForExit()}; $app.Dispose()}}
