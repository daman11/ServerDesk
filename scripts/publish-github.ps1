param([switch]$Publish)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Push-Location $repo
$notesFile=$null
try {
    $version=([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version
    if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid release version'}
    $origin=git remote get-url origin
    if($LASTEXITCODE -ne 0 -or $origin -ne 'https://github.com/daman11/ServerDesk.git'){throw 'Review origin before publishing; expected daman11/ServerDesk'}
    $directory=Join-Path $repo "artifacts/releases/$version"
    Write-Output "Destination: https://github.com/daman11/ServerDesk | branch main | tag v$version"
    Write-Output "Packages: $directory (all ten frozen release files)"
    if(-not $Publish){Write-Output 'PREVIEW ONLY. After reviewing source/history and the release gates, sign in with gh auth login, then rerun with -Publish. No network mutation performed.'; return}
    & ./scripts/check-release-state.ps1 -RequireCurrentRelease
    if((git branch --show-current) -ne 'main'){throw 'Publish the validated main branch only'}
    if(-not (Get-Command gh -ErrorAction SilentlyContinue)){throw 'Install GitHub CLI from https://cli.github.com and run gh auth login. Never put a token in this script.'}
    & gh auth status
    if($LASTEXITCODE -ne 0){throw 'Sign in with gh auth login first'}
    & gh release view "v$version" --repo daman11/ServerDesk *> $null
    if($LASTEXITCODE -eq 0){throw 'Release already exists. Never overwrite a frozen public release.'}
    & git push -u origin main
    if($LASTEXITCODE -ne 0){throw 'Source push failed'}
    & git push origin "v$version"
    if($LASTEXITCODE -ne 0){throw 'Tag push failed'}
    [string[]]$beta=@()
    if(([version]$version).Major -lt 1){$beta=@('--prerelease')}
    # The frozen notes originate in docs/releases; GitHub release bodies resolve
    # relative links differently. Render a copy without changing frozen assets.
    $notes=[IO.File]::ReadAllText((Join-Path $directory 'RELEASE-NOTES.md'))
    $notes=$notes -replace '\]\(\.\./([^\s)]+)\)',("](https://github.com/daman11/ServerDesk/blob/v$version/docs/"+'$1)')
    $notesFile=[IO.Path]::GetTempFileName()
    [IO.File]::WriteAllText($notesFile,$notes,[Text.UTF8Encoding]::new($false))
    & gh release create "v$version" --repo daman11/ServerDesk --verify-tag --draft @beta --title "ServerDesk $version" --notes-file $notesFile
    if($LASTEXITCODE -ne 0){throw 'Could not create draft release'}
    $files=@(Get-ChildItem -LiteralPath $directory -File | Select-Object -ExpandProperty FullName)
    & gh release upload "v$version" @files --repo daman11/ServerDesk
    if($LASTEXITCODE -ne 0){throw 'Upload failed. Draft remains unpublished; inspect it on GitHub.'}
    # GitHub's tag endpoint returns published releases; drafts must be found
    # in the authenticated release list and addressed by their release ID.
    $metadata=& gh api 'repos/daman11/ServerDesk/releases?per_page=100'
    if($LASTEXITCODE -ne 0){throw 'Cannot verify uploaded assets; draft remains unpublished'}
    $matching=@(($metadata | ConvertFrom-Json) | Where-Object tag_name -eq "v$version")
    if($matching.Count -ne 1 -or -not $matching[0].draft){throw 'Expected exactly one unpublished draft for this tag'}
    $release=$matching[0]
    if($release.assets.Count -ne 10){throw 'Incomplete draft assets; draft remains unpublished'}
    foreach($file in $files){
        $asset=@($release.assets | Where-Object name -eq ([IO.Path]::GetFileName($file)))
        $expected='sha256:'+(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()
        if($asset.Count -ne 1 -or $asset[0].digest -ne $expected -or $asset[0].size -ne (Get-Item -LiteralPath $file).Length){throw 'GitHub digest/size differs from frozen files; draft remains unpublished'}
    }
    & gh api --method PATCH ("repos/daman11/ServerDesk/releases/"+$release.id) -F draft=false --jq '.html_url'
    if($LASTEXITCODE -ne 0){throw 'Draft verified but could not be published'}
    Write-Output "Published https://github.com/daman11/ServerDesk/releases/tag/v$version"
} finally {
    if($notesFile -and (Test-Path -LiteralPath $notesFile)){Remove-Item -LiteralPath $notesFile -Force}
    Pop-Location
}
