using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static Button Button(string text, Action action)
    {
        var glyph=DeskIcons.ForAction(text); var label=L.T(text);
        var button=new Button { Content=glyph==null?label:DeskIcons.Labelled(glyph,label),Tag=text,Margin=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Center,IsCancel=text=="Anuluj",IsDefault=text is "Zapisz" or "Połącz" or "OK" or "Potwierdź" };
        if(button.IsDefault)button.Classes.Add("primary");
        Avalonia.Automation.AutomationProperties.SetName(button,label); ToolTip.SetTip(button,L.T(DeskIcons.DescriptionForAction(text)??text)); button.Click+=(_,_)=>action(); return button;
    }
    internal static TextBlock Text(string text,double size=DesignTokens.Body) { var label=DesignSystem.Label(text,false,size); label.TextWrapping=Avalonia.Media.TextWrapping.Wrap; label.Margin=new Thickness(0,DesignTokens.Small); return label; }
    private static Window Window(string title, Control content)
    {
        ScrollViewer? scroll=null;
        if(content is StackPanel stack)
        {
            // Keep confirmation/authentication actions reachable when the message or font is large.
            var footer=stack.Children.LastOrDefault() is StackPanel { Orientation:Orientation.Horizontal } actions?actions:null;
            if(footer!=null)stack.Children.Remove(footer);
            scroll=new ScrollViewer { Name="DialogScroll",Content=stack,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,MaxHeight=510 };
            var body=new Grid { RowDefinitions=new("*,Auto"),RowSpacing=18,Children={scroll} };
            if(footer!=null) { footer.Name="DialogActions"; Grid.SetRow(footer,1); body.Children.Add(footer); }
            content=body;
        }
        var dialog=new Window { Title=L.T(title),Width=500,MinWidth=360,SizeToContent=SizeToContent.Height,MaxHeight=720,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new Border { Padding=DesignTokens.PagePadding,Child=content },CanResize=true };
        dialog.Opened+=(_,_)=>
        {
            FitToOwnerScreen(dialog,dialog.Owner as Window??dialog);
            if(scroll!=null)scroll.MaxHeight=Math.Max(1,Math.Min(510,dialog.MaxHeight-150));
        };
        if(scroll!=null)dialog.PropertyChanged+=(_,e)=> { if(e.Property==Avalonia.Controls.Window.MaxHeightProperty)scroll.MaxHeight=Math.Max(1,Math.Min(510,dialog.MaxHeight-150)); };
        dialog.KeyDown+=(_,e)=> { if(!e.Handled && e.Key==Avalonia.Input.Key.Escape) { dialog.Close(); e.Handled=true; } };
        return dialog;
    }
    internal static void FitToOwnerScreen(Window dialog,Window owner)
        =>DialogScreenPlacement.Track(dialog,owner);
    public static async Task<bool> Confirm(Window owner,string title,string text,CancellationToken ct=default,string? language=null)
    {
        var stack=new StackPanel { Spacing=18 }; stack.Children.Add(DesignSystem.PageHeading("info",title)); stack.Children.Add(Text(text)); var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium,Margin=new Thickness(0,6,0,0) }; stack.Children.Add(row);
        var dialog=Window(title,stack); row.Children.Add(Button("Anuluj",()=>dialog.Close(false))); row.Children.Add(Button("Potwierdź",()=>dialog.Close(true)));
        if(language!=null)
        {
            dialog.Title=title;
            foreach(var button in row.Children.OfType<Button>())
            { var source=(string)button.Tag!; var label=L.T(source,language); var glyph=DeskIcons.ForAction(source); button.Content=glyph==null?label:DeskIcons.Labelled(glyph,label); Avalonia.Automation.AutomationProperties.SetName(button,label); ToolTip.SetTip(button,label); }
        }
        if(ct.IsCancellationRequested)return false;
        using var cancelled=ct.Register(()=>Avalonia.Threading.Dispatcher.UIThread.Post(()=>dialog.Close(false)));
        return await dialog.ShowDialog<bool>(owner);
    }
    public static async Task<string?> Prompt(Window owner,string title,string value="",bool secret=false,CancellationToken ct=default)
    {
        var box=new TextBox { Text=value,PasswordChar=secret?'●':'\0',MaxLength=secret?CredentialLimits.MaximumSecretBytes:4096 }; Avalonia.Automation.AutomationProperties.SetName(box,title); var stack=new StackPanel { Spacing=18 }; stack.Children.Add(DesignSystem.PageHeading(secret?"key":"pencil",title)); stack.Children.Add(box);
        var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium }; stack.Children.Add(row); var dialog=Window(title,stack);
        row.Children.Add(Button("Anuluj",()=>dialog.Close())); row.Children.Add(Button("OK",()=>dialog.Close(box.Text??""))); dialog.Opened+=(_,_)=>box.Focus(); dialog.KeyDown+=(_,e)=> { if(e.Key==Avalonia.Input.Key.Escape)dialog.Close(); if(e.Key==Avalonia.Input.Key.Enter)dialog.Close(box.Text??""); };
        if(ct.IsCancellationRequested)return null;
        using var cancelled=ct.Register(()=>Avalonia.Threading.Dispatcher.UIThread.Post(()=>dialog.Close()));
        try { return await dialog.ShowDialog<string?>(owner); }
        finally { if(secret)box.Text=""; }
    }
    public static Task<ServerProfile?> Edit(Window owner,ServerProfile? profile,IEnumerable<string> folders,IEnumerable<string>? knownTags=null,IReadOnlyDictionary<string,string>? tagColors=null) => ProfileEditorDialog.Show(owner,profile,folders,knownTags,tagColors);
}
