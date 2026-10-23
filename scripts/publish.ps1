param([ValidateSet('win-x64','linux-x64')][string]$Runtime='win-x64',[switch]$Versioned)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $dotnet=if(Test-Path .tools/dotnet/dotnet.exe){(Resolve-Path .tools/dotnet/dotnet.exe).Path}else{'dotnet'}
    if(-not $env:DOTNET_CLI_HOME){$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/cli'}
    if(-not $env:NUGET_PACKAGES){$env:NUGET_PACKAGES=Join-Path $repo '.tools/packages'}
    $releaseVersion=([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
    $publishDirectory=if($Versioned){"artifacts/$Runtime-$releaseVersion"}else{"artifacts/$Runtime"}
    $archivePath=if($Versioned){"artifacts/ServerDesk-$releaseVersion-$Runtime.zip"}else{"artifacts/ServerDesk-$Runtime.zip"}
    # Publish into a fresh sibling first: a failed build cannot corrupt an existing release.
    $staging=Join-Path $repo ('artifacts/publish-staging-'+[Guid]::NewGuid().ToString('N'))
    & $dotnet publish src/ServerDesk.App -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -p:RestoreLockedMode=true -p:DebugType=None -p:DebugSymbols=false -m:1 -o $staging
    if($LASTEXITCODE -ne 0){throw 'Publish failed'}
    if($Runtime -eq 'win-x64'){
        $updaterStaging=Join-Path $repo ('artifacts/updater-staging-'+[Guid]::NewGuid().ToString('N'))
        & $dotnet publish src/ServerDesk.Updater -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -p:DebugType=None -p:DebugSymbols=false -m:1 -o $updaterStaging
        if($LASTEXITCODE -ne 0){throw 'Update helper publish failed'}
        Copy-Item -LiteralPath "$updaterStaging/ServerDesk.Updater.exe" -Destination $staging
    }
    # Some native NuGet packages copy their symbols even when app symbols are disabled.
    foreach($symbol in (Get-ChildItem -LiteralPath $staging -Recurse -File -Filter '*.pdb')){Remove-Item -LiteralPath $symbol.FullName -Force}
    if($Runtime -eq 'win-x64'){ & ./scripts/test-windows-manifest.ps1 -Executable "$staging/ServerDesk.App.exe" }
    foreach($file in @('README.md','LICENSE','CHANGELOG.md','SECURITY.md','CONTRIBUTING.md','AGENTS.md')){Copy-Item -LiteralPath $file -Destination $staging}
    & ./scripts/prepare-bitwarden.ps1 -Runtime $Runtime -Destination $staging
    # Preserve repository-relative links from the offline guides to component provenance.
    New-Item -ItemType Directory -Force "$staging/third-party/bitwarden" | Out-Null
    foreach($file in @('README.md','manifest.json','LICENSE.txt','LICENSE_GPL.txt')){
        Copy-Item -LiteralPath "third-party/bitwarden/$file" -Destination "$staging/third-party/bitwarden"
    }
    # Keep linked guides and historical reports usable from the offline payload.
    Copy-Item -LiteralPath 'docs' -Destination "$staging/docs" -Recurse
    & ./scripts/generate-notices.ps1 -Output $staging -Runtime $Runtime
    [ordered]@{product='ServerDesk';version=$releaseVersion;channel='public-beta';runtime=$Runtime;selfContained=$true;license='MIT';signed=$false} | ConvertTo-Json | Set-Content "$staging/RELEASE.json" -Encoding utf8
    # The only recursive deletion target is this script's fixed publish output inside artifacts.
    $target=[IO.Path]::GetFullPath((Join-Path $repo $publishDirectory))
    $artifactsRoot=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))+[IO.Path]::DirectorySeparatorChar
    if(-not $target.StartsWith($artifactsRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe publish directory'}
    if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Recurse -Force}
    Move-Item -LiteralPath $staging -Destination $target
    if($Runtime -eq 'win-x64'){ Compress-Archive -Path "$target/*" -DestinationPath $archivePath -Force }
} finally {Pop-Location}
