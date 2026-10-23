using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed class BitwardenConnectionSettings:StackPanel
{
    private readonly ComboBox service=new() { Name="BitwardenService",HorizontalAlignment=HorizontalAlignment.Stretch,ItemsSource=new[]{"Bitwarden USA","Bitwarden EU","Vaultwarden / "+L.T("Własny serwer")} };
    private readonly TextBox server=new() { Name="BitwardenServerAddress",MaxLength=2048,PlaceholderText="https://vault.example.com" };
    private readonly TextBox email=new() { Name="BitwardenEmail",MaxLength=254,PlaceholderText="user@example.com" };
    private readonly CheckBox external=new() { Name="BitwardenExternalClient",Content=L.T("Użyj zewnętrznego klienta (zaawansowane)") };
    private readonly TextBox executable=new() { Name="BitwardenExecutable",MaxLength=4096 };
    private readonly TextBox directory=new() { Name="BitwardenDataDirectory",MaxLength=4096 };
    private readonly StackPanel address=new() { Spacing=5 },legacy=new() { Spacing=7 };
    private string externalServer="",externalEmail="";
    internal event Action? Changed;
    internal Action SaveAndConnect { get; set; }=()=>{};
    internal Control FocusTarget=>service.SelectedIndex==2?server:service;
    internal BitwardenConnectionSettings(BitwardenCliOptions value)
    {
        Spacing=10;
        void Field(Panel panel,string label,Control control)
        { panel.Children.Add(DesignSystem.Label(L.T(label),true,12)); panel.Children.Add(control); AutomationProperties.SetName(control,L.T(label)); }
        Children.Add(DesignSystem.Hint(L.F($"Dołączony klient Bitwarden OSS: {BitwardenRuntime.Version}")));
        Field(this,"Usługa sejfu",service); Field(address,"Adres serwera sejfu",server); Children.Add(address); Field(this,"E-mail konta sejfu",email);
        Children.Add(DesignSystem.Hint(L.T("Zapisz konfigurację i zaloguj się w oknie sejfu. Hasło główne i kody 2FA nie są zapisywane w ustawieniach.")));
        var connect=Dialogs.Button("Zapisz i połącz z sejfem",()=>SaveAndConnect()); connect.Name="BitwardenSaveConnect"; connect.HorizontalAlignment=HorizontalAlignment.Left; Children.Add(connect);
        legacy.Children.Add(external); var paths=new StackPanel { Spacing=7 };
        Field(paths,"Program Bitwarden CLI",executable); Field(paths,"Katalog profilu CLI (opcjonalnie)",directory); legacy.Children.Add(paths);
        var advanced=new Expander { Name="BitwardenAdvanced",Header=L.T("Zewnętrzny klient i zgodność ze starszą konfiguracją"),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Content=legacy,IsExpanded=value.Executable.Length>0,Classes={"network-options"} };
        Children.Add(advanced);
        Children.Add(DesignSystem.Hint(L.T("Dołączony klient używa osobnego profilu ServerDesk dla wybranego serwera i e-maila. Nie zmienia konta w aplikacji ani rozszerzeniu Bitwarden.")));
        void Update() { address.IsVisible=service.SelectedIndex==2; paths.IsVisible=external.IsChecked==true; service.IsEnabled=email.IsEnabled=server.IsEnabled=external.IsChecked!=true; }
        service.SelectionChanged+=(_,_)=> { Update(); Changed?.Invoke(); }; external.IsCheckedChanged+=(_,_)=> { Update(); Changed?.Invoke(); };
        foreach(var box in new[]{server,email,executable,directory})box.TextChanged+=(_,_)=>Changed?.Invoke();
        Apply(value); Update();
    }
    internal BitwardenCliOptions Read()
    {
        if(external.IsChecked==true && string.IsNullOrWhiteSpace(executable.Text))
            throw new ArgumentException(L.T("Podaj pełną ścieżkę do Bitwarden CLI i opcjonalnego katalogu danych, bez argumentów."),nameof(ApplicationPreferences.Bitwarden));
        if(external.IsChecked!=true && service.SelectedIndex==2 && string.IsNullOrWhiteSpace(server.Text))
            throw new ArgumentException(L.T("Niepoprawny adres serwera Bitwarden."),nameof(ApplicationPreferences.Bitwarden));
        return new(external.IsChecked==true?executable.Text?.Trim()??"":"",
            external.IsChecked==true?directory.Text?.Trim()??"":"",
            external.IsChecked==true?externalServer:service.SelectedIndex switch { 0=>"",1=>"https://vault.bitwarden.eu",_=>server.Text?.Trim()??"" },
            external.IsChecked==true?externalEmail:email.Text?.Trim()??"");
    }
    internal void Apply(BitwardenCliOptions value)
    {
        externalServer=value.Server; externalEmail=value.Email;
        executable.Text=value.Executable; directory.Text=value.DataDirectory; external.IsChecked=value.Executable.Length>0; email.Text=value.Email;
        var url=value.EffectiveServer;
        service.SelectedIndex=url=="https://vault.bitwarden.com"?0:url=="https://vault.bitwarden.eu"?1:2; server.Text=url;
    }
}
