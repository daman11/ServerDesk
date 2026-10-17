param([string]$Compiler,[string]$Payload,[string]$Output)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$version=([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
if(-not $Compiler){$Compiler=Join-Path $repo '.tools/nsis-3.13/makensis.exe'}
if(-not $Payload){$Payload=Join-Path $repo "artifacts/win-x64-$version"}
if(-not $Output){$Output=Join-Path $repo "artifacts/ServerDesk-$version-win-x64-Setup.exe"}
$Payload=(Resolve-Path -LiteralPath $Payload).Path
if(-not (Test-Path -LiteralPath "$Payload/RELEASE.json")){throw 'Publish a versioned Windows payload first'}
$release=Get-Content "$Payload/RELEASE.json" -Raw | ConvertFrom-Json
if($release.version -ne $version -or $release.runtime -ne 'win-x64'){throw 'Wrong payload version or platform'}
$files=@(Get-ChildItem -LiteralPath $Payload -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($Payload,$_.FullName).Replace('/','\')} | Sort-Object)
$ownedPath=Join-Path $repo 'packaging/windows/owned-files.json'
$previous=if(Test-Path $ownedPath){@(Get-Content $ownedPath -Raw | ConvertFrom-Json)}else{@()}
$owned=@(@($previous)+$files | Sort-Object -Unique)
foreach($path in $owned){if([IO.Path]::IsPathRooted($path) -or $path.Split('\').Contains('..') -or $path -match '["$*?\r\n]'){throw 'Unsafe owned-file path'}}
$generated=Join-Path $repo "artifacts/installer-$version"
New-Item -ItemType Directory -Force $generated | Out-Null
$uninstall=[Collections.Generic.List[string]]::new()
$clean=[Collections.Generic.List[string]]::new()
foreach($path in $owned){
    $uninstall.Add('Delete "$INSTDIR\'+$path+'"')
    if($files -notcontains $path){$clean.Add('Delete "$INSTDIR\'+$path+'"')}
}
$directories=@($owned | ForEach-Object {$dir=[IO.Path]::GetDirectoryName($_); while($dir){$dir;$dir=[IO.Path]::GetDirectoryName($dir)}} | Sort-Object -Unique | Sort-Object Length -Descending)
foreach($dir in $directories){$uninstall.Add('RMDir "$INSTDIR\'+$dir+'"')}
$uninstall | Set-Content "$generated/uninstall.nsh" -Encoding utf8
if($clean.Count -eq 0){$clean.Add('; No retired owned files in this release.')}
$clean | Set-Content "$generated/clean.nsh" -Encoding utf8
$busy=[Collections.Generic.List[string]]::new()
foreach($path in ($owned | Where-Object {$_ -match '\.(exe|dll)$'})){
    $busy.Add('IfFileExists "$INSTDIR\'+$path+'" 0 +8')
    $busy.Add('System::Call ''kernel32::CreateFileW(w "$INSTDIR\'+$path+'", i 0x80000000, i 0, p 0, i 3, i 0, p 0) p.r0''')
    $busy.Add('IntPtrCmp $0 -1 0 +4 +4')
    $busy.Add('MessageBox MB_ICONSTOP "$(RunningText)" /SD IDOK')
    $busy.Add('SetErrorLevel 4')
    $busy.Add('Quit')
    $busy.Add('System::Call ''kernel32::CloseHandle(p r0)''')
    $busy.Add('ClearErrors')
}
$busy | Set-Content "$generated/busy.nsh" -Encoding utf8
$size=[int][Math]::Ceiling((Get-ChildItem $Payload -File -Recurse | Measure-Object Length -Sum).Sum/1024)
# Unicode true controls the generated EXE, not decoding of BOM-less source/includes.
& $Compiler /INPUTCHARSET UTF8 /V2 "/DVERSION=$version" "/DPAYLOAD=$Payload" "/DOUTPUT=$([IO.Path]::GetFullPath($Output))" "/DUNINSTALL_INCLUDE=$(Join-Path $generated 'uninstall.nsh')" "/DCLEAN_INCLUDE=$(Join-Path $generated 'clean.nsh')" "/DBUSY_INCLUDE=$(Join-Path $generated 'busy.nsh')" "/DSIZE_KB=$size" (Join-Path $repo 'packaging/windows/ServerDesk.nsi')
if($LASTEXITCODE -ne 0){throw 'NSIS compiler failed'}
# Retain this cumulative ownership ledger in future releases to remove retired binaries safely.
[IO.File]::WriteAllText($ownedPath,($owned | ConvertTo-Json).Replace("`r`n","`n")+"`n",[Text.UTF8Encoding]::new($false))
Write-Output "Installer: $Output"
