using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public static class BitwardenRuntime
{
    private sealed record Binary(string File,string ArchiveUrl,string ArchiveSha256,string Sha256);
    private sealed record Manifest(string Version,string Commit,string SourceUrl,string SourceSha256,string License,Dictionary<string,Binary> Runtimes);
    private static readonly Manifest Bundled=ReadManifest();
    public static string Version=>Bundled.Version;
    public static string Runtime=>RuntimeInformation.ProcessArchitecture==Architecture.X64?(OperatingSystem.IsWindows()?"win-x64":OperatingSystem.IsLinux()?"linux-x64":throw Unavailable()):throw Unavailable();
    public static string Executable(string baseDirectory)=>Path.Combine(baseDirectory,"components","bitwarden",Bundled.Runtimes[Runtime].File);
    public static string ProfileDirectory(string dataDirectory,BitwardenCliOptions options)
    {
        options.Validate();
        var id=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(options.EffectiveServer+"\n"+options.Email.ToLowerInvariant())));
        return Path.Combine(dataDirectory,"bitwarden-accounts",id);
    }
    internal static BitwardenCliOptions Resolve(BitwardenCliOptions options)
    {
        var paths=new AppPaths(); var directory=ProfileDirectory(paths.Data,options);
        foreach(var path in new[]{Path.GetDirectoryName(directory)!,directory})
        {
            Directory.CreateDirectory(path);
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new BitwardenException(BitwardenFailure.Configuration);
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        }
        return options with { Executable=Executable(AppContext.BaseDirectory),DataDirectory=directory };
    }
    public static async Task VerifyAsync(string executable,CancellationToken ct)
    {
        try
        {
            var info=new FileInfo(executable);
            if(!info.Exists || (info.Attributes&FileAttributes.ReparsePoint)!=0)throw Unavailable();
            await using var input=new FileStream(executable,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.Asynchronous|FileOptions.SequentialScan);
            var digest=Convert.ToHexStringLower(await SHA256.HashDataAsync(input,ct).ConfigureAwait(false));
            if(digest!=Bundled.Runtimes[Runtime].Sha256)throw Unavailable();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { throw Unavailable(); }
    }
    private static BitwardenException Unavailable()=>new(BitwardenFailure.ComponentUnavailable);
    private static Manifest ReadManifest()
    {
        using var input=typeof(BitwardenRuntime).Assembly.GetManifestResourceStream("ServerDesk.Infrastructure.Bitwarden.manifest.json")??throw new InvalidOperationException("Missing component manifest");
        return JsonSerializer.Deserialize<Manifest>(input,new JsonSerializerOptions { PropertyNameCaseInsensitive=true })??throw new InvalidOperationException("Invalid component manifest");
    }
}
