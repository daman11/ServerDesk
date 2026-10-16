param([switch]$Test)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $dotnet=if(Test-Path .tools/dotnet/dotnet.exe){(Resolve-Path .tools/dotnet/dotnet.exe).Path}else{'dotnet'}
    if(-not $env:DOTNET_CLI_HOME){$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/cli'}
    if(-not $env:NUGET_PACKAGES){$env:NUGET_PACKAGES=Join-Path $repo '.tools/packages'}
    & $dotnet build ServerDesk.sln -c Release -m:1
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    if($Test){ & $dotnet test tests/ServerDesk.Tests -c Release --no-build --logger 'trx;LogFileName=unit.trx' --results-directory artifacts/test-results; if($LASTEXITCODE -ne 0){throw 'Tests failed'} }
} finally {Pop-Location}
