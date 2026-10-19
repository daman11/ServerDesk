using Microsoft.Data.Sqlite;
using System.Text;

namespace ServerDesk.Infrastructure;

public static class ExceptionDiagnostics
{
    public const int MaximumCharacters=64*1024;
    // General UI operations may contain remote replies or credentials in Message/Data.
    // Record types, codes and bounded stacks, without copying that untrusted content.
    public static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var result=new StringBuilder(); var seen=new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        void Append(Exception cause)
        {
            if(result.Length>=MaximumCharacters || seen.Count>=16 || !seen.Add(cause))return;
            result.Append(cause.GetType().FullName).Append(" HResult=0x").Append(cause.HResult.ToString("X8"));
            if(cause is SqliteException database)result.Append(" SQLite=").Append(database.SqliteErrorCode).Append('/').Append(database.SqliteExtendedErrorCode);
            if(cause is System.Net.Sockets.SocketException socket)result.Append(" SocketError=").Append(socket.SocketErrorCode);
            if(cause is Renci.SshNet.Common.SshConnectionException ssh)result.Append(" SshDisconnectReason=").Append(ssh.DisconnectReason);
            if(cause is SshLoginException login)result.Append(" SshAuthentication=").Append(login.Method).Append(" OfferedMethods=").Append(string.Join(",",login.OfferedMethods));
            if(cause is SshPrivateKeyException key)result.Append(" SshPrivateKeyReason=").Append(key.Reason);
            string trace;
            try { trace=cause.StackTrace??"<no stack trace>"; }
            catch { trace="<stack trace unavailable>"; }
            result.AppendLine(); result.Append(trace.AsSpan(0,Math.Min(trace.Length,Math.Max(0,MaximumCharacters-result.Length)))); result.AppendLine();
            if(cause is AggregateException aggregate)foreach(var nested in aggregate.InnerExceptions)Append(nested);
            else if(cause.InnerException is {} inner)Append(inner);
        }
        Append(error); return result.Length>=MaximumCharacters?result.ToString(0,MaximumCharacters)+"\n<truncated>":result.ToString();
    }
}
