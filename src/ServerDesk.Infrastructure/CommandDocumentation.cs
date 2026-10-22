using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed record CommandDocumentationSnapshot(int Version,string SourceId,string Url,string Title,string Text,string Sha256,DateTimeOffset CheckedAt,DateTimeOffset ChangedAt,string? ETag,DateTimeOffset? LastModified,bool Truncated);

/// <summary>Opt-in public reference text, never a command/template update channel.</summary>
public sealed partial class CommandDocumentationClient:IDisposable
{
    public const int MaximumResponseBytes=2*1024*1024,MaximumTextCharacters=100000;
    private readonly ProfileRepository repository;
    private readonly HttpClient http;
    private readonly bool ownsHttp;
    public CommandDocumentationClient(ProfileRepository repository,HttpClient? http=null)
    {
        this.repository=repository; ownsHttp=http==null;
        this.http=http??new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false,CheckCertificateRevocationList=true,AutomaticDecompression=DecompressionMethods.All }) { Timeout=Timeout.InfiniteTimeSpan };
    }
    private static string Key(string source)=>"commands.documentation.v1."+source;
    public async Task<CommandDocumentationSnapshot?> ReadAsync(string id,CancellationToken ct=default)
    {
        var source=CommandKnowledgeCatalog.Default.Source(id); var value=await repository.SettingAsync(Key(id),ct).ConfigureAwait(false); if(value==null)return null;
        if(value.Length>700000)throw new InvalidDataException(L.T("Zapisana dokumentacja jest uszkodzona. Odśwież ją ponownie."));
        try
        {
            var snapshot=JsonSerializer.Deserialize<CommandDocumentationSnapshot>(value,CommandValidation.Json)??throw new JsonException();
            if(snapshot.Version!=1 || snapshot.SourceId!=id || snapshot.Url!=source.Url || snapshot.Text==null || snapshot.Text.Length is <1 or >MaximumTextCharacters || snapshot.Title==null || snapshot.Title.Length>300 || snapshot.Sha256!=Hash(snapshot.Text) || Unsafe(snapshot.Text) || Unsafe(snapshot.Title) || snapshot.ETag?.Length>256 || snapshot.ETag?.Any(char.IsControl)==true)throw new JsonException();
            return snapshot;
        }
        catch(Exception ex) when(ex is JsonException or ArgumentException) { throw new InvalidDataException(L.T("Zapisana dokumentacja jest uszkodzona. Odśwież ją ponownie.")); }
    }
    public async Task<CommandDocumentationSnapshot> RefreshAsync(string id,CancellationToken ct=default)
    {
        var source=CommandKnowledgeCatalog.Default.Source(id); CommandDocumentationSnapshot? cached=null;
        try { cached=await ReadAsync(id,ct).ConfigureAwait(false); } catch(InvalidDataException) { /* explicit refresh can replace a malformed cache */ }
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15)); var token=timeout.Token;
        try
        {
            var original=new Uri(source.Url); var uri=new UriBuilder(original) { Fragment="" }.Uri;
            for(var redirects=0;redirects<=3;redirects++)
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,uri); request.Headers.UserAgent.ParseAdd("ServerDesk-Documentation/1.0"); request.Headers.Accept.ParseAdd("text/html, text/plain;q=0.9");
                if(cached?.ETag is {} tag && EntityTagHeaderValue.TryParse(tag,out var etag))request.Headers.IfNoneMatch.Add(etag);
                if(cached?.LastModified is {} modified)request.Headers.IfModifiedSince=modified;
                using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
                if(response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    if(redirects==3 || response.Headers.Location is not {} location)throw InvalidSource();
                    var next=new Uri(uri,location);
                    if(next.Scheme!="https" || !next.IsDefaultPort || next.UserInfo.Length>0 || !next.Host.Equals(original.Host,StringComparison.OrdinalIgnoreCase))throw InvalidSource();
                    uri=new UriBuilder(next) { Fragment="" }.Uri; continue;
                }
                var now=DateTimeOffset.UtcNow;
                if(response.StatusCode==HttpStatusCode.NotModified)
                {
                    if(cached==null)throw InvalidSource();
                    var checkedSnapshot=cached with { CheckedAt=now }; await SaveAsync(checkedSnapshot,token).ConfigureAwait(false); return checkedSnapshot;
                }
                if(!response.IsSuccessStatusCode)throw new HttpRequestException(L.T("Oficjalna dokumentacja jest teraz niedostępna. Zachowano poprzednią kopię."),null,response.StatusCode);
                var media=response.Content.Headers.ContentType?.MediaType;
                if(media is not ("text/html" or "application/xhtml+xml" or "text/plain" or "text/markdown") || response.Content.Headers.ContentLength>MaximumResponseBytes)throw InvalidSource();
                await using var input=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false); var bytes=await StreamLimits.ReadAsync(input,MaximumResponseBytes,token).ConfigureAwait(false);
                var extracted=await Task.Run(()=>Extract(new UTF8Encoding(false,true).GetString(bytes),media is "text/html" or "application/xhtml+xml"),token).ConfigureAwait(false);
                if(extracted.Text.Length<40)throw InvalidSource();
                var hash=Hash(extracted.Text); var etagValue=response.Headers.ETag?.ToString();
                if(etagValue?.Length>256)etagValue=null;
                var snapshot=new CommandDocumentationSnapshot(1,id,source.Url,extracted.Title.Length>0?extracted.Title:source.Title,extracted.Text,hash,now,cached?.Sha256==hash?cached.ChangedAt:now,etagValue,response.Content.Headers.LastModified,extracted.Truncated);
                await SaveAsync(snapshot,token).ConfigureAwait(false); return snapshot;
            }
            throw InvalidSource();
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) { throw new TimeoutException(L.T("Upłynął czas pobierania dokumentacji. Zachowano poprzednią kopię.")); }
        catch(DecoderFallbackException) { throw InvalidSource(); }
    }
    private Task SaveAsync(CommandDocumentationSnapshot snapshot,CancellationToken ct)=>repository.SetSettingAsync(Key(snapshot.SourceId),JsonSerializer.Serialize(snapshot,CommandValidation.Json),ct);
    private static InvalidDataException InvalidSource()=>new(L.T("Nie udało się odczytać oficjalnej dokumentacji. Zachowano poprzednią kopię."));
    private static string Hash(string text)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static bool Unsafe(string text)=>text.Any(c=>CommandValidation.UnsafeCharacter(c)&&c is not ('\n' or '\t'));
    public void Dispose() { if(ownsHttp)http.Dispose(); }
    internal static (string Title,string Text,bool Truncated) Extract(string value,bool html)
    {
        string title="";
        if(html)
        {
            var heading=TitleElement().Match(value); if(heading.Success)title=WebUtility.HtmlDecode(Tags().Replace(heading.Groups[1].Value,""));
            var main=MainElement().Match(value); if(!main.Success)main=ArticleElement().Match(value); if(main.Success)value=main.Groups[1].Value;
            foreach(var element in new[]{"head","script","style","nav","footer","header","aside","svg","form","template"})value=Regex.Replace(value,"<"+element+@"\b[^>]*>[\s\S]*?</"+element+@"\s*>","",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.NonBacktracking);
            value=Breaks().Replace(value,"\n"); value=WebUtility.HtmlDecode(Tags().Replace(value,""));
        }
        string Clean(string text)=>new(text.Where(c=>!CommandValidation.UnsafeCharacter(c)||c is '\n' or '\t').ToArray());
        title=Spaces().Replace(Clean(title)," ").Trim(); if(title.Length>300)title=title[..300];
        if(title.Length>0 && char.IsHighSurrogate(title[^1]))title=title[..^1];
        var body=new StringBuilder(); var blank=false; var truncated=false;
        foreach(var line in Clean(value.Replace("\r\n","\n")).Split('\n'))
        {
            var text=Spaces().Replace(line," ").Trim(); if(text.Length==0) { if(!blank && body.Length>0)body.Append('\n'); blank=true; continue; }
            blank=false; body.Append(text).Append('\n');
            if(body.Length>MaximumTextCharacters) { truncated=true; break; }
        }
        var result=body.ToString().Trim();
        if(result.Length>MaximumTextCharacters)result=result[..MaximumTextCharacters];
        if(result.Length>0 && char.IsHighSurrogate(result[^1]))result=result[..^1];
        return(title,result,truncated);
    }
    [GeneratedRegex(@"<title\b[^>]*>([\s\S]*?)</title\s*>",RegexOptions.IgnoreCase|RegexOptions.NonBacktracking)] private static partial Regex TitleElement();
    [GeneratedRegex(@"<main\b[^>]*>([\s\S]*?)</main\s*>",RegexOptions.IgnoreCase|RegexOptions.NonBacktracking)] private static partial Regex MainElement();
    [GeneratedRegex(@"<article\b[^>]*>([\s\S]*?)</article\s*>",RegexOptions.IgnoreCase|RegexOptions.NonBacktracking)] private static partial Regex ArticleElement();
    [GeneratedRegex(@"<[^>]*>",RegexOptions.NonBacktracking)] private static partial Regex Tags();
    [GeneratedRegex(@"<(?:br\b[^>]*|/(?:p|div|li|h[1-6]|pre|tr|dt|dd|section|blockquote))\s*>",RegexOptions.IgnoreCase|RegexOptions.NonBacktracking)] private static partial Regex Breaks();
    [GeneratedRegex(@"[^\S\r\n]+",RegexOptions.NonBacktracking)] private static partial Regex Spaces();
}
