using System.Globalization;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class SortingTests
{
    [Theory]
    [InlineData("server2","server10")]
    [InlineData("server9-backup2","server9-backup11")]
    [InlineData("file2.txt","file02.txt")]
    [InlineData("0","000")]
    [InlineData("file99999999999999999999999999999","file100000000000000000000000000000")]
    public void Natural_names_compare_numeric_runs_without_overflow(string first,string second)
    { Assert.True(NaturalStringComparer.OrdinalIgnoreCase.Compare(first,second)<0); Assert.True(NaturalStringComparer.OrdinalIgnoreCase.Compare(second,first)>0); }
    [Fact] public void Natural_order_is_case_insensitive_and_transitive()
    {
        string[] values=["","a","A","a0","a00","a01","a1","a2","a10","a1b2","a1b10","a-1","a.10","2","10","!2","żółć2","żółć10","server000000000000001","server9","server999999999999999999999999999999"];
        var compare=NaturalStringComparer.OrdinalIgnoreCase; Assert.Equal(0,compare.Compare("SERVER2","server2"));
        foreach(var a in values)foreach(var b in values)
        {
            Assert.Equal(-Math.Sign(compare.Compare(b,a)),Math.Sign(compare.Compare(a,b)));
            foreach(var c in values)if(compare.Compare(a,b)<=0 && compare.Compare(b,c)<=0)Assert.True(compare.Compare(a,c)<=0,$"{a} <= {b} <= {c}");
        }
    }
    [Theory]
    [InlineData("pl-PL")]
    [InlineData("en-US")]
    public void Name_and_folder_numeric_order_does_not_depend_on_language(string culture)
    {
        var old=CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture); var profiles=new[]{"server10","server2","Server1"}.Select(name=>new ServerProfile { Name=name }); Assert.Equal(new[]{"Server1","server2","server10"},ProfileOrdering.Sort(profiles,ProfileSort.Name).Select(p=>p.Name)); Assert.Equal(new[]{"rack1","rack2","rack10"},new[]{"rack10","rack2","rack1"}.OrderBy(name=>name,NaturalStringComparer.CurrentCultureIgnoreCase)); }
        finally { CultureInfo.CurrentCulture=old; }
    }
    [Fact] public void Ip_sort_compares_all_octets_ipv6_bytes_and_numeric_ports()
    {
        var source=new[]{("10.0.0.100",22),("10.0.0.20",3389),("10.0.0.2",22),("10.0.1.1",22),("10.0.0.20",22),("2001:db8::10",22),("2001:db8::2",22),("server10",22),("server2",22)}.Select(pair=>new ServerProfile { Name=pair.Item1+":"+pair.Item2,Host=pair.Item1,Port=pair.Item2 }).ToArray();
        var expected=new[]{source[2],source[4],source[1],source[0],source[3],source[6],source[5],source[8],source[7]};
        Assert.Equal(expected,ProfileOrdering.Sort(source,ProfileSort.Host,false)); Assert.Equal(expected.Reverse(),ProfileOrdering.Sort(source,ProfileSort.Host,true));
    }
    [Fact] public void Equivalent_ip_and_dns_spellings_use_stable_name_ties()
    {
        var a=new ServerProfile { Name="server2",Host="2001:db8::2" }; var b=a with { Id=Guid.NewGuid(),Name="server10",Host="2001:0db8:0000:0000:0000:0000:0000:0002" };
        Assert.Equal(new[]{a,b},ProfileOrdering.Sort(new[]{b,a},ProfileSort.Host));
        a=a with { Host="EXAMPLE.com." }; b=b with { Host="example.com" }; Assert.Equal(new[]{a,b},ProfileOrdering.Sort(new[]{b,a},ProfileSort.Host));
        a=a with { Host="192.0.2.2" }; b=b with { Host="::ffff:192.0.2.2" }; Assert.Equal(new[]{a,b},ProfileOrdering.Sort(new[]{b,a},ProfileSort.Host));
    }
    [Fact] public void Serial_addresses_and_protocol_names_sort_by_visible_values()
    {
        var serial=new[]{"COM10","COM2","COM1"}.Select(port=>new ServerProfile { Name="same",Protocol=Protocol.Serial,Serial=new() { PortName=port } }).ToArray();
        Assert.Equal(new[]{"COM1","COM2","COM10"},ProfileOrdering.Sort(serial,ProfileSort.Host).Select(p=>p.Serial.PortName));
        var protocols=Enum.GetValues<Protocol>().Select(protocol=>new ServerProfile { Protocol=protocol }); Assert.Equal(new[]{Protocol.RDP,Protocol.Serial,Protocol.SSH,Protocol.VNC},ProfileOrdering.Sort(protocols,ProfileSort.Protocol).Select(p=>p.Protocol));
    }
    [Fact] public void Last_connection_defaults_newest_and_missing_values_remain_last_in_both_directions()
    {
        var profiles=new[]{new ServerProfile { Name="never" },new ServerProfile { Name="old",LastConnected=DateTimeOffset.Parse("2026-01-01T12:00:00Z") },new ServerProfile { Name="new",LastConnected=DateTimeOffset.Parse("2026-01-02T12:00:00Z") }};
        Assert.Equal(new[]{"new","old","never"},ProfileOrdering.Sort(profiles,ProfileSort.LastConnected).Select(p=>p.Name)); Assert.Equal(new[]{"old","new","never"},ProfileOrdering.Sort(profiles,ProfileSort.LastConnected,false).Select(p=>p.Name));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void File_names_are_natural_with_parent_and_directories_first(bool descending)
    {
        FileEntry Entry(string name,bool directory=false)=>new(name,"/"+name,directory,false,10,new(2026,1,1),"");
        var files=new[]{Entry("file10.txt"),Entry("folder10",true),Entry("file2.txt"),Entry("..",true),Entry("folder2",true)};
        Assert.Equal(descending?new[]{"..","folder10","folder2","file10.txt","file2.txt"}:new[]{"..","folder2","folder10","file2.txt","file10.txt"},FileListing.Apply(files,"",false,FileSort.Name,descending).Select(f=>f.Name));
        Assert.Equal(new[]{"..","folder2","folder10","file2.txt","file10.txt"},FileListing.Apply(files,"",false,FileSort.Size,true).Select(f=>f.Name));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unknown_file_dates_remain_last_in_both_directions(bool descending)
    {
        var files=new[]{new FileEntry("unknown","/unknown",false,false,1,DateTime.MinValue,""),new FileEntry("dated","/dated",false,false,1,new(2026,1,1),"")};
        Assert.Equal(new[]{"dated","unknown"},FileListing.Apply(files,"",false,FileSort.Modified,descending).Select(f=>f.Name));
    }
    [Fact] public void Directory_size_metadata_does_not_sort_a_value_displayed_as_unknown()
    {
        var files=new[]{new FileEntry("folder2","/folder2",true,false,999,new(2026,1,1),""),new FileEntry("folder10","/folder10",true,false,0,new(2026,1,1),"")};
        Assert.Equal(new[]{"folder2","folder10"},FileListing.Apply(files,"",false,FileSort.Size,false).Select(f=>f.Name));
    }
    [Fact] public async Task Sort_preferences_migrate_legacy_roundtrip_and_reject_invalid_updates()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-sorting-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); Assert.Equal((ProfileSort.Name,false),await repo.ProfileOrderingAsync());
            await repo.SetSettingAsync("serverSort","LastConnected"); Assert.Equal((ProfileSort.LastConnected,true),await repo.ProfileOrderingAsync());
            await repo.SaveProfileOrderingAsync(ProfileSort.Host,true); Assert.Equal((ProfileSort.Host,true),await new ProfileRepository(new AppPaths(root)).ProfileOrderingAsync());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>repo.SaveProfileOrderingAsync((ProfileSort)900,false)); Assert.Equal((ProfileSort.Host,true),await repo.ProfileOrderingAsync());
            await repo.SaveProfileOrderingAsync(ProfileSort.Name,false); Assert.Equal((ProfileSort.Name,false),await repo.ProfileOrderingAsync());
        }
        finally { DatabaseTestCleanup.Clear(root); var resolved=Path.GetFullPath(root); if(!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture path"); if(Directory.Exists(resolved))Directory.Delete(resolved,true); }
    }
}
