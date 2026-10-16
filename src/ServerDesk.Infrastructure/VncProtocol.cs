using MarcusW.VncClient.Protocol;
using MarcusW.VncClient.Protocol.Implementation;
using MarcusW.VncClient.Protocol.Implementation.Services.Communication;
using MarcusW.VncClient.Protocol.Services;
using MarcusW.VncClient.Protocol.MessageTypes;
using MarcusW.VncClient.Utils;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

internal sealed class VncProtocol(VncAuthentication authentication) : DefaultImplementation(
    context=>GetDefaultSecurityTypes(context).Where(s=>s.Id==(authentication==VncAuthentication.Password?2:1)),
    GetDefaultMessageTypes,
    // alpha4's ZRLE run lengths are not bounded by the tile size. Use Raw,
    // CopyRect and bounded ZLib until a reviewed ZRLE decoder is available.
    context=>GetDefaultEncodingTypes(context).Where(e=>e.Id is not (0 or 7 or 16)).Append(new VncSecurity.Raw()))
{
    private Exception? failure;
    internal Exception? Failure=>Volatile.Read(ref failure);
    private void RecordFailure(Exception error)=>Interlocked.CompareExchange(ref failure,error,null);
    public override IRfbHandshaker CreateRfbHandshaker(RfbConnectionContext context)=>new VncSecurity.Handshaker(context);
    public override IRfbInitializer CreateRfbInitializer(RfbConnectionContext context)=>new VncSecurity.Initializer(context);
    public override IZLibInflater CreateZLibInflater(RfbConnectionContext context)=>new VncSecurity.Inflater();
    // Upstream alpha4's BackgroundThread excludes OperationCanceledException from
    // its catch filter, allowing normal shutdown to terminate the process. Keep
    // expected cancellation within the worker; other faults follow upstream's path.
    public override IRfbMessageReceiver CreateMessageReceiver(RfbConnectionContext context)=>new Receiver(context,RecordFailure);
    public override IRfbMessageSender CreateMessageSender(RfbConnectionContext context)=>new Sender(context,RecordFailure);
    private sealed class Receiver(RfbConnectionContext context,Action<Exception> reportFailure) : BackgroundThread("ServerDesk VNC read"),IRfbMessageReceiver
    {
        public void StartReceiveLoop()=>Start();
        public Task StopReceiveLoopAsync()
        {
            var stopped=StopAndWaitAsync();
            // A peer may stay silent mid-packet. Cancellation alone cannot interrupt
            // synchronous NetworkStream.Read; close this connection's transport.
            context.Transport?.Dispose();
            return stopped;
        }
        protected override void ThreadWorker(CancellationToken token)
        {
            try
            {
                var messages=context.SupportedMessageTypes!.OfType<IIncomingMessageType>().ToDictionary(m=>m.Id);
                var state=context.GetState<ProtocolState>();
                var transport=context.Transport!;
                while(!token.IsCancellationRequested)
                {
                    var id=transport.Stream.ReadByte();
                    if(id<0)throw new EndOfStreamException(L.T("Zamknięto transport VNC."));
                    if(!messages.TryGetValue((byte)id,out var message))throw new InvalidDataException(L.T("Nieobsługiwany komunikat serwera VNC."));
                    if(!message.IsStandardMessageType)state.EnsureMessageTypeIsMarkedAsUsed(message);
                    message.ReadMessage(transport,token);
                }
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested) { }
            catch(Exception ex) when(token.IsCancellationRequested && ex is IOException or ObjectDisposedException) { }
            catch(Exception ex) { reportFailure(ex); throw; }
        }
    }
    private sealed class Sender(RfbConnectionContext context,Action<Exception> reportFailure) : RfbMessageSender(context)
    {
        protected override void ThreadWorker(CancellationToken token)
        { try { base.ThreadWorker(token); } catch(OperationCanceledException) when(token.IsCancellationRequested) { } catch(Exception ex) { reportFailure(ex); throw; } }
    }
}
