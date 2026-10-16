using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;

public sealed class FileSelectionTests
{
    private static FileEntry Entry(string path,string? name=null,bool directory=false,bool link=false)=>new(name??Path.GetFileName(path),path,directory,link,1,DateTime.UtcNow,"");
    [Fact] public void CaptureRemovesParentAndDuplicatesWithoutMutatingInput()
    {
        var one=Entry("/srv/a"); var input=new[]{Entry("/srv/..","..",true),one,one,Entry("/srv/b")};
        var selection=FileSelection.Capture(input,false); Assert.Equal(new[]{"a","b"},selection.Select(e=>e.Name)); Assert.Equal(4,input.Length);
    }
    [Fact] public void RetainUsesNewMetadataAndDropsHiddenOrDeletedEntries()
    {
        var old=Entry("/srv/a"); var updated=old with { Size=200 }; var selected=new[]{old,Entry("/srv/gone")};
        var result=FileSelection.Retain(new[]{updated,Entry("/srv/new")},selected,false); Assert.Same(updated,Assert.Single(result));
    }
    [Fact] public void RemoteNamesRemainCaseSensitive()
    {
        var entries=new[]{Entry("/srv/A"),Entry("/srv/a")}; Assert.Equal(2,FileSelection.Capture(entries,false).Count);
        Assert.Single(FileSelection.Retain(entries[..1],entries,false));
    }
    [Fact] public void LocalNamesFollowPlatformCaseSensitivity()
    {
        var entries=new[]{Entry(Path.GetFullPath("A")),Entry(Path.GetFullPath("a"))};
        Assert.Equal(OperatingSystem.IsWindows()?1:2,FileSelection.Capture(entries,true).Count);
    }
    [Fact] public void UploadCapturesAllDestinationsAndSourcesBeforeTheQueueStarts()
    {
        var entries=new List<FileEntry> { Entry("folder/żółć.txt","żółć.txt"),Entry("folder/directory","directory",true) };
        var plan=FileSelection.Upload(entries,"/original"); entries.Clear();
        Assert.Equal(new[]{"/original/żółć.txt","/original/directory"},plan.Select(p=>p.Target)); Assert.All(plan,p=>Assert.True(Path.IsPathFullyQualified(p.Entry.FullPath)));
    }
    [Fact] public void DownloadCapturesLocalTargetsWithoutMutatingEntries()
    {
        var entry=Entry("/srv/żółć.txt"); var plan=FileSelection.Download([entry],"downloads");
        Assert.Equal(Path.GetFullPath(Path.Combine("downloads","żółć.txt")),Assert.Single(plan).Target); Assert.Equal("/srv/żółć.txt",entry.FullPath);
    }
    [Fact] public void DuplicateDestinationsAreRejectedBeforeAnyJobsAreReturned()
        =>Assert.Throws<InvalidOperationException>(()=>FileSelection.Upload([Entry("a/config.json","config.json"),Entry("b/config.json","config.json")],"/target"));
    [Fact] public void DownloadCollisionUsesTheLocalFilesystemRules()
    {
        var entries=new[]{Entry("/srv/A"),Entry("/srv/a")};
        if(OperatingSystem.IsWindows())Assert.Throws<InvalidOperationException>(()=>FileSelection.Download(entries,"downloads"));
        else Assert.Equal(2,FileSelection.Download(entries,"downloads").Count);
    }
    [Fact] public void ExplicitSymlinkInABatchIsRejected()
    {
        var entries=new[]{Entry("/srv/regular"),Entry("/srv/link",link:true)};
        Assert.Throws<InvalidOperationException>(()=>FileSelection.Upload(entries,"/target")); Assert.Throws<InvalidOperationException>(()=>FileSelection.Download(entries,"downloads"));
    }
    [Theory] [InlineData("../escape")] [InlineData("sub/file")] [InlineData("sub\\file")] [InlineData(".")] [InlineData("")]
    public void DownloadCannotEscapeDestinationThroughAFileName(string name)
        =>Assert.Throws<ArgumentException>(()=>FileSelection.Download([Entry("/srv/source",name)],"downloads"));
    [Fact] public void TenThousandEntriesKeepOnlyVisibleSelectedPaths()
    {
        var entries=Enumerable.Range(0,10000).Select(i=>Entry("/srv/file-"+i)).ToArray();
        var result=FileSelection.Retain(entries.Where((_,i)=>i%2==0),entries.Where((_,i)=>i%3==0),false);
        Assert.Equal(1667,result.Count); Assert.All(result,e=>Assert.Equal(0,int.Parse(e.Name[5..])%6));
    }
}
