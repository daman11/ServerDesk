using System.Globalization;
using Avalonia.Controls;

namespace ServerDesk.App;

internal sealed class SettingsSearchIndex
{
    private sealed record Group(Control Card,StackPanel Fields,int Page,string Keywords);
    private sealed record Entry(Control View,Group Group,string Keywords);
    private readonly Dictionary<StackPanel,(int Index,string Keywords)> pages=[];
    private readonly Dictionary<StackPanel,Group> groups=[];
    private readonly List<Entry> entries=[];
    private readonly Dictionary<Control,bool> notes=[];
    internal void Page(StackPanel page,string title) =>pages.Add(page,(pages.Count,title));
    internal void Section(StackPanel page,Control card,StackPanel fields,string title)=>groups.Add(fields,new(card,fields,pages[page].Index,pages[page].Keywords+" "+title));
    internal void Field(StackPanel section,Control view,string label)=>entries.Add(new(view,groups[section],label));
    internal int[] Apply(string query)
    {
        var terms=query.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        var count=new int[pages.Count];
        foreach(var entry in entries)
        {
            entry.View.IsVisible=terms.All(term=>CultureInfo.InvariantCulture.CompareInfo.IndexOf(entry.Group.Keywords+" "+entry.Keywords,term,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0);
            if(entry.View.IsVisible)count[entry.Group.Page]++;
        }
        foreach(var group in groups.Values)
        {
            group.Card.IsVisible=entries.Any(entry=>ReferenceEquals(entry.Group,group) && entry.View.IsVisible);
            foreach(var child in group.Fields.Children.Where(child=>entries.All(entry=>!ReferenceEquals(entry.View,child))))
            { if(!notes.ContainsKey(child))notes[child]=child.IsVisible; child.IsVisible=terms.Length==0 && notes[child]; }
        }
        return count;
    }
}
