using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Renci.SshNet.Common;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

[Collection("Localization")]
public sealed class OperationErrorTests:IDisposable
{
    [Theory][InlineData("pl")][InlineData("en")]
    public void Local_filesystem_database_and_ssh_errors_have_distinct_guidance_without_raw_replies(string language)
    {
        AppText.SetLanguage(language); const string privateReply="private-remote-reply-sentinel";
        var errors=new Exception[]{new DirectoryNotFoundException(privateReply),new FileNotFoundException(privateReply,"local-path"),new UnauthorizedAccessException(privateReply),new IOException(privateReply),new SqliteException(privateReply,14),new SshAuthenticationException(privateReply),new SshConnectionException(privateReply)};
        var messages=errors.Select(OperationErrorMessages.Describe).ToArray(); Assert.Equal(messages.Length,messages.Distinct().Count());
        foreach(var message in messages) { Assert.DoesNotContain(privateReply,message); Assert.DoesNotContain("Sprawdź adres, uprawnienia i połączenie",message); }
        Assert.Contains(language=="en"?"local":"lokalnego",messages[0]); Assert.Contains("SSH",messages[5]);
        Assert.Equal("Application-specific guidance",OperationErrorMessages.Describe(new InvalidOperationException("Application-specific guidance")));
        Assert.Equal("Install external tool",OperationErrorMessages.Describe(new FileNotFoundException("Install external tool")));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception Failure()
    {
        try { throw new IOException("secret-message-sentinel"); }
        catch(Exception inner) { try { throw new InvalidOperationException("secret-message-sentinel",inner); } catch(Exception outer) { outer.Data["secret"]= "secret-data-sentinel"; return outer; } }
    }
    [Fact] public void General_diagnostics_include_full_outer_and_inner_stacks_and_codes_without_message_or_data()
    {
        var trace=ExceptionDiagnostics.Describe(Failure()); Assert.Contains("System.IO.IOException",trace); Assert.Contains("System.InvalidOperationException",trace); Assert.Contains("HResult=0x",trace); Assert.Contains(nameof(Failure),trace); Assert.DoesNotContain("secret-message-sentinel",trace); Assert.DoesNotContain("secret-data-sentinel",trace);
    }
    [Fact] public async Task Missing_Secret_Service_backend_remains_an_error_with_the_original_cause()
    {
        var store=new SecretServiceCredentialStore("serverdesk-nonexistent-secret-tool-fixture");
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>store.DeleteAsync(Guid.NewGuid())); Assert.IsType<Win32Exception>(error.InnerException); Assert.Contains("SecretServiceCredentialStore",ExceptionDiagnostics.Describe(error));
    }
    public void Dispose()=>AppText.SetLanguage("pl");
}
