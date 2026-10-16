[CmdletBinding(SupportsShouldProcess=$true,ConfirmImpact='Medium')]
param([string]$Path,[string[]]$Keep=@())

$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$workspaceArtifacts=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd([char[]]'\/')
$comparison=if($IsWindows){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
$separator=[IO.Path]::DirectorySeparatorChar
$workspacePrefix=$workspaceArtifacts+$separator
$root=[IO.Path]::GetFullPath($(if($Path){$Path}else{$workspaceArtifacts})).TrimEnd([char[]]'\/')
if($root -ne $workspaceArtifacts -and -not $root.StartsWith($workspacePrefix,$comparison)){throw 'Cleanup is restricted to the repository artifacts directory'}
foreach($protected in @('releases','reports')){
    $full=Join-Path $workspaceArtifacts $protected
    if($root.Equals($full,$comparison) -or $root.StartsWith($full+$separator,$comparison)){throw 'Release archives and retained reports cannot be cleanup roots'}
}
if(-not (Test-Path -LiteralPath $root -PathType Container)){throw 'Artifacts directory does not exist'}
# Check every ancestor inside artifacts before considering any deletion.
$ancestor=Get-Item -LiteralPath $root -Force
while($true){
    if(($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Cleanup roots must not traverse a symbolic link or junction'}
    if($ancestor.FullName.TrimEnd([char[]]'\/').Equals($workspaceArtifacts,$comparison)){break}
    $ancestor=$ancestor.Parent
}
foreach($name in $Keep){
    if($name -in @('','.', '..') -or $name -match '[\\/:*?"<>|]'){throw 'Keep entries must be literal top-level names, not paths or patterns'}
}
$rootPrefix=$root+$separator
$preserved=[Collections.Generic.HashSet[string]]::new($(if($IsWindows){[StringComparer]::OrdinalIgnoreCase}else{[StringComparer]::Ordinal}))
foreach($name in @('releases','reports','README.md')+$Keep){[void]$preserved.Add($name)}
$entries=@((Get-Item -LiteralPath $root).EnumerateFileSystemInfos())
$references=[Collections.Generic.List[string]]::new()
if($IsWindows){
    foreach($process in (Get-CimInstance Win32_Process -Filter "Name LIKE 'ServerDesk%' OR Name = 'dotnet.exe' OR Name = 'MSBuild.exe'")){
        if($process.ExecutablePath){$references.Add($process.ExecutablePath)}
        if($process.CommandLine){$references.Add($process.CommandLine)}
    }
    # A portable build can be referenced by a shortcut even while it is closed.
    $shell=New-Object -ComObject WScript.Shell
    foreach($place in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))){
        if(-not (Test-Path -LiteralPath $place)){continue}
        foreach($shortcut in (Get-ChildItem -LiteralPath $place -Filter '*ServerDesk*.lnk' -File -Recurse)){
            $link=$shell.CreateShortcut($shortcut.FullName)
            if($link.TargetPath){$references.Add($link.TargetPath)}
            if($link.WorkingDirectory){$references.Add($link.WorkingDirectory)}
        }
    }
}else{
    foreach($process in (Get-Process)){
        try { if($process.Path){$references.Add($process.Path)} } catch { continue }
        if($process.ProcessName -match '^(dotnet|MSBuild|ServerDesk)'){
            $command="/proc/$($process.Id)/cmdline"
            if(Test-Path -LiteralPath $command){try{$references.Add([IO.File]::ReadAllText($command).Replace([char]0,' '))}catch{}}
        }
    }
}
$regexOptions=if($IsWindows){[Text.RegularExpressions.RegexOptions]::IgnoreCase}else{[Text.RegularExpressions.RegexOptions]::None}
$active=[Collections.Generic.List[string]]::new()
foreach($entry in $entries){
    $fullPattern=[regex]::Escape($entry.FullName.Replace('\','/'))+'(?=[/\s"'']|$)'
    $relativePattern='(?:^|[\s"''])artifacts/'+[regex]::Escape($entry.Name)+'(?=[/\s"'']|$)'
    if($references | Where-Object {[regex]::IsMatch($_.Replace('\','/'),$fullPattern,$regexOptions) -or [regex]::IsMatch($_.Replace('\','/'),$relativePattern,$regexOptions)} | Select-Object -First 1){
        [void]$preserved.Add($entry.Name); $active.Add($entry.Name)
    }
}
$targets=@($entries | Where-Object {-not $preserved.Contains($_.Name)})
foreach($target in $targets){
    $absolute=[IO.Path]::GetFullPath($target.FullName)
    if(-not $absolute.StartsWith($rootPrefix,$comparison) -or (Split-Path -Parent $absolute) -ne $root){throw 'Unsafe cleanup target'}
}
function Remove-OwnedEntry([IO.FileSystemInfo]$Entry){
    # Walk explicitly; never use recursive deletion on a reparse point.
    $pending=[Collections.Generic.Stack[object]]::new()
    $pending.Push(@($Entry,$false))
    while($pending.Count -gt 0){
        $frame=$pending.Pop(); $item=[IO.FileSystemInfo]$frame[0]; $visited=[bool]$frame[1]
        $absolute=[IO.Path]::GetFullPath($item.FullName)
        if(-not $absolute.StartsWith($rootPrefix,$comparison)){throw 'Unsafe descendant path'}
        $attributes=$item.Attributes
        if(($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
            if(($attributes -band [IO.FileAttributes]::Directory) -ne 0){[IO.Directory]::Delete($absolute,$false)}else{[IO.File]::Delete($absolute)}
        }elseif($item -is [IO.DirectoryInfo] -and -not $visited){
            $pending.Push(@($item,$true))
            foreach($child in $item.EnumerateFileSystemInfos()){$pending.Push(@($child,$false))}
        }else{
            if(($attributes -band [IO.FileAttributes]::ReadOnly) -ne 0){$item.Attributes=$attributes -band (-bnot [IO.FileAttributes]::ReadOnly)}
            if($item -is [IO.DirectoryInfo]){[IO.Directory]::Delete($absolute,$false)}else{[IO.File]::Delete($absolute)}
        }
    }
}
$summary=[ordered]@{
    Root=$root
    CandidateEntries=$targets.Count
    Preserved=@($entries | Where-Object {$preserved.Contains($_.Name)} | Select-Object -ExpandProperty Name)
    ReferencedByProcessOrShortcut=@($active)
    RemovedEntries=0
    Failures=@()
    Completed=$false
}
if(-not $PSCmdlet.ShouldProcess("$root ($($targets.Count) generated entries)",'Remove generated artifacts; preserve releases, reports and referenced builds')){[pscustomobject]$summary; return}
$failures=[Collections.Generic.List[object]]::new()
foreach($target in $targets){
    try { Remove-OwnedEntry $target; $summary.RemovedEntries++ }
    catch { $failures.Add([pscustomobject]@{Entry=$target.Name;Error=$_.Exception.GetType().Name;Message=$_.Exception.Message}) }
}
$summary.Failures=@($failures.ToArray()); $summary.Completed=$failures.Count -eq 0
$reports=Join-Path $root 'reports'
if(Test-Path -LiteralPath $reports){
    if(((Get-Item -LiteralPath $reports -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Cleanup report directory must not be a link'}
}else{[void][IO.Directory]::CreateDirectory($reports)}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reports 'cleanup-latest.json') -Encoding utf8
[pscustomobject]$summary
if($failures.Count -gt 0){throw 'Some artifacts could not be removed; see reports/cleanup-latest.json. No process was terminated.'}
