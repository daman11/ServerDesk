using Microsoft.Data.Sqlite;

// Fixtures run concurrently with independent databases. Clearing every process
// pool can dispose another fixture's native SQLite handle during its next call.
internal static class DatabaseTestCleanup
{
    internal static void Clear(string root)
    {
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.Combine(root,"serverdesk.db") }.ToString());
        SqliteConnection.ClearPool(connection);
    }
}
