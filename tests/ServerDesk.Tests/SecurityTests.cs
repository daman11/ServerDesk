using System.Text;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class SecurityTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-security-"+Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("report.")][InlineData("report ")][InlineData(".. ")][InlineData(". ")]
    [InlineData("CON")][InlineData("nul.txt")][InlineData("COM1.log")][InlineData("LPT³.txt")]
    [InlineData("CONIN$")][InlineData("CONOUT$")]
    public void Windows_download_rejects_ambiguous_names_before_creating_jobs(string name)
    {
        if(!OperatingSystem.IsWindows())return;
        var entry=new FileEntry(name,"/remote/"+name,false,false,1,DateTime.UtcNow,"");
        Assert.Throws<ArgumentException>(()=>FileSelection.Download([entry],root));
    }
    [Fact] public async Task Imported_null_profile_is_rejected_without_changing_database()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync();
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":2,\"Servers\":[null],\"Folders\":[]}"));
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.ImportAsync(stream)); Assert.Empty(await repo.ListAsync());
    }
    [Fact] public async Task Imported_deep_folder_is_rejected_before_ancestor_expansion()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync();
        var json=JsonSerializer.Serialize(new ProfileRepository.ConfigurationExport(2,[],[string.Join('/',Enumerable.Repeat("a",100))]));
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes(json));
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.ImportAsync(stream)); Assert.Empty(await repo.FoldersAsync());
    }
    [Fact] public void External_windows_rdp_uses_system_executable()
    {
        if(!OperatingSystem.IsWindows())return;
        var launch=new ExternalRdpClient(new AppPaths(root)).Prepare(new() { Name="Test",Host="localhost",Protocol=Protocol.RDP,Port=3389 },new());
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"mstsc.exe"),launch.Executable);
    }
    [Theory][InlineData("Host")][InlineData("Username")][InlineData("Domain")][InlineData("Folder")][InlineData("Tags")][InlineData("Description")][InlineData("PrivateKeyPath")]
    public void Null_profile_fields_have_a_validation_error(string property)
    {
        var json="{\"Name\":\"Test\",\"Host\":\"localhost\",\"Username\":\"user\",\""+property+"\":null}";
        Assert.Throws<ArgumentException>(()=>JsonSerializer.Deserialize<ServerProfile>(json)!.Validate());
    }
    [Fact] public async Task Generated_ancestors_count_toward_the_import_limit_and_failure_is_atomic()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync();
        var folders=Enumerable.Range(0,334).Select(i=>"root-"+i+"/"+string.Join('/',Enumerable.Repeat("a",31))).ToList();
        using var stream=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new ProfileRepository.ConfigurationExport(2,[],folders)));
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.ImportAsync(stream)); Assert.Empty(await repo.FoldersAsync());
    }
    [Fact] public void Folder_limits_allow_boundary_values_and_preserve_unicode()
    {
        var path=string.Join('/',Enumerable.Repeat("Żółć",32)); ProfileFolder.Validate(path);
        Assert.Equal(32,ProfileFolder.Ancestors(path).Count()); ProfileFolder.Validate(new string('a',1024));
        Assert.Throws<ArgumentException>(()=>ProfileFolder.Validate(new string('a',1025)));
        Assert.Throws<ArgumentException>(()=>ProfileFolder.Validate(path+"/extra"));
        Assert.Throws<ArgumentException>(()=>ProfileFolder.Validate("root/\nchild"));
    }
    [Theory][InlineData(".config")][InlineData("żółć.txt")][InlineData("COM10.log")][InlineData("console.txt")]
    public void Regular_local_names_remain_usable(string name)
    {
        var target=LocalFileName.InDirectory(root,name); Assert.Equal(Path.GetFullPath(root),Path.GetDirectoryName(target));
    }
    [Fact] public void Linux_can_keep_names_that_are_only_reserved_by_windows()
    {
        if(OperatingSystem.IsWindows())return;
        foreach(var name in new[]{"CON","report.","report ","NUL.txt"})Assert.EndsWith('/'+name,LocalFileName.InDirectory(root,name));
    }
    [Fact] public void Credentials_are_reused_only_for_the_same_connection_identity()
    {
        var profile=new ServerProfile { Name="Original",Host="host",Username="admin" };
        Assert.True(CredentialReference.CanReuse(profile,profile with { Name="Renamed",Folder="Production",Color="#112233",Host="HOST" }));
        foreach(var changed in new[]{profile with { Host="other" },profile with { Port=2222 },profile with { Username="Admin" },profile with { Protocol=Protocol.RDP },profile with { Authentication=Authentication.PrivateKey },profile with { Id=Guid.NewGuid() }})Assert.False(CredentialReference.CanReuse(profile,changed));
        var key=profile with { Authentication=Authentication.PrivateKey,PrivateKeyPath="key-a" }; Assert.False(CredentialReference.CanReuse(key,key with { PrivateKeyPath="key-b" }));
        var rdp=profile with { Protocol=Protocol.RDP,Username="ACME\\Admin" }; Assert.True(CredentialReference.CanReuse(rdp,rdp with { Username="admin",Domain="acme" })); Assert.False(CredentialReference.CanReuse(rdp,rdp with { Username="Other" }));
        var vnc=profile with { Protocol=Protocol.VNC }; Assert.False(CredentialReference.CanReuse(vnc,vnc with { Vnc=vnc.Vnc with { Authentication=VncAuthentication.None } }));
    }
    public void Dispose()
    {
        DatabaseTestCleanup.Clear(root);
        if(Directory.Exists(root))Directory.Delete(root,true);
    }
}
