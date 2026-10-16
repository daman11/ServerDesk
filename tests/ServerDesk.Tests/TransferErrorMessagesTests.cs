using System.Net.Sockets;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;
using Renci.SshNet.Sftp;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

[Collection("Localization")]
public sealed class TransferErrorMessagesTests : IDisposable
{
    private const string PrivateReply="opaque-server-reply-secret-marker";
    [Theory]
    [InlineData("remote-permission","permissions")]
    [InlineData("permission-code","permissions")]
    [InlineData("local-permission","permissions")]
    [InlineData("local-file","path")]
    [InlineData("local-directory","path")]
    [InlineData("remote-file","path")]
    [InlineData("missing-code","path")]
    [InlineData("authentication","credentials")]
    [InlineData("ssh-timeout","beginning")]
    [InlineData("timeout","beginning")]
    [InlineData("socket-timeout","beginning")]
    [InlineData("socket-dns","DNS")]
    [InlineData("socket-refused","port")]
    [InlineData("socket-reset","network")]
    [InlineData("connection","network")]
    [InlineData("connection-code","network")]
    [InlineData("unsupported-code","retrying")]
    [InlineData("disposed","network")]
    [InlineData("wrapped-network","network")]
    [InlineData("host-key","host key")]
    [InlineData("key-exchange","secure SSH")]
    [InlineData("server-failure","did not specify")]
    [InlineData("unknown","retrying")]
    public void Typed_failures_get_localized_guidance_without_raw_server_replies(string kind,string expected)
    {
        Exception error=kind switch
        {
            "remote-permission"=>new SftpPermissionDeniedException(PrivateReply),
            "permission-code"=>new SftpException(StatusCode.PermissionDenied,PrivateReply),
            "local-permission"=>new UnauthorizedAccessException(PrivateReply),
            "local-file"=>new FileNotFoundException(PrivateReply),
            "local-directory"=>new DirectoryNotFoundException(PrivateReply),
            "remote-file"=>new SftpPathNotFoundException(PrivateReply),
            "missing-code"=>new SftpException(StatusCode.NoSuchFile,PrivateReply),
            "authentication"=>new SshAuthenticationException(PrivateReply),
            "ssh-timeout"=>new SshOperationTimeoutException(PrivateReply),
            "timeout"=>new TimeoutException(PrivateReply),
            "socket-timeout"=>new SocketException((int)SocketError.TimedOut),
            "socket-dns"=>new SocketException((int)SocketError.HostNotFound),
            "socket-refused"=>new SocketException((int)SocketError.ConnectionRefused),
            "socket-reset"=>new SocketException((int)SocketError.ConnectionReset),
            "connection"=>new SshConnectionException(PrivateReply),
            "connection-code"=>new SftpException(StatusCode.ConnectionLost,PrivateReply),
            "unsupported-code"=>new SftpException(StatusCode.OperationUnsupported,PrivateReply),
            "disposed"=>new ObjectDisposedException(PrivateReply),
            "wrapped-network"=>new IOException(PrivateReply,new SocketException((int)SocketError.NetworkUnreachable)),
            "host-key"=>new SshConnectionException(PrivateReply,DisconnectReason.HostKeyNotVerifiable),
            "key-exchange"=>new SshConnectionException(PrivateReply,DisconnectReason.KeyExchangeFailed),
            "server-failure"=>new SftpException(StatusCode.Failure,PrivateReply),
            _=>new FormatException(PrivateReply)
        };
        AppText.SetLanguage("pl"); var polish=TransferErrorMessages.Describe(error);
        AppText.SetLanguage("en"); var english=TransferErrorMessages.Describe(error);
        Assert.DoesNotContain(PrivateReply,polish); Assert.DoesNotContain(PrivateReply,english);
        Assert.NotEqual(polish,english); Assert.Contains(expected,english,StringComparison.OrdinalIgnoreCase);
    }
    [Theory] [InlineData(unchecked((int)0x80070070))] [InlineData(unchecked((int)0x80070027))]
    public void Known_local_disk_full_codes_give_local_guidance(int code)
    {
        AppText.SetLanguage("en"); var detail=TransferErrorMessages.Describe(new IOException(PrivateReply,code));
        Assert.Contains("local disk",detail); Assert.Contains("Free up space",detail); Assert.DoesNotContain(PrivateReply,detail);
    }
    [Fact] public void Generic_remote_failure_does_not_guess_disk_full_from_its_text()
    {
        AppText.SetLanguage("en"); var detail=TransferErrorMessages.Describe(new SftpException(StatusCode.Failure,"Disk full: "+PrivateReply));
        Assert.Contains("did not specify",detail); Assert.DoesNotContain("local disk",detail); Assert.DoesNotContain(PrivateReply,detail);
    }
    [Fact] public void Wrapped_host_key_warning_keeps_its_fingerprints_and_instructions()
    {
        const string warning="ZMIANA KLUCZA HOSTA fixture:22. SHA256:old -> SHA256:new. Sprawdź tożsamość.";
        var error=new SshConnectionException(PrivateReply,new InvalidOperationException(warning));
        Assert.Equal(warning,TransferErrorMessages.Describe(error));
    }
    [Theory] [InlineData("pl")] [InlineData("en")]
    public void Specific_integrity_and_target_validation_messages_are_preserved(string language)
    {
        AppText.SetLanguage(language);
        var integrity=AppText.T("Rozmiar pobranego pliku zmienił się podczas transferu. Plik docelowy pozostał bez zmian.");
        Assert.Equal(integrity,TransferErrorMessages.Describe(new IOException(integrity)));
        const string target="Own target validation message";
        Assert.Equal(target,TransferErrorMessages.Describe(new InvalidDataException(target)));
        Assert.Equal(target,TransferErrorMessages.Describe(new ArgumentException(target)));
    }
    public void Dispose()=>AppText.SetLanguage("pl");

    [Theory]
    [InlineData("pl",true)] [InlineData("pl",false)]
    [InlineData("en",true)] [InlineData("en",false)]
    public void Cleanup_guidance_preserves_exact_path_and_warns_that_retry_keeps_older_files(string language,bool remote)
    {
        AppText.SetLanguage(language);
        const string path="/owned/żółć {0}/.serverdesk-123.part";
        var detail=TransferErrorMessages.DescribeCleanup(new(path,remote));
        Assert.Contains(path,detail);
        Assert.Contains(language=="en"?"Retrying does not remove":"Ponowienie nie usuwa",detail);
        Assert.Contains(remote?(language=="en"?"on the server:":"na serwerze:"):(language=="en"?"on this computer:":"na komputerze:"),detail);
        if(language=="en")Assert.DoesNotContain("Nie potwierdzono",detail);
    }
}
