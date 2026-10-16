using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using ServerDesk.Core;

namespace ServerDesk.App;
internal sealed class ProfileAppearancePicker : StackPanel
{
    private readonly TextBox color=new();
    private readonly Border swatch=new() { Width=24,Height=24,CornerRadius=new CornerRadius(4) };
    private readonly ProfileVisual preview;
    private readonly TextBlock feedback=DesignSystem.Label(L.T("PNG, JPEG lub WebP · do 4 MiB"),true,12);
    private readonly Button import,remove;
    private readonly ServerProfile original;
    private readonly Window owner;
    private string png;
    internal string SelectedColor=>(color.Text??"").Trim().ToUpperInvariant();
    internal string CustomIconPng=>png;
    internal TextBox ColorInput=>color;
    internal TextBlock ColorError { get; }=DesignSystem.Label("",false,12);
    internal event Action? Changed;
    internal ProfileAppearancePicker(Window owner,ServerProfile profile)
    {
        this.owner=owner; original=profile; png=profile.CustomIconPng; preview=new ProfileVisual(profile,32); Spacing=DesignTokens.Medium;
        Children.Add(DesignSystem.Label(L.T("Kolor serwera"),true,12));
        var palette=new WrapPanel { Orientation=Orientation.Horizontal };
        foreach(var value in ProfileAppearance.Colors)
        {
            var chip=new Button { Width=30,Height=30,MinHeight=30,Padding=new Thickness(3),Margin=new Thickness(0,0,4,0),Content=value.Length==0?DeskIcons.Create("x",16):new Border { Width=20,Height=20,CornerRadius=new CornerRadius(3),Background=ProfileVisual.ColorBrush(value) } };
            AutomationProperties.SetName(chip,value.Length==0?L.T("Bez koloru"):value); ToolTip.SetTip(chip,value.Length==0?L.T("Bez koloru"):value); chip.Click+=(_,_)=>color.Text=value; palette.Children.Add(chip);
        }
        Children.Add(palette); color.Text=profile.Color; color.PlaceholderText=L.T("#RRGGBB lub puste"); AutomationProperties.SetName(color,L.T("Kolor serwera"));
        var colorRow=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*"),ColumnSpacing=DesignTokens.Medium,Children={swatch,color} }; Grid.SetColumn(color,1); Children.Add(colorRow);
        ColorError.IsVisible=false; ColorError.TextWrapping=Avalonia.Media.TextWrapping.Wrap; DesignSystem.Resource(ColorError,TextBlock.ForegroundProperty,"Danger"); Children.Add(ColorError);
        color.TextChanged+=(_,_)=> { var valid=ProfileAppearance.IsColor(SelectedColor); color.Classes.Set("error",!valid); ColorError.Text=valid?"":L.T("Wpisz kolor #RRGGBB lub pozostaw pole puste."); ColorError.IsVisible=!valid; swatch.Background=valid?ProfileVisual.ColorBrush(SelectedColor):Avalonia.Media.Brushes.Transparent; Update(); Changed?.Invoke(); };
        swatch.Background=ProfileVisual.ColorBrush(profile.Color);
        var actions=new WrapPanel { Orientation=Orientation.Horizontal,Children={preview} };
        import=Dialogs.Button("Importuj grafikę",()=>_ = ImportAsync()); remove=Dialogs.Button("Usuń grafikę",()=>SetImage("")); import.Margin=new Thickness(0,0,8,4); remove.Margin=new Thickness(0,0,0,4); actions.Children.Add(import); actions.Children.Add(remove); Children.Add(actions); Children.Add(feedback); Update();
    }
    internal void SetImage(string value) { ProfileAppearance.Validate("",value); png=value; Update(); Changed?.Invoke(); }
    private void Update() { preview.Profile=original with { CustomIconPng=png,Color=ProfileAppearance.IsColor(SelectedColor)?SelectedColor:"" }; preview.IsVisible=png.Length>0; remove.IsEnabled=png.Length>0; feedback.Text=png.Length>0?L.T("Własna grafika zastępuje wybraną ikonę. Jest zapisana w profilu i eksporcie JSON."):L.T("PNG, JPEG lub WebP · do 4 MiB"); feedback.TextWrapping=Avalonia.Media.TextWrapping.Wrap; }
    private async Task ImportAsync()
    {
        import.IsEnabled=false;
        try
        {
            var provider=TopLevel.GetTopLevel(this)?.StorageProvider??owner.StorageProvider;
            var files=await provider.OpenFilePickerAsync(new FilePickerOpenOptions { Title=L.T("Grafika serwera"),AllowMultiple=false,FileTypeFilter=[new FilePickerFileType(L.T("Grafiki")) { Patterns=["*.png","*.jpg","*.jpeg","*.webp"],MimeTypes=["image/png","image/jpeg","image/webp"] }] });
            if(files.Count==0)return; await using var stream=await files[0].OpenReadAsync(); var image=await ProfileImageImport.ImportAsync(stream); SetImage(image);
        }
        catch(Exception ex) { feedback.Text=ex is InvalidDataException or ArgumentException?ex.Message:L.T("Nie udało się odczytać grafiki. Wybierz inny plik."); }
        finally { import.IsEnabled=true; }
    }
}
