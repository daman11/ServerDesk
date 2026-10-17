using System.Text;
using Avalonia.Platform.Storage;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static class ExportDestination
{
    internal static Task SaveAsync(IStorageFile file,Func<Stream,CancellationToken,Task> write,CancellationToken ct)
        =>SaveAsync(file.TryGetLocalPath(),write,ct);
    internal static async Task SaveAsync(string? path,Func<Stream,CancellationToken,Task> write,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(path))throw new NotSupportedException(L.T("Eksport wymaga pliku ze ścieżką w systemie plików. Wybierz folder na dysku komputera."));
        try { await ExportFile.WriteAsync(path,write,ct); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        { throw new InvalidOperationException(L.T("Nie udało się zapisać eksportu. Poprzedni plik pozostał zachowany. Sprawdź wolne miejsce, uprawnienia i blokadę pliku, a następnie ponów zapis."),ex); }
    }
    internal static async Task WriteTextAsync(Stream stream,string content,CancellationToken ct)
    {
        await using var writer=new StreamWriter(stream,new UTF8Encoding(false),1024,leaveOpen:true);
        await writer.WriteAsync(content.AsMemory(),ct); await writer.FlushAsync(ct);
    }
}
