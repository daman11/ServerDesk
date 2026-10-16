using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private const int BackgroundFileViewThreshold=2000;
    private readonly SemaphoreSlim fileViewGate=new(1);
    private readonly HashSet<Task> fileViewTasks=[];
    private CancellationTokenSource? fileViewCancellation;
    private Task fileViewUpdate=Task.CompletedTask;
    private long fileViewVersion;

    private sealed record FileViewRequest(IReadOnlyList<FileEntry> Entries,string? Query,bool Hidden,FileSort Sort,bool Descending)
    {
        internal IReadOnlyList<FileEntry> Build(CancellationToken ct)=>FileListing.Apply(Entries,Query,Hidden,Sort,Descending,ct);
    }

    private void CancelFileViews()
    {
        fileViewVersion++; fileViewCancellation?.Cancel(); fileViewCancellation=null;
    }

    private Task FilterFilesAsync()
    {
        if(disposed!=0)return Task.CompletedTask;
        CancelFileViews(); UpdateFileSortControls();
        var showHidden=hidden.IsChecked==true;
        if(Math.Max(localEntries.Count,remoteEntries.Count)<BackgroundFileViewThreshold)
        {
            PublishFileViews(FileListing.Apply(localEntries,localFilter.Text,showHidden,(FileSort)Math.Max(0,localSort.SelectedIndex),localDescending),
                FileListing.Apply(remoteEntries,remoteFilter.Text,showHidden,(FileSort)Math.Max(0,remoteSort.SelectedIndex),remoteDescending));
            return fileViewUpdate=Task.CompletedTask;
        }
        // Only immutable snapshots cross the dispatcher boundary. At most one
        // request sorts at a time; superseded waiting requests never start sorting.
        var local=new FileViewRequest(localEntries,localFilter.Text,showHidden,(FileSort)Math.Max(0,localSort.SelectedIndex),localDescending);
        var remote=new FileViewRequest(remoteEntries,remoteFilter.Text,showHidden,(FileSort)Math.Max(0,remoteSort.SelectedIndex),remoteDescending);
        var cancel=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); fileViewCancellation=cancel;
        var work=BuildFileViewsAsync(local,remote,localPath.Text,remotePath.Text,fileViewVersion,cancel);
        fileViewTasks.Add(work); return fileViewUpdate=ObserveFileViewAsync(work);
    }

    private async Task ObserveFileViewAsync(Task work)
    {
        try { await work; }
        finally { fileViewTasks.Remove(work); }
    }

    private async Task BuildFileViewsAsync(FileViewRequest local,FileViewRequest remote,string? localDirectory,string? remoteDirectory,long version,CancellationTokenSource cancel)
    {
        using(cancel)
        using(BeginBusy(L.T("Filtrowanie i sortowanie plików…")))
        {
            var ct=cancel.Token;
            try
            {
                await Task.Delay(80,ct);
                await fileViewGate.WaitAsync(ct);
                (IReadOnlyList<FileEntry> Local,IReadOnlyList<FileEntry> Remote) result;
                try
                {
                    var localSnapshot=local with { Entries=local.Entries.ToArray() };
                    var remoteSnapshot=remote with { Entries=remote.Entries.ToArray() };
                    result=await Task.Run(()=> (localSnapshot.Build(ct),remoteSnapshot.Build(ct)),ct);
                }
                finally { fileViewGate.Release(); }
                ct.ThrowIfCancellationRequested();
                if(disposed==0 && version==fileViewVersion && localPath.Text==localDirectory && remotePath.Text==remoteDirectory)
                    PublishFileViews(result.Local,result.Remote);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
            finally { if(ReferenceEquals(fileViewCancellation,cancel))fileViewCancellation=null; }
        }
    }
}
