param([Parameter(Mandatory=$true)][string]$Output,[ValidateSet('win-x64','linux-x64')][string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$assets=Get-Content -LiteralPath (Join-Path $repo 'src/ServerDesk.App/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
$packageRoot=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
$licenseRoot=Join-Path $Output 'licenses'
New-Item -ItemType Directory -Force $licenseRoot | Out-Null
$upstream=Get-Content (Join-Path $repo 'third-party/licenses/manifest.json') -Raw | ConvertFrom-Json
foreach($notice in $upstream) {
    $source=Join-Path $repo "third-party/licenses/$($notice.file)"
    if((Get-FileHash -LiteralPath $source).Hash -ne $notice.sha256){throw "Changed upstream notice: $($notice.file)"}
    Copy-Item -LiteralPath $source -Destination $licenseRoot
}
Copy-Item (Join-Path $repo 'third-party/licenses/manifest.json') $licenseRoot
$runtimeConfig=Get-Content (Join-Path $Output 'ServerDesk.App.runtimeconfig.json') -Raw | ConvertFrom-Json
$runtimeVersion=($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App').version
$runtimeDirectory=Get-Item (Join-Path $packageRoot "microsoft.netcore.app.runtime.$Runtime/$runtimeVersion")
if(-not $runtimeDirectory){throw "Missing .NET runtime package for $Runtime"}
Copy-Item (Join-Path $runtimeDirectory.FullName 'LICENSE.TXT') (Join-Path $Output 'DOTNET-LICENSE.txt')
Copy-Item (Join-Path $runtimeDirectory.FullName 'THIRD-PARTY-NOTICES.TXT') (Join-Path $Output 'DOTNET-THIRD-PARTY-NOTICES.txt')
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('# Third-party licenses and dependency inventory')
$lines.Add('')
$lines.Add('Generated from the locked NuGet dependency graph. Includes build-time and other-platform packages; not every listed component is a binary in this platform package. Full upstream notices are in licenses/. .NET runtime notices are supplied separately. ServerDesk itself is MIT licensed; third-party components retain their own licenses.')
$lines.Add('')
$inventory=@()
foreach($entry in ($assets.libraries.GetEnumerator() | Sort-Object Key)) {
    if($entry.Value.type -ne 'package'){continue}
    $directory=Join-Path $packageRoot $entry.Value.path
    $nuspec=Get-ChildItem -LiteralPath $directory -Filter '*.nuspec' | Select-Object -First 1
    if(-not $nuspec){throw "Missing package metadata: $($entry.Key)"}
    [xml]$xml=Get-Content -LiteralPath $nuspec.FullName -Raw
    $meta=$xml.package.metadata
    $id=[string]$meta.id; $license=if($meta.license){$meta.license.InnerText}else{[string]$meta.licenseUrl}
    $files=[Collections.Generic.List[string]]::new()
    foreach($file in (Get-ChildItem -LiteralPath $directory -File | Where-Object {$_.Name -match '^(?i)(licen[sc]e|notice|third.party.notices|copying)([.\-_].*)?$'})) {
        $name="$id-$($meta.version)-$($file.Name)"
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $licenseRoot $name)
        $files.Add("licenses/$name")
    }
    foreach($notice in $upstream) {
        if(@($notice.packages | Where-Object {$id -like $_}).Count -gt 0){$files.Add("licenses/$($notice.file)")}
    }
    if($id -like 'Microsoft.*' -or $id -like 'System.*' -or $id -like 'runtime.*System.IO.Ports'){$files.Add('DOTNET-LICENSE.txt')}
    if($id -eq 'MarcusW.VncClient'){$files.Add('MARCUSW-VNC-LICENSE.txt')}
    if($files.Count -eq 0){throw "Missing full license for $id ($license)"}
    $lines.Add("## $($entry.Key)"); $lines.Add('')
    $lines.Add("License: $license"); $lines.Add("Authors: $($meta.authors)")
    if($meta.copyright){$lines.Add("Copyright: $($meta.copyright)")}
    if($meta.repository.url){$lines.Add("Source: $($meta.repository.url)")}
    $lines.Add('Full texts: '+($files -join ', ')); $lines.Add('')
    $inventory+=[ordered]@{id=$id;version=[string]$meta.version;license=$license;authors=[string]$meta.authors;copyright=[string]$meta.copyright;source=[string]$meta.repository.url;nugetContentHash=$entry.Value.sha512;notices=@($files)}
}
Copy-Item (Join-Path $repo 'third-party/marcusw-vnc/LICENSE') (Join-Path $Output 'MARCUSW-VNC-LICENSE.txt')
Copy-Item (Join-Path $repo 'third-party/lucide/LICENSE') (Join-Path $Output 'LUCIDE-LICENSE.txt')
Copy-Item (Join-Path $repo 'third-party/lucide/manifest.json') (Join-Path $Output 'LUCIDE-MANIFEST.json')
$brandNotices=Join-Path $licenseRoot 'simple-icons'; New-Item -ItemType Directory -Force $brandNotices | Out-Null
Copy-Item (Join-Path $repo 'third-party/simple-icons/*') $brandNotices
$brandSources=Join-Path $brandNotices 'icons'; New-Item -ItemType Directory -Force $brandSources | Out-Null
Copy-Item (Join-Path $repo 'src/ServerDesk.App/Assets/Brands/*') $brandSources -Force
$lines.Add('## Additional bundled material'); $lines.Add('')
$lines.Add('Bitwarden CLI OSS is an unmodified separate GPL v3 process. components/bitwarden contains its license, pinned binary/source hashes, upstream notices and corresponding OSS clients source archive (SOURCE.tar.gz), excluding commercial modules. ServerDesk code remains MIT; this does not imply Bitwarden endorsement. See the component README for build/source directions.')
$lines.Add('Inter fonts: licenses/inter-LICENSE.txt (SIL OFL 1.1). Lucide 1.55.0 SVG subset: LUCIDE-LICENSE.txt (ISC/MIT), hashes in LUCIDE-MANIFEST.json. MarcusW.VncClient: MARCUSW-VNC-LICENSE.txt (MIT). Windows installer uses NSIS 3.13 with zlib compression: licenses/nsis-COPYING.txt. System fallback fonts and external mstsc/FreeRDP/secret-tool are not distributed by ServerDesk.')
$lines.Add('Simple Icons 16.0.0 subset: licenses/simple-icons/ contains the upstream CC0 dedication, disclaimer, individual icon attribution/license/source metadata, applicable Apache-2.0 and CC-BY-SA texts, and unchanged SVGs. See README.md and manifest.json there. Brand marks identify user-selected systems/services; they do not imply endorsement.')
$lines | Set-Content -LiteralPath (Join-Path $Output 'THIRD-PARTY-NOTICES.md') -Encoding utf8
$vault=Get-Content (Join-Path $repo 'third-party/bitwarden/manifest.json') -Raw | ConvertFrom-Json
$components=@([ordered]@{id='Bitwarden CLI OSS';version=$vault.version;license=$vault.license;commit=$vault.commit;sha256=$vault.runtimes.$Runtime.sha256;sourceSha256=$vault.sourceSha256;notices=@('components/bitwarden/LICENSE.txt','components/bitwarden/LICENSE_GPL.txt');source='components/bitwarden/SOURCE.tar.gz'})
[ordered]@{format='ServerDesk dependency inventory v1';runtime=$Runtime;runtimeVersion=$runtimeDirectory.Name;scope='Resolved NuGet graph including build-time and other-platform assets; separate bundled components are inventoried below';packages=$inventory;components=$components} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Output 'DEPENDENCIES.json') -Encoding utf8
