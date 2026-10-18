using Microsoft.Data.Sqlite;
using System.Text;

namespace ServerDesk.Infrastructure;

public static class ExceptionDiagnostics
{
    // General UI operations may contain remote replies or credentials in Message/Data.
    // Record types, codes and complete stacks, without copying that untrusted content.
    public static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var result=new StringBuilder(); var seen=new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        void Append(Exception cause)
        {
            if(seen.Count>=16 || !seen.Add(cause))return;
            result.Append(cause.GetType().FullName).Append(" HResult=0x").Append(cause.HResult.ToString("X8"));
            if(cause is SqliteException database)result.Append(" SQLite=").Append(database.SqliteErrorCode).Append('/').Append(database.SqliteExtendedErrorCode);
            if(cause is System.Net.Sockets.SocketException socket)result.Append(" SocketError=").Append(socket.SocketErrorCode);
            if(cause is Renci.SshNet.Common.SshConnectionException ssh)result.Append(" SshDisconnectReason=").Append(ssh.DisconnectReason);
            if(cause is SshLoginException login)result.Append(" SshAuthentication=").Append(login.Method).Append(" OfferedMethods=").Append(string.Join(",",login.OfferedMethods));
            if(cause is SshPrivateKeyException key)result.Append(" SshPrivateKeyReason=").Append(key.Reason);
            result.AppendLine().AppendLine(cause.StackTrace??"<no stack trace>");
            if(cause is AggregateException aggregate)foreach(var nested in aggregate.InnerExceptions)Append(nested);
            else if(cause.InnerException is {} inner)Append(inner);
        }
        Append(error); return result.ToString();
    }
}
