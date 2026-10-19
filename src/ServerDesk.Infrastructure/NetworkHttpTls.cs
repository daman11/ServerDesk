using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;
public sealed partial class NetworkToolsService
{
    public static string Redact(string text,NetworkSecrets secrets)=>RedactKnown(text,KnownSecrets(secrets));
    private static IEnumerable<string> KnownSecrets(NetworkSecrets secrets)=>new[]{secrets.Password,secrets.Token,secrets.AuthPassword,secrets.PrivacyPassword,secrets.Community=="public"?"":secrets.Community};
    private static string RedactKnown(string text,IEnumerable<string> secrets)
    {
        var values=secrets.Where(value=>!string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).OrderByDescending(value=>value.Length).ToArray();
        var pattern=values.Length==0?null:string.Join("|",values.Select(Regex.Escape));
        // Instance-only: the static Regex cache must not retain passwords.
        var matcher=pattern==null?null:new Regex(pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(250));
        string Mask(string value)
        {
            if(matcher==null)return value;
            // One replacement pass: a short secret must not rematch the marker
            // inserted for another secret. Expensive input fails closed.
            try { return matcher.Replace(value,_=>"[redacted]"); }
            catch(RegexMatchTimeoutException) { return "[redacted]"; }
        }
        try
        {
            var node=JsonNode.Parse(text,new JsonNodeOptions(),new JsonDocumentOptions { MaxDepth=64 });
            JsonNode? Visit(JsonNode? item)
            {
                if(item is JsonObject obj)foreach(var key in obj.Select(p=>p.Key).ToArray())obj[key]=Sensitive(key)?JsonValue.Create("[redacted]"):Visit(obj[key]);
                else if(item is JsonArray array)for(var i=0;i<array.Count;i++)array[i]=Visit(array[i]);
                else if(item is JsonValue value)
                {
                    if(value.TryGetValue<string>(out var content))return JsonValue.Create(Mask(content));
                    var raw=value.ToJsonString(); var masked=Mask(raw); if(masked!=raw)return JsonValue.Create(masked);
                }
                return item;
            }
            return Visit(node)?.ToJsonString(new JsonSerializerOptions { WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })??Mask(text);
        }
        catch(JsonException) { return Mask(text); }
    }
    private static bool Sensitive(string name)
    { name=name.Replace("_","").Replace("-",""); return name.Contains("token",StringComparison.OrdinalIgnoreCase) || name.Contains("password",StringComparison.OrdinalIgnoreCase) || name.Contains("secret",StringComparison.OrdinalIgnoreCase) || name.Contains("apikey",StringComparison.OrdinalIgnoreCase) || name.Equals("Authorization",StringComparison.OrdinalIgnoreCase) || name.Contains("cookie",StringComparison.OrdinalIgnoreCase) || name.Equals("key",StringComparison.OrdinalIgnoreCase) || name.Equals("sig",StringComparison.OrdinalIgnoreCase); }
    private static string RedactHttp(string text,NetworkRequest request,NetworkSecrets secrets)
    {
        var values=Pairs(request.Headers,':').Where(pair=>Sensitive(pair.Key)).SelectMany(pair=>new[]{pair.Value,pair.Key.Equals("Authorization",StringComparison.OrdinalIgnoreCase)?pair.Value.Split(' ').Last():""}).Concat(Pairs(request.Query,'=').Where(pair=>Sensitive(pair.Key)).Select(pair=>pair.Value)).ToList();
        if(request.Authentication=="Basic")values.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Username+":"+secrets.Password)));
        if(request.ContentType=="application/x-www-form-urlencoded")values.AddRange(Pairs(request.Body,'=').Where(pair=>Sensitive(pair.Key)).Select(pair=>pair.Value));
        var query=new Uri(request.Target).Query.TrimStart('?');
        foreach(var item in query.Split('&',StringSplitOptions.RemoveEmptyEntries)) { var pair=item.Split('=',2); if(pair.Length==2 && Sensitive(Uri.UnescapeDataString(pair[0])))values.Add(Uri.UnescapeDataString(pair[1])); }
        try
        {
            using var body=JsonDocument.Parse(request.Body,new JsonDocumentOptions { MaxDepth=64 });
            void Visit(JsonElement element)
            { if(values.Count>=64)return; if(element.ValueKind==JsonValueKind.Object)foreach(var property in element.EnumerateObject()) { if(Sensitive(property.Name) && property.Value.ValueKind==JsonValueKind.String)values.Add(property.Value.GetString()??""); else Visit(property.Value); } else if(element.ValueKind==JsonValueKind.Array)foreach(var child in element.EnumerateArray())Visit(child); }
            Visit(body.RootElement);
        }
        catch(JsonException) { }
        return RedactKnown(text,KnownSecrets(secrets).Concat(values));
    }
    private static async Task<NetworkResult> HttpAsync(NetworkRequest r,NetworkSecrets secrets,IProgress<NetworkUpdate> output,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(r.TimeoutMs);
        using var handler=new SocketsHttpHandler { AllowAutoRedirect=false,UseCookies=false,AutomaticDecompression=DecompressionMethods.All,ConnectTimeout=TimeSpan.FromMilliseconds(r.TimeoutMs),MaxResponseHeadersLength=64 };
        using var client=new HttpClient(handler) { Timeout=Timeout.InfiniteTimeSpan };
        var uri=new Uri(r.Target); var original=uri; var method=r.Method; var body=r.Body; var watch=Stopwatch.StartNew();
        if(r.Query.Length>0)
        {
            var builder=new UriBuilder(uri); var query=string.Join("&",Pairs(r.Query,'=').Select(pair=>Uri.EscapeDataString(pair.Key)+"="+Uri.EscapeDataString(pair.Value)));
            builder.Query=(builder.Query.Length>1?builder.Query[1..]+"&":"")+query; uri=builder.Uri;
        }
        for(var redirects=0;redirects<=8;redirects++)
        {
            using var message=new HttpRequestMessage(new HttpMethod(method),uri); var same=uri.GetLeftPart(UriPartial.Authority)==original.GetLeftPart(UriPartial.Authority);
            if(body.Length>0)
            {
                if(!same)throw new InvalidDataException(L.T("Przekierowanie z treścią do innego serwera wymaga ręcznego otwarcia adresu."));
                if(r.ContentType=="application/json")using(JsonDocument.Parse(body,new JsonDocumentOptions { MaxDepth=64 })) { }
                message.Content=r.ContentType=="application/x-www-form-urlencoded"?new FormUrlEncodedContent(Pairs(body,'=')):new StringContent(body,Encoding.UTF8,r.ContentType);
            }
            if(same)
            {
                foreach(var header in Pairs(r.Headers,':'))
                {
                    if(new[]{"Host","Content-Length","Transfer-Encoding","Connection","Proxy-Authorization"}.Contains(header.Key,StringComparer.OrdinalIgnoreCase) || !message.Headers.TryAddWithoutValidation(header.Key,header.Value))throw new ArgumentException(L.T("Niepoprawny lub niedozwolony nagłówek HTTP."));
                }
                if(r.Authentication=="Basic")message.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(r.Username+":"+secrets.Password)));
                if(r.Authentication=="Bearer")message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secrets.Token);
            }
            using var response=await client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
            var status=(int)response.StatusCode;
            if(r.FollowRedirects && status is 301 or 302 or 303 or 307 or 308 && response.Headers.Location is {} location)
            {
                if(redirects==8)throw new InvalidDataException(L.T("Przekroczono limit ośmiu przekierowań HTTP."));
                var next=new Uri(uri,location); if(next.Scheme is not ("http" or "https") || next.UserInfo.Length>0 || uri.Scheme=="https" && next.Scheme=="http")throw new InvalidDataException(L.T("Odrzucono niebezpieczne przekierowanie HTTP."));
                if(status==303 && method!="HEAD" || status is 301 or 302 && method=="POST") { method="GET"; body=""; }
                uri=next; continue;
            }
            const int limit=2*1024*1024; using var buffer=new MemoryStream(); var bytes=new byte[16384]; var truncated=false;
            await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            while(true) { var count=await stream.ReadAsync(bytes,timeout.Token).ConfigureAwait(false); if(count==0)break; var left=limit-(int)buffer.Length; buffer.Write(bytes,0,Math.Min(left,count)); if(count>left) { truncated=true; break; } }
            var headers=string.Join("\n",response.Headers.Concat(response.Content.Headers).Select(header=>header.Key+": "+(Sensitive(header.Key)?"[redacted]":string.Join(", ",header.Value))));
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding=Encoding.UTF8; var charset=response.Content.Headers.ContentType?.CharSet?.Trim('"');
            if(!string.IsNullOrWhiteSpace(charset)) { try { encoding=Encoding.GetEncoding(charset); } catch(ArgumentException) { } }
            var content=RedactHttp(encoding.GetString(buffer.ToArray()),r,secrets); headers=RedactHttp(headers,r,secrets);
            var summary=L.F($"HTTP {status} · {Number(watch.Elapsed.TotalMilliseconds)} ms · {buffer.Length} B")+(truncated?L.T(" · odpowiedź skrócona do 2 MiB"):"");
            output.Report(new(new("http",new[]{status.ToString(),Number(watch.Elapsed.TotalMilliseconds),buffer.Length.ToString(),RedactHttp(response.Content.Headers.ContentType?.ToString()??"",r,secrets),truncated?L.T("Skrócona"):""},status>=400),1,1,summary));
            return new(summary,content,headers);
        }
        throw new InvalidDataException(L.T("Przekroczono limit przekierowań HTTP."));
    }
    private static IEnumerable<KeyValuePair<string,string>> Pairs(string input,char separator)
    {
        foreach(var line in input.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var i=line.IndexOf(separator); if(i<=0 || line.Contains('\r') || line.Any(c=>char.IsControl(c) && c!='\t'))throw new ArgumentException(L.T("Wpisz po jednym parametrze w wierszu: nazwa i wartość."));
            yield return new(line[..i].Trim(),line[(i+1)..].Trim());
        }
    }
    private static async Task<NetworkResult> TlsAsync(NetworkRequest r,IProgress<NetworkUpdate> output,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(r.TimeoutMs);
        using var tcp=new TcpClient(); await tcp.ConnectAsync(NetworkRequest.Host(r.Target),r.Port,timeout.Token).ConfigureAwait(false);
        X509Certificate2? certificate=null; var errors=SslPolicyErrors.None; var chainRows=new List<string[]>();
        // Diagnostic handshake only. No HTTP, authentication or application data is sent on this stream.
        using var ssl=new SslStream(tcp.GetStream(),false,(_,cert,chain,policy)=>
        {
            if(cert!=null)certificate=X509CertificateLoader.LoadCertificate(cert.GetRawCertData()); errors=policy;
            if(chain!=null)foreach(var element in chain.ChainElements.Cast<X509ChainElement>().Take(16))chainRows.Add(new[]{element.Certificate.Subject,element.Certificate.Issuer,string.Join(", ",element.ChainElementStatus.Select(s=>s.Status.ToString()))});
            return cert!=null;
        });
        try
        {
            var name=r.Sni.Length>0?NetworkRequest.Host(r.Sni):NetworkRequest.Host(r.Target);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost=name,EnabledSslProtocols=SslProtocols.None,CertificateRevocationCheckMode=X509RevocationMode.NoCheck },timeout.Token).ConfigureAwait(false);
            if(certificate==null)throw new InvalidDataException(L.T("Serwer nie przedstawił certyfikatu."));
            var days=(certificate.NotAfter.ToUniversalTime()-DateTime.UtcNow).TotalDays; var valid=days<0?L.T("Wygasły"):errors!=SslPolicyErrors.None || certificate.NotBefore.ToUniversalTime()>DateTime.UtcNow?L.T("Nieprawidłowy lub niezaufany"):days<=30?L.T("Wkrótce wygasa"):L.T("Ważny");
            using var rsa=certificate.GetRSAPublicKey(); using var ec=certificate.GetECDsaPublicKey(); using var dsa=certificate.GetDSAPublicKey();
            var keyBits=rsa?.KeySize??ec?.KeySize??dsa?.KeySize; var keyDescription=(certificate.PublicKey.Oid.FriendlyName??certificate.PublicKey.Oid.Value??"")+" "+(keyBits.HasValue?keyBits.Value+" bit":"—");
            var rows=new (string,string)[]{("Status",valid),("Subject",certificate.Subject),("Issuer",certificate.Issuer),("Serial",certificate.SerialNumber),("Not Before",certificate.NotBefore.ToUniversalTime().ToString("O")),("Not After",certificate.NotAfter.ToUniversalTime().ToString("O")),(L.T("Dni do wygaśnięcia"),Number(days)),("SAN",string.Join("\n",certificate.Extensions.Cast<X509Extension>().Where(e=>e.Oid?.Value=="2.5.29.17").Select(e=>e.Format(true)))),("Signature",certificate.SignatureAlgorithm.FriendlyName??certificate.SignatureAlgorithm.Value??""),("Public key",keyDescription),("SHA-256",certificate.GetCertHashString(HashAlgorithmName.SHA256)),("TLS",ssl.SslProtocol.ToString()),("Cipher suite",ssl.NegotiatedCipherSuite.ToString()),(L.T("Walidacja"),errors.ToString()),("SNI / identity",name)};
            foreach(var (key,value) in rows)output.Report(new(new(key,new[]{key,value},key=="Status" && (days<0 || errors!=SslPolicyErrors.None))));
            foreach(var chain in chainRows)output.Report(new(new(Guid.NewGuid().ToString(),new[]{L.T("Łańcuch"),string.Join("\n",chain)})));
            return new(L.T("Pokazano uzgodnione TLS/cipher suite, nie pełny audyt protokołów. Nie sprawdzano unieważnienia certyfikatu. Dla IP nazwa musi pasować do SAN; SNI pozwala wskazać nazwę usługi."),Pem:certificate.ExportCertificatePem());
        }
        finally { certificate?.Dispose(); }
    }
}
