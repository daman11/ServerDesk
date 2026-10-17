namespace ServerDesk.Core;

public sealed record ProfileIconOption(string Id,string Label);
public static class ProfileIcons
{
    public static IReadOnlyList<ProfileIconOption> Choices { get; }=Array.AsReadOnly<ProfileIconOption>([
        new("auto","Automatycznie — ikona protokołu"),new("server","Serwer"),new("database","Baza danych"),new("cloud","Chmura"),new("container","Kontenery"),new("layers","Wirtualizacja"),new("router","Router"),new("network","Sieć"),new("shield","Bezpieczeństwo"),new("globe","Usługi WWW"),new("laptop","Stacja robocza"),new("hard-drive","Magazyn danych"),new("code","Środowisko developerskie"),new("cpu","Obliczenia"),new("archive","Kopie zapasowe"),
        new("os-windows","Windows Server"),new("brand-linux","Linux"),new("brand-ubuntu","Ubuntu"),new("brand-debian","Debian"),new("brand-rockylinux","Rocky Linux"),new("brand-almalinux","AlmaLinux"),new("brand-redhat","Red Hat"),new("brand-freebsd","FreeBSD"),
        new("brand-proxmox","Proxmox"),new("brand-vmware","VMware"),new("brand-docker","Docker"),new("brand-kubernetes","Kubernetes"),new("brand-portainer","Portainer"),
        new("brand-nginx","NGINX"),new("brand-apache","Apache HTTP Server"),new("brand-traefikproxy","Traefik Proxy"),new("brand-caddy","Caddy"),
        new("brand-postgresql","PostgreSQL"),new("brand-mysql","MySQL"),new("brand-mariadb","MariaDB"),new("brand-redis","Redis"),new("brand-mongodb","MongoDB"),new("brand-elasticsearch","Elasticsearch"),
        new("brand-grafana","Grafana"),new("brand-prometheus","Prometheus"),new("brand-jenkins","Jenkins"),new("brand-gitlab","GitLab"),new("brand-gitea","Gitea"),new("brand-ansible","Ansible"),new("brand-terraform","Terraform"),
        new("brand-truenas","TrueNAS"),new("brand-synology","Synology"),new("brand-pfsense","pfSense"),new("brand-openwrt","OpenWrt"),new("brand-homeassistant","Home Assistant"),new("brand-nextcloud","Nextcloud")
    ]);
    public static bool IsKnown(string? id)=>Choices.Any(c=>c.Id==id);
    public static string Resolve(ServerProfile profile)=>profile.IconId!="auto" && IsKnown(profile.IconId)?profile.IconId:profile.Protocol switch { Protocol.RDP=>"rdp",Protocol.VNC=>"monitor",Protocol.Serial=>"serial",_=>"ssh" };
    public static void Validate(string? id)
    {
        if(string.IsNullOrWhiteSpace(id)||id.Length>64||id.Any(c=>!(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))throw new ArgumentException(L.T("Niepoprawny identyfikator ikony."));
        // Unknown well-formed IDs from newer exports remain round-trippable and render as auto.
    }
}
