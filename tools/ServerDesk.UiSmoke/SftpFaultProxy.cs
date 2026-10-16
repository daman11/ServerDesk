using System.Net;
using System.Net.Sockets;

// Own loopback connection relay. Faults affect only an explicitly armed connection,
// never OS networking, the SSH listener or another user's session.
internal sealed class SftpFaultProxy : IDisposable
{
    internal sealed class Connection : IDisposable
    {
        private readonly TcpClient incoming,outgoing;
        private readonly CancellationTokenSource lifetime=new();
        private int disposed;
        internal volatile bool Paused,Slow;
        internal Connection(TcpClient incoming,TcpClient outgoing) { this.incoming=incoming; this.outgoing=outgoing; }
        internal async Task Run()
        {
            async Task Copy(Stream source,Stream target)
            {
                var data=new byte[8192];
                while(!lifetime.IsCancellationRequested)
                {
                    var count=await source.ReadAsync(data,lifetime.Token); if(count==0)return;
                    while(Paused)await Task.Delay(10,lifetime.Token);
                    if(Slow)await Task.Delay(4,lifetime.Token);
                    await target.WriteAsync(data.AsMemory(0,count),lifetime.Token);
                }
            }
            var forward=Copy(incoming.GetStream(),outgoing.GetStream()); var backward=Copy(outgoing.GetStream(),incoming.GetStream());
            try { await Task.WhenAny(forward,backward); }
            catch(Exception ex) when(ex is IOException or OperationCanceledException or ObjectDisposedException or SocketException) { }
            finally
            {
                Dispose();
                try { await Task.WhenAll(forward,backward); }
                catch(Exception ex) when(ex is IOException or OperationCanceledException or ObjectDisposedException or SocketException) { }
            }
        }
        public void Dispose() { if(Interlocked.Exchange(ref disposed,1)!=0)return; lifetime.Cancel(); incoming.Dispose(); outgoing.Dispose(); }
    }
    private readonly TcpListener listener=new(IPAddress.Loopback,0);
    private readonly CancellationTokenSource lifetime=new();
    private readonly List<Connection> connections=[];
    private readonly string host; private readonly int port;
    private TaskCompletionSource<Connection>? armed;
    internal int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
    internal Task Loop { get; }
    internal SftpFaultProxy(string host,int port) { this.host=host; this.port=port; listener.Start(); Loop=Task.Run(Run); }
    internal Task<Connection> ArmNext()
    { lock(connections) { if(armed!=null)throw new Exception("Already armed"); armed=new(TaskCreationOptions.RunContinuationsAsynchronously); return armed.Task; } }
    private async Task Run()
    {
        try
        {
            while(!lifetime.IsCancellationRequested)
            {
                var incoming=await listener.AcceptTcpClientAsync(lifetime.Token); var outgoing=new TcpClient();
                try { await outgoing.ConnectAsync(host,port,lifetime.Token); }
                catch { incoming.Dispose(); outgoing.Dispose(); throw; }
                var connection=new Connection(incoming,outgoing);
                lock(connections) { connections.Add(connection); if(armed!=null) { connection.Slow=true; armed.SetResult(connection); armed=null; } }
                _=connection.Run();
            }
        }
        catch(Exception ex) when(ex is OperationCanceledException or ObjectDisposedException or SocketException) { }
    }
    public void Dispose()
    { lifetime.Cancel(); listener.Stop(); lock(connections)foreach(var connection in connections)connection.Dispose(); }
}
