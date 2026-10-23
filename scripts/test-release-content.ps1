param([string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$version=([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
$head=git -C $repo rev-parse HEAD
if($LASTEXITCODE -ne 0){throw 'Release verification requires the source Git checkout'}
$windowsVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $repo "artifacts/win-x64-$version/ServerDesk.App.exe"))
if($windowsVersion.FileVersion -ne "$version.0" -or $windowsVersion.ProductVersion -ne "$version+$head"){throw 'Windows payload version/commit differs from current source; rebuild from the final release commit'}
foreach($runtime in @('win-x64','linux-x64')) {
    $directory=Join-Path $repo "artifacts/$runtime-$version"
    $release=Get-Content "$directory/RELEASE.json" -Raw | ConvertFrom-Json
    if($release.version -ne $version -or $release.runtime -ne $runtime -or $release.license -ne 'MIT'){throw 'Wrong payload metadata'}
    foreach($required in @('LICENSE','CHANGELOG.md','SECURITY.md','THIRD-PARTY-NOTICES.md','DEPENDENCIES.json','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt','LUCIDE-LICENSE.txt','MARCUSW-VNC-LICENSE.txt','licenses/inter-LICENSE.txt','licenses/nsis-COPYING.txt','licenses/simple-icons/README.md','licenses/simple-icons/LICENSE.md','licenses/simple-icons/manifest.json','docs/getting-started.md','docs/getting-started.en.md','docs/known-issues.md',"docs/releases/release-$version.md","docs/reports/release-verification-$version.md")){if(-not (Test-Path -LiteralPath "$directory/$required")){throw "Missing $required in $runtime"}}
    foreach($file in (Get-ChildItem (Join-Path $repo 'docs') -File -Recurse)){
        $relative=[IO.Path]::GetRelativePath($repo,$file.FullName)
        $copy=Join-Path $directory $relative
        if(-not (Test-Path -LiteralPath $copy) -or (Get-FileHash -LiteralPath $copy).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw "Missing or stale offline documentation: $relative in $runtime"}
    }
    $inventory=Get-Content "$directory/DEPENDENCIES.json" -Raw | ConvertFrom-Json
    $vault=Get-Content "$repo/third-party/bitwarden/manifest.json" -Raw | ConvertFrom-Json
    $component=Join-Path $directory 'components/bitwarden'
    foreach($file in @('README.md','manifest.json','LICENSE.txt','LICENSE_GPL.txt')){
        if((Get-FileHash "$component/$file").Hash -ne (Get-FileHash "$repo/third-party/bitwarden/$file").Hash){throw "Changed vault metadata: $file"}
        if((Get-FileHash "$directory/third-party/bitwarden/$file").Hash -ne (Get-FileHash "$repo/third-party/bitwarden/$file").Hash){throw "Missing/changed offline component provenance: $file"}
    }
    if((Get-FileHash "$component/$($vault.runtimes.$runtime.file)").Hash -ne $vault.runtimes.$runtime.sha256 -or (Get-FileHash "$component/SOURCE.tar.gz").Hash -ne $vault.sourceSha256){throw 'Changed vault executable/source'}
    $registered=@($inventory.components | Where-Object id -eq 'Bitwarden CLI OSS')
    if($registered.Count -ne 1 -or $registered[0].version -ne $vault.version -or $registered[0].sha256 -ne $vault.runtimes.$runtime.sha256 -or $registered[0].license -ne $vault.license){throw 'Incomplete component inventory'}
    $brands=Get-Content "$directory/licenses/simple-icons/manifest.json" -Raw | ConvertFrom-Json
    foreach($asset in $brands.assets){if((Get-FileHash -LiteralPath "$directory/licenses/simple-icons/icons/$($asset.name).svg").Hash -ne $asset.sha256){throw "Changed brand asset: $($asset.name) in $runtime"}}
    foreach($notice in $brands.notices){if((Get-FileHash -LiteralPath "$directory/licenses/simple-icons/$($notice.file)").Hash -ne $notice.sha256){throw "Changed brand notice: $($notice.file) in $runtime"}}
    foreach($package in $inventory.packages){foreach($notice in $package.notices){if(-not (Test-Path -LiteralPath "$directory/$notice")){throw "Missing notice for $($package.id)"}}}
    foreach($file in (Get-ChildItem $directory -Recurse -File)) {
        $relative=[IO.Path]::GetRelativePath($directory,$file.FullName).Replace('\','/')
        if($relative -match '(?i)(^|/)(\.tools|bin|obj|credentials|logs|\.git)(/|$)|\.(db|sqlite|trx|pem|pfx|key|pdb)$|integration_key|^appsettings\.Development'){throw "Unexpected private/build file: $relative"}
    }
}
$zipPath=Join-Path $repo "artifacts/ServerDesk-$version-win-x64.zip"
$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    foreach($entry in $archive.Entries){
        if($entry.Name -eq ''){continue}
        $file=Join-Path $repo "artifacts/win-x64-$version/$($entry.FullName)"
        if(-not (Test-Path -LiteralPath $file)){throw "Stale ZIP entry: $($entry.FullName)"}
        $stream=$entry.Open(); try{$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
        if($sha -ne (Get-FileHash -LiteralPath $file).Hash){throw "ZIP payload mismatch: $($entry.FullName)"}
    }
    $expected=@(Get-ChildItem (Join-Path $repo "artifacts/win-x64-$version") -Recurse -File).Count
    if(@($archive.Entries | Where-Object Name -ne '').Count -ne $expected){throw 'ZIP omits payload files'}
} finally {$archive.Dispose()}
if($ReleaseDirectory) {
    $release=Get-Content "$ReleaseDirectory/release.json" -Raw | ConvertFrom-Json
    foreach($file in $release.files){$actual=Get-Item (Join-Path $ReleaseDirectory $file.name); if($actual.Length -ne $file.bytes -or (Get-FileHash $actual.FullName).Hash -ne $file.sha256){throw "Release digest mismatch: $($file.name)"}}
    $checksums=@(Get-Content "$ReleaseDirectory/SHA256SUMS.txt" | Where-Object {$_})
    foreach($line in $checksums){if($line -notmatch '^([a-f0-9]{64})  ([^/\\]+)$'){throw 'Malformed checksum line'}; if((Get-FileHash (Join-Path $ReleaseDirectory $Matches[2])).Hash -ne $Matches[1]){throw 'Checksum mismatch'}}
    if($checksums.Count -ne $release.files.Count+1){throw 'Incomplete checksums'}
    $source=[IO.Compression.ZipFile]::OpenRead((Join-Path $ReleaseDirectory "ServerDesk-$version-source.zip"))
    try {
        foreach($entry in $source.Entries){if($entry.FullName -match '(?i)(^|/)(\.tools|artifacts|bin|obj|\.git|credentials|logs)(/|$)|integration_key|\.(db|sqlite|trx|pem|pfx|key|exe|dll|pdb)$'){throw "Private/build file in source: $($entry.FullName)"}}
        $prefix="ServerDesk-$version/"
        foreach($required in @('LICENSE','AGENTS.md','.editorconfig','.gitattributes','NuGet.Config','.github/workflows/ci.yml','docs/README.md','docs/github.md','Directory.Build.props','src/ServerDesk.App/packages.lock.json','src/ServerDesk.Infrastructure/packages.lock.json','tests/ServerDesk.Tests/packages.lock.json','SOURCE-MANIFEST.sha256')){if(-not $source.GetEntry($prefix+$required)){throw "Missing source file: $required"}}
        $reader=[IO.StreamReader]::new($source.GetEntry($prefix+'SOURCE-MANIFEST.sha256').Open())
        try{$manifest=$reader.ReadToEnd()}finally{$reader.Dispose()}
        foreach($line in ($manifest -split '\r?\n' | Where-Object {$_})) {
            if($line -notmatch '^([a-f0-9]{64})  (.+)$'){throw 'Malformed source manifest'}
            $expectedHash=$Matches[1]; $entry=$source.GetEntry($prefix+$Matches[2]); if(-not $entry){throw 'Source manifest file missing'}
            $stream=$entry.Open(); try{$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            if($sha -ne $expectedHash){throw 'Source hash mismatch'}
        }
    } finally {$source.Dispose()}
}
Write-Output 'PASS release metadata, full notice references, archive completeness, private/build-file exclusions and checksums'
