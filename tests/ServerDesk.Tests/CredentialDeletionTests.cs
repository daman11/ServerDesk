using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class CredentialDeletionTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-delete-"+Guid.NewGuid().ToString("N"),"Żółte profile ze spacjami");
    [Fact] public async Task Missing_credential_directory_is_already_forgotten_and_is_not_created()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root));
        await store.DeleteAsync(Guid.NewGuid());
        Assert.False(Directory.Exists(Path.Combine(root,"credentials")));
    }
    [Fact] public async Task Missing_credential_file_and_repeated_delete_are_successful()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid();
        await store.SaveAsync(id,"disposable-target-secret"); await store.DeleteAsync(id); await store.DeleteAsync(id);
        Assert.Null(await store.GetAsync(id));
    }
    [Fact] public async Task A_previously_removed_credential_directory_does_not_block_forgetting()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid();
        await store.SaveAsync(id,"disposable-secret"); await store.DeleteAsync(id); Directory.Delete(Path.Combine(root,"credentials"));
        await store.DeleteAsync(id); await store.DeleteAsync(CredentialReference.Vnc(id));
        Assert.False(Directory.Exists(Path.Combine(root,"credentials")));
    }
    [Fact] public async Task Deleting_one_identity_preserves_other_secrets_and_unrelated_files_byte_for_byte()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var target=Guid.NewGuid(); var other=Guid.NewGuid();
        await store.SaveAsync(target,"disposable-target"); await store.SaveAsync(other,"disposable-other");
        var otherPath=Path.Combine(root,"credentials",other+".bin"); var bytes=await File.ReadAllBytesAsync(otherPath);
        var unrelated=Path.Combine(root,"credentials","unrelated.txt"); await File.WriteAllTextAsync(unrelated,"keep");
        await store.DeleteAsync(target);
        Assert.Equal(bytes,await File.ReadAllBytesAsync(otherPath)); Assert.Equal("keep",await File.ReadAllTextAsync(unrelated)); Assert.Equal("disposable-other",await store.GetAsync(other));
    }
    [Fact] public async Task Locked_credentials_are_reported_without_deleting_them()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid(); await store.SaveAsync(id,"disposable-secret");
        using(var locked=new FileStream(Path.Combine(root,"credentials",id+".bin"),FileMode.Open,FileAccess.Read,FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(()=>store.DeleteAsync(id));
        Assert.Equal("disposable-secret",await store.GetAsync(id));
    }
    [Fact] public async Task Cancellation_does_not_delete_an_existing_credential()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new CredentialStore(new AppPaths(root)); var id=Guid.NewGuid(); await store.SaveAsync(id,"disposable-secret"); using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.DeleteAsync(id,cancelled.Token)); Assert.Equal("disposable-secret",await store.GetAsync(id));
    }
    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
}
