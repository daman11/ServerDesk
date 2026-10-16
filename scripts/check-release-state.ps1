param([switch]$RequireCurrentRelease)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $version=([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version
    if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid application version'}
    $tag="v$version"
    $head=git rev-parse HEAD
    if($LASTEXITCODE -ne 0){throw 'Run from a Git checkout'}
    $tags=@(git tag --list $tag)
    $issues=[Collections.Generic.List[string]]::new()
    if($tags -notcontains $tag){$issues.Add("Missing Git tag $tag")}
    else {
        $tagCommit=git rev-parse "$tag^{}"
        if($LASTEXITCODE -ne 0){throw 'Cannot resolve release tag'}
        if($tagCommit -ne $head){$issues.Add("HEAD differs from $tag; code is ahead of or outside its release")}
        $tagType=git cat-file -t "refs/tags/$tag"
        if($tagType -ne 'tag'){$issues.Add('Release tag must be annotated')}
    }
    if(@(git status --porcelain).Count -ne 0){$issues.Add('Source checkout has uncommitted changes')}
    $release=Join-Path $repo "artifacts/releases/$version"
    $expected=@("ServerDesk-$version-win-x64-Setup.exe","ServerDesk-$version-win-x64.zip","ServerDesk-$version-linux-x64.tar.gz","serverdesk_${version}_amd64.deb","ServerDesk-$version-source.zip",'RELEASE-NOTES.md','VERIFICATION.md','LICENSE','release.json','SHA256SUMS.txt')
    if(-not (Test-Path -LiteralPath $release -PathType Container)){$issues.Add("Missing local release directory: $release")}
    else {
        $actual=@(Get-ChildItem -LiteralPath $release -File | Select-Object -ExpandProperty Name)
        if(@(Compare-Object $expected $actual).Count -ne 0){$issues.Add('Release does not contain exactly the ten expected files')}
        if(Test-Path "$release/release.json") {
            $metadata=Get-Content "$release/release.json" -Raw | ConvertFrom-Json
            if($metadata.version -ne $version){$issues.Add('Release metadata version differs from source')}
        }
        if(Test-Path "$release/SHA256SUMS.txt") {
            $lines=@(Get-Content "$release/SHA256SUMS.txt" | Where-Object {$_})
            $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach($line in $lines) {
                if($line -notmatch '^([a-f0-9]{64})  ([^/\\]+)$'){throw 'Malformed release checksum'}
                $digest=$Matches[1]; $name=$Matches[2]
                if($name -notin $expected -or $name -eq 'SHA256SUMS.txt' -or -not $names.Add($name)){throw 'Unexpected or duplicate release checksum'}
                $file=Join-Path $release $name
                if(-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file).Hash -ne $digest){$issues.Add("Release digest mismatch: $name")}
            }
            if($names.Count -ne 9){$issues.Add('Incomplete release checksums')}
        }
        $sourcePath=Join-Path $release "ServerDesk-$version-source.zip"
        if(Test-Path -LiteralPath $sourcePath) {
            $source=[IO.Compression.ZipFile]::OpenRead($sourcePath)
            try {
                $prefix="ServerDesk-$version/"
                $files=@(git -c core.quotePath=false ls-files | ForEach-Object {$_.Replace('\','/')})
                $entries=@($source.Entries | Where-Object {$_.Name -ne '' -and $_.FullName -ne $prefix+'SOURCE-MANIFEST.sha256'})
                if($entries.Count -ne $files.Count){$issues.Add('Packaged source file count differs from tracked source')}
                foreach($file in $files) {
                    $entry=$source.GetEntry($prefix+$file)
                    if(-not $entry){$issues.Add("Packaged source omits $file"); continue}
                    $stream=$entry.Open()
                    try{$digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
                    if($digest -ne (Get-FileHash -LiteralPath (Join-Path $repo $file)).Hash){$issues.Add("Packaged source differs: $file")}
                }
            } finally {$source.Dispose()}
        }
    }
    Write-Output "ServerDesk $version | HEAD $($head.Substring(0,7)) | expected tag $tag"
    if($issues.Count -gt 0) {
        $issues | ForEach-Object {Write-Output "PENDING: $_"}
        if($RequireCurrentRelease){throw 'Current source is not a complete, tagged local release'}
    } else {Write-Output 'PASS clean tagged source, complete local release, SHA-256 and identical packaged source'}
} finally {Pop-Location}
