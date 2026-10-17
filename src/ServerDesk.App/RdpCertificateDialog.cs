using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task<bool> RdpCertificate(Window owner,ServerProfile profile,RdpEndpointInfo certificate,CancellationToken ct)
    {
        if(ct.IsCancellationRequested)return false;
        var fingerprint=RdpCertificateFingerprint.Display(certificate.CertificateFingerprint!);
        var stack=new StackPanel { Spacing=DesignTokens.Section };
        stack.Children.Add(DesignSystem.PageHeading("shield",L.T("Weryfikacja certyfikatu RDP")));
        stack.Children.Add(Text(L.T("Nie można automatycznie potwierdzić tożsamości tego serwera. Porównaj odcisk SHA-256 z zaufanym źródłem, zanim zezwolisz na logowanie.")));
        var target=DesignSystem.Label(RdpConfiguration.Address(profile),false,17); target.TextWrapping=TextWrapping.Wrap; stack.Children.Add(target);
        var issues=new StackPanel { Spacing=6 };
        void Issue(string text) { var label=Text(L.T(text),12); DesignSystem.Resource(label,TextBlock.ForegroundProperty,"Warning"); issues.Children.Add(label); }
        if(certificate.CertificateErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch))Issue("Nazwa w certyfikacie nie pasuje do adresu serwera.");
        if(certificate.CertificateErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors))Issue("System nie ufa wystawcy lub łańcuchowi certyfikatu.");
        if(certificate.CertificateStarts>DateTime.Now || certificate.CertificateExpires<DateTime.Now)Issue("Certyfikat jest poza okresem ważności.");
        stack.Children.Add(issues);
        var digest=new StackPanel { Spacing=6 };
        digest.Children.Add(DesignSystem.Label(L.T("Odcisk SHA-256"),true,12));
        digest.Children.Add(new SelectableTextBlock { Text=fingerprint,TextWrapping=TextWrapping.Wrap });
        stack.Children.Add(DesignSystem.Card(digest));
        var checkedFingerprint=new CheckBox { Name="RdpCertificateVerified",Content=new TextBlock { Text=L.T("Porównałem odcisk i potwierdzam tożsamość serwera"),TextWrapping=TextWrapping.Wrap } }; stack.Children.Add(checkedFingerprint);
        stack.Children.Add(Text(L.T("Zgoda dotyczy tego certyfikatu w bieżącej karcie. Nie dodajemy go do systemowego magazynu zaufania."),12));
        var details=new StackPanel { Spacing=6 };
        void Detail(string label,string? value) { details.Children.Add(DesignSystem.Label(L.T(label),true,12)); details.Children.Add(new SelectableTextBlock { Text=value??L.T("Brak danych"),TextWrapping=TextWrapping.Wrap }); }
        Detail("Podmiot",certificate.CertificateSubject); Detail("Wystawca",certificate.CertificateIssuer);
        Detail("Ważność certyfikatu",$"{certificate.CertificateStarts:yyyy-MM-dd HH:mm} — {certificate.CertificateExpires:yyyy-MM-dd HH:mm}");
        stack.Children.Add(new Expander { Header=L.T("Szczegóły"),Content=DesignSystem.Card(details),HorizontalAlignment=HorizontalAlignment.Stretch });
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=DesignTokens.Medium }; stack.Children.Add(actions);
        var dialog=Window("Weryfikacja certyfikatu RDP",stack); dialog.Name="RdpCertificateDialog"; dialog.Width=660; dialog.MinWidth=420;
        var cancel=Button("Anuluj",()=>dialog.Close(false)); cancel.IsDefault=true; actions.Children.Add(cancel);
        var accept=Button("Zaufaj w tej karcie",()=> { if(checkedFingerprint.IsChecked==true)dialog.Close(true); }); accept.Name="RdpCertificateAccept"; accept.Content=DeskIcons.Labelled("shield",L.T("Zaufaj w tej karcie")); accept.Classes.Add("primary"); accept.IsDefault=false; accept.IsEnabled=false; actions.Children.Add(accept);
        checkedFingerprint.IsCheckedChanged+=(_,_)=>accept.IsEnabled=checkedFingerprint.IsChecked==true;
        using var cancelled=ct.Register(()=>Avalonia.Threading.Dispatcher.UIThread.Post(()=>dialog.Close(false)));
        return await dialog.ShowDialog<bool>(owner);
    }
}
