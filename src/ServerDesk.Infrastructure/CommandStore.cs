using System.Text;
using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

// Definitions can accidentally contain sensitive data. They use the same OS-backed
// credential store as saved connections; the SQLite setting holds an opaque vault ID.
// UI preferences, ID-only favourites and optional ID/timestamp history are nonsecret.
public sealed class CommandStore(ProfileRepository repository,ICredentialStore credentials)
{
    private readonly SemaphoreSlim gate=new(1);
    private const string VaultKey="commands.vault.v1",StateKey="commands.state.v1";
    private sealed record Manifest(int Version,Guid[] Parts);
    private static Guid[] Parts(string? value)
    {
        if(value==null)return [];
        var manifest=JsonSerializer.Deserialize<Manifest>(value,CommandValidation.Json);
        if(manifest==null||manifest.Version!=1||manifest.Parts==null||manifest.Parts.Length is <1 or >128||manifest.Parts.Contains(Guid.Empty))throw new InvalidDataException(L.T("Niepoprawny odnośnik biblioteki komend."));
        return manifest.Parts;
    }
    public async Task<CommandUserLibrary> ReadLibraryAsync(CancellationToken ct=default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { return await ReadLibraryCoreAsync(ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    private async Task<CommandUserLibrary> ReadLibraryCoreAsync(CancellationToken ct)
    {
        var pointer=await repository.SettingAsync(VaultKey,ct).ConfigureAwait(false); if(pointer==null)return new();
        using var bytes=new MemoryStream();
        foreach(var id in Parts(pointer))
        {
            var text=await credentials.GetAsync(id,ct).ConfigureAwait(false)??throw new InvalidOperationException(L.T("Odblokuj systemowy magazyn poświadczeń, aby otworzyć własne komendy."));
            if(text.Length>50000)throw new InvalidDataException("Invalid command vault part.");
            var part=Convert.FromBase64String(text); if(bytes.Length+part.Length>CommandValidation.MaxDocumentBytes)throw new InvalidDataException("Command vault too large."); bytes.Write(part);
        }
        return Parse(new UTF8Encoding(false,true).GetString(bytes.ToArray()));
    }
    public async Task SaveLibraryAsync(CommandUserLibrary library,CancellationToken ct=default)
    {
        CommandValidation.Validate(library); var text=JsonSerializer.Serialize(library,CommandValidation.Json);
        // Windows Credential Manager is not used here: ServerDesk's credential
        // abstraction uses DPAPI files / Secret Service. Keep the document bounded.
        if(Encoding.UTF8.GetByteCount(text)>CommandValidation.MaxDocumentBytes)throw new ArgumentException(L.T("Biblioteka przekracza limit 4 MiB."));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var previous=Parts(await repository.SettingAsync(VaultKey,ct).ConfigureAwait(false)); var parts=new List<Guid>(); string? newPointer=null;
            try
            {
                var bytes=Encoding.UTF8.GetBytes(text);
                for(var offset=0;offset<bytes.Length;offset+=32768)
                {
                    var id=Guid.NewGuid(); parts.Add(id);
                    await credentials.SaveAsync(id,Convert.ToBase64String(bytes,offset,Math.Min(32768,bytes.Length-offset)),ct).ConfigureAwait(false);
                }
                newPointer=JsonSerializer.Serialize(new Manifest(1,parts.ToArray()),CommandValidation.Json);
                await repository.SetSettingAsync(VaultKey,newPointer,ct).ConfigureAwait(false);
            }
            catch
            {
                // Cancellation/storage errors can leave commit acknowledgement
                // uncertain. Never delete blobs that the database may reference.
                var mayBeCommitted=newPointer!=null;
                if(newPointer!=null)try { mayBeCommitted=await repository.SettingAsync(VaultKey,CancellationToken.None).ConfigureAwait(false)==newPointer; } catch { }
                if(!mayBeCommitted)foreach(var id in parts)try { await credentials.DeleteAsync(id,CancellationToken.None).ConfigureAwait(false); } catch { }
                throw;
            }
            // The new snapshot is committed before retiring the old encrypted blob.
            foreach(var old in previous)try { await credentials.DeleteAsync(old,CancellationToken.None).ConfigureAwait(false); } catch { /* harmless encrypted orphan; never roll back a committed snapshot */ }
        }
        finally { gate.Release(); }
    }
    public async Task<CommandUserState> ReadStateAsync(CancellationToken ct=default)
    {
        var text=await repository.SettingAsync(StateKey,ct).ConfigureAwait(false); if(text==null)return new();
        try
        {
            if(text.Length>600000)throw new JsonException();
            var state=JsonSerializer.Deserialize<CommandUserState>(text,CommandValidation.Json)??throw new JsonException(); ValidateState(state); return state;
        }
        catch(Exception ex) when(ex is JsonException or ArgumentException) { throw new InvalidDataException(L.T("Niepoprawny stan panelu komend. Biblioteka pozostaje zachowana."),ex); }
    }
    public async Task SaveStateAsync(CommandUserState state,CancellationToken ct=default)
    { ValidateState(state); await repository.SetSettingAsync(StateKey,JsonSerializer.Serialize(state,CommandValidation.Json),ct).ConfigureAwait(false); }
    private static void ValidateState(CommandUserState state)
    {
        if(state.Favorites==null||state.Recent==null||state.Preferences==null||state.Hosts==null||state.Favorites.Length>4000||state.Recent.Length>100||state.Hosts.Count>2000||state.Favorites.Any(s=>s==null||s.Length>120)||state.Recent.Any(r=>r==null||r.CommandId==null||r.CommandId.Length>120))throw new ArgumentException("Invalid command state.");
        foreach(var p in state.Hosts.Values.Prepend(state.Preferences))if(p==null||p.System==null||!Systems.Contains(p.System)||p.Version==null||p.Version.Length>40||p.Version.Any(char.IsControl)||!Enum.IsDefined(p.Shell))throw new ArgumentException("Invalid command preferences.");
    }
    public static string[] Systems { get; }=["general","linux","ubuntu","debian","rhel","rocky","almalinux","centos","fedora","arch","alpine","windows","windows10","windows11","windows-server"];
    public static CommandUserLibrary Parse(string text)
    {
        if(Encoding.UTF8.GetByteCount(text)>CommandValidation.MaxDocumentBytes)throw new InvalidDataException(L.T("Biblioteka przekracza limit 4 MiB."));
        try
        {
            using var json=JsonDocument.Parse(text,new() { MaxDepth=20 }); RejectDuplicates(json.RootElement);
            var document=json.RootElement.Deserialize<CommandUserLibrary>(CommandValidation.Json)??throw new JsonException(); CommandValidation.Validate(document); return document;
        }
        catch(JsonException ex) { throw new InvalidDataException(L.T("Niepoprawny plik JSON biblioteki komend."),ex); }
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object)
        {
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in element.EnumerateObject()) { if(!keys.Add(property.Name))throw new JsonException("Duplicate property."); RejectDuplicates(property.Value); }
        }
        else if(element.ValueKind==JsonValueKind.Array)foreach(var item in element.EnumerateArray())RejectDuplicates(item);
    }
    public static CommandUserLibrary Merge(CommandUserLibrary current,CommandUserLibrary imported)
    {
        CommandValidation.Validate(current); CommandValidation.Validate(imported);
        // Import never overwrites existing definitions, IDs, favourites or app settings.
        var commands=current.Commands.Concat(imported.Commands.Select(c=>c with { Id="user."+Guid.NewGuid().ToString("N"),Origin=CommandOrigin.User,HostId=null,HostFolder="" })).ToArray();
        var merged=new CommandUserLibrary { Commands=commands,Categories=current.Categories.Concat(imported.Categories).Distinct(StringComparer.Ordinal).ToArray() }; CommandValidation.Validate(merged); return merged;
    }
    public static CommandUserLibrary Duplicate(CommandUserLibrary current,CommandDefinition source)
    {
        var command=source with { Id="user."+Guid.NewGuid().ToString("N"),Origin=CommandOrigin.User,Operation="",Name=source.Name+" (kopia)",NameEn=source.NameEn.Length>0?source.NameEn+" (copy)":"" };
        var updated=current with { Commands=current.Commands.Append(command).ToArray() }; CommandValidation.Validate(updated); return updated;
    }
}
