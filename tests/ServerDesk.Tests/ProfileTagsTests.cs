using System.Text;
using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ProfileTagsTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-tags-"+Guid.NewGuid());
    private ProfileRepository Repo()=>new(new AppPaths(root));
    private static ServerProfile Profile(string tags)=>new() { Name="Tag fixture",Host="fixture.invalid",Username="fixture",Tags=tags };
    [Fact] public void Legacy_delimiters_duplicates_case_and_multiword_labels_are_supported()
    {
        Assert.Equal(new[]{"Produkcja","linux","baza danych","ważny"},ProfileTags.Parse(" Produkcja , linux; LINUX\n baza danych\r\nważny,,"));
        Assert.Empty(ProfileTags.Parse(" ;,\r\n ")); Assert.Empty(ProfileTags.Parse(null));
        Assert.Equal("prod, Linux",ProfileTags.Format(new[]{" prod ","Linux","PROD"}));
    }
    [Theory]
    [InlineData(TagFilterMode.Any,"prod,linux",true)]
    [InlineData(TagFilterMode.Any,"production,linux",true)]
    [InlineData(TagFilterMode.Any,"production",false)]
    [InlineData(TagFilterMode.All,"PROD;Linux",true)]
    [InlineData(TagFilterMode.All,"prod",false)]
    [InlineData(TagFilterMode.Untagged,"prod",false)]
    [InlineData(TagFilterMode.Untagged," ;, ",true)]
    public void Tag_filters_use_exact_case_insensitive_membership(TagFilterMode mode,string tags,bool expected)
        =>Assert.Equal(expected,ProfileTags.CompileFilter(["prod","linux"],mode)(Profile(tags)));
    [Theory][InlineData(TagFilterMode.Any)][InlineData(TagFilterMode.All)]
    public void Empty_selection_keeps_all_profiles(TagFilterMode mode)
    { Assert.True(ProfileTags.CompileFilter([],mode)(Profile("prod"))); Assert.True(ProfileTags.CompileFilter([],mode)(Profile(""))); }
    [Fact] public void Auto_colors_are_stable_and_explicit_colors_ignore_case()
    {
        Assert.Equal(ProfileTags.Color("ważny"),ProfileTags.Color("WAŻNY")); Assert.Contains(ProfileTags.Color("prod"),ProfileAppearance.Colors);
        var colors=ProfileTags.ValidateColors(new Dictionary<string,string> { ["Prod"]="#aabbcc" }); Assert.Equal("#AABBCC",ProfileTags.Color("PROD",colors));
    }
    [Theory][InlineData("prod,linux","#112233")][InlineData(" prod","#112233")][InlineData("prod","red")][InlineData("prod","")][InlineData("bad\0tag","#112233")]
    public void Invalid_color_metadata_is_rejected(string name,string color)
        =>Assert.Throws<ArgumentException>(()=>ProfileTags.ValidateColors(new Dictionary<string,string> { [name]=color }));
    [Fact] public void Duplicate_color_names_are_rejected_even_with_different_case()
        =>Assert.Throws<ArgumentException>(()=>ProfileTags.ValidateColors(new Dictionary<string,string> { ["prod"]="#112233",["PROD"]="#445566" }));
    [Fact] public async Task Colors_roundtrip_and_exports_merge_with_local_colors_winning()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SaveAsync(Profile("prod, linux"));
        await repo.SaveTagColorsAsync(new Dictionary<string,string> { ["Prod"]="#112233",["Linux"]="#445566" });
        Assert.Equal("#112233",(await Repo().TagColorsAsync())["PROD"]);
        using var export=new MemoryStream(); await repo.ExportAsync(export);
        using var document=JsonDocument.Parse(export.ToArray()); Assert.Equal(2,document.RootElement.GetProperty("Version").GetInt32());
        await repo.SaveTagColorsAsync(new Dictionary<string,string> { ["PROD"]="#AABBCC" });
        export.Position=0; await repo.ImportAsync(export); var colors=await repo.TagColorsAsync();
        Assert.Equal("#AABBCC",colors["prod"]); Assert.Equal("#445566",colors["linux"]); Assert.Equal(2,(await repo.ListAsync()).Count);
        var clean=new ProfileRepository(new AppPaths(Path.Combine(root,"clean"))); await clean.InitializeAsync(); export.Position=0; await clean.ImportAsync(export);
        Assert.Equal("#112233",(await clean.TagColorsAsync())["prod"]); Assert.Equal("prod, linux",Assert.Single(await clean.ListAsync()).Tags);
    }
    [Fact] public async Task Invalid_import_or_save_does_not_change_profiles_or_colors()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SaveAsync(Profile("old")); await repo.SaveTagColorsAsync(new Dictionary<string,string> { ["old"]="#112233" });
        var invalid=new Dictionary<string,string> { ["bad"]="invalid" };
        await Assert.ThrowsAsync<ArgumentException>(async()=>await repo.SaveTagColorsAsync(invalid));
        var data=new ProfileRepository.ConfigurationExport(2,[Profile("new")],["New/Folder"]) { TagColors=invalid };
        using var stream=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(data)); await Assert.ThrowsAsync<ArgumentException>(()=>repo.ImportAsync(stream));
        Assert.Equal("old",Assert.Single(await repo.ListAsync()).Tags); Assert.Empty(await repo.FoldersAsync()); Assert.Equal("#112233",Assert.Single(await repo.TagColorsAsync()).Value);
    }
    [Fact] public async Task Legacy_configuration_without_colors_preserves_its_tags_and_local_metadata()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SaveTagColorsAsync(new Dictionary<string,string> { ["local"]="#112233" });
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":1,\"Servers\":[{\"Name\":\"Legacy\",\"Host\":\"fixture.invalid\",\"Username\":\"fixture\",\"Tags\":\"Prod; Linux\"}],\"Folders\":[]}"));
        await repo.ImportAsync(stream); Assert.Equal("Prod; Linux",Assert.Single(await repo.ListAsync()).Tags); Assert.Equal("#112233",(await repo.TagColorsAsync())["local"]);
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); DatabaseTestCleanup.Clear(Path.Combine(root,"clean")); if(Directory.Exists(root))Directory.Delete(root,true); }
}
