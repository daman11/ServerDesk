$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $files=@(git -c core.quotePath=false ls-files --cached --others --exclude-standard | Sort-Object -Unique | Where-Object {Test-Path -LiteralPath $_ -PathType Leaf})
    if($LASTEXITCODE -ne 0 -or $files.Count -eq 0){throw 'Run this check from a Git checkout containing source files'}
    $problems=[Collections.Generic.List[string]]::new()
    foreach($ignored in @(git ls-files --cached --ignored --exclude-standard)){$problems.Add("Tracked file matches .gitignore: $ignored")}
    $paths=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($file in $files){[void]$paths.Add($file.Replace('\','/'))}
    foreach($required in @('AGENTS.md','.gitignore','.gitattributes','.editorconfig','NuGet.Config','global.json','LICENSE','README.md','CONTRIBUTING.md','SECURITY.md','.github/workflows/ci.yml','docs/README.md','docs/github.md','docs/releases/README.md','docs/reports/README.md')){
        if(-not $paths.Contains($required)){$problems.Add("Missing repository file: $required")}
    }
    foreach($file in $files){
        $relative=$file.Replace('\','/')
        if($relative.EndsWith('.csproj') -and -not $paths.Contains((Split-Path -Parent $relative).Replace('\','/')+'/packages.lock.json')){$problems.Add("Missing dependency lockfile beside $relative")}
        if($relative -match '(^|/)(\.tools|artifacts|bin|obj|TestResults|logs|credentials|\.ssh)(/|$)|\.(db(?:-.*)?|sqlite3?(?:-.*)?|log|trx|pem|key|pfx|p12|ppk|rdp|exe|dll|pdb|zip|deb|nupkg)$|(^|/)\.env(?:\.|$)|(^|/)(id_rsa|id_ed25519|integration_key)|^serverdesk\.json$'){
            if($relative -ne '.env.example'){$problems.Add("Private or generated file tracked: $relative")}
        }
        if((Get-Item -LiteralPath $file).Length -gt 10MB){$problems.Add("Source file exceeds 10 MiB; review before tracking: $relative")}
        if([IO.Path]::GetExtension($file) -in @('.png','.ico','.jpg','.jpeg')){continue}
        $text=[IO.File]::ReadAllText((Join-Path $repo $file))
        if($text -match '(?m)^-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----\r?$' -or $text -match '(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{60,}|AKIA[A-Z0-9]{16})'){
            $problems.Add("Potential credential material in $relative (value omitted)")
        }
        if($relative -match '^(src|tools|scripts)/' -and $text -match '(?:/mnt/[a-z]/Users/[^/\s]+/Desktop/|[A-Z]:\\Users\\[^\\]+\\Desktop\\)'){
            $problems.Add("Machine-specific developer path: $relative")
        }
        if($relative -like '.github/workflows/*'){
            foreach($use in [regex]::Matches($text,'(?m)^\s*uses:\s*([^\s@]+)@([^\s#]+)')){
                if($use.Groups[2].Value -notmatch '^[0-9a-f]{40}$'){$problems.Add("Action must use a full commit SHA: $relative")}
            }
        }
        if([IO.Path]::GetExtension($file) -ne '.md'){continue}
        foreach($link in [regex]::Matches($text,'\[[^\]\r\n]*\]\((?<target>[^)\r\n]+)\)')){
            $raw=$link.Groups['target'].Value
            if($raw -match '^([a-zA-Z][a-zA-Z0-9+.-]*:|#|/)' -or $raw -match '\s'){continue}
            $target=($raw -split '#',2)[0]
            $resolved=[IO.Path]::GetFullPath((Join-Path (Split-Path -Parent (Join-Path $repo $file)) ([Uri]::UnescapeDataString($target))))
            $local=[IO.Path]::GetRelativePath($repo,$resolved).Replace('\','/')
            if(-not $paths.Contains($local) -and -not ($paths | Where-Object {$_.StartsWith($local.TrimEnd('/')+'/',[StringComparison]::Ordinal)} | Select-Object -First 1)){
                $problems.Add("Broken or case-mismatched Markdown link: $relative -> $raw")
            }
        }
    }
    foreach($notice in (Get-Content third-party/licenses/manifest.json -Raw | ConvertFrom-Json)){
        if((Get-FileHash -LiteralPath "third-party/licenses/$($notice.file)").Hash -ne $notice.sha256){$problems.Add("Upstream notice bytes changed: $($notice.file)")}
    }
    $brands=Get-Content third-party/simple-icons/manifest.json -Raw | ConvertFrom-Json
    foreach($asset in $brands.assets){
        if((Get-FileHash -LiteralPath "src/ServerDesk.App/Assets/Brands/$($asset.name).svg").Hash -ne $asset.sha256){$problems.Add("Upstream brand bytes changed: $($asset.name)")}
    }
    foreach($notice in $brands.notices){
        if((Get-FileHash -LiteralPath "third-party/simple-icons/$($notice.file)").Hash -ne $notice.sha256){$problems.Add("Upstream brand notice bytes changed: $($notice.file)")}
    }
    if($problems.Count -gt 0){throw ($problems -join "`n")}
    Write-Output "PASS repository content: $($files.Count) files, required metadata, generated/private-file exclusions, credential patterns, portable paths, pinned Actions, Markdown links and upstream notice hashes"
    Write-Output 'This is a bounded content check, not a complete secret or security audit.'
} finally {Pop-Location}
