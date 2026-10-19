using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;
public sealed class NetworkToolsStore(ProfileRepository repository,ICredentialStore credentials)
{
    private readonly SemaphoreSlim gate=new(1);
    private const string HistoryKey="networkTools.history.v1",CollectionsKey="networkTools.collections.v1";
    public static NetworkRequest SafeHistory(NetworkRequest request)=>request with { Target=request.Tool==NetworkToolKind.Http?new Uri(request.Target).GetLeftPart(UriPartial.Authority):request.Target,Headers="",Query="",Body="",Username="",Authentication="None" };
    private async Task<List<T>> ReadAsync<T>(string key,CancellationToken ct)
    {
        return Parse<T>(await repository.SettingAsync(key,ct).ConfigureAwait(false));
    }
    private static List<T> Parse<T>(string? json)
    {
        if(json==null)return [];
        try
        {
            if(json.Length>600000)throw new JsonException();
            var rows=JsonSerializer.Deserialize<List<T>>(json)??throw new JsonException();
            if(rows.Count>200 || rows.Any(row=>!ValidRow(row)))throw new JsonException();
            var ids=rows.OfType<NetworkHistory>().Select(row=>row.Id).Concat(rows.OfType<NetworkCollection>().Select(row=>row.Id)).ToArray();
            if(ids.Distinct().Count()!=ids.Length)throw new JsonException(); return rows;
        }
        catch(Exception ex) when(ex is JsonException or ArgumentException)
        { throw new InvalidDataException(L.T("Nie można odczytać zapisanych danych narzędzi sieciowych. Oryginalne dane zachowano."),ex); }
    }
    private static bool ValidRow(object? row)=>row switch
    {
        string value=>value.Length is >=1 and <=512,
        NetworkCollection collection=>collection.Id!=Guid.Empty && !string.IsNullOrWhiteSpace(collection.Name) && collection.Name.Length<=100 && !collection.Name.Any(char.IsControl) && Enum.IsDefined(collection.Tool),
        NetworkHistory history=>history.Id!=Guid.Empty && history.When!=default && ValidHistory(history.Request),
        _=>false
    };
    private static bool ValidHistory(NetworkRequest? request)
    {
        if(request==null)return false; request.Validate();
        if(request.Headers.Length>0 || request.Query.Length>0 || request.Body.Length>0 || request.Username.Length>0 || request.Authentication!="None")return false;
        return request.Tool!=NetworkToolKind.Http || new Uri(request.Target).GetLeftPart(UriPartial.Authority)==request.Target.TrimEnd('/');
    }
    public async Task<IReadOnlyList<NetworkHistory>> HistoryAsync(CancellationToken ct=default)=>await ReadAsync<NetworkHistory>(HistoryKey,ct).ConfigureAwait(false);
    public async Task AddHistoryAsync(NetworkRequest request,CancellationToken ct=default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { await repository.UpdateSettingAsync(HistoryKey,json=> { var rows=Parse<NetworkHistory>(json); rows.Insert(0,new(Guid.NewGuid(),DateTimeOffset.UtcNow,SafeHistory(request))); return JsonSerializer.Serialize(rows.Take(100)); },ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task ClearHistoryAsync(CancellationToken ct=default)
    { await gate.WaitAsync(ct).ConfigureAwait(false); try { await repository.SetSettingAsync(HistoryKey,"[]",ct).ConfigureAwait(false); } finally { gate.Release(); } }
    public async Task DeleteHistoryAsync(Guid id,CancellationToken ct=default)
    { await gate.WaitAsync(ct).ConfigureAwait(false); try { await repository.UpdateSettingAsync(HistoryKey,json=>JsonSerializer.Serialize(Parse<NetworkHistory>(json).Where(row=>row.Id!=id)),ct).ConfigureAwait(false); } finally { gate.Release(); } }
    public async Task<IReadOnlyList<NetworkCollection>> CollectionsAsync(CancellationToken ct=default)=>await ReadAsync<NetworkCollection>(CollectionsKey,ct).ConfigureAwait(false);
    public async Task SaveCollectionAsync(string name,NetworkRequest request,NetworkSecrets secrets,CancellationToken ct=default)
    {
        name=name?.Trim()??""; if(name.Length is <1 or >100 || name.Any(char.IsControl))throw new ArgumentException(L.T("Nazwa kolekcji musi mieć od 1 do 100 znaków.")); request.Validate();
        var json=JsonSerializer.Serialize(new SavedNetworkRequest(request,secrets)); if(System.Text.Encoding.UTF8.GetByteCount(json)>48000)throw new ArgumentException(L.T("Kolekcja przekracza limit 48 KiB bezpiecznego magazynu."));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var rows=await ReadAsync<NetworkCollection>(CollectionsKey,ct).ConfigureAwait(false); if(rows.Count>=100)throw new ArgumentException(L.T("Usuń kolekcję przed dodaniem kolejnej (limit 100)."));
            var entry=new NetworkCollection(Guid.NewGuid(),name,request.Tool); await credentials.SaveAsync(entry.Id,json,ct).ConfigureAwait(false);
            try { await repository.UpdateSettingAsync(CollectionsKey,json=> { var latest=Parse<NetworkCollection>(json); if(latest.Count>=100)throw new ArgumentException(L.T("Usuń kolekcję przed dodaniem kolejnej (limit 100).")); latest.Insert(0,entry); return JsonSerializer.Serialize(latest); },ct).ConfigureAwait(false); }
            catch
            {
                // A failed acknowledgement may follow a committed SQLite write.
                // Keep the blob unless we can prove the index does not reference it.
                var referenced=true;
                try { referenced=(await CollectionsAsync(CancellationToken.None).ConfigureAwait(false)).Any(row=>row.Id==entry.Id); } catch { }
                if(!referenced)try { await credentials.DeleteAsync(entry.Id,CancellationToken.None).ConfigureAwait(false); } catch { }
                throw;
            }
        }
        finally { gate.Release(); }
    }
    public async Task<SavedNetworkRequest> LoadCollectionAsync(Guid id,CancellationToken ct=default)
    {
        var entry=(await CollectionsAsync(ct).ConfigureAwait(false)).SingleOrDefault(row=>row.Id==id)??throw new ArgumentException(L.T("Kolekcja nie istnieje."));
        var json=await credentials.GetAsync(id,ct).ConfigureAwait(false); if(json==null || System.Text.Encoding.UTF8.GetByteCount(json)>48000)throw new InvalidDataException(L.T("Nie można odczytać kolekcji z bezpiecznego magazynu systemowego."));
        try
        {
            var saved=JsonSerializer.Deserialize<SavedNetworkRequest>(json)??throw new JsonException();
            if(saved.Request==null || saved.Secrets==null || saved.Request.Tool!=entry.Tool || new[]{saved.Secrets.Password,saved.Secrets.Token,saved.Secrets.Community,saved.Secrets.AuthPassword,saved.Secrets.PrivacyPassword}.Any(value=>value==null))throw new JsonException();
            saved.Request.Validate(); return saved;
        }
        catch(Exception ex) when(ex is JsonException or ArgumentException) { throw new InvalidDataException(L.T("Niepoprawna kolekcja. Zapisane dane pozostają zachowane."),ex); }
    }
    public async Task DeleteCollectionAsync(Guid id,CancellationToken ct=default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var rows=await ReadAsync<NetworkCollection>(CollectionsKey,ct).ConfigureAwait(false); if(!rows.Any(row=>row.Id==id))return;
            // Commit removal first: a database error must leave the saved request
            // readable. Failure to retire an encrypted orphan cannot undo deletion.
            await repository.UpdateSettingAsync(CollectionsKey,json=>JsonSerializer.Serialize(Parse<NetworkCollection>(json).Where(row=>row.Id!=id)),ct).ConfigureAwait(false);
            try { await credentials.DeleteAsync(id,CancellationToken.None).ConfigureAwait(false); } catch { }
        }
        finally { gate.Release(); }
    }
    public async Task<string[]> ValuesAsync(string key,CancellationToken ct=default)=> (await ReadAsync<string>("networkTools."+key,ct).ConfigureAwait(false)).Where(value=>value.Length<=512).Take(30).ToArray();
    public async Task RememberAsync(string key,string value,CancellationToken ct=default)
    {
        if(value.Length is <1 or >512)return; await gate.WaitAsync(ct).ConfigureAwait(false);
        try { await repository.UpdateSettingAsync("networkTools."+key,json=>JsonSerializer.Serialize(new[]{value}.Concat(Parse<string>(json)).Distinct().Take(30)),ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
}
public sealed record SavedNetworkRequest(NetworkRequest Request,NetworkSecrets Secrets);
