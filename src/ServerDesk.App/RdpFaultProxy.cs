using System.Net;
using System.Net.Sockets;

namespace ServerDesk.App;

// Opt-in test fixture only: forward encrypted bytes to the requested test server.
// Faults affect these owned sockets, never adapters, firewall rules or other apps.
internal sealed class RdpFaultProxy : IDisposable
{
    private readonly TcpListener listener=new(IPAddress.Loopback,0);
    private readonly CancellationTokenSource stop=new();
    private readonly HashSet<TcpClient> sockets=[];
    private readonly string destination;
    private DateTime unavailableUntil;
    internal int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
    internal RdpFaultProxy(string destination) { this.destination=destination; listener.Start(); _=AcceptAsync(); }
    internal void Reset(TimeSpan outage)
    {
        lock(sockets)
        {
            unavailableUntil=DateTime.UtcNow+outage;
            foreach(var socket in sockets.ToArray()) { try { socket.Client.LingerState=new(true,0); socket.Dispose(); } catch(Exception ex) when(ex is SocketException or ObjectDisposedException) { } }
        }
    }
    private async Task AcceptAsync()
    {
        try { while(!stop.IsCancellationRequested) { var incoming=await listener.AcceptTcpClientAsync(stop.Token); _=RelayAsync(incoming); } }
        catch(Exception ex) when(stop.IsCancellationRequested && ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    private async Task RelayAsync(TcpClient incoming)
    {
        using(incoming) using(var outgoing=new TcpClient())
        {
            lock(sockets) { if(DateTime.UtcNow<unavailableUntil) { incoming.Client.LingerState=new(true,0); return; } sockets.Add(incoming); sockets.Add(outgoing); }
            try
            {
                await outgoing.ConnectAsync(destination,3389,stop.Token);
                using var session=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                var a=incoming.GetStream().CopyToAsync(outgoing.GetStream(),session.Token);
                var b=outgoing.GetStream().CopyToAsync(incoming.GetStream(),session.Token);
                await Task.WhenAny(a,b); session.Cancel();
                try { await Task.WhenAll(a,b); } catch(Exception ex) when(ex is IOException or OperationCanceledException or ObjectDisposedException) { }
            }
            catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            finally { lock(sockets) { sockets.Remove(incoming); sockets.Remove(outgoing); } }
        }
    }
    public void Dispose() { stop.Cancel(); listener.Stop(); lock(sockets)foreach(var socket in sockets)socket.Dispose(); stop.Dispose(); }
}
