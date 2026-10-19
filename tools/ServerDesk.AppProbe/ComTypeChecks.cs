using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

internal static class ComTypeChecks
{
    [DllImport("oleaut32.dll",CharSet=CharSet.Unicode)] private static extern int LoadTypeLibEx(string path,int mode,out ITypeLib library);
    internal static void Run()
    {
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"mstscax.dll"),2,out var library));
        for(var i=0;i<library.GetTypeInfoCount();i++)
        {
            library.GetDocumentation(i,out var name,out _,out _,out _);
            if(name is not ("IMsTscNonScriptable" or "IMsRdpClientNonScriptable" or "IMsRdpClient9" or "IMsRdpInputSink" or "IMsRdpClipboard" or "IMsRdpClientNonScriptable7" or "IMsRdpExtendedSettings"))continue;
            library.GetTypeInfo(i,out var info); info.GetTypeAttr(out var pointer); var attr=Marshal.PtrToStructure<TYPEATTR>(pointer); info.ReleaseTypeAttr(pointer);
            Console.WriteLine(name+" "+attr.guid);
            for(var j=0;j<attr.cFuncs;j++)
            {
                info.GetFuncDesc(j,out var function); var desc=Marshal.PtrToStructure<FUNCDESC>(function);
                info.GetDocumentation(desc.memid,out var member,out _,out _,out _);
                Console.WriteLine($"  {member}: slot={desc.oVft/IntPtr.Size}, disp={desc.memid}, parameters={desc.cParams}, kind={desc.invkind}"); info.ReleaseFuncDesc(function);
            }
        }
    }
}
