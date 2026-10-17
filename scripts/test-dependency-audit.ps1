param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo ('artifacts/audit-gate-test-'+[Guid]::NewGuid().ToString('N'))}
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$projects=@([regex]::Matches([IO.File]::ReadAllText((Join-Path $repo 'ServerDesk.sln')),'"([^"\r\n]+\.csproj)"') | ForEach-Object {@{path=[IO.Path]::GetFullPath((Join-Path $repo $_.Groups[1].Value.Replace('\','/')))}})
$clean=@{version=1;parameters='--vulnerable --include-transitive';sources=@('https://api.nuget.org/v3/index.json');projects=$projects} | ConvertTo-Json -Depth 10
$results=[Collections.Generic.List[object]]::new()
function Case([string]$Name,[scriptblock]$Change,[bool]$Expected=$false,[int]$ExitCode=0,[string]$Raw){
    $document=$clean | ConvertFrom-Json -AsHashtable; & $Change $document
    $folder=Join-Path $OutputDirectory $Name; New-Item -ItemType Directory -Force $folder | Out-Null
    $fixtureInput=Join-Path $folder 'input.json'; $(if($Raw){$Raw}else{$document | ConvertTo-Json -Depth 15}) | Set-Content -LiteralPath $fixtureInput -Encoding utf8NoBOM
    $passed=$false
    try {& (Join-Path $repo 'scripts/audit-dependencies.ps1') -InputReport $fixtureInput -CommandExitCode $ExitCode -OutputDirectory $folder *> (Join-Path $folder 'output.log'); $passed=$true} catch { }
    $summary=Get-Content (Join-Path $folder 'audit-summary.json') -Raw | ConvertFrom-Json
    if($passed -ne $Expected -or $summary.passed -ne $Expected){throw "Unexpected audit decision: $Name"}
    $results.Add(@{case=$Name;passed=$true;expectedGatePass=$Expected})
}
Case 'clean-omitted-frameworks' {} $true
Case 'clean-empty-packages' {param($d) $d.projects[0].frameworks=@(@{framework='net10.0';topLevelPackages=@();transitivePackages=@()})} $true
Case 'command-failed' {} $false 1
Case 'unsupported-schema' {param($d) $d.version=2}
Case 'malformed-json' {} $false 0 '{broken'
Case 'unknown-schema-field' {param($d) $d.errors=@('Cannot audit')}
foreach($flag in @('--vulnerable','--include-transitive')){Case ('missing-'+$flag.TrimStart('-')) {param($d) $d.parameters=$d.parameters.Replace($flag,'')}}
Case 'no-sources' {param($d) $d.sources=@()}
Case 'invalid-source' {param($d) $d.sources=@('not a source')}
Case 'no-projects' {param($d) $d.projects=@()}
Case 'missing-project' {param($d) $d.projects=@($d.projects | Select-Object -Skip 1)}
Case 'duplicate-project' {param($d) $d.projects[1].path=$d.projects[0].path}
Case 'unexpected-project' {param($d) $d.projects[0].path=Join-Path $repo 'unlisted.csproj'}
Case 'source-error-zero-exit' {param($d) $d.problems=@(@{level='error';text='Unable to load service index'})}
Case 'source-warning-zero-exit' {param($d) $d.problems=@(@{level='warning';text='NU1900'})}
Case 'project-diagnostic' {param($d) $d.projects[0].problems=@(@{level='error';text='Project unavailable'})}
Case 'framework-diagnostic' {param($d) $d.projects[0].frameworks=@(@{framework='net10.0';problems=@(@{level='warning';text='Unavailable source'})})}
foreach($kind in @('topLevelPackages','transitivePackages')){
    Case ('vulnerable-'+$kind) {param($d) $frame=@{framework='net10.0'}; $frame[$kind]=@(@{id='Fixture.Package';resolvedVersion='1.0.0';vulnerabilities=@(@{severity='High';advisoryurl='https://example.invalid/advisory'})}); $d.projects[0].frameworks=@($frame)}
}
Case 'malformed-package' {param($d) $d.projects[0].frameworks=@(@{framework='net10.0';topLevelPackages=@(@{id='Fixture.Package'})})}
Case 'null-frameworks' {param($d) $d.projects[0].frameworks=$null}
Case 'unnamed-framework' {param($d) $d.projects[0].frameworks=@(@{topLevelPackages=@()})}
$results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'results.json') -Encoding utf8NoBOM
Write-Output "PASS $($results.Count) dependency-audit gate fixtures"
