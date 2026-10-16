using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class TransferTraversalTests
{
    [Fact] public void CountIsBoundedIncludingSkippedEntries()
    {
        var walk=new TransferTraversal(false);
        for(var i=0;i<TransferTraversal.MaximumEntries;i++)walk.Visit(default);
        Assert.Throws<IOException>(()=>walk.Visit(default));
    }
    [Fact] public void CancellationStopsBeforeVisitingAnEntry()
    {
        using var ct=new CancellationTokenSource(); ct.Cancel();
        Assert.Throws<OperationCanceledException>(()=>new TransferTraversal(false).Visit(ct.Token));
    }
    [Fact] public void DepthIsBoundedAndScopesRestoreIt()
    {
        var walk=new TransferTraversal(false); var scopes=new List<IDisposable>();
        for(var i=0;i<TransferTraversal.MaximumDepth;i++)scopes.Add(walk.EnterDirectory("/root/"+i));
        Assert.Throws<IOException>(()=>walk.EnterDirectory("/too-deep"));
        scopes[^1].Dispose(); using var next=walk.EnterDirectory("/replacement");
        foreach(var scope in scopes)scope.Dispose();
    }
    [Fact] public void ActiveAncestorCyclesAreRejectedButLaterAliasesAreAllowed()
    {
        var walk=new TransferTraversal(false);
        using(var root=walk.EnterDirectory("/physical/root"))
        {
            Assert.Throws<IOException>(()=>walk.EnterDirectory("/physical/root/"));
            root.Dispose(); // Double Dispose must not remove a later scope.
            using var alias=walk.EnterDirectory("/physical/root"); root.Dispose();
            Assert.Throws<IOException>(()=>walk.EnterDirectory("/physical/root"));
        }
        using var again=walk.EnterDirectory("/physical/root");
    }
    [Fact] public void RemoteCaseIsDistinct()
    {
        var walk=new TransferTraversal(false);
        using var one=walk.EnterDirectory("/A"); using var two=walk.EnterDirectory("/a");
    }
    [Fact] public void LocalCaseFollowsTheFilesystemPlatform()
    {
        var walk=new TransferTraversal(true); using var one=walk.EnterDirectory(Path.GetFullPath("A"));
        if(OperatingSystem.IsWindows())Assert.Throws<IOException>(()=>walk.EnterDirectory(Path.GetFullPath("a")));
        else { using var two=walk.EnterDirectory(Path.GetFullPath("a")); }
    }
    [Fact] public void LocalRegularSourcesHaveFreshMetadataAndResolvedPaths()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-source-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var file=Path.Combine(root,"żółć text.txt"); File.WriteAllBytes(file,[1,2,3]);
            var regular=LocalTransferSource.Read(file); Assert.False(regular.IsSymbolicLink); Assert.False(regular.IsDirectory); Assert.Equal(3,regular.Size);
            Assert.Equal(file,LocalTransferSource.Read(file,true).FullPath);
            Assert.True(LocalTransferSource.Read(root).IsDirectory);
            Assert.Throws<FileNotFoundException>(()=>LocalTransferSource.Read(Path.Combine(root,"missing")));
        }
        finally { Directory.Delete(root,true); }
    }
}
