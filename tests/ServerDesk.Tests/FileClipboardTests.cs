using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;

public sealed class FileClipboardTests
{
    private static FileEntry Entry(string name,string path,bool link=false)=>new(name,path,false,link,1,DateTime.MinValue,"");
    [Fact] public void Text_copy_excludes_parent_and_duplicates_but_keeps_links_and_selection_order()
    {
        var one=Entry("żółć.txt","/srv/app space/żółć.txt"); var link=Entry("link","/srv/app space/link",true);
        var entries=new[]{Entry("..","/srv"),link,one,one};
        Assert.Equal("link"+Environment.NewLine+"żółć.txt",FileSelection.ClipboardText(entries,false,false));
        Assert.Equal(link.FullPath+Environment.NewLine+one.FullPath,FileSelection.ClipboardText(entries,false,true));
        Assert.Equal(4,entries.Length); Assert.True(link.IsSymbolicLink);
    }
    [Theory][InlineData(@"C:\Work space\żółć.txt")][InlineData("/srv/app space/A.txt")][InlineData("/srv/with'quote/notes.txt")]
    public void Full_paths_are_verbatim_text_without_local_normalization_or_shell_quoting(string path)
        =>Assert.Equal(path,FileSelection.ClipboardText([Entry("notes.txt",path)],false,true));
    [Fact] public void Remote_case_variants_remain_distinct_and_local_variants_follow_platform_rules()
    {
        var entries=new[]{Entry("A","/srv/A"),Entry("a","/srv/a")};
        Assert.Equal("A"+Environment.NewLine+"a",FileSelection.ClipboardText(entries,false,false));
        Assert.Equal(OperatingSystem.IsWindows()?"A":"A"+Environment.NewLine+"a",FileSelection.ClipboardText(entries,true,false));
    }
    [Fact] public void Empty_or_parent_only_selection_has_no_clipboard_text()
    {
        Assert.Empty(FileSelection.ClipboardText([],false,true));
        Assert.Empty(FileSelection.ClipboardText([Entry("..","/")],false,false));
    }
    [Fact] public void Oversized_selection_is_rejected_without_returning_a_partial_copy()
    {
        var path=new string('x',FileSelection.ClipboardCharacters);
        Assert.Equal(path,FileSelection.ClipboardText([Entry("one",path)],false,true));
        Assert.Throws<InvalidOperationException>(()=>FileSelection.ClipboardText([Entry("one",path+"x")],false,true));
        Assert.Throws<InvalidOperationException>(()=>FileSelection.ClipboardText([Entry("one",path),Entry("two","/different")],false,true));
        Assert.Equal("one",FileSelection.ClipboardText([Entry("one",path+"x")],false,false));
    }
}
