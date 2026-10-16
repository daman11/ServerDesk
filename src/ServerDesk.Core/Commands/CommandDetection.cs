namespace ServerDesk.Core;

public sealed record CommandProbeResult(int? ExitCode,string Output);
public static class CommandDetection
{
    public static string[] ToolNames { get; }=["apt","apt-get","dpkg","dnf","yum","rpm","pacman","apk","systemctl","rc-service","rc-update","netplan","nmcli","ufw","firewall-cmd","docker","git","kubectl","ansible","ss","ip","ping","curl","wget","dig","nslookup","journalctl","lsblk","findmnt","lvs","pvs","vgs","smartctl","zpool","zfs","sudo","nft","iptables","tar","find","grep","ps","free","df","du","uname","ls","cat","tail","head","id","who","getent","chmod","chown","useradd","usermod","passwd","cp","mv","mkdir","rm","uptime","hostname","date","printenv","which","lsof","traceroute","winget","Get-Service","Get-NetTCPConnection","Get-NetIPAddress","Get-Volume","Get-LocalUser","Get-WinEvent","Get-NetFirewallRule","Get-ComputerInfo","Get-CimInstance","Get-HotFix","Get-Process","Get-FileHash","Test-NetConnection","Resolve-DnsName","Get-ScheduledTask","Get-MpComputerStatus","DISM.exe","sfc.exe","whoami","apt-cache","rc-status","Start-Service","Stop-Service","Restart-Service","Get-Acl","Get-LocalGroup","Get-Disk","systeminfo","ipconfig","netstat","route","sc.exe","net","tasklist","wevtutil","docker-compose-plugin"];
    public static string LinuxProbe { get; }="printf 'SDCMD_LINUX_V1\\n'; if [ -r /etc/os-release ]; then cat /etc/os-release; else printf 'SDCMD_OS_UNKNOWN\\n'; fi; printf '\\nSDCMD_KERNEL=%s\\n' \"$(uname -s 2>/dev/null)\"; printf '\\nSDCMD_SHELL=%s\\n' \"$SHELL\"; "+string.Join("; ",ToolNames.Where(t=>!t.Contains('.')&&!t.Any(char.IsUpper)).Select(t=>"if command -v "+t+" >/dev/null 2>&1; then printf 'SDCMD_TOOL="+t+":A\\n'; else printf 'SDCMD_TOOL="+t+":N\\n'; fi"))+"; if command -v docker >/dev/null 2>&1; then if docker compose version >/dev/null 2>&1; then printf 'SDCMD_TOOL=docker-compose-plugin:A\\n'; else printf 'SDCMD_TOOL=docker-compose-plugin:N\\n'; fi; fi; printf 'SDCMD_END\\n'";
    public static string WindowsProbe { get; }=BuildWindowsProbe();
    private static string BuildWindowsProbe()
    {
        var tools=string.Join(",",ToolNames.Select(t=>"'"+t+"'"));
        var script="$ErrorActionPreference='Stop'; Write-Output 'SDCMD_WINDOWS_V1'; try { $os=Get-CimInstance Win32_OperatingSystem; Write-Output ('SDCMD_NAME='+$os.Caption); Write-Output ('SDCMD_VERSION='+$os.Version); Write-Output ('SDCMD_SERVER='+($os.ProductType -ne 1)) } catch { Write-Output 'SDCMD_NAME=Windows'; Write-Output ('SDCMD_VERSION='+[Environment]::OSVersion.Version.ToString()) }; foreach($t in @("+tools+")) { try { $c=Get-Command -Name $t -ErrorAction SilentlyContinue; if($c) { Write-Output ('SDCMD_TOOL='+$t+':A') } else { Write-Output ('SDCMD_TOOL='+$t+':N') } } catch { Write-Output ('SDCMD_TOOL='+$t+':U') } }; try { docker compose version 2>$null | Out-Null; if($LASTEXITCODE -eq 0) { Write-Output 'SDCMD_TOOL=docker-compose-plugin:A' } else { Write-Output 'SDCMD_TOOL=docker-compose-plugin:N' } } catch { Write-Output 'SDCMD_TOOL=docker-compose-plugin:U' }; Write-Output 'SDCMD_END'";
        return "powershell.exe -NoLogo -NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
    }
    public static async Task<CommandEnvironment> DetectAsync(Func<string,CancellationToken,Task<CommandProbeResult>> run,CancellationToken ct)
    {
        CommandProbeResult linux;
        try { linux=await run(LinuxProbe,ct).ConfigureAwait(false); }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch { linux=new(null,""); }
        if(ParseLinux(linux) is {} detected)return detected;
        CommandProbeResult windows;
        try { windows=await run(WindowsProbe,ct).ConfigureAwait(false); }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch { return Unknown(); }
        var result=ParseWindows(windows); if(result==null)return Unknown();
        // An explicit PowerShell subprocess proves availability, not the interactive
        // login shell. Check the SSH default command interpreter independently.
        try
        {
            var cmd=await run("echo %COMSPEC%",ct).ConfigureAwait(false);
            if(cmd.ExitCode==0 && cmd.Output.Trim().EndsWith("cmd.exe",StringComparison.OrdinalIgnoreCase))return result with { Shell=CommandShell.Cmd };
            var ps=await run("Write-Output $PSVersionTable.PSVersion.Major",ct).ConfigureAwait(false);
            if(ps.ExitCode==0 && int.TryParse(ps.Output.Trim(),out _))return result with { Shell=CommandShell.PowerShell };
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch { }
        return result;
    }
    public static CommandEnvironment Unknown()=>new() { Diagnostic=L.T("Nie udało się rozpoznać systemu. Wybierz profil ręcznie; dostępność narzędzi pozostaje nieznana.") };
    public static CommandEnvironment? ParseLinux(CommandProbeResult result)
    {
        if(result.ExitCode!=0||!Complete(result.Output,"SDCMD_LINUX_V1"))return null;
        var values=Values(result.Output); var id=Unquote(values.GetValueOrDefault("ID","")).ToLowerInvariant();
        if(id.Length==0&&values.GetValueOrDefault("SDCMD_KERNEL")!="Linux")return null;
        return new() { Platform=CommandPlatform.Linux,Distribution=id,Family=Family(id,Unquote(values.GetValueOrDefault("ID_LIKE",""))),Version=Unquote(values.GetValueOrDefault("VERSION_ID","")),PrettyName=Unquote(values.GetValueOrDefault("PRETTY_NAME",id)),Shell=Shell(values.GetValueOrDefault("SDCMD_SHELL","")),Tools=Tools(result.Output),Detected=true };
    }
    public static CommandEnvironment? ParseWindows(CommandProbeResult result)
    {
        if(result.ExitCode!=0||!Complete(result.Output,"SDCMD_WINDOWS_V1"))return null;
        var values=Values(result.Output); var name=values.GetValueOrDefault("SDCMD_NAME","Windows");
        return new() { Platform=CommandPlatform.Windows,Distribution=values.GetValueOrDefault("SDCMD_SERVER")=="True"?"windows-server":name.Contains("11",StringComparison.Ordinal)?"windows11":name.Contains("10",StringComparison.Ordinal)?"windows10":"windows",PrettyName=name,Version=values.GetValueOrDefault("SDCMD_VERSION",""),Tools=Tools(result.Output),Detected=true };
    }
    private static bool Complete(string text,string marker)=>text.Length<=128*1024&&text.Replace("\r","").Split('\n').FirstOrDefault()==marker&&text.TrimEnd().EndsWith("SDCMD_END",StringComparison.Ordinal);
    private static Dictionary<string,string> Values(string text)
    {
        var values=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var line in text.Replace("\r","").Split('\n')) { var equal=line.IndexOf('='); if(equal>0 && line.Length<=4096)values[line[..equal]]=line[(equal+1)..]; }
        return values;
    }
    private static Dictionary<string,ToolAvailability> Tools(string text)
    {
        var tools=new Dictionary<string,ToolAvailability>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in text.Replace("\r","").Split('\n').Where(l=>l.StartsWith("SDCMD_TOOL=",StringComparison.Ordinal)))
        { var bits=line[11..].Split(':'); if(bits.Length==2&&ToolNames.Contains(bits[0]))tools[bits[0]]=bits[1] switch { "A"=>ToolAvailability.Available,"N"=>ToolAvailability.NotDetected,_=>ToolAvailability.Unknown }; }
        return tools;
    }
    private static string Unquote(string value)
    {
        if(value.Length>=2 && (value[0]=='"'&&value[^1]=='"'||value[0]=='\''&&value[^1]=='\''))value=value[1..^1];
        // Parse os-release as data. Never source or evaluate shell expansion.
        return value.Replace("\\\"","\"",StringComparison.Ordinal).Replace("\\\\","\\",StringComparison.Ordinal).Replace("\\$","$",StringComparison.Ordinal).Replace("\\`","`",StringComparison.Ordinal);
    }
    private static CommandShell Shell(string value)=>value.Split('/').Last() switch { "bash"=>CommandShell.Bash,"zsh"=>CommandShell.Zsh,_=>CommandShell.Any };
    public static string Family(string id,string like)
    {
        static string Known(string value)=>value switch { "ubuntu" or "debian"=>"debian","rhel" or "rocky" or "almalinux" or "centos" or "fedora"=>"rhel","arch"=>"arch","alpine"=>"alpine",_=>"" };
        var direct=Known(id); if(direct.Length>0)return direct;
        return like.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(Known).FirstOrDefault(f=>f.Length>0)??"";
    }
}
