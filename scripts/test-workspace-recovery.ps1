$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $dotnet=if(Test-Path .tools/dotnet/dotnet.exe){(Resolve-Path .tools/dotnet/dotnet.exe).Path}else{'dotnet'}
    $env:DOTNET_CLI_HOME=Join-Path $repo '.tools/cli'
    $env:NUGET_PACKAGES=Join-Path $repo '.tools/packages'
    & $dotnet build tools/ServerDesk.UiSmoke -c Release -m:1
    if($LASTEXITCODE -ne 0){throw 'UI harness build failed'}
    $taskRunRoot=Join-Path $repo ('artifacts/ui-recovery-'+[Guid]::NewGuid().ToString('N'))
    & $dotnet tools/ServerDesk.UiSmoke/bin/Release/net10.0/ServerDesk.UiSmoke.dll $taskRunRoot --workspace-crash-writer | Tee-Object artifacts/workspace-crash-writer.txt
    if($LASTEXITCODE -ne 0){throw 'Workspace automatic-save/crash writer failed'}
    & $dotnet tools/ServerDesk.UiSmoke/bin/Release/net10.0/ServerDesk.UiSmoke.dll $taskRunRoot --workspace-crash-recovery | Tee-Object artifacts/workspace-crash-recovery.txt
    if($LASTEXITCODE -ne 0){throw 'Workspace recovery failed'}
    Write-Output "Recovery screenshots/data: $taskRunRoot"
} finally {Pop-Location}
