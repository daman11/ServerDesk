param([string]$Installer,[string]$Report)
$ErrorActionPreference='Stop'
if(-not $IsWindows){throw 'This native installer check requires Windows'}
$repo=Split-Path -Parent $PSScriptRoot
$version=([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
if(-not $Installer){$Installer=Join-Path $repo "artifacts/ServerDesk-$version-win-x64-Setup.exe"}
if(-not $Report){$Report=Join-Path $repo "artifacts/installer-text-$version.json"}
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class ServerDeskInstallerText {
    private delegate bool EnumProc(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent,EnumProc callback,IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr capacity,StringBuilder text,uint flags,uint timeout,out IntPtr result);
    public static string[] Read(int process) {
        var values=new List<string>();
        EnumProc read=(window,parameter)=> {
            var text=new StringBuilder(8192);
            SendMessageTimeout(window,0x000D,(IntPtr)text.Capacity,text,2,500,out _);
            if(text.Length>0)values.Add(text.ToString()); return true;
        };
        EnumWindows((window,parameter)=> {
            GetWindowThreadProcessId(window,out var owner);
            if(owner==(uint)process){read(window,IntPtr.Zero); EnumChildWindows(window,read,IntPtr.Zero);}
            return true;
        },IntPtr.Zero);
        return values.ToArray();
    }
}
'@
# Only inspect an owned welcome page; never advance installation or touch registration.
$target=Join-Path $repo ('artifacts/installer-text-unused-'+[Guid]::NewGuid().ToString('N'))
$start=[Diagnostics.ProcessStartInfo]::new((Resolve-Path -LiteralPath $Installer).Path,"/NOREGISTER /D=$target")
$start.UseShellExecute=$false; $start.WindowStyle='Hidden'
$process=[Diagnostics.Process]::Start($start)
try {
    $deadline=[DateTime]::UtcNow.AddSeconds(20); $texts=@()
    do {
        if($process.HasExited){throw 'Installer exited before showing its welcome page'}
        $texts=@([ServerDeskInstallerText]::Read($process.Id))
        if(($texts -join "`n").Contains('SSH')){break}
        Start-Sleep -Milliseconds 100
    } while([DateTime]::UtcNow -lt $deadline)
    $joined=$texts -join "`n"
    if(-not $joined.Contains('SSH · SFTP · RDP · VNC · Serial')){throw "Incorrect Unicode protocol separators in compiled installer: $joined"}
    $polish=$joined.Contains('Instalacja dla')
    if($polish -and (-not $joined.Contains('bieżącego konta Windows') -or -not $joined.Contains('zapamiętane hasła zostaną zachowane'))){throw "Corrupted Polish welcome text in compiled installer: $joined"}
    if(-not $polish -and -not $joined.Contains('Installs for your Windows account')){throw 'Welcome text was not found'}
    [ordered]@{success=$true;installer=$Installer;language=$(if($polish){'pl'}else{'en'});texts=$texts} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $Report -Encoding utf8
    if(Test-Path -LiteralPath $target){throw 'Welcome-page check unexpectedly installed files'}
    Write-Output "PASS native compiled installer welcome text: protocol separators and $(if($polish){'Polish diacritics'}else{'English copy'}). Report: $Report"
} finally {
    if(-not $process.HasExited){$process.Kill($true); $process.WaitForExit()}
    $process.Dispose()
}
