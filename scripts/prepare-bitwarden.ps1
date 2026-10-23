param([ValidateSet('win-x64','linux-x64')][string]$Runtime='win-x64',[string]$Destination)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$manifest=Get-Content (Join-Path $repo 'third-party/bitwarden/manifest.json') -Raw | ConvertFrom-Json
$binary=$manifest.runtimes.$Runtime
$cache=Join-Path $repo '.tools/bitwarden-runtime'
New-Item -ItemType Directory -Force $cache | Out-Null
function Fetch([string]$Url,[string]$Sha,[string]$Name) {
    $target=Join-Path $cache $Name
    if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant() -eq $Sha){return $target}}
    $temporary=$target+'.part'
    try {
        # HTTPS publisher URL and SHA-256 are reviewed source metadata, not a latest redirect.
        if(([Uri]$Url).Scheme -ne 'https'){throw 'Only pinned HTTPS inputs are allowed'}
        & curl --fail --location --proto '=https' --proto-redir '=https' --connect-timeout 15 --max-time 240 --silent --show-error --output $temporary $Url
        if($LASTEXITCODE -ne 0){throw 'Bitwarden component download failed'}
        if((Get-FileHash -LiteralPath $temporary).Hash.ToLowerInvariant() -ne $Sha){throw 'Bitwarden component hash mismatch'}
        Move-Item -LiteralPath $temporary -Destination $target -Force
    } finally {if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
    return $target
}
$archive=Fetch $binary.archiveUrl $binary.archiveSha256 ([IO.Path]::GetFileName(([Uri]$binary.archiveUrl).AbsolutePath))
$upstreamSource=Fetch $manifest.sourceUrl $manifest.upstreamSourceSha256 ('clients-'+$manifest.commit.Substring(0,7)+'.tar.gz')
$source=Join-Path $cache ('clients-'+$manifest.commit.Substring(0,7)+'-oss.tar.gz')
if(-not(Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant() -ne $manifest.sourceSha256){
    $script=Join-Path $repo 'scripts/prepare-bitwarden-source.py'
    if($IsWindows){
        $python=Get-Command python -CommandType Application -ErrorAction SilentlyContinue | Where-Object Source -NotLike '*WindowsApps*' | Select-Object -First 1
        if($python){ & $python.Source $script $upstreamSource $source }
        else{
            function LinuxPath([string]$Value){$full=[IO.Path]::GetFullPath($Value); return '/mnt/'+$full.Substring(0,1).ToLowerInvariant()+$full.Substring(2).Replace('\','/')}
            & wsl.exe --exec python3 (LinuxPath $script) (LinuxPath $upstreamSource) (LinuxPath $source)
        }
    }else{ & python3 $script $upstreamSource $source }
    if($LASTEXITCODE -ne 0){throw 'Cannot prepare OSS corresponding source'}
}
if((Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant() -ne $manifest.sourceSha256){throw 'OSS corresponding source hash mismatch'}
$extract=Join-Path $cache $Runtime
New-Item -ItemType Directory -Force $extract | Out-Null
$executable=Join-Path $extract $binary.file
if(-not(Test-Path -LiteralPath $executable) -or (Get-FileHash -LiteralPath $executable).Hash.ToLowerInvariant() -ne $binary.sha256){
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try {
        if($zip.Entries.Count -ne 1 -or $zip.Entries[0].FullName -ne $binary.file -or $zip.Entries[0].Length -gt 200MB){throw 'Unexpected Bitwarden archive layout'}
        [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.Entries[0],$executable,$true)
    } finally {$zip.Dispose()}
}
if((Get-FileHash -LiteralPath $executable).Hash.ToLowerInvariant() -ne $binary.sha256){throw 'Bitwarden executable hash mismatch'}
if(-not $IsWindows -and $Runtime -eq 'linux-x64'){& chmod 755 $executable; if($LASTEXITCODE){throw 'Cannot set Bitwarden executable permissions'}}
if($Destination){
    $destinationPath=[IO.Path]::GetFullPath($Destination)
    $prefix=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))+[IO.Path]::DirectorySeparatorChar
    if(-not $destinationPath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Component destination must be inside workspace artifacts'}
    $output=Join-Path $destinationPath 'components/bitwarden'
    New-Item -ItemType Directory -Force $output | Out-Null
    Copy-Item -LiteralPath $executable -Destination (Join-Path $output $binary.file)
    Copy-Item -LiteralPath $source -Destination (Join-Path $output 'SOURCE.tar.gz')
    foreach($file in @('README.md','LICENSE.txt','LICENSE_GPL.txt','manifest.json')){Copy-Item -LiteralPath (Join-Path $repo ('third-party/bitwarden/'+$file)) -Destination $output}
}
Write-Output "PASS pinned Bitwarden OSS $($manifest.version) $Runtime archive, executable and corresponding source"
