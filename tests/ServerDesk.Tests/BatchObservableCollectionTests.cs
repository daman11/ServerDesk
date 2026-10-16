using System.Collections.Specialized;
using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class BatchObservableCollectionTests
{
    [Fact] public void ReplacementPublishesOnlyFinalListInOneReset()
    {
        var collection=new BatchObservableCollection<int> { 9 }; var changes=0;
        collection.CollectionChanged+=(_,e)=> { changes++; Assert.Equal(NotifyCollectionChangedAction.Reset,e.Action); Assert.Equal(10000,collection.Count); };
        collection.ReplaceWith(Enumerable.Range(0,10000)); Assert.Equal(1,changes); Assert.Equal(9999,collection[^1]);
    }
    [Fact] public void SelfReplacementAndEmptyReplacementWork()
    {
        var collection=new BatchObservableCollection<int> { 1,2,3 }; collection.ReplaceWith(collection); Assert.Equal([1,2,3],collection); collection.ReplaceWith([]); Assert.Empty(collection);
    }
    [Fact] public void FailedEnumerationPreservesPreviousList()
    {
        var collection=new BatchObservableCollection<int> { 7 }; var changes=0; collection.CollectionChanged+=(_,_)=>changes++;
        static IEnumerable<int> Broken() { yield return 1; throw new IOException("fixture"); }
        Assert.Throws<IOException>(()=>collection.ReplaceWith(Broken())); Assert.Equal([7],collection); Assert.Equal(0,changes);
    }
}
