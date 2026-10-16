using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static Button Button(string text, Action action)
    { var glyph=DeskIcons.ForAction(text); var button=new Button { Content=glyph==null?L.T(text):DeskIcons.Labelled(glyph,L.T(text)),Tag=text,Margin=new Thickness(0),IsCancel=text=="Anuluj",IsDefault=text is "Zapisz" or "Połącz" or "OK" or "Potwierdź" }; Avalonia.Automation.AutomationProperties.SetName(button,L.T(text)); if(DeskIcons.DescriptionForAction(text) is {} description)ToolTip.SetTip(button,L.T(description)); button.Click+=(_,_)=>action(); return button; }
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
            if(scroll!=null)scroll.MaxHeight=Math.Max(100,Math.Min(510,dialog.MaxHeight-150));
        };
        dialog.KeyDown+=(_,e)=> { if(!e.Handled && e.Key==Avalonia.Input.Key.Escape) { dialog.Close(); e.Handled=true; } };
        return dialog;
    }
    internal static void FitToOwnerScreen(Window dialog,Window owner)
    {
        var screen=owner.Screens.ScreenFromWindow(owner)??owner.Screens.Primary;
        if(screen==null)return;
        dialog.MaxWidth=Math.Min(dialog.MaxWidth,Math.Max(280,screen.WorkingArea.Width/screen.Scaling-40));
        dialog.MaxHeight=Math.Min(dialog.MaxHeight,Math.Max(240,screen.WorkingArea.Height/screen.Scaling-64));
        dialog.MinWidth=Math.Min(dialog.MinWidth,dialog.MaxWidth); dialog.MinHeight=Math.Min(dialog.MinHeight,dialog.MaxHeight);
        if(!double.IsNaN(dialog.Width))dialog.Width=Math.Min(dialog.Width,dialog.MaxWidth);
        if(!double.IsNaN(dialog.Height))dialog.Height=Math.Min(dialog.Height,dialog.MaxHeight);
    }
    public static async Task<bool> Confirm(Window owner,string title,string text,CancellationToken ct=default,string? language=null)
    {
        var stack=new StackPanel { Spacing=18 }; stack.Children.Add(DesignSystem.PageHeading("info",title)); stack.Children.Add(Text(text)); var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium,Margin=new Thickness(0,6,0,0) }; stack.Children.Add(row);
        var dialog=Window(title,stack); row.Children.Add(Button("Anuluj",()=>dialog.Close(false))); row.Children.Add(Button("Potwierdź",()=>dialog.Close(true)));
        if(language!=null)
        {
            dialog.Title=title;
            foreach(var button in row.Children.OfType<Button>())
            { var label=L.T((string)button.Tag!,language); button.Content=label; Avalonia.Automation.AutomationProperties.SetName(button,label); }
        }
        if(ct.IsCancellationRequested)return false;
        using var cancelled=ct.Register(()=>Avalonia.Threading.Dispatcher.UIThread.Post(()=>dialog.Close(false)));
        return await dialog.ShowDialog<bool>(owner);
    }
    public static async Task<string?> Prompt(Window owner,string title,string value="",bool secret=false)
    {
        var box=new TextBox { Text=value,PasswordChar=secret?'●':'\0',MaxLength=secret?CredentialLimits.MaximumSecretBytes:4096 }; Avalonia.Automation.AutomationProperties.SetName(box,title); var stack=new StackPanel { Spacing=18 }; stack.Children.Add(DesignSystem.PageHeading(secret?"key":"pencil",title)); stack.Children.Add(box);
        var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium }; stack.Children.Add(row); var dialog=Window(title,stack);
        row.Children.Add(Button("Anuluj",()=>dialog.Close())); row.Children.Add(Button("OK",()=>dialog.Close(box.Text??""))); dialog.Opened+=(_,_)=>box.Focus(); dialog.KeyDown+=(_,e)=> { if(e.Key==Avalonia.Input.Key.Escape)dialog.Close(); if(e.Key==Avalonia.Input.Key.Enter)dialog.Close(box.Text??""); };
        return await dialog.ShowDialog<string?>(owner);
    }
    public static Task<ServerProfile?> Edit(Window owner,ServerProfile? profile,IEnumerable<string> folders) => ProfileEditorDialog.Show(owner,profile,folders);
}
