namespace ServerDesk.Core;
public static class StreamLimits
{
    public const int ConfigurationBytes=32*1024*1024;
    public static async Task<byte[]> ReadAsync(Stream source,int maximum,CancellationToken ct=default)
    {
        using var target=new MemoryStream(); var buffer=new byte[65536]; int count;
        while((count=await source.ReadAsync(buffer.AsMemory(0,Math.Min(buffer.Length,maximum-(int)target.Length+1)),ct))>0)
        {
            if(target.Length+count>maximum)throw new InvalidDataException(L.F($"Plik przekracza limit {maximum/1024/1024} MiB."));
            target.Write(buffer,0,count);
        }
        return target.ToArray();
    }
}
