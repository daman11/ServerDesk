using System.Net.NetworkInformation;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;
internal sealed partial class NetworkToolPane
{
    private static FuncDataTemplate<string> OptionTemplate()=>new((value,_)=>DesignSystem.Label(NetworkToolCatalog.OptionName(value??"")));
    private readonly Dictionary<string,TextBox> secretFields=[];
    private NetworkRequest Defaults()=>new() { Tool=Tool,Port=Tool==NetworkToolKind.Wake?9:Tool==NetworkToolKind.Snmp?161:443,TimeoutMs=Tool is NetworkToolKind.Http or NetworkToolKind.Tls?10000:2000 };
    private string Text(string key)=>((TextBox)fields[key]).Text??"";
    private void Group(string key,string label,Control input)
    {
        AutomationProperties.SetName(input,L.T(label)); var text=DesignSystem.Label(L.T(label),true,12); text.TextWrapping=Avalonia.Media.TextWrapping.Wrap;
        var group=new StackPanel { Width=input is TextBox { AcceptsReturn:true }?340:190,Spacing=4,Margin=new Thickness(0,0,12,10),Children={text,input} }; fieldGroups[key]=group; form.Children.Add(group);
    }
    private void Input(string key,string label,bool multiline=false,int maxLength=2048)
    {
        var value=(string)typeof(NetworkRequest).GetProperty(key)!.GetValue(Defaults())!;
        var input=new TextBox { Text=value,AcceptsReturn=multiline,MaxLength=maxLength,MinHeight=multiline?70:30,MaxHeight=multiline?110:double.PositiveInfinity,TextWrapping=multiline?Avalonia.Media.TextWrapping.Wrap:Avalonia.Media.TextWrapping.NoWrap };
        fields[key]=input; Group(key,label,input);
    }
    private void Number(string key,string label,int min,int max)
    { var value=(int)typeof(NetworkRequest).GetProperty(key)!.GetValue(Defaults())!; var input=new NumericUpDown { Minimum=min,Maximum=max,Value=value,Increment=1,FormatString="0" }; fields[key]=input; Group(key,label,input); }
    private void Choice(string key,string label,params string[] options)
    { var value=typeof(NetworkRequest).GetProperty(key)!.GetValue(Defaults()) as string; var input=new ComboBox { ItemsSource=options,ItemTemplate=OptionTemplate(),SelectedIndex=Math.Max(0,Array.IndexOf(options,value)),HorizontalAlignment=HorizontalAlignment.Stretch }; fields[key]=input; Group(key,label,input); }
    private void Flag(string key,string label)
    { var input=new CheckBox { Content=L.T(label),IsChecked=(bool)typeof(NetworkRequest).GetProperty(key)!.GetValue(Defaults())! }; fields[key]=input; Group(key,"",input); }
    private void Secret(string key,string label,string initial="")
    { var input=new TextBox { Text=initial,PasswordChar='●',MaxLength=4096 }; secretFields[key]=input; Group(key,label,input); }
    private void BuildForm()
    {
        form.SizeChanged+=(_,_)=> { foreach(var group in fieldGroups.Values.OfType<StackPanel>()) { var preferred=group.Children.LastOrDefault() is TextBox { AcceptsReturn:true }?340:190; group.Width=Math.Max(90,Math.Min(preferred,form.Bounds.Width-12)); } };
        if(Tool is NetworkToolKind.Ping or NetworkToolKind.Trace or NetworkToolKind.Ports or NetworkToolKind.Snmp)Choice("Family","Rodzina adresu","Auto","IPv4","IPv6");
        if(Tool is not (NetworkToolKind.Wake or NetworkToolKind.Subnet))Number("TimeoutMs","Timeout [ms]",100,60000);
        switch(Tool)
        {
            case NetworkToolKind.Ping:
                Number("Count","Liczba pakietów",1,100000); Number("IntervalMs","Interwał [ms]",100,60000); Number("PayloadBytes","Dane ICMP [B]",0,1400); Flag("Continuous","Ping ciągły"); break;
            case NetworkToolKind.Trace:
                Number("MaxHops","Maksymalna liczba hopów",1,64); Number("IntervalMs","Interwał cyklu [ms]",100,60000); Flag("Continuous","Monitoruj cyklicznie (MTR)"); Flag("ResolveNames","Rozwiązuj nazwy routerów"); break;
            case NetworkToolKind.Dns:
                Choice("RecordType","Typ rekordu",NetworkToolsService.DnsTypes); Number("Retries","Ponowienia",0,3); Input("Resolvers","Resolvery: IP lub IP:port, po przecinku",true,512);
                var resolver=new ComboBox { ItemTemplate=OptionTemplate(),ItemsSource=new[]{"System","Cloudflare · 1.1.1.1","Google · 8.8.8.8","Quad9 · 9.9.9.9","Custom DNS"},PlaceholderText=L.T("Wybierz resolver"),HorizontalAlignment=HorizontalAlignment.Stretch }; resolver.SelectionChanged+=(_,_)=> { if(resolver.SelectedIndex is >=0 and <4)((TextBox)fields["Resolvers"]).Text=new[]{"","1.1.1.1","8.8.8.8","9.9.9.9"}[resolver.SelectedIndex]; }; Group("resolverPreset","Szybki wybór DNS",resolver);
                rememberedResolvers=new ComboBox { PlaceholderText=L.T("Własny resolver"),HorizontalAlignment=HorizontalAlignment.Stretch }; rememberedResolvers.SelectionChanged+=(_,_)=> { if(rememberedResolvers.SelectedItem is string value)((TextBox)fields["Resolvers"]).Text=value; }; Group("savedResolvers","Ostatnie resolvery",rememberedResolvers); break;
            case NetworkToolKind.Ports:
                Input("Ports","Porty, np. 22,80,443 lub 8000-8100",false,16384); Number("Concurrency","Równoległe połączenia",1,64);
                var profile=new ComboBox { ItemTemplate=OptionTemplate(),ItemsSource=NetworkToolsService.PortProfiles.Keys.Append("Custom").ToArray(),PlaceholderText=L.T("Zestaw portów"),HorizontalAlignment=HorizontalAlignment.Stretch }; profile.SelectionChanged+=(_,_)=> { if(profile.SelectedItem is string name && NetworkToolsService.PortProfiles.TryGetValue(name,out var value))((TextBox)fields["Ports"]).Text=value; }; Group("portProfile","Profil skanowania",profile); break;
            case NetworkToolKind.Wake:
                Input("Broadcast","Broadcast IPv4"); Number("Port","Port UDP",1,65535); Input("LocalAddress","Lokalny IPv4 (puste: domyślny interfejs)");
                var interfaces=new ComboBox { ItemsSource=NetworkInterface.GetAllNetworkInterfaces().SelectMany(adapter=>adapter.GetIPProperties().UnicastAddresses.Where(ip=>ip.Address.AddressFamily==AddressFamily.InterNetwork).Select(ip=>new HostChoice(adapter.Name,ip.Address.ToString()))).ToArray(),PlaceholderText=L.T("Wybierz interfejs"),HorizontalAlignment=HorizontalAlignment.Stretch }; interfaces.SelectionChanged+=(_,_)=> { if(interfaces.SelectedItem is HostChoice choice)((TextBox)fields["LocalAddress"]).Text=choice.Host; }; Group("interface","Interfejs sieciowy",interfaces); break;
            case NetworkToolKind.Http:
                Choice("Method","Metoda HTTP","GET","POST","PUT","PATCH","DELETE","HEAD"); Choice("ContentType","Format body","application/json","text/plain","application/x-www-form-urlencoded"); Flag("FollowRedirects","Podążaj za przekierowaniami");
                Input("Headers","Nagłówki: Nazwa: wartość, po jednym wierszu",true,16384); Input("Query","Query: nazwa=wartość, po jednym wierszu",true,16384); Input("Body","Body / formularz: nazwa=wartość",true,262144);
                Choice("Authentication","Uwierzytelnianie","None","Basic","Bearer"); Input("Username","Użytkownik"); Secret("Password","Hasło HTTP"); Secret("Token","Bearer token"); break;
            case NetworkToolKind.Tls:
                Number("Port","Port TLS",1,65535); Input("Sni","Nazwa SNI / walidacji (opcjonalna)"); break;
            case NetworkToolKind.Subnet:
                Number("SplitPrefix","Nowy prefiks (-1: bez podziału)",-1,128); ((NumericUpDown)fields["SplitPrefix"]).ValueChanged+=(_,_)=> { if(!applying)_=RecalculateAsync(); }; break;
            case NetworkToolKind.Snmp:
                Choice("SnmpVersion","Wersja SNMP","v2c","v3"); Choice("SnmpOperation","Operacja","GET","GETNEXT","WALK","GETBULK"); Number("Port","Port UDP",1,65535); Number("Retries","Ponowienia",0,3); Input("Oid","OID");
                oidChoices=new ComboBox { ItemsSource=new[]{"1.3.6.1.2.1.1.1.0 · sysDescr","1.3.6.1.2.1.1.5.0 · sysName","1.3.6.1.2.1.1.3.0 · sysUpTime"},PlaceholderText=L.T("Popularny lub zapisany OID"),HorizontalAlignment=HorizontalAlignment.Stretch }; oidChoices.SelectionChanged+=(_,_)=> { if(oidChoices.SelectedItem is string value)((TextBox)fields["Oid"]).Text=value.Split(' ')[0]; }; Group("oidPreset","Szybki wybór OID",oidChoices);
                var favorite=Dialogs.Button("Zapisz OID",()=>_=GuardAsync(async()=> { await store.RememberAsync("oidFavorites",Text("Oid"),lifetime.Token); await RefreshSavedAsync(); })); Group("favoriteOid","Ulubione OID",favorite);
                Secret("Community","Community SNMP v2c","public"); Input("Username","Użytkownik SNMP v3"); Input("Context","Kontekst SNMP v3"); Choice("SecurityLevel","Poziom ochrony v3","noAuthNoPriv","authNoPriv","authPriv"); ((ComboBox)fields["SecurityLevel"]).SelectedItem="authPriv";
                Choice("AuthProtocol","Uwierzytelnianie v3","SHA256","SHA384","SHA512"); Secret("AuthPassword","Hasło uwierzytelniania v3"); Choice("PrivacyProtocol","Szyfrowanie v3","AES128"); Secret("PrivacyPassword","Hasło szyfrowania AES-128 v3"); break;
        }
        foreach(var key in new[]{"Authentication","SnmpVersion","SecurityLevel"})if(fields.TryGetValue(key,out var control))((ComboBox)control).SelectionChanged+=(_,_)=>UpdateSecretVisibility();
        UpdateSecretVisibility();
    }
    private void UpdateSecretVisibility()
    {
        if(Tool==NetworkToolKind.Http)
        { var auth=((ComboBox)fields["Authentication"]).SelectedItem as string; fieldGroups["Username"].IsVisible=fieldGroups["Password"].IsVisible=auth=="Basic"; fieldGroups["Token"].IsVisible=auth=="Bearer"; }
        if(Tool==NetworkToolKind.Snmp)
        {
            var v3=((ComboBox)fields["SnmpVersion"]).SelectedItem as string=="v3"; var level=((ComboBox)fields["SecurityLevel"]).SelectedItem as string;
            fieldGroups["Community"].IsVisible=!v3; foreach(var key in new[]{"Username","Context","SecurityLevel"})fieldGroups[key].IsVisible=v3;
            fieldGroups["AuthProtocol"].IsVisible=fieldGroups["AuthPassword"].IsVisible=v3 && level!="noAuthNoPriv"; fieldGroups["PrivacyProtocol"].IsVisible=fieldGroups["PrivacyPassword"].IsVisible=v3 && level=="authPriv";
        }
    }
    internal NetworkRequest ReadRequest()
    {
        var request=Defaults() with { Target=(target.Text??"").Trim() };
        foreach(var (key,control) in fields)
        {
            object? value=control switch { TextBox text=>text.Text??"",NumericUpDown number=>(int)(number.Value??number.Minimum),CheckBox flag=>flag.IsChecked==true,ComboBox choice=>choice.SelectedItem as string??"",_=>null };
            typeof(NetworkRequest).GetProperty(key)!.SetValue(request,value);
        }
        return request;
    }
    private NetworkSecrets ReadSecrets()
    { string Value(string key)=>secretFields.TryGetValue(key,out var text)?text.Text??"":""; return new(Value("Password"),Value("Token"),Value("Community"),Value("AuthPassword"),Value("PrivacyPassword")); }
    internal void ApplyRequest(NetworkRequest request,NetworkSecrets? secrets=null)
    {
        if(request.Tool!=Tool)return; applying=true;
        try
        {
            target.Text=request.Target;
            foreach(var (key,control) in fields)
            {
                var value=typeof(NetworkRequest).GetProperty(key)!.GetValue(request);
                if(control is TextBox text)text.Text=value as string;
                else if(control is NumericUpDown number)number.Value=Convert.ToDecimal(value);
                else if(control is CheckBox flag)flag.IsChecked=(bool)value!;
                else if(control is ComboBox choice)choice.SelectedItem=value;
            }
            foreach(var (key,input) in secretFields)input.Text=secrets==null?key=="Community"?"public":"":(string?)typeof(NetworkSecrets).GetProperty(key)!.GetValue(secrets);
        }
        finally { applying=false; UpdateSecretVisibility(); }
        if(Tool==NetworkToolKind.Subnet)_=RecalculateAsync();
    }
}
