using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;

public sealed class DirectoryNavigationHistoryTests
{
    [Fact] public void PreviewsDoNotCommitFailedNavigation()
    {
        var history=new DirectoryNavigationHistory(); Assert.Null(history.Back); Assert.Null(history.Forward);
        history.Commit("/a"); history.Commit("/b"); var back=history.Back;
        Assert.Equal("/a",back!.Value.Path); Assert.Equal("/b",history.Current); Assert.Null(history.Forward);
        Assert.Equal(back,history.Back); history.Commit(back.Value.Path,back); Assert.Equal("/a",history.Current); Assert.Equal("/b",history.Forward!.Value.Path);
        var forward=history.Forward; history.Commit(forward.Value.Path,forward); Assert.Equal("/b",history.Current);
    }
    [Fact] public void NewVisitAfterBackRemovesOnlyForwardBranch()
    {
        var history=new DirectoryNavigationHistory(); history.Commit("/a"); history.Commit("/b"); history.Commit("/c");
        var back=history.Back!.Value; history.Commit(back.Path,back); history.Commit("/d");
        Assert.Equal(3,history.Count); Assert.Null(history.Forward); Assert.Equal("/b",history.Back!.Value.Path);
    }
    [Fact] public void RefreshDoesNotDuplicateOrRemoveForward()
    {
        var history=new DirectoryNavigationHistory(); history.Commit("/a"); history.Commit("/b"); var back=history.Back!.Value;
        history.Commit(back.Path,back); history.Commit("/a"); Assert.Equal(2,history.Count); Assert.Equal("/b",history.Forward!.Value.Path);
    }
    [Fact] public void StaleTargetCannotMoveCursorInAnotherBranch()
    {
        var history=new DirectoryNavigationHistory(); history.Commit("/a"); history.Commit("/b"); var old=history.Back;
        history.Commit("/c"); history.Commit("/a",old); Assert.Equal(4,history.Count); Assert.Equal("/c",history.Back!.Value.Path);
    }
    [Fact] public void ResolvedTargetChangeCreatesAVisit()
    {
        var history=new DirectoryNavigationHistory(); history.Commit("/a"); history.Commit("/b");
        history.Commit("/other",history.Back); Assert.Equal("/b",history.Back!.Value.Path); Assert.Equal("/other",history.Current);
    }
    [Fact] public void HistoryIsBoundedAndClearInvalidatesTargets()
    {
        var history=new DirectoryNavigationHistory(); for(var i=0;i<120;i++)history.Commit("/"+i);
        Assert.Equal(100,history.Count); var old=history.Back;
        for(var i=0;i<99;i++){var back=history.Back!.Value; history.Commit(back.Path,back);}
        Assert.Equal("/20",history.Current); Assert.Null(history.Back); history.Clear(); history.Commit("/new"); history.Commit("/118",old);
        Assert.Equal(2,history.Count); Assert.Equal("/new",history.Back!.Value.Path);
    }
    [Theory] [InlineData(true,1)] [InlineData(false,2)] public void LocalAndRemoteCaseRulesAreExplicit(bool insensitive,int count)
    {
        var history=new DirectoryNavigationHistory(insensitive?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        history.Commit("/Data"); history.Commit("/data"); Assert.Equal(count,history.Count);
    }
    [Fact] public void InvalidPathDoesNotChangeHistory()
    {
        var history=new DirectoryNavigationHistory(); history.Commit("/a"); Assert.Throws<ArgumentException>(()=>history.Commit(" ")); Assert.Equal("/a",history.Current);
    }
}
