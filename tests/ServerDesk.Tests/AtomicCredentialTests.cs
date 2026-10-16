using System.Text;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class AtomicCredentialTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-atomic-"+Guid.NewGuid().ToString("N"));
    private string Target() { Directory.CreateDirectory(root); return Path.Combine(root,"fixture.bin"); }
    [Fact] public async Task Write_failure_preserves_existing_file_and_removes_temporary_data()
    {
        var path=Target(); await File.WriteAllTextAsync(path,"original");
        await Assert.ThrowsAsync<IOException>(()=>AtomicFile.ReplaceAsync(path,async(stream,ct)=> { await stream.WriteAsync("partial"u8.ToArray(),ct); throw new IOException("Fixture write failure"); },CancellationToken.None));
        Assert.Equal("original",await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(root));
    }
    [Fact] public async Task Cancellation_after_write_does_not_publish_partial_replacement()
    {
        var path=Target(); await File.WriteAllTextAsync(path,"original"); using var cancelled=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>AtomicFile.ReplaceAsync(path,async(stream,ct)=> { await stream.WriteAsync("partial"u8.ToArray(),ct); cancelled.Cancel(); },cancelled.Token));
        Assert.Equal("original",await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(root));
    }
    [Fact] public async Task Readers_see_old_complete_file_until_the_new_file_is_ready()
    {
        var path=Target(); await File.WriteAllTextAsync(path,"original");
        var staged=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var publish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writing=AtomicFile.ReplaceAsync(path,async(stream,ct)=> { await stream.WriteAsync("replacement"u8.ToArray(),ct); staged.SetResult(); await publish.Task; },CancellationToken.None);
        try { await staged.Task.WaitAsync(TimeSpan.FromSeconds(3)); Assert.Equal("original",await File.ReadAllTextAsync(path)); }
        finally { publish.TrySetResult(); await writing; }
        Assert.Equal("replacement",await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(root));
    }
    [Fact] public async Task Unpublishable_target_preserves_original_and_cleans_staging_file()
    {
        if(!OperatingSystem.IsWindows())return;
        var path=Target(); await File.WriteAllTextAsync(path,"original");
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            var error=await Record.ExceptionAsync(()=>AtomicFile.ReplaceAsync(path,(stream,ct)=>stream.WriteAsync("replacement"u8.ToArray(),ct).AsTask(),CancellationToken.None));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("original",await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(root));
    }
    [Fact] public async Task Failed_first_write_leaves_no_credential_file()
    {
        var path=Target();
        await Assert.ThrowsAsync<IOException>(()=>AtomicFile.ReplaceAsync(path,(_,_)=>Task.FromException(new IOException("Fixture failure")),CancellationToken.None));
        Assert.Empty(Directory.GetFiles(root));
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task Corrupt_or_oversized_ciphertext_is_reported_without_deleting_it(bool oversized)
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid(); var path=Path.Combine(root,"credentials",id+".bin"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var original=oversized?new byte[CredentialLimits.MaximumProtectedBytes+1]:"not-dpapi"u8.ToArray(); await File.WriteAllBytesAsync(path,original);
        var error=await Assert.ThrowsAsync<CredentialReadException>(()=>store.GetAsync(id)); Assert.NotNull(error.InnerException); Assert.Equal(original,await File.ReadAllBytesAsync(path));
        await store.SaveAsync(id,"manual-replacement"); Assert.Equal("manual-replacement",await store.GetAsync(id));
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task Oversized_secret_cannot_replace_a_saved_password(bool unicode)
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid(); await store.SaveAsync(id,"original");
        await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(id,new string(unicode?'界':'x',CredentialLimits.MaximumSecretBytes/(unicode?3:1)+1)));
        Assert.Equal("original",await store.GetAsync(id));
    }
    [Fact] public async Task Concurrent_encrypted_replacements_are_always_readable_and_never_plaintext()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid(); await store.SaveAsync(id,"fixture-0");
        var updates=Enumerable.Range(1,12).Select(i=>store.SaveAsync(id,"fixture-"+i)).ToArray();
        for(var i=0;i<12;i++)Assert.StartsWith("fixture-",await store.GetAsync(id));
        await Task.WhenAll(updates); Assert.StartsWith("fixture-",await store.GetAsync(id));
        var files=Directory.GetFiles(Path.Combine(root,"credentials")); Assert.Single(files); Assert.DoesNotContain("fixture-",Encoding.UTF8.GetString(await File.ReadAllBytesAsync(files[0])));
    }
    [Fact] public void Credential_size_is_measured_in_utf8_bytes()
    {
        CredentialLimits.Validate(new string('x',CredentialLimits.MaximumSecretBytes));
        Assert.Throws<ArgumentException>(()=>CredentialLimits.Validate(new string('x',CredentialLimits.MaximumSecretBytes+1)));
        Assert.Throws<ArgumentException>(()=>CredentialLimits.Validate(new string('界',CredentialLimits.MaximumSecretBytes/3+1)));
    }
    [Fact] public async Task Forget_after_a_started_save_does_not_resurrect_the_password()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid(); await store.SaveAsync(id,"original");
        var saving=store.SaveAsync(id,"updated"); var forgetting=store.DeleteAsync(id); await Task.WhenAll(saving,forgetting);
        Assert.Null(await store.GetAsync(id)); Assert.Empty(Directory.GetFiles(Path.Combine(root,"credentials")));
    }
    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
}
