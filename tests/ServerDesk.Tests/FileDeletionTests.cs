using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class FileDeletionTests
{
    private static FileEntry Entry(string path,bool directory=false,bool link=false,long size=3)=>new(path[(path.LastIndexOf('/')+1)..],path,directory,link,size,new DateTime(2026,1,1),"");
    private sealed class Store(params FileEntry[] entries) : IDeletionFileSystem
    {
        internal Dictionary<string,FileEntry> Items { get; }=entries.ToDictionary(e=>e.FullPath);
        internal List<string> Deleted { get; }=[];
        internal Dictionary<string,IReadOnlyList<FileEntry>> Lists { get; }=[];
        internal Func<FileEntry,Exception?>? Fail;
        public bool IsLocal=>false;
        public Task<FileEntry> ReadAsync(string path,CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Items[path]); }
        public Task<IReadOnlyList<FileEntry>> ListAsync(string path,CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(Lists.GetValueOrDefault(path)??Items.Values.Where(e=>RemotePath.Parent(e.FullPath)==path).ToArray()); }
        public Task DeleteAsync(FileEntry expected,CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); if(Fail?.Invoke(expected) is {} ex)throw ex; Items.Remove(expected.FullPath); Deleted.Add(expected.FullPath); return Task.CompletedTask; }
    }
    private sealed class InlineProgress(Action<FileDeletionProgress> action) : IProgress<FileDeletionProgress> { public void Report(FileDeletionProgress value)=>action(value); }
    [Fact] public async Task Preview_is_read_only_counts_hidden_content_and_orders_children_before_parents()
    {
        var folder=Entry("/scope/folder",true); var child=Entry("/scope/folder/sub",true); var file=Entry("/scope/folder/sub/.hidden"); var link=Entry("/scope/link",link:true);
        var store=new Store(folder,child,file,link); var service=new FileDeletionService(store);
        var plan=await service.PrepareAsync([folder,link],null,default);
        Assert.Empty(store.Deleted); Assert.Equal(4,store.Items.Count); Assert.Equal(1,plan.Files); Assert.Equal(2,plan.Directories); Assert.Equal(1,plan.Links);
        Assert.Equal(new[]{file.FullPath,child.FullPath,folder.FullPath,link.FullPath},plan.Entries.Select(e=>e.FullPath));
    }
    [Fact] public async Task A_directory_link_is_never_enumerated()
    {
        var link=Entry("/scope/link",true,true); var store=new Store(link); store.Lists[link.FullPath]=[Entry("/outside/valuable")];
        var service=new FileDeletionService(store); var plan=await service.PrepareAsync([link],null,default);
        Assert.Single(plan.Entries); var result=await service.ExecuteAsync(plan,null,default); Assert.True(result.Succeeded); Assert.Equal(new[]{link.FullPath},store.Deleted);
    }
    [Theory] [InlineData("/")] [InlineData("/scope/../outside")] [InlineData("/scope/./file")] [InlineData("relative")] [InlineData("//scope/file")] [InlineData("")]
    public async Task Invalid_root_and_traversal_paths_never_reach_the_store(string path)
    { var store=new Store(); await Assert.ThrowsAnyAsync<ArgumentException>(()=>new FileDeletionService(store).PrepareAsync([Entry(path)],null,default)); Assert.Empty(store.Deleted); }
    [Fact] public async Task Selection_cannot_include_unrelated_parents_duplicates_or_parent_aliases()
    {
        var a=Entry("/scope/a"); var b=Entry("/other/b"); var store=new Store(a,b); var service=new FileDeletionService(store);
        await Assert.ThrowsAnyAsync<ArgumentException>(()=>service.PrepareAsync([a,b],null,default));
        await Assert.ThrowsAnyAsync<ArgumentException>(()=>service.PrepareAsync([a,a],null,default));
        await Assert.ThrowsAnyAsync<ArgumentException>(()=>service.PrepareAsync([a with { Name=".." }],null,default)); Assert.Empty(store.Deleted);
    }
    [Theory] [InlineData("../outside","/outside")] [InlineData("outside","/other/outside")] [InlineData("nested/file","/scope/folder/nested/file")]
    public async Task Malformed_directory_entries_abort_preview_without_deleting(string name,string path)
    {
        var folder=Entry("/scope/folder",true); var store=new Store(folder); store.Lists[folder.FullPath]=[Entry(path) with { Name=name }];
        await Assert.ThrowsAnyAsync<Exception>(()=>new FileDeletionService(store).PrepareAsync([folder],null,default)); Assert.Empty(store.Deleted);
    }
    [Fact] public async Task Duplicate_children_cannot_create_an_unbounded_plan()
    {
        var folder=Entry("/scope/folder",true); var file=Entry("/scope/folder/file"); var store=new Store(folder,file); store.Lists[folder.FullPath]=[file,file];
        await Assert.ThrowsAsync<IOException>(()=>new FileDeletionService(store).PrepareAsync([folder],null,default)); Assert.Empty(store.Deleted);
    }
    [Fact] public async Task Changed_file_metadata_blocks_the_entire_plan_before_first_delete()
    {
        var a=Entry("/scope/a"); var b=Entry("/scope/b"); var store=new Store(a,b); var service=new FileDeletionService(store); var plan=await service.PrepareAsync([a,b],null,default);
        store.Items[b.FullPath]=b with { Size=99 };
        var result=await service.ExecuteAsync(plan,null,default); Assert.False(result.Succeeded); Assert.IsType<IOException>(result.Error); Assert.Equal(0,result.Deleted); Assert.Empty(store.Deleted);
    }
    [Fact] public async Task A_directory_replaced_by_a_link_blocks_deletion()
    {
        var directory=Entry("/scope/dir",true); var store=new Store(directory); var service=new FileDeletionService(store); var plan=await service.PrepareAsync([directory],null,default);
        store.Items[directory.FullPath]=directory with { IsSymbolicLink=true }; var result=await service.ExecuteAsync(plan,null,default);
        Assert.Equal(0,result.Deleted); Assert.IsType<IOException>(result.Error);
    }
    [Fact] public async Task Cancellation_after_one_delete_reports_partial_work_and_preserves_the_rest()
    {
        var a=Entry("/scope/a"); var b=Entry("/scope/b"); var store=new Store(a,b); var service=new FileDeletionService(store); var plan=await service.PrepareAsync([a,b],null,default);
        using var cancel=new CancellationTokenSource(); var result=await service.ExecuteAsync(plan,new InlineProgress(_=>cancel.Cancel()),cancel.Token);
        Assert.True(result.Cancelled); Assert.Equal(1,result.Deleted); Assert.Equal(2,result.Total); Assert.Contains(b.FullPath,store.Items.Keys);
    }
    [Fact] public async Task Failure_stops_after_acknowledged_deletions_without_retrying()
    {
        var a=Entry("/scope/a"); var b=Entry("/scope/b"); var c=Entry("/scope/c"); var store=new Store(a,b,c) { Fail=e=>e==b?new IOException("fixture disconnect"):null };
        var service=new FileDeletionService(store); var result=await service.ExecuteAsync(await service.PrepareAsync([a,b,c],null,default),null,default);
        Assert.Equal(1,result.Deleted); Assert.IsType<IOException>(result.Error); Assert.Equal(new[]{a.FullPath},store.Deleted); Assert.Contains(c.FullPath,store.Items.Keys);
    }
    [Fact] public async Task Plans_are_bound_to_the_session_that_prepared_them()
    {
        var a=Entry("/scope/a"); var store=new Store(a); var first=new FileDeletionService(store); var second=new FileDeletionService(store);
        var plan=await first.PrepareAsync([a],null,default); await Assert.ThrowsAsync<ArgumentException>(()=>second.ExecuteAsync(plan,null,default)); Assert.Empty(store.Deleted);
    }
    [Fact] public async Task Cancelled_preview_and_depth_limit_never_delete_anything()
    {
        var top=Entry("/scope/top",true); var all=new List<FileEntry> { top }; var path=top.FullPath;
        for(var i=0;i<FileDeletionService.MaximumDepth+1;i++){path+="/dir"; all.Add(Entry(path,true));}
        var store=new Store(all.ToArray()); var service=new FileDeletionService(store);
        await Assert.ThrowsAsync<IOException>(()=>service.PrepareAsync([top],null,default));
        using var cancel=new CancellationTokenSource(); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.PrepareAsync([top],null,cancel.Token)); Assert.Empty(store.Deleted);
    }
    [Fact] public async Task Entry_count_is_bounded_before_any_mutation()
    {
        var top=Entry("/scope/top",true); var all=new[]{top}.Concat(Enumerable.Range(0,FileDeletionService.MaximumEntries).Select(i=>Entry(top.FullPath+"/"+i))).ToArray();
        var store=new Store(all); await Assert.ThrowsAsync<IOException>(()=>new FileDeletionService(store).PrepareAsync([top],null,default)); Assert.Empty(store.Deleted);
    }
    [Fact] public async Task A_new_file_after_preview_is_never_added_to_the_deletion_plan()
    {
        var root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"serverdesk-delete-"+Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(root);
        try
        {
            var folder=Directory.CreateDirectory(Path.Combine(root,"selected")).FullName; var original=Path.Combine(folder,"original.txt"); await File.WriteAllTextAsync(original,"original");
            var fileSystem=new LocalDeletionFileSystem(); var service=new FileDeletionService(fileSystem); var plan=await service.PrepareAsync([await fileSystem.ReadAsync(folder,default)],null,default);
            var added=Path.Combine(folder,"new.txt"); await File.WriteAllTextAsync(added,"new file must survive");
            var result=await service.ExecuteAsync(plan,null,default);
            Assert.False(result.Succeeded); Assert.Equal(1,result.Deleted); Assert.NotNull(result.Error); Assert.False(File.Exists(original)); Assert.Equal("new file must survive",await File.ReadAllTextAsync(added));
        }
        finally
        {
            if(!root.StartsWith(Path.GetFullPath(Path.GetTempPath()),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) || !Path.GetFileName(root).StartsWith("serverdesk-delete-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe fixture cleanup");
            Directory.Delete(root,true);
        }
    }
    [Fact] public async Task Local_nonempty_directories_are_deleted_without_touching_siblings()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-delete-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var top=Directory.CreateDirectory(Path.Combine(root,"folder")).FullName; var sub=Directory.CreateDirectory(Path.Combine(top,"nested")).FullName;
            await File.WriteAllTextAsync(Path.Combine(sub,".hidden"),"owned fixture"); var sentinel=Path.Combine(root,"keep.txt"); await File.WriteAllTextAsync(sentinel,"keep");
            var store=new LocalDeletionFileSystem(); var service=new FileDeletionService(store); var plan=await service.PrepareAsync([await store.ReadAsync(top,default)],null,default);
            Assert.True(Directory.Exists(top)); Assert.True((await service.ExecuteAsync(plan,null,default)).Succeeded); Assert.False(Directory.Exists(top)); Assert.Equal("keep",await File.ReadAllTextAsync(sentinel));
            Assert.Throws<ArgumentException>(()=>FileDeletionService.Normalize(Path.GetPathRoot(top)!,true));
        }
        finally
        {
            var cleanup=Path.GetFullPath(root); var temporary=Path.GetFullPath(Path.GetTempPath());
            if(!cleanup.StartsWith(temporary,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) || !Path.GetFileName(cleanup).StartsWith("serverdesk-delete-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe fixture cleanup");
            if(Directory.Exists(cleanup))Directory.Delete(cleanup,true);
        }
    }
}
