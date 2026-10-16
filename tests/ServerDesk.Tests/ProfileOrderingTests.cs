using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

public sealed class ProfileOrderingTests
{
    [Fact]
    public void Host_sort_and_recent_sort_produce_different_predictable_orders()
    {
        var old=new ServerProfile { Name="Alpha",Host="z-host",LastConnected=DateTimeOffset.UtcNow.AddDays(-1) };
        var recent=new ServerProfile { Name="Beta",Host="a-host",LastConnected=DateTimeOffset.UtcNow };
        var unused=new ServerProfile { Name="Unused",Host="b-host" };
        Assert.Equal(new[]{recent,unused,old},ProfileOrdering.Sort(new[]{old,recent,unused},ProfileSort.Host));
        Assert.Equal(new[]{recent,old,unused},ProfileOrdering.Sort(new[]{old,recent,unused},ProfileSort.LastConnected));
    }
    [Fact]
    public async Task History_pages_do_not_repeat_and_survive_profile_deletion()
    {
        var directory=Path.Combine(Path.GetTempPath(),"serverdesk-history-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repository=new ProfileRepository(new AppPaths(directory)); await repository.InitializeAsync();
            var one=new ServerProfile { Name="First",Host="first",Username="admin" }; var two=one with { Id=Guid.NewGuid(),Name="Second" };
            await repository.SaveAsync(one); await repository.SaveAsync(two);
            await repository.ConnectedAsync(one); await repository.ConnectedAsync(two); await repository.ConnectedAsync(one);
            var page=await repository.HistoryAsync(2); var older=await repository.HistoryAsync(2,page.Last().Id);
            Assert.Equal(2,page.Count); Assert.Single(older); Assert.Empty(page.Select(e=>e.Id).Intersect(older.Select(e=>e.Id)));
            Assert.Equal(2,(await repository.HistoryAsync(profileId:one.Id)).Count);
            await repository.DeleteAsync(two.Id); Assert.Null((await repository.HistoryAsync(profileId:two.Id)).Single().Profile);
        }
        finally { DatabaseTestCleanup.Clear(directory); if(Directory.Exists(directory))Directory.Delete(directory,true); }
    }
}
