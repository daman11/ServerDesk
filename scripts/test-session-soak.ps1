param(
    [Parameter(Mandatory)][string]$ReportDirectory,
    [int]$Seconds=14400,
    [int]$Cycles=100,
    [string]$Harness='artifacts/soak-harness/ServerDesk.UiSmoke.dll'
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Set-Location $repo
if(-not $IsWindows){throw 'This coordinator owns a Windows process and the local WSL SSH fixture. Use the opt-in harness directly on Linux.'}
if($Seconds -lt 30 -or $Seconds -gt 86400 -or $Cycles -lt 1 -or $Cycles -gt 1000 -or $Seconds/$Cycles -lt 1){throw 'Invalid bounded soak duration/cycles'}
$artifacts=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))+[IO.Path]::DirectorySeparatorChar
$root=[IO.Path]::GetFullPath($ReportDirectory)
if(-not $root.StartsWith($artifacts,[StringComparison]::OrdinalIgnoreCase)){throw 'Soak reports must stay inside workspace artifacts'}
for($ancestor=[IO.DirectoryInfo]::new((Split-Path -Parent $root));$ancestor;$ancestor=$ancestor.Parent){
    if($ancestor.Exists -and ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Soak reports must not traverse a symbolic link or junction'}
    if($ancestor.FullName.Equals($artifacts.TrimEnd([IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)){break}
}
if(Test-Path -LiteralPath $root){throw 'Choose a new report directory; previous evidence is immutable'}
$harnessPath=[IO.Path]::GetFullPath((Join-Path $repo $Harness))
if(-not $harnessPath.StartsWith($artifacts,[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path -LiteralPath $harnessPath)){throw 'Missing workspace harness'}
if($Seconds -ge 14400 -and $Cycles -ge 100){
    if(@(git status --porcelain).Count){throw 'The full soak must start from clean, committed source'}
    $sourceVersion=([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version
    $sourceRevision=git rev-parse HEAD
    $payloadVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path (Split-Path -Parent $harnessPath) 'ServerDesk.App.dll')).ProductVersion
    if($payloadVersion -ne "$sourceVersion+$sourceRevision"){throw 'Rebuild the stopped harness from the final clean commit before the full run'}
}
New-Item -ItemType Directory -Path $root | Out-Null
$run=Join-Path $root 'run'; New-Item -ItemType Directory -Path $run | Out-Null
function Save-State($value){
    [IO.File]::WriteAllText((Join-Path $root 'coordinator.json.tmp'),($value | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    [IO.File]::Move((Join-Path $root 'coordinator.json.tmp'),(Join-Path $root 'coordinator.json'),$true)
}
Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class ServerDeskSoakPower {
    [DllImport("kernel32.dll", SetLastError=true)] public static extern uint SetThreadExecutionState(uint flags);
}
'@
$fixtureStarted=$false; $child=$null; $outFile=$null; $errFile=$null; $outCopy=$null; $errCopy=$null
$exitCode=1; $started=[DateTimeOffset]::UtcNow; $powerHeld=$false
$state=[ordered]@{state='PREPARING';pid=$PID;startedUtc=$started.ToString('O');seconds=$Seconds;cycles=$Cycles;harness=$harnessPath;sourceCommit=(git rev-parse HEAD);childPid=$null;automaticSleepPrevented=$false}
Save-State $state
try {
    if([ServerDeskSoakPower]::SetThreadExecutionState([uint32]2147483649) -eq 0){throw 'Cannot request process-scoped automatic-sleep prevention'}
    $powerHeld=$true; $state.automaticSleepPrevented=$true
    $wslRepo=(& wsl.exe --exec wslpath -u $repo).Trim()
    if($LASTEXITCODE -ne 0){throw 'Cannot resolve the fixture workspace in WSL'}
    & wsl.exe -u root --exec env SERVERDESK_TEST_OS_USER=damian SERVERDESK_TEST_LISTEN_ADDRESS=127.0.0.1 bash "$wslRepo/scripts/start-test-sshd.sh" *> (Join-Path $root 'fixture-start.log')
    if($LASTEXITCODE -ne 0){throw 'Cannot start the owned SSH fixture; an existing service is never stopped or reused'}
    $fixtureStarted=$true
    $publicFingerprint=& wsl.exe -u root --exec ssh-keygen -l -E sha256 -f /tmp/serverdesk-sshd-test/host_key.pub
    if($LASTEXITCODE -ne 0 -or ($publicFingerprint -join ' ') -notmatch 'SHA256:[A-Za-z0-9+/]+'){throw 'Cannot read the disposable host-key fingerprint'}
    $fingerprint=$Matches[0]
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $repo '.tools/dotnet/dotnet.exe'))
    $start.WorkingDirectory=$repo; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $repo '.tools/cli'
    $start.Environment['NUGET_PACKAGES']=Join-Path $repo '.tools/packages'
    foreach($argument in @($harnessPath,$run,(Join-Path $repo '.tools/linux-ssh/integration_key'),$fingerprint,'--session-soak',[string]$Seconds,[string]$Cycles)){[void]$start.ArgumentList.Add($argument)}
    $child=[Diagnostics.Process]::Start($start)
    $outFile=[IO.File]::Open((Join-Path $root 'stdout.log'),[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    $errFile=[IO.File]::Open((Join-Path $root 'stderr.log'),[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    $outCopy=$child.StandardOutput.BaseStream.CopyToAsync($outFile); $errCopy=$child.StandardError.BaseStream.CopyToAsync($errFile)
    $state.state='RUNNING'; $state.childPid=$child.Id; $state.fixtureFingerprint=$fingerprint; Save-State $state
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds($Seconds+1200)
    while(-not $child.WaitForExit(5000)){
        if(Test-Path -LiteralPath (Join-Path $root 'cancel.request')){[IO.File]::WriteAllText((Join-Path $run 'cancel.request'),'Cancel this owned run')}
        if([DateTimeOffset]::UtcNow -gt $deadline){$child.Kill($true); throw 'Owned soak exceeded its bounded duration plus cleanup allowance'}
    }
    [void]$outCopy.GetAwaiter().GetResult(); [void]$errCopy.GetAwaiter().GetResult()
    $state.childExitCode=$child.ExitCode
    $resultPath=Join-Path $run 'result.json'
    $result=if(Test-Path -LiteralPath $resultPath){Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json}else{$null}
    if($result.state -eq 'CANCELLED'){$state.state='CANCELLED';$exitCode=2}
    elseif($child.ExitCode -ne 0 -or -not $result.passed){throw 'Owned soak failed; inspect run/result.json, stdout.log and stderr.log'}
    else{$state.state='PASSED';$exitCode=0}
} catch {
    $state.state='FAILED'; $state.error=$_.Exception.Message
    [IO.File]::WriteAllText((Join-Path $root 'coordinator-failure.txt'),($_ | Out-String))
} finally {
    if($child -and -not $child.HasExited){$child.Kill($true); $child.WaitForExit(10000) | Out-Null}
    if($outCopy){try{[void]$outCopy.GetAwaiter().GetResult()}catch{}}
    if($errCopy){try{[void]$errCopy.GetAwaiter().GetResult()}catch{}}
    if($outFile){$outFile.Dispose()}; if($errFile){$errFile.Dispose()}; if($child){$child.Dispose()}
    if($fixtureStarted){& wsl.exe -u root --exec bash "$wslRepo/scripts/stop-test-sshd.sh" *> (Join-Path $root 'fixture-stop.log')}
    if($powerHeld){$state.automaticSleepRestored=[ServerDeskSoakPower]::SetThreadExecutionState([uint32]2147483648) -ne 0}
    $state.coordinatorExitCode=$exitCode
    $state.finishedUtc=[DateTimeOffset]::UtcNow.ToString('O'); Save-State $state
}
exit $exitCode
