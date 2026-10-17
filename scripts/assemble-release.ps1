param([string]$Output)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$version=([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
if(-not $Output){$Output=Join-Path $repo "artifacts/releases/$version"}
if(Test-Path -LiteralPath $Output){throw 'Release directory already exists. Use a new version or an empty staging location; do not overwrite a published release.'}
$destination=[IO.Path]::GetFullPath($Output)
$Output=Join-Path $repo ('artifacts/release-staging-'+[Guid]::NewGuid().ToString('N'))
$assets=@("ServerDesk-$version-win-x64-Setup.exe","ServerDesk-$version-win-x64.zip","ServerDesk-$version-linux-x64.tar.gz","serverdesk_${version}_amd64.deb")
foreach($file in $assets){if(-not (Test-Path -LiteralPath (Join-Path $repo "artifacts/$file"))){throw "Missing release artifact: $file"}}
New-Item -ItemType Directory -Path $Output | Out-Null
foreach($file in $assets){Copy-Item -LiteralPath (Join-Path $repo "artifacts/$file") -Destination $Output}
$sourceRoot=Join-Path $repo ('artifacts/source-staging-'+[Guid]::NewGuid().ToString('N'))
$source=Join-Path $sourceRoot "ServerDesk-$version"
New-Item -ItemType Directory -Path $source | Out-Null
$sourceFiles=[Collections.Generic.List[string]]::new()
function Add-SourceDirectory([string]$Directory) {
    foreach($item in (Get-ChildItem -LiteralPath (Join-Path $repo $Directory) -Force)) {
        if($item.PSIsContainer){if($item.Name -notin @('bin','obj','.git','.tools','artifacts','TestResults')){Add-SourceDirectory ([IO.Path]::GetRelativePath($repo,$item.FullName))}; continue}
        if($item.Extension -notin @('.cs','.csproj','.json','.props','.targets','.manifest','.svg','.png','.ico','.md','.sh','.ps1','.py','.nsi','.txt','.desktop','.yml','.yaml') -and $item.Name -notin @('LICENSE','COPYING','NOTICE')){throw "Unexpected source file; review before export: $($item.Name)"}
        $relative=[IO.Path]::GetRelativePath($repo,$item.FullName)
        # Credential audit guides are source documentation, not credential data.
        # Keep the filename guard for data/config files and all key/env candidates.
        $credentialGuide=$relative.Replace('\','/').StartsWith('docs/',[StringComparison]::Ordinal) -and $item.Extension -eq '.md' -and $item.BaseName -match '^credentials?(?:-|$)' -and $item.Name -notmatch '(?i)integration_key|^\.env'
        if($item.Name -match '(?i)integration_key|credential|^\.env' -and $item.Extension -ne '.cs' -and -not $credentialGuide){throw "Sensitive source candidate: $relative"}
        $destination=Join-Path $source $relative
        New-Item -ItemType Directory -Force (Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath $item.FullName -Destination $destination
        $sourceFiles.Add($relative)
    }
}
foreach($directory in @('src','tests','tools','scripts','packaging','third-party','docs','.github')){Add-SourceDirectory $directory}
foreach($file in @('.gitignore','.gitattributes','.editorconfig','.gitleaks.toml','AGENTS.md','NuGet.Config','Directory.Build.props','global.json','ServerDesk.sln','README.md','LICENSE','CHANGELOG.md','CONTRIBUTING.md','SECURITY.md')){Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $source; $sourceFiles.Add($file)}
$sourceManifest=foreach($file in ($sourceFiles | Sort-Object)){(Get-FileHash -LiteralPath (Join-Path $source $file)).Hash.ToLowerInvariant()+'  '+$file.Replace('\','/')}
$sourceManifest | Set-Content (Join-Path $source 'SOURCE-MANIFEST.sha256') -Encoding utf8
$sourceName="ServerDesk-$version-source.zip"
Compress-Archive -LiteralPath $source -DestinationPath (Join-Path $Output $sourceName)
Copy-Item (Join-Path $repo "docs/releases/release-$version.md") (Join-Path $Output 'RELEASE-NOTES.md')
Copy-Item (Join-Path $repo "artifacts/release-verification-$version.md") (Join-Path $Output 'VERIFICATION.md')
Copy-Item (Join-Path $repo 'LICENSE') $Output
$files=@($assets)+@($sourceName,'RELEASE-NOTES.md','VERIFICATION.md','LICENSE')
$metadata=[ordered]@{product='ServerDesk';version=$version;channel='public-beta';license='MIT';createdUtc=[DateTime]::UtcNow.ToString('O');signed=$false;automaticUpdates=$false;files=@($files | ForEach-Object {$item=Get-Item (Join-Path $Output $_); [ordered]@{name=$_;bytes=$item.Length;sha256=(Get-FileHash -LiteralPath $item.FullName).Hash.ToLowerInvariant()}})}
$metadata | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $Output 'release.json') -Encoding utf8
@($files)+@('release.json') | Sort-Object | ForEach-Object {(Get-FileHash -LiteralPath (Join-Path $Output $_)).Hash.ToLowerInvariant()+'  '+$_} | Set-Content (Join-Path $Output 'SHA256SUMS.txt') -Encoding utf8
New-Item -ItemType Directory -Force (Split-Path -Parent $destination) | Out-Null
[IO.Directory]::Move($Output,$destination)
Write-Output "Release ready for review: $destination ($($sourceFiles.Count) source files)"
