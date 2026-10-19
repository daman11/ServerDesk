using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ServerDesk.App;

// Read-only view capped to the validated descriptor size. A remote stream must
// never write more than advertised or become a route to modify the source.
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class RdpClipboardReadStream(IStream source,long length,Action validate,long offset=0):IStream
{
    private long position=offset;
    private bool endChecked;
    private void CheckEnd()
    {
        if(endChecked || position!=length)return;
        validate();var actual=Marshal.AllocHGlobal(4);
        try
        {
            Marshal.WriteInt32(actual,0);source.Read(new byte[1],1,actual);
            if(Marshal.ReadInt32(actual)!=0)throw new IOException(L.T("Przesyłanie pliku RDP zostało przerwane lub plik zmienił rozmiar. Skopiuj go ponownie."));
            validate();endChecked=true;
        }
        finally {Marshal.FreeHGlobal(actual);}
    }
    public void Read(byte[] buffer,int count,nint read)
    {
        if(read!=0)Marshal.WriteInt32(read,0);validate();
        if(count<0 || count>buffer.Length)throw new ArgumentOutOfRangeException(nameof(count));
        var requested=(int)Math.Min(count,length-position);if(requested==0){if(count>0)CheckEnd();return;}
        var actual=Marshal.AllocHGlobal(4);
        try
        {
            Marshal.WriteInt32(actual,0);source.Read(buffer,requested,actual);var received=Marshal.ReadInt32(actual);
            if(received<=0 || received>requested)throw new IOException(L.T("Przesyłanie pliku RDP zostało przerwane lub plik zmienił rozmiar. Skopiuj go ponownie."));
            validate();position+=received;if(read!=0)Marshal.WriteInt32(read,received);
        }
        finally {Marshal.FreeHGlobal(actual);}
    }
    public void Seek(long move,int origin,nint result)
    {
        validate();var target=origin switch {0=>move,1=>checked(position+move),2=>checked(length+move),_=>throw new ArgumentOutOfRangeException(nameof(origin))};
        if(target<0 || target>length)throw new IOException();
        var actual=Marshal.AllocHGlobal(8);
        try {source.Seek(target,0,actual);if(Marshal.ReadInt64(actual)!=target)throw new IOException();position=target;endChecked=false;if(result!=0)Marshal.WriteInt64(result,target);}
        finally {Marshal.FreeHGlobal(actual);}
    }
    public void CopyTo(IStream destination,long count,nint read,nint written)
    {
        if(read!=0)Marshal.WriteInt64(read,0);if(written!=0)Marshal.WriteInt64(written,0);
        // COM cb is ULARGE_INTEGER. A negative managed Int64 represents a
        // request larger than this bounded stream (commonly "copy all").
        var remaining=count<0?length-position:Math.Min(count,length-position);long total=0;var buffer=new byte[65536];var actual=Marshal.AllocHGlobal(4);var output=Marshal.AllocHGlobal(4);
        try
        {
            while(remaining>0)
            {
                Read(buffer,(int)Math.Min(buffer.Length,remaining),actual);var received=Marshal.ReadInt32(actual);
                Marshal.WriteInt32(output,0);destination.Write(buffer,received,output);if(Marshal.ReadInt32(output)!=received)throw new IOException();
                total+=received;remaining-=received;if(read!=0)Marshal.WriteInt64(read,total);if(written!=0)Marshal.WriteInt64(written,total);
            }
            CheckEnd();
        }
        finally {Marshal.FreeHGlobal(actual);Marshal.FreeHGlobal(output);}
    }
    public void Stat(out STATSTG result,int flags){validate();result=new() {type=2,cbSize=length,grfMode=0};}
    public void Clone(out IStream result){validate();source.Clone(out var copy);result=new RdpClipboardReadStream(copy,length,validate,position);}
    public void Write(byte[] bytes,int count,nint written)=>throw new NotSupportedException();
    public void SetSize(long value)=>throw new NotSupportedException();
    public void Commit(int flags)=>throw new NotSupportedException();
    public void Revert()=>throw new NotSupportedException();
    public void LockRegion(long start,long count,int type)=>throw new NotSupportedException();
    public void UnlockRegion(long start,long count,int type)=>throw new NotSupportedException();
}
