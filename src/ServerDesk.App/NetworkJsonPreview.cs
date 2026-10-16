using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ServerDesk.App;
internal sealed class NetworkJsonPreview : Grid
{
    private static readonly Regex Tokens=new("\"(?:\\\\.|[^\"\\\\])*\"|\\b(?:true|false|null|-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)\\b",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(200));
    internal NetworkJsonPreview(TextBox plain)
    {
        var text=new SelectableTextBlock { FontFamily=plain.FontFamily,TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(12) };
        var scroll=new ScrollViewer { Content=text,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,IsVisible=false }; Children.Add(plain); Children.Add(scroll);
        void Update()
        {
            var value=plain.Text??""; text.Inlines!.Clear(); scroll.IsVisible=false; plain.IsVisible=true;
            if(value.Length is 0 or >65536)return;
            try
            {
                using var json=JsonDocument.Parse(value,new JsonDocumentOptions { MaxDepth=64 }); var matches=Tokens.Matches(value); if(matches.Count>4000)return;
                void Add(string content,string role) { var run=new Run(content); DesignSystem.Resource(run,TextElement.ForegroundProperty,role); text.Inlines.Add(run); }
                var previous=0;
                foreach(Match match in matches)
                {
                    if(match.Index>previous)Add(value[previous..match.Index],"ForegroundPrimary");
                    var key=match.Value.StartsWith('"') && value[(match.Index+match.Length)..].TrimStart().StartsWith(':');
                    Add(match.Value,key?"AccentPrimary":match.Value.StartsWith('"')?"Success":match.Value=="null"?"ForegroundSecondary":"Warning"); previous=match.Index+match.Length;
                }
                if(previous<value.Length)Add(value[previous..],"ForegroundPrimary"); plain.IsVisible=false; scroll.IsVisible=true;
            }
            catch(Exception ex) when(ex is JsonException or RegexMatchTimeoutException) { }
        }
        plain.TextChanged+=(_,_)=>Update(); Update();
    }
}
