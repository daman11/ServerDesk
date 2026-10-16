$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$artifacts=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))
$testRoot=Join-Path $artifacts ('cleanup-fixture-'+[Guid]::NewGuid().ToString('N'))
$cache=Join-Path $testRoot 'cache'
$outside=Join-Path $testRoot 'outside'
$link=Join-Path $cache 'build/link'
$cleaner=Join-Path $PSScriptRoot 'clean-artifacts.ps1'
try {
    foreach($directory in @('releases/fixture','reports','keep-me','build/deep')){
        [void][IO.Directory]::CreateDirectory((Join-Path $cache $directory))
    }
    [void][IO.Directory]::CreateDirectory($outside)
    foreach($relative in @('releases/fixture/release.txt','reports/result.txt','keep-me/user.txt','build/deep/generated.txt','temporary.txt')){
        [IO.File]::WriteAllText((Join-Path $cache $relative),'fixture')
    }
    $external=Join-Path $outside 'preserve.txt'; [IO.File]::WriteAllText($external,'external target')
    New-Item -ItemType $(if($IsWindows){'Junction'}else{'SymbolicLink'}) -Path $link -Target $outside | Out-Null
    (Get-Item -LiteralPath (Join-Path $cache 'build/deep/generated.txt')).IsReadOnly=$true
    foreach($unsafe in @($repo,(Join-Path $artifacts 'releases'),$link)){
        $rejected=$false
        try { & $cleaner -Path $unsafe -WhatIf | Out-Null } catch { $rejected=$true }
        if(-not $rejected){throw 'Unsafe root was accepted'}
    }
    & $cleaner -Path $cache -Keep 'keep-me' -WhatIf | Out-Null
    if(-not (Test-Path -LiteralPath (Join-Path $cache 'temporary.txt'))){throw 'WhatIf removed a file'}
    $result=& $cleaner -Path $cache -Keep 'keep-me' -Confirm:$false
    if(-not $result.Completed -or $result.RemovedEntries -ne 2){throw 'Generated entries were not removed'}
    foreach($relative in @('releases/fixture/release.txt','reports/result.txt','keep-me/user.txt')){
        if([IO.File]::ReadAllText((Join-Path $cache $relative)) -ne 'fixture'){throw 'Preserved content changed'}
    }
    if([IO.File]::ReadAllText($external) -ne 'external target'){throw 'Cleanup followed a link outside its root'}
    if(Test-Path -LiteralPath (Join-Path $cache 'build')){throw 'Build directory remains'}
    Write-Output 'PASS artifact cleanup: WhatIf, root/ancestor guards, release/report/Keep preservation, readonly file and junction/symlink target protection'
}finally{
    # The same no-follow cleaner removes this test's two disposable trees.
    if(Test-Path -LiteralPath $testRoot){
        & $cleaner -Path $testRoot -Confirm:$false | Out-Null
        $report=Join-Path $testRoot 'reports/cleanup-latest.json'
        if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report -Force}
        if(Test-Path -LiteralPath (Join-Path $testRoot 'reports')){[IO.Directory]::Delete((Join-Path $testRoot 'reports'),$false)}
        [IO.Directory]::Delete($testRoot,$false)
    }
}
