param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference='Stop'
if(-not $IsWindows){throw 'Windows PE manifest check must run on Windows'}
if(-not ('ServerDesk.Packaging.ManifestReader' -as [type])) {
    Add-Type -Namespace ServerDesk.Packaging -Name ManifestReader -MemberDefinition @'
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode, SetLastError=true)]
    private static extern System.IntPtr LoadLibraryExW(string path,System.IntPtr file,uint flags);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError=true)]
    private static extern System.IntPtr FindResourceW(System.IntPtr module,System.IntPtr name,System.IntPtr type);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError=true)]
    private static extern System.IntPtr LoadResource(System.IntPtr module,System.IntPtr resource);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern System.IntPtr LockResource(System.IntPtr resource);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SizeofResource(System.IntPtr module,System.IntPtr resource);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(System.IntPtr module);
    public static string Read(string path) {
        var module=LoadLibraryExW(path,System.IntPtr.Zero,0x22);
        if(module==System.IntPtr.Zero)throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        try {
            var resource=FindResourceW(module,(System.IntPtr)1,(System.IntPtr)24);
            if(resource==System.IntPtr.Zero)throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            var bytes=new byte[checked((int)SizeofResource(module,resource))];
            var data=LockResource(LoadResource(module,resource));
            if(data==System.IntPtr.Zero)throw new System.InvalidOperationException("Manifest data is missing");
            System.Runtime.InteropServices.Marshal.Copy(data,bytes,0,bytes.Length);
            return System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').TrimEnd('\0');
        } finally { FreeLibrary(module); }
    }
'@
}
$rdpExecutable=(Resolve-Path -LiteralPath $Executable).Path
[xml]$rdpManifest=[ServerDesk.Packaging.ManifestReader]::Read($rdpExecutable)
$rdpXmlNs=[System.Xml.XmlNamespaceManager]::new($rdpManifest.NameTable)
$rdpXmlNs.AddNamespace('c','urn:schemas-microsoft-com:compatibility.v1')
$rdpXmlNs.AddNamespace('t','urn:schemas-microsoft-com:asm.v3')
if(-not $rdpManifest.SelectSingleNode('//c:supportedOS[@Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"]',$rdpXmlNs)){throw 'Published EXE manifest is missing Windows 10/11 compatibility required by embedded RDP'}
$rdpPrivileges=$rdpManifest.SelectSingleNode('//t:requestedExecutionLevel',$rdpXmlNs)
if($rdpPrivileges.level -ne 'asInvoker' -or $rdpPrivileges.uiAccess -ne 'false'){throw 'Published EXE should run asInvoker without UI access elevation'}
'PASS embedded Windows 10/11 manifest, asInvoker, uiAccess=false: '+[System.IO.Path]::GetFileName($rdpExecutable)
