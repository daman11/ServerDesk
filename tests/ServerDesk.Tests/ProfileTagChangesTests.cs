using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ProfileTagChangesTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-tag-changes-"+Guid.NewGuid().ToString("N"));
    private ProfileRepository Repo()=>new(new AppPaths(root));
    private static ServerProfile Profile(string tags)=>new() { Name="Batch fixture",Host="fixture.invalid",Username="fixture",Tags=tags,Folder="Lab",CredentialId=Guid.NewGuid() };
    private static async Task Sql(ProfileRepository repo,string sql)
    {
        await using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()); await db.OpenAsync();
        using var command=db.CreateCommand(); command.CommandText=sql; await command.ExecuteNonQueryAsync();
    }
    [Fact] public void Plan_preserves_existing_spelling_and_uses_exact_case_insensitive_membership()
    {
        var p=Profile("Prod; linux, production, LINUX");
        var change=Assert.Single(ProfileTags.PlanChanges([p],"LINUX, backup, Backup","PROD"));
        Assert.Equal(p.Tags,change.OriginalTags); Assert.Equal("linux, production, backup",change.UpdatedTags);
        Assert.Equal(p.Id,change.ProfileId); Assert.Equal("Prod; linux, production, LINUX",p.Tags);
    }
    [Theory][InlineData("PROD","")][InlineData("","missing")][InlineData("","")]
    public void Noop_does_not_rewrite_legacy_formatting(string add,string remove)
        =>Assert.Empty(ProfileTags.PlanChanges([Profile("prod; prod")],add,remove));
    [Fact] public void Overlap_and_duplicate_targets_are_rejected()
    {
        var p=Profile("prod"); Assert.Throws<ArgumentException>(()=>ProfileTags.PlanChanges([p],"backup","BACKUP"));
        Assert.Throws<ArgumentException>(()=>ProfileTags.PlanChanges([p,p],"backup",""));
    }
    [Fact] public async Task Batch_is_durable_preserves_other_fields_unknown_json_colors_and_unselected_profiles()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=Profile("prod, linux"); var other=Profile("untouched");
        await repo.SaveAsync(p); await repo.SaveAsync(other); await repo.SaveTagColorsAsync(new Dictionary<string,string> { ["prod"]="#AABBCC" });
        await Sql(repo,$"UPDATE profiles SET json=json_set(json,'$.futureMetadata','keep') WHERE id='{p.Id}'");
        var plan=ProfileTags.PlanChanges([p],"backup","prod");
        await repo.SaveAsync(other with { Description="unrelated edit" });
        await Sql(repo,$"UPDATE profiles SET json=json_set(json,'$.Description','concurrent edit') WHERE id='{p.Id}'");
        await repo.ApplyTagChangesAsync(plan);
        var reopened=Repo(); await reopened.InitializeAsync(); var saved=await reopened.ListAsync(); var changed=saved.Single(x=>x.Id==p.Id);
        Assert.Equal(p with { Tags="linux, backup",Description="concurrent edit" },changed); Assert.Equal("untouched",saved.Single(x=>x.Id==other.Id).Tags);
        Assert.Equal("unrelated edit",saved.Single(x=>x.Id==other.Id).Description); Assert.Equal("#AABBCC",(await reopened.TagColorsAsync())["prod"]);
        await using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()); await db.OpenAsync();
        using var command=db.CreateCommand(); command.CommandText="SELECT json FROM profiles WHERE id=$id"; command.Parameters.AddWithValue("$id",p.Id.ToString());
        Assert.Equal("keep",JsonNode.Parse((string)(await command.ExecuteScalarAsync())!)!["futureMetadata"]!.GetValue<string>());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Stale_or_deleted_target_rolls_back_the_entire_batch(bool deleted)
    {
        var repo=Repo(); await repo.InitializeAsync(); var first=Profile("prod"); var second=Profile("lab"); await repo.SaveAsync(first); await repo.SaveAsync(second);
        var plan=ProfileTags.PlanChanges([first,second],"backup","");
        if(deleted)await repo.DeleteAsync(second.Id); else await repo.SaveAsync(second with { Tags="external edit" });
        await Assert.ThrowsAsync<ProfileTagConflictException>(()=>repo.ApplyTagChangesAsync(plan));
        var saved=await repo.ListAsync(); Assert.Equal("prod",saved.Single(p=>p.Id==first.Id).Tags);
        Assert.Equal(deleted?1:2,saved.Count); if(!deleted)Assert.Equal("external edit",saved.Single(p=>p.Id==second.Id).Tags);
    }
    [Fact] public async Task Mid_batch_write_failure_rolls_back_and_explicit_retry_succeeds()
    {
        var repo=Repo(); await repo.InitializeAsync(); var first=Profile("prod"); var second=Profile("lab"); await repo.SaveAsync(first); await repo.SaveAsync(second);
        var plan=ProfileTags.PlanChanges([first,second],"backup","");
        await Sql(repo,$"CREATE TRIGGER reject_tag_batch BEFORE UPDATE ON profiles WHEN NEW.id='{second.Id}' BEGIN SELECT RAISE(ABORT,'fixture refusal'); END");
        await Assert.ThrowsAsync<SqliteException>(()=>repo.ApplyTagChangesAsync(plan)); Assert.DoesNotContain(await repo.ListAsync(),p=>p.Tags.Contains("backup"));
        await Sql(repo,"DROP TRIGGER reject_tag_batch"); await repo.ApplyTagChangesAsync(plan); Assert.All(await repo.ListAsync(),p=>Assert.Contains("backup",p.Tags));
    }
    [Fact] public async Task Cancellation_invalid_input_and_empty_batch_do_not_write()
    {
        var repo=Repo(); await repo.ApplyTagChangesAsync([]); Assert.False(File.Exists(repo.DatabasePath)); await repo.InitializeAsync(); var p=Profile("prod"); await repo.SaveAsync(p);
        using var canceled=new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repo.ApplyTagChangesAsync(ProfileTags.PlanChanges([p],"backup",""),canceled.Token));
        var change=new ProfileTagChange(p.Id,p.Tags,"backup"); await Assert.ThrowsAsync<ArgumentException>(()=>repo.ApplyTagChangesAsync([change,change]));
        Assert.Equal(p,Assert.Single(await repo.ListAsync()));
    }
    [Fact] public void Large_plan_changes_only_selected_profiles_without_mutating_snapshots()
    {
        var profiles=Enumerable.Range(0,5000).Select(i=>Profile(i%2==0?"prod":"lab")).ToArray();
        var plan=ProfileTags.PlanChanges(profiles.Where((_,i)=>i%2==0),"backup","prod"); Assert.Equal(2500,plan.Count);
        Assert.All(plan,c=>Assert.Equal("backup",c.UpdatedTags)); Assert.Equal(2500,profiles.Count(p=>p.Tags=="prod"));
    }
    public void Dispose() { SqliteConnection.ClearAllPools(); if(Directory.Exists(root))Directory.Delete(root,true); }
}
