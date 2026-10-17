[CmdletBinding(DefaultParameterSetName='Run')]
param(
    [Parameter(ParameterSetName='Run')][string]$DotnetPath,
    [Parameter(Mandatory=$true,ParameterSetName='Report')][string]$InputReport,
    [Parameter(Mandatory=$true,ParameterSetName='Report')][int]$CommandExitCode,
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
$repo=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo 'artifacts/dependency-audit'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$summaryFile=Join-Path $OutputDirectory 'audit-summary.json'
if($InputReport -and [IO.Path]::GetFullPath($InputReport) -eq $summaryFile){throw 'The audit summary must not overwrite its input report'}
$issues=[Collections.Generic.List[string]]::new()
if($PSCmdlet.ParameterSetName -eq 'Run'){
    if(-not $DotnetPath){$local=Join-Path $repo '.tools/dotnet/dotnet.exe'; $DotnetPath=if($IsWindows -and (Test-Path -LiteralPath $local)){$local}else{'dotnet'}}
    $InputReport=Join-Path $OutputDirectory 'nuget-audit.json'
    try {
        & $DotnetPath list (Join-Path $repo 'ServerDesk.sln') package --no-restore --vulnerable --include-transitive --format json --output-version 1 1> $InputReport 2> (Join-Path $OutputDirectory 'nuget-stderr.log')
        $CommandExitCode=$LASTEXITCODE
        if(-not [string]::IsNullOrWhiteSpace([IO.File]::ReadAllText((Join-Path $OutputDirectory 'nuget-stderr.log')))){$issues.Add('NuGet wrote diagnostics to stderr; audit coverage is not confirmed')}
    } catch {$CommandExitCode=1; $issues.Add('NuGet audit could not run')}
}
if($CommandExitCode -ne 0){$issues.Add('NuGet command failed')}
$expected=@([regex]::Matches([IO.File]::ReadAllText((Join-Path $repo 'ServerDesk.sln')),'"([^"\r\n]+\.csproj)"') | ForEach-Object {[IO.Path]::GetFullPath((Join-Path $repo $_.Groups[1].Value.Replace('\','/')))})
$comparer=if($IsWindows){[StringComparer]::OrdinalIgnoreCase}else{[StringComparer]::Ordinal}
$seen=[Collections.Generic.HashSet[string]]::new($comparer)
$findingCount=0; $digest=$null
function Check-Diagnostics($node){
    if($node -is [Collections.IDictionary]){
        foreach($key in $node.Keys){
            if($key -in @('problems','errors','warnings') -and @($node[$key]).Where({$null -ne $_}).Count -gt 0){$issues.Add('NuGet reported diagnostics; audit coverage is not confirmed')}
            Check-Diagnostics $node[$key]
        }
    } elseif($node -is [array]){foreach($entry in $node){Check-Diagnostics $entry}}
}
try {
    if((Get-Item -LiteralPath $InputReport).Length -gt 16MB){throw 'Audit report is too large'}
    $digest=(Get-FileHash -LiteralPath $InputReport).Hash.ToLowerInvariant()
    $report=Get-Content -LiteralPath $InputReport -Raw | ConvertFrom-Json -AsHashtable
    if($report -isnot [Collections.IDictionary] -or $report.version -ne 1){throw 'Unsupported audit report schema'}
    if(@($report.Keys | Where-Object {$_ -notin @('version','parameters','sources','projects','problems')}).Count -gt 0){throw 'Unknown audit report fields'}
    foreach($flag in @('--vulnerable','--include-transitive')){
        if($report.parameters -isnot [string] -or $flag -notin ($report.parameters -split '\s+')){throw 'Incomplete vulnerability audit parameters'}
    }
    if($report.sources -isnot [array] -or $report.sources.Count -eq 0){throw 'Missing audit sources'}
    foreach($source in $report.sources){$uri=$null; if($source -isnot [string] -or -not [Uri]::TryCreate($source,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https'){throw 'Invalid audit source'}}
    Check-Diagnostics $report
    if($report.projects -isnot [array] -or $report.projects.Count -ne $expected.Count -or $expected.Count -eq 0){throw 'Incomplete solution project coverage'}
    foreach($project in $report.projects){
        if($project -isnot [Collections.IDictionary] -or $project.path -isnot [string] -or [string]::IsNullOrWhiteSpace($project.path)){throw 'Missing audited project path'}
        $path=[IO.Path]::GetFullPath($project.path.Replace('\','/'))
        if(-not $seen.Add($path) -or -not ($expected | Where-Object {$comparer.Equals($_,$path)})){throw 'Duplicate or unexpected audited project'}
        # NuGet omits frameworks entirely when the vulnerable-only query is empty.
        if($project.Contains('frameworks')){
            if($project.frameworks -isnot [array]){throw 'Invalid audit frameworks'}
            foreach($framework in $project.frameworks){
                if($framework -isnot [Collections.IDictionary] -or [string]::IsNullOrWhiteSpace($framework.framework)){throw 'Missing audit framework name'}
                foreach($kind in @('topLevelPackages','transitivePackages')){
                    if(-not $framework.Contains($kind)){continue}
                    if($framework[$kind] -isnot [array]){throw 'Invalid audited package list'}
                    foreach($package in $framework[$kind]){
                        if($package -isnot [Collections.IDictionary] -or $package.vulnerabilities -isnot [array] -or $package.vulnerabilities.Count -eq 0){throw 'Vulnerable-only package result has no advisory details'}
                        $findingCount+=$package.vulnerabilities.Count
                    }
                }
            }
        }
    }
} catch {$issues.Add('Invalid or incomplete audit report: '+$_.Exception.Message)}
if($findingCount -gt 0){$issues.Add('Known package vulnerabilities were reported')}
$passed=$issues.Count -eq 0
[ordered]@{format=1;checkedAtUtc=[DateTime]::UtcNow.ToString('O');passed=$passed;commandExitCode=$CommandExitCode;projectCount=$seen.Count;expectedProjectCount=$expected.Count;advisoryCount=$findingCount;inputSha256=$digest;issues=@($issues);scope='NuGet known-advisory query, including transitive packages; not a complete security review'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryFile -Encoding utf8NoBOM
if(-not $passed){throw "Dependency audit gate failed. See $summaryFile"}
Write-Output "PASS dependency audit: $($seen.Count) projects, no reported advisories or diagnostics; $summaryFile"
