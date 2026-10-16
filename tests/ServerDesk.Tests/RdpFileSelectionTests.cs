using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class RdpFileSelectionTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"ServerDesk-rdp-files-"+Guid.NewGuid().ToString("N"));
    public RdpFileSelectionTests()=>Directory.CreateDirectory(root);
    [Fact] public void UnicodeSpacesFoldersAndDuplicatePathsAreInspectedOnce()
    {
        var folder=Directory.CreateDirectory(Path.Combine(root,"żółć folder")).FullName;
        var file=Path.Combine(folder,"plik ą.txt"); File.WriteAllText(file,"Hello");
        var result=RdpFileSelection.Inspect([folder,file,folder]); Assert.Equal(2,result.Paths.Count); Assert.Equal(5,result.Bytes); Assert.Equal(1,result.Files);
    }
    [Fact] public void MissingSelectionDoesNotPartiallyPass()
    { var file=Path.Combine(root,"ok.txt"); File.WriteAllText(file,"ok"); Assert.Throws<FileNotFoundException>(()=>RdpFileSelection.Inspect([file,Path.Combine(root,"missing")])); }
    [Fact] public void OversizedClipboardFilesAreRejectedWithoutReadingContents()
    { var file=Path.Combine(root,"large.bin"); using(var stream=File.Create(file))stream.SetLength(RdpFileSelection.MaxBytes+1); Assert.Throws<ArgumentException>(()=>RdpFileSelection.Inspect([file])); }
    [Fact] public void CancelledFolderScanStopsBeforeClipboardUpdate()
    { using var cancel=new CancellationTokenSource(); cancel.Cancel(); Assert.Throws<OperationCanceledException>(()=>RdpFileSelection.Inspect([root],cancel.Token)); }
    [Fact] public void EmptySelectionIsRejected()=>Assert.Throws<ArgumentException>(()=>RdpFileSelection.Inspect([]));
    public void Dispose()=>Directory.Delete(root,true);
}
