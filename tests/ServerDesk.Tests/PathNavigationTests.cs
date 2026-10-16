using ServerDesk.Core;
using Xunit;

public sealed class PathNavigationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad\0path")]
    public void Incomplete_remote_input_has_no_navigation_targets(string? input)=>Assert.Empty(PathNavigation.Breadcrumbs(input,true));

    [Fact]
    public void Root_remains_a_clickable_navigation_target()=>Assert.Equal(new PathCrumb("/","/"),Assert.Single(PathNavigation.Breadcrumbs("/",true)));

    [Fact]
    public void Absolute_remote_unicode_paths_preserve_each_destination()
    {
        var crumbs=PathNavigation.Breadcrumbs("/home/żółty katalog/plik",true);
        Assert.Equal(new[]{"/","/home","/home/żółty katalog","/home/żółty katalog/plik"},crumbs.Select(c=>c.Path));
    }

    [Fact]
    public void Relative_remote_input_does_not_drop_its_first_character()
    {
        var crumbs=PathNavigation.Breadcrumbs("home/admin",true);
        Assert.Equal(new[]{"home","home/admin"},crumbs.Select(c=>c.Path));
    }

    [Fact]
    public void Repeated_remote_separators_do_not_create_empty_buttons()=>Assert.Equal(new[]{"/","/home","/home/admin"},PathNavigation.Breadcrumbs("//home///admin/",true).Select(c=>c.Path));

    [Fact]
    public void Local_path_uses_current_platform_path_rules()
    {
        var root=Path.GetPathRoot(Path.GetFullPath("."))!;
        var path=Path.Combine(root,"folder with space","żółć");
        Assert.Equal(path,PathNavigation.Breadcrumbs(path,false).Last().Path);
        Assert.Empty(PathNavigation.Breadcrumbs("",false));
    }
}
