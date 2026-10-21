using System.Net;
using System.Net.Http;
using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

// The repository is compiled into the application. No tokens, profile data or
// configurable download/command endpoints are accepted by the updater.
public sealed class GitHubUpdateClient : IDisposable
{
    private readonly HttpClient http;
    public GitHubUpdateClient():this(new HttpClientHandler { AllowAutoRedirect=false,UseCookies=false }) { }
    public GitHubUpdateClient(HttpMessageHandler handler)
    {
        http=new(handler) { Timeout=TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ServerDesk-Updater/1.0");
    }
    public async Task<ApplicationUpdate?> CheckAsync(Version current,CancellationToken ct=default)
    {
        using var json=await MetadataAsync($"https://api.github.com/repos/{ApplicationUpdate.Repository}/releases?per_page=100",ct);
        if(json.RootElement.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Invalid GitHub release list.");
        ApplicationUpdate? newest=null;
        foreach(var release in json.RootElement.EnumerateArray())
        {
            if(release.GetProperty("draft").GetBoolean() || current.Major>=1 && release.GetProperty("prerelease").GetBoolean())continue;
            var tag=release.GetProperty("tag_name").GetString()??"";
            if(!tag.StartsWith('v'))continue;
            Version version; try { version=ApplicationUpdate.ParseVersion(tag[1..]); } catch(InvalidDataException) { continue; }
            if(version<=current || newest!=null && version<=ApplicationUpdate.ParseVersion(newest.Version))continue;
            newest=ReadAsset(release,tag[1..]);
        }
        return newest;
    }
    public async Task<ApplicationUpdate> GetReleaseAsync(string version,CancellationToken ct=default)
    {
        ApplicationUpdate.ParseVersion(version);
        using var json=await MetadataAsync($"https://api.github.com/repos/{ApplicationUpdate.Repository}/releases/tags/v{version}",ct);
        if(json.RootElement.GetProperty("draft").GetBoolean() || json.RootElement.GetProperty("tag_name").GetString()!="v"+version)throw new InvalidDataException("Release unavailable.");
        return ReadAsset(json.RootElement,version);
    }
    private static ApplicationUpdate ReadAsset(JsonElement release,string version)
    {
        var assets=release.GetProperty("assets").EnumerateArray().Where(a=>a.GetProperty("name").GetString()==$"ServerDesk-{version}-win-x64-Setup.exe").ToArray();
        if(assets.Length!=1)throw new InvalidDataException("Release must contain exactly one Windows installer.");
        var asset=assets[0]; var digest=asset.GetProperty("digest").GetString()??"";
        if(asset.GetProperty("state").GetString()!="uploaded" || !digest.StartsWith("sha256:",StringComparison.Ordinal))throw new InvalidDataException("GitHub SHA-256 digest is unavailable.");
        var result=new ApplicationUpdate(version,asset.GetProperty("browser_download_url").GetString()??"",asset.GetProperty("size").GetInt64(),digest[7..]);
        result.Validate(); return result;
    }
    private async Task<JsonDocument> MetadataAsync(string url,CancellationToken ct)
    {
        using var response=await GetAsync(new(url),false,ct); response.EnsureSuccessStatusCode();
        using var stream=await response.Content.ReadAsStreamAsync(ct); using var memory=new MemoryStream();
        await CopyBoundedAsync(stream,memory,2*1024*1024,null,ct);
        var json=JsonDocument.Parse(memory.ToArray(),new() { MaxDepth=32 });
        try { RejectDuplicates(json.RootElement); return json; } catch { json.Dispose(); throw; }
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in element.EnumerateObject()) { if(!names.Add(property.Name))throw new InvalidDataException("Duplicate update metadata property."); RejectDuplicates(property.Value); }
        }
        else if(element.ValueKind==JsonValueKind.Array)foreach(var child in element.EnumerateArray())RejectDuplicates(child);
    }
    private async Task<HttpResponseMessage> GetAsync(Uri uri,bool asset,CancellationToken ct)
    {
        for(var count=0;count<5;count++)
        {
            var allowed=asset?uri.Host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com":uri.Host=="api.github.com";
            if(!allowed || uri.Scheme!="https" || !uri.IsDefaultPort || uri.UserInfo.Length!=0 || uri.Fragment.Length!=0)throw new InvalidDataException("Untrusted update destination.");
            using var request=new HttpRequestMessage(HttpMethod.Get,uri);
            if(!asset) { request.Headers.Accept.ParseAdd("application/vnd.github+json"); request.Headers.Add("X-GitHub-Api-Version","2022-11-28"); }
            var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if(response.StatusCode is not (HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))return response;
            var next=response.Headers.Location; response.Dispose();
            if(next==null)throw new InvalidDataException("Missing update redirect."); uri=new(uri,next);
        }
        throw new InvalidDataException("Too many update redirects.");
    }
    public async Task<string> DownloadAsync(ApplicationUpdate update,string directory,IProgress<long>? progress=null,CancellationToken ct=default)
    {
        update.Validate(); Directory.CreateDirectory(directory);
        var target=Path.Combine(directory,update.FileName); var partial=target+".part";
        // Each attempt uses a new owned directory; never replace an existing file.
        var ownsPartial=false;
        try
        {
            using var response=await GetAsync(new(update.DownloadUrl),true,ct); response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is {} length && length!=update.Size)throw new InvalidDataException("Update content length mismatch.");
            await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,65536,FileOptions.Asynchronous))
            {
                ownsPartial=true;
                using var input=await response.Content.ReadAsStreamAsync(ct);
                await CopyBoundedAsync(input,output,update.Size,progress,ct); await output.FlushAsync(ct); await update.VerifyAsync(output,ct);
            }
            File.Move(partial,target); return target;
        }
        catch { if(ownsPartial)try { File.Delete(partial); } catch(IOException) { } throw; }
    }
    private static async Task CopyBoundedAsync(Stream input,Stream output,long limit,IProgress<long>? progress,CancellationToken ct)
    {
        var buffer=new byte[65536]; long total=0; int count;
        while((count=await input.ReadAsync(buffer,ct))!=0)
        {
            total+=count; if(total>limit)throw new InvalidDataException("Update response exceeds size limit.");
            await output.WriteAsync(buffer.AsMemory(0,count),ct); progress?.Report(total);
        }
    }
    public void Dispose()=>http.Dispose();
}
