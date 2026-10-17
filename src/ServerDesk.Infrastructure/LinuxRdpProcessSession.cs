using System.Diagnostics;
using System.Text;

namespace ServerDesk.Infrastructure;

/// <summary>Owns one GUI-authenticated FreeRDP child. No secret reaches argv, files or logs.</summary>
public sealed class LinuxRdpProcessSession:IAsyncDisposable
{
    private readonly Process process;
    private readonly string configuration;
    private readonly object gate=new();
    private Task? stopping;
    public Task<int> Completion { get; }
    public int ProcessId { get; }
    public bool IsRunning=>!Completion.IsCompleted;
    private LinuxRdpProcessSession(Process process,string configuration)
    { this.process=process; this.configuration=configuration; ProcessId=process.Id; Completion=ObserveAsync(); }
    public static void ValidatePassword(string password)
    {
        if(password.Contains('\r') || password.Contains('\n') || password.Contains('\0') || Encoding.UTF8.GetByteCount(password)>511)
            throw new ArgumentException(L.T("Klient FreeRDP obsługuje hasło do 511 bajtów UTF-8, bez znaków nowej linii i NUL. Hasło nie zostało wysłane."));
    }
    public static async Task<LinuxRdpProcessSession> StartAsync(string executable,string[] arguments,string password,CancellationToken ct)
    {
        ValidatePassword(password); ct.ThrowIfCancellationRequested();
        var configuration=Path.Combine(Path.GetTempPath(),"serverdesk-rdp-"+Guid.NewGuid().ToString("N"));
        if(OperatingSystem.IsLinux())Directory.CreateDirectory(configuration,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute); else Directory.CreateDirectory(configuration);
        LinuxRdpProcessSession? session=null;
        try
        {
            var info=new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false) };
            foreach(var arg in arguments)info.ArgumentList.Add(arg);
            // Do not inherit an unrelated client's trust cache or let it persist consent.
            info.Environment["XDG_CONFIG_HOME"]=configuration;
            var process=Process.Start(info)??throw new InvalidOperationException(L.T("Nie można uruchomić klienta RDP."));
            session=new(process,configuration);
            await process.StandardInput.WriteLineAsync(password.AsMemory(),ct); await process.StandardInput.FlushAsync(ct); process.StandardInput.Close();
            return session;
        }
        catch
        {
            if(session!=null)await session.StopAsync(); else RemoveConfiguration(configuration);
            throw;
        }
    }
    private async Task<int> ObserveAsync()
    {
        // Drain, never retain or display raw client output: it may contain identities
        // or implementation diagnostics. Exit codes drive the localized GUI message.
        var output=process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        var error=process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        try { await Task.WhenAll(output,error,process.WaitForExitAsync()); return process.ExitCode; }
        finally { RemoveConfiguration(configuration); }
    }
    private static void RemoveConfiguration(string path)
    {
        // Only this instance's random directory. Never traverse child links.
        void Remove(DirectoryInfo directory)
        {
            if(directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) { directory.Delete(); return; }
            foreach(var item in directory.EnumerateFileSystemInfos())
            {
                if(item is DirectoryInfo child && !item.Attributes.HasFlag(FileAttributes.ReparsePoint))Remove(child);
                else if(item is DirectoryInfo link)link.Delete(); else item.Delete();
            }
            directory.Delete();
        }
        if(Directory.Exists(path))Remove(new DirectoryInfo(path));
    }
    public Task StopAsync() { lock(gate)return stopping??=StopCoreAsync(); }
    private async Task StopCoreAsync()
    {
        try
        {
            if(!Completion.IsCompleted) { try { process.Kill(entireProcessTree:true); } catch(InvalidOperationException)when(process.HasExited) { } }
            await Completion;
        }
        finally { process.Dispose(); }
    }
    public async ValueTask DisposeAsync()=>await StopAsync();
}
