using System.Collections.Immutable;
using System.ComponentModel;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation;
using MarcusW.VncClient.Protocol.Implementation.Services.Transports;
using MarcusW.VncClient.Protocol.SecurityTypes;
using MarcusW.VncClient.Security;
using Microsoft.Extensions.Logging.Abstractions;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed class VncSession : IDisposable
{
    public RfbConnection Connection { get; }
    public VncFramebuffer Framebuffer { get; }
    private readonly TaskCompletionSource<TerminalCompletion> ended=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<TerminalCompletion> Completion=>ended.Task;
    private int disposed;
    private readonly VncProtocol protocol;
    public Task Shutdown { get; private set; }=Task.CompletedTask;
    public bool IsConnected=>Volatile.Read(ref disposed)==0 && Connection.ConnectionState==ConnectionState.Connected;
    private VncSession(RfbConnection connection,VncFramebuffer framebuffer,VncProtocol protocol)
    { Connection=connection; Framebuffer=framebuffer; this.protocol=protocol; Connection.PropertyChanged+=Changed; Changed(null,new(nameof(Connection.ConnectionState))); }
    private void Changed(object? sender,PropertyChangedEventArgs args)
    {
        if(args.PropertyName!=nameof(RfbConnection.ConnectionState))return;
        if(Connection.ConnectionState is ConnectionState.Closed or ConnectionState.ReconnectFailed or ConnectionState.Interrupted)
        {
            // The library can announce Closed before assigning InterruptionCause.
            var error=protocol.Failure??Connection.InterruptionCause;
            ended.TrySetResult(new(Volatile.Read(ref disposed)!=0?TerminalEndReason.Disposed:error!=null?TerminalEndReason.TransportError:TerminalEndReason.RemoteClosed,error?.GetType().Name));
        }
    }
    public static async Task<VncSession> ConnectAsync(ServerProfile profile,string password,CancellationToken ct=default)
    {
        profile.Validate(); if(profile.Protocol!=Protocol.VNC)throw new ArgumentException(L.T("Profil nie jest połączeniem VNC."));
        if(profile.Vnc.Authentication==VncAuthentication.Password && password.Length==0)throw new ArgumentException(L.T("Podaj hasło VNC lub jawnie wybierz profil bez uwierzytelniania."));
        var frame=new VncFramebuffer();
        // Restrict negotiation to the selected method; never silently downgrade password authentication to None.
        var implementation=new VncProtocol(profile.Vnc.Authentication);
        var client=new VncClient(NullLoggerFactory.Instance,implementation);
        try
        {
            var connection=await client.ConnectAsync(new ConnectParameters { TransportParameters=new TcpTransportParameters { Host=profile.Host,Port=profile.Port },AuthenticationHandler=new PasswordHandler(password),InitialRenderTarget=frame,MaxReconnectAttempts=0,ConnectTimeout=TimeSpan.FromSeconds(15),AllowSharedConnection=profile.Vnc.Shared },ct);
            return new(connection,frame,implementation);
        }
        catch(OperationCanceledException) { frame.Dispose(); throw; }
        catch(Exception ex) { frame.Dispose(); throw new InvalidOperationException(L.T("Nie udało się połączyć VNC. Sprawdź adres, port i wybrane uwierzytelnianie (hasło VNC albo brak uwierzytelniania). TLS/VeNCrypt i konta systemowe nie są obsługiwane."),ex); }
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        Connection.PropertyChanged-=Changed; Connection.RenderTarget=null; ended.TrySetResult(new(TerminalEndReason.Disposed));
        Shutdown=Task.Run(async()=> { try { await Connection.CloseAsync(); } finally { Connection.Dispose(); Framebuffer.Dispose(); } });
    }
    private sealed class PasswordHandler(string password) : IAuthenticationHandler
    {
        public Task<TInput> ProvideAuthenticationInputAsync<TInput>(RfbConnection connection,ISecurityType securityType,IAuthenticationInputRequest<TInput> request) where TInput:class,IAuthenticationInput
        {
            if(typeof(TInput)!=typeof(PasswordAuthenticationInput))throw new NotSupportedException(L.T("Serwer wymaga nieobsługiwanego uwierzytelniania VNC."));
            return Task.FromResult((TInput)(object)new PasswordAuthenticationInput(password));
        }
    }
}
