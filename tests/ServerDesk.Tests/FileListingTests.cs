using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class FileListingTests
{
    private static FileEntry Entry(string name,bool directory=false,long size=0,int day=1)=>new(name,"/"+name,directory,false,size,new(2026,1,day),"");
    private static readonly FileEntry[] Entries=[Entry("small.txt",size:1,day:3),Entry("..",true),Entry("B",true),Entry("a",true),Entry("Z.log",size:100,day:2),Entry(".secret",size:3),Entry("żółć.TXT",size:10)];
    [Theory]
    [InlineData(FileSort.Name,false,"small.txt")]
    [InlineData(FileSort.Name,true,"żółć.TXT")]
    [InlineData(FileSort.Size,false,"small.txt")]
    [InlineData(FileSort.Size,true,"Z.log")]
    [InlineData(FileSort.Modified,false,"żółć.TXT")]
    [InlineData(FileSort.Modified,true,"small.txt")]
    [InlineData(FileSort.Type,false,"Z.log")]
    public void ParentAndDirectoriesStayAboveSortedFiles(FileSort sort,bool descending,string firstFile)
    {
        var result=FileListing.Apply(Entries,"",false,sort,descending);
        Assert.Equal("..",result[0].Name); Assert.All(result.Skip(1).Take(2),e=>Assert.True(e.IsDirectory));
        Assert.Equal(firstFile,result[3].Name); Assert.DoesNotContain(result,e=>e.Name==".secret");
    }
    [Fact] public void FiltersAreCaseInsensitiveAndPreserveParent()
    { Assert.Equal(new[]{"..","żółć.TXT"},FileListing.Apply(Entries," ŻÓŁĆ ",false).Select(e=>e.Name)); }
    [Fact] public void HiddenFilterDoesNotMutateTheSource()
    { Assert.Contains(FileListing.Apply(Entries,"secret",true),e=>e.Name==".secret"); Assert.Equal(7,Entries.Length); Assert.Single(FileListing.Apply(Entries,"secret",false)); }
    [Fact] public void MixedOutcomesCannotReportFullSuccess()
    {
        var skipped=new TransferOutcome(Skipped:1); Assert.Equal(TransferState.Skipped,skipped.State);
        var partial=new TransferOutcome(Files:2)+skipped; Assert.Equal(TransferState.CompletedWithSkips,partial.State); Assert.Equal(2,partial.Files);
        Assert.Equal(TransferState.Completed,new TransferOutcome(Directories:1).State);
    }
}
