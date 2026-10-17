using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed record QuickConnectionChoice(ServerProfile Profile,bool EditProfile);

internal static partial class Dialogs
{
    internal static async Task<QuickConnectionChoice?> QuickConnection(Window owner)
    {
        var dialog=new Window { Name="QuickConnectionDialog",Title=L.T("Szybkie połączenie"),Width=640,Height=530,MinWidth=420,MinHeight=440,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        FitToOwnerScreen(dialog,owner);
        var root=new Grid { Margin=new Thickness(20),RowDefinitions=new RowDefinitions("*,Auto"),RowSpacing=16 }; dialog.Content=root;
        var body=new StackPanel { Spacing=14 };
        var scroll=new ScrollViewer { Content=body,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }; root.Children.Add(scroll);
        body.Children.Add(DesignSystem.PageHeading("connect",L.T("Szybkie połączenie"),L.T("Podaj adres i sprawdź cel przed otwarciem sesji. Hasło podasz dopiero przy łączeniu.")));
        var input=new TextBox { Name="QuickConnectionAddress",MaxLength=4096,PlaceholderText="ssh://login@host:22",Text="" }; AutomationProperties.SetName(input,L.T("Adres połączenia")); body.Children.Add(input);
        var examples=new WrapPanel { ItemSpacing=5,LineSpacing=5 };
        foreach(var (name,example) in new[]{("SSH","ssh://admin@server.example:22"),("RDP","rdp://server.example:3389"),("VNC","vnc://server.example:5900"),("Serial",OperatingSystem.IsWindows()?"serial://COM3?baud=115200":"serial:///dev/ttyUSB0?baud=115200")})
        {
            var button=Button(name,()=> { input.Text=example; input.Focus(); input.SelectAll(); }); button.Name="QuickConnectionExample"; button.Tag=name; button.Classes.Add("chrome"); ToolTip.SetTip(button,example); examples.Children.Add(button);
        }
        body.Children.Add(examples);
        var feedback=DesignSystem.Hint(""); feedback.Name="QuickConnectionFeedback"; body.Children.Add(feedback);
        var preview=new StackPanel { Spacing=6 }; var previewCard=DesignSystem.Card(preview,new Thickness(12)); previewCard.Name="QuickConnectionPreview"; body.Children.Add(previewCard);
        var note=DesignSystem.Hint(L.T("Szybkie połączenie nie zapisuje profilu. Wybierz „Edytuj jako profil”, aby nadać nazwę, folder i zapisać ustawienia.")); body.Children.Add(note);
        var actions=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),ColumnSpacing=8 }; Grid.SetRow(actions,1); root.Children.Add(actions);
        var edit=Button("Edytuj jako profil",()=> { }); edit.Name="QuickConnectionEdit"; edit.IsDefault=false; edit.HorizontalAlignment=HorizontalAlignment.Left; actions.Children.Add(edit);
        var cancel=Button("Anuluj",dialog.Close); Grid.SetColumn(cancel,1); actions.Children.Add(cancel);
        var connect=Button("Połącz",()=> { }); connect.Name="QuickConnectionConnect"; Grid.SetColumn(connect,2); actions.Children.Add(connect);
        void LayoutActions()
        {
            var compact=dialog.ClientSize.Width<540;
            actions.RowDefinitions=new RowDefinitions(compact?"Auto,Auto":"Auto"); actions.RowSpacing=compact?8:0;
            Grid.SetRow(edit,0); Grid.SetColumnSpan(edit,compact?3:1); Grid.SetRow(cancel,compact?1:0); Grid.SetRow(connect,compact?1:0);
        }
        dialog.SizeChanged+=(_,_)=>LayoutActions(); LayoutActions();
        ServerProfile? Parse()
        {
            var text=(input.Text??"").Trim();
            preview.Children.Clear(); previewCard.IsVisible=false;
            if(text.Length==0) { feedback.Text=L.T("Wpisz adres lub wybierz przykład protokołu."); DesignSystem.Resource(feedback,TextBlock.ForegroundProperty,"ForegroundSecondary"); return null; }
            try
            {
                var profile=QuickConnect.Parse(text);
                feedback.Text=L.T("Adres jest poprawny. Sprawdź dane połączenia poniżej."); DesignSystem.Resource(feedback,TextBlock.ForegroundProperty,"Success");
                preview.Children.Add(DesignSystem.SectionHeading(profile.Protocol==Protocol.Serial?"serial":profile.Protocol==Protocol.RDP?"rdp":profile.Protocol==Protocol.VNC?"monitor":"ssh",profile.Protocol.ToString()));
                var endpoint=DesignSystem.Label(profile.Endpoint); endpoint.Name="QuickConnectionEndpoint"; endpoint.TextWrapping=TextWrapping.Wrap; preview.Children.Add(endpoint);
                if(profile.Username.Length>0)preview.Children.Add(DesignSystem.Hint(L.F($"Użytkownik: {profile.Username}")));
                if(profile.Protocol==Protocol.Serial)preview.Children.Add(DesignSystem.Hint(profile.Serial.Summary));
                previewCard.IsVisible=true; return profile;
            }
            catch(Exception ex) when(ex is ArgumentException or FormatException)
            { feedback.Text=ex is ArgumentException?ex.Message:L.T("Niepoprawny adres połączenia."); DesignSystem.Resource(feedback,TextBlock.ForegroundProperty,"Danger"); return null; }
        }
        void Refresh() { var valid=Parse()!=null; connect.IsEnabled=edit.IsEnabled=valid; }
        void Finish(bool asProfile) { var profile=Parse(); if(profile!=null)dialog.Close(new QuickConnectionChoice(profile,asProfile)); else Refresh(); }
        // Reparse on activation: TextChanged may still be queued after a paste.
        connect.Click+=(_,_)=>Finish(false); edit.Click+=(_,_)=>Finish(true); input.TextChanged+=(_,_)=>Refresh();
        input.KeyDown+=(_,e)=> { if(e.Key==Key.Enter && e.KeyModifiers==KeyModifiers.None) { Finish(false); e.Handled=true; } };
        dialog.KeyDown+=(_,e)=> { if(e.Key==Key.Escape) { dialog.Close(); e.Handled=true; } };
        dialog.Opened+=(_,_)=>input.Focus(); Refresh(); return await dialog.ShowDialog<QuickConnectionChoice?>(owner);
    }
}
