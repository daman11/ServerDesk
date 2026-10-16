using ServerDesk.Core;
using Xunit;
namespace ServerDesk.Tests;
public sealed class FileIconTests
{
    [Theory]
    [InlineData("settings.JSON","file-braces")]
    [InlineData("sshd.conf","file-cog")]
    [InlineData("deploy.sh","file-code")]
    [InlineData("client.pem","file-key")]
    [InlineData(".env.production","file-key")]
    [InlineData("Dockerfile","container")]
    [InlineData("backup.tar.gz","file-archive")]
    [InlineData("żółć.png","image")]
    [InlineData("unknown.bin","file")]
    public void File_icons_are_selected_without_reading_contents(string name,string expected)=>Assert.Equal(expected,FileIcons.Resolve(new(name,"/"+name,false,false,0,DateTime.MinValue,"")));
    [Fact]
    public void Link_and_directory_kinds_take_precedence_over_extensions()
    {
        Assert.Equal("link",FileIcons.Resolve(new("secret.pem","/secret.pem",true,true,0,DateTime.MinValue,"")));
        Assert.Equal("folder",FileIcons.Resolve(new("folder.zip","/folder.zip",true,false,0,DateTime.MinValue,"")));
    }
}
