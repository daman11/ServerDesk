using System.Net;
using System.Net.Sockets;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

[Collection("Localization")]
public sealed class SshErrorMessagesTests:IDisposable
{
    private const string Private="private-reply-password-sentinel";
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-ssh-errors-"+Guid.NewGuid().ToString("N"));
    [Theory][InlineData("pl")][InlineData("en")]
    public void Authentication_reports_the_selected_method_and_opaque_server_rejection_without_claiming_a_wrong_password(string language)
    {
        AppText.SetLanguage(language);
        foreach(var method in new[]{Authentication.Password,Authentication.PrivateKey})
        {
            var error=new SshLoginException(method,["publickey","keyboard-interactive","publickey","password\n"+Private,Private],new SshAuthenticationException(Private));
            var message=OperationErrorMessages.Describe(error); Assert.Contains(language=="en"?"SSH did not provide":"SSH nie podał",message); Assert.DoesNotContain(Private,message); Assert.Contains("publickey, keyboard-interactive",message);
            Assert.Contains(method==Authentication.Password?(language=="en"?"password":"hasłem"):(language=="en"?"private key":"kluczem prywatnym"),message);
            var trace=ExceptionDiagnostics.Describe(error); Assert.Contains("SshAuthentication="+method,trace); Assert.Contains("OfferedMethods=publickey,keyboard-interactive",trace); Assert.DoesNotContain(Private,trace);
        }
        Assert.DoesNotContain(Private,OperationErrorMessages.Describe(new SshAuthenticationException(Private)));
    }
    [Theory]
    [InlineData(DisconnectReason.HostNotAllowedToConnect,"hosta","host")]
    [InlineData(DisconnectReason.KeyExchangeFailed,"algorytmów","algorithms")]
    [InlineData(DisconnectReason.HostKeyNotVerifiable,"tożsamości","identity")]
    [InlineData(DisconnectReason.TooManyConnections,"limitu","limit")]
    [InlineData(DisconnectReason.ServiceNotAvailable,"usługi","service")]
    [InlineData(DisconnectReason.ProtocolVersionNotSupported,"wersję","version")]
    [InlineData(DisconnectReason.IllegalUserName,"użytkownika","username")]
    [InlineData(DisconnectReason.NoMoreAuthenticationMethodsAvailable,"metod","methods")]
    [InlineData(DisconnectReason.ProtocolError,"protokołu","protocol")]
    [InlineData(DisconnectReason.ByApplication,"dokładnej","exact")]
    public void Server_disconnect_codes_have_specific_safe_guidance(DisconnectReason code,string polish,string english)
    {
        var error=new SshConnectionException(Private,code);
        foreach(var language in new[]{"pl","en"}) { AppText.SetLanguage(language); var message=OperationErrorMessages.Describe(error); Assert.Contains(language=="en"?english:polish,message,StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(Private,message); }
        Assert.Contains("SshDisconnectReason="+code,ExceptionDiagnostics.Describe(error)); Assert.DoesNotContain(Private,ExceptionDiagnostics.Describe(error));
    }
    [Theory]
    [InlineData(SocketError.ConnectionRefused,"port","port")]
    [InlineData(SocketError.HostNotFound,"DNS","DNS")]
    [InlineData(SocketError.NetworkUnreachable,"trasy","route")]
    [InlineData(SocketError.TimedOut,"czasie","time")]
    [InlineData(SocketError.ConnectionReset,"przerwane","interrupted")]
    [InlineData(SocketError.AccessDenied,"System","system")]
    public void Socket_causes_are_unwrapped_without_treating_them_as_authentication_or_local_disk_failures(SocketError code,string polish,string english)
    {
        var error=new IOException(Private,new SocketException((int)code));
        foreach(var language in new[]{"pl","en"}) { AppText.SetLanguage(language); var message=OperationErrorMessages.Describe(error); Assert.Contains(language=="en"?english:polish,message,StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(Private,message); }
        Assert.Contains("SocketError="+code,ExceptionDiagnostics.Describe(error)); Assert.DoesNotContain(Private,ExceptionDiagnostics.Describe(error));
    }
    [Theory][InlineData("pl")][InlineData("en")]
    public void Local_key_failures_and_ssh_timeouts_are_distinct_and_redacted(string language)
    {
        AppText.SetLanguage(language);
        var messages=Enum.GetValues<SshPrivateKeyFailure>().Select(reason=>OperationErrorMessages.Describe(new SshPrivateKeyException(reason,new IOException(Private)))).ToArray();
        Assert.Equal(messages.Length,messages.Distinct().Count()); foreach(var message in messages)Assert.DoesNotContain(Private,message);
        var timeout=OperationErrorMessages.Describe(new SshOperationTimeoutException(Private)); Assert.Contains("SSH",timeout); Assert.Contains(language=="en"?"time":"czas",timeout); Assert.DoesNotContain(Private,timeout);
    }
    [Fact] public async Task Missing_and_malformed_local_keys_fail_before_network_access_with_specific_messages()
    {
        AppText.SetLanguage("pl"); var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var factory=new SshConnectionFactory(repo);
        var profile=new ServerProfile { Name="Fixture",Host="must-not-connect.invalid",Username="fixture",Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.Combine(root,"missing-key") };
        var missing=await Assert.ThrowsAsync<SshPrivateKeyException>(()=>factory.SshAsync(profile,Private,CancellationToken.None)); Assert.Equal(SshPrivateKeyFailure.Missing,missing.Reason);
        var path=Path.Combine(root,"invalid-key"); await File.WriteAllTextAsync(path,Private);
        var invalid=await Assert.ThrowsAsync<SshPrivateKeyException>(()=>factory.SshAsync(profile with { PrivateKeyPath=path },Private,CancellationToken.None)); Assert.Equal(SshPrivateKeyFailure.InvalidOrPassphrase,invalid.Reason);
        Assert.DoesNotContain(Private,OperationErrorMessages.Describe(invalid)); Assert.Contains("SshPrivateKeyReason=InvalidOrPassphrase",ExceptionDiagnostics.Describe(invalid)); Assert.DoesNotContain(Private,ExceptionDiagnostics.Describe(invalid));
    }
    [Fact] public async Task A_real_bound_but_non_listening_loopback_port_reports_refusal_without_sending_credentials()
    {
        AppText.SetLanguage("pl"); using var bound=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp); bound.Bind(new IPEndPoint(IPAddress.Loopback,0));
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync();
        var profile=new ServerProfile { Name="Fixture",Host="127.0.0.1",Port=((IPEndPoint)bound.LocalEndPoint!).Port,Username="fixture" };
        var error=await Record.ExceptionAsync(()=>new SshConnectionFactory(repo).SshAsync(profile,Private,CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(error); Assert.Contains("portem",OperationErrorMessages.Describe(error)); Assert.Contains("SocketError=ConnectionRefused",ExceptionDiagnostics.Describe(error)); Assert.DoesNotContain(Private,ExceptionDiagnostics.Describe(error));
    }
    public void Dispose() { AppText.SetLanguage("pl"); DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
