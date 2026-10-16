using System.Collections.Immutable;
using System.Text.Json;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation.MessageTypes.Outgoing;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class AdditionalProtocolTests
{
    [Fact]
    public void Legacy_numeric_protocols_and_default_settings_remain_compatible()
    {
        var old=JsonSerializer.Deserialize<ServerProfile>("{\"Name\":\"Old\",\"Host\":\"host\",\"Protocol\":1}")!; old.Validate();
        Assert.Equal(Protocol.RDP,old.Protocol); Assert.NotNull(old.Vnc); Assert.Equal(115200,old.Serial.BaudRate);
        Assert.Equal(0,(int)Protocol.SSH); Assert.Equal(1,(int)Protocol.RDP);
    }
    [Fact]
    public void Vnc_does_not_reuse_legacy_ssh_or_rdp_credential_reference()
    {
        var profile=new ServerProfile(); Assert.Equal(profile.Id,CredentialReference.ForProfile(profile));
        var vnc=profile with { Protocol=Protocol.VNC }; Assert.NotEqual(profile.Id,CredentialReference.ForProfile(vnc)); Assert.Equal(CredentialReference.Vnc(profile.Id),CredentialReference.ForProfile(vnc));
        Assert.NotEqual(CredentialReference.ForProfile(vnc),CredentialReference.ForProfile(vnc with { Id=Guid.NewGuid() }));
    }
    [Theory]
    [InlineData("vnc://example:5901",Protocol.VNC,"example:5901")]
    [InlineData("vnc://[::1]",Protocol.VNC,"::1:5900")]
    [InlineData("serial://COM12?baud=9600",Protocol.Serial,"COM12")]
    [InlineData("serial:///dev/ttyUSB0",Protocol.Serial,"/dev/ttyUSB0")]
    public void Quick_connection_parses_new_endpoints(string input,Protocol protocol,string endpoint)
    { var profile=QuickConnect.Parse(input); Assert.Equal(protocol,profile.Protocol); Assert.Equal(endpoint,profile.Endpoint); profile.Validate(); }
    [Theory]
    [InlineData("serial://COM1?baud=0")][InlineData("serial://COM1?baud=9600&baud=1")][InlineData("serial://user@COM1")][InlineData("vnc://user:secret@host")][InlineData("vnc://host?password=secret")]
    public void Quick_connection_rejects_invalid_options_and_embedded_secrets(string input)=>Assert.Throws<ArgumentException>(()=>QuickConnect.Parse(input));
    [Theory][InlineData(0,8)][InlineData(115200,9)][InlineData(4000001,7)]
    public void Serial_rejects_invalid_line_parameters(int baud,int bits)=>Assert.Throws<ArgumentException>(()=>(new SerialOptions { PortName="COM3",BaudRate=baud,DataBits=bits }).Validate());
    [Fact]
    public async Task New_options_round_trip_in_repository_and_export_without_reusing_identity()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-protocols-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync();
            var serial=new ServerProfile { Name="Console",Protocol=Protocol.Serial,Serial=new() { PortName="COM17",BaudRate=9600,Parity=SerialParity.Even,DataBits=7,StopBits=SerialStopBits.Two,Handshake=SerialHandshake.RequestToSend,DtrEnable=true } };
            var vnc=new ServerProfile { Name="Desktop",Protocol=Protocol.VNC,Host="localhost",Port=5901,Vnc=new() { Authentication=VncAuthentication.None,ViewOnly=true,FitToWindow=false,Shared=false } };
            await repo.SaveAsync(serial); await repo.SaveAsync(vnc); using var data=new MemoryStream(); await repo.ExportAsync(data); data.Position=0; await repo.ImportAsync(data);
            var profiles=await repo.ListAsync(); Assert.Equal(4,profiles.Count); Assert.Equal(2,profiles.Count(p=>p.Serial==serial.Serial)); Assert.Equal(2,profiles.Count(p=>p.Vnc==vnc.Vnc));
            Assert.Equal(4,profiles.Select(p=>p.Id).Distinct().Count()); Assert.True(ProfileSearch.Matches(serial,"COM17 9600 serial"));
        }
        finally { DatabaseTestCleanup.Clear(root); Directory.Delete(root,true); }
    }
    [Fact]
    public void Framebuffer_is_bounded_and_releases_pinned_memory()
    {
        using var buffer=new VncFramebuffer();
        Assert.Throws<InvalidDataException>(()=>buffer.GrabFramebufferReference(new(8193,1),ImmutableHashSet<Screen>.Empty));
        Assert.Throws<InvalidDataException>(()=>buffer.GrabFramebufferReference(new(8192,8192),ImmutableHashSet<Screen>.Empty));
        using(buffer.GrabFramebufferReference(new(10,10),ImmutableHashSet<Screen>.Empty)) { }
        buffer.Read((size,bytes)=>Assert.Equal(400,bytes.Length)); buffer.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>buffer.GrabFramebufferReference(new(10,10),ImmutableHashSet<Screen>.Empty));
    }
    [Theory][InlineData(null)][InlineData("test1234")]
    public async Task Vnc_receives_pixels_sends_keys_and_pointer_and_reports_network_loss(string? password)
    {
        using var server=new VncTestServer(password);
        var profile=new ServerProfile { Name="Fixture",Protocol=Protocol.VNC,Host="127.0.0.1",Port=server.Port,Vnc=new() { Authentication=password==null?VncAuthentication.None:VncAuthentication.Password } };
        using var session=await VncSession.ConnectAsync(profile,password??"");
        var frame=false; await Until(()=> { session.Framebuffer.Read((_,pixels)=>frame=pixels[0]==40 && pixels[1]==190 && pixels[2]==90); return frame; });
        session.Connection.EnqueueMessage(new KeyEventMessage(true,KeySymbol.a),CancellationToken.None);
        session.Connection.EnqueueMessage(new PointerEventMessage(new(12,17),(MouseButtons)1),CancellationToken.None);
        await Until(()=>server.Keys.Any(k=>k.Symbol==97 && k.Down) && server.Pointers.Any(p=>p==(12,17,1)));
        server.Drop(); var result=await session.Completion.WaitAsync(TimeSpan.FromSeconds(8)); Assert.False(session.IsConnected); Assert.NotEqual(TerminalEndReason.Disposed,result.Reason);
    }
    [Fact]
    public async Task Vnc_password_profile_does_not_negotiate_no_authentication()
    { using var server=new VncTestServer(); var profile=new ServerProfile { Name="No downgrade",Protocol=Protocol.VNC,Host="127.0.0.1",Port=server.Port }; await Assert.ThrowsAnyAsync<Exception>(()=>VncSession.ConnectAsync(profile,"test1234")); }
    [Fact]
    public async Task Vnc_rejects_wrong_password()
    { using var server=new VncTestServer("test1234"); var profile=new ServerProfile { Name="Auth",Protocol=Protocol.VNC,Host="127.0.0.1",Port=server.Port }; await Assert.ThrowsAnyAsync<Exception>(()=>VncSession.ConnectAsync(profile,"wrong123")); }
    [Fact]
    public async Task Vnc_can_close_while_transport_is_waiting_for_more_frames()
    {
        using var server=new VncTestServer(); var profile=new ServerProfile { Name="Close",Protocol=Protocol.VNC,Host="127.0.0.1",Port=server.Port,Vnc=new() { Authentication=VncAuthentication.None } };
        var session=await VncSession.ConnectAsync(profile,""); session.Dispose(); await session.Shutdown.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(session.IsConnected); Assert.Equal(TerminalEndReason.Disposed,(await session.Completion).Reason);
    }
    private static async Task Until(Func<bool> check)
    { using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12)); while(!check())await Task.Delay(20,timeout.Token); }
}
