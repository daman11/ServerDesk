namespace ServerDesk.Infrastructure;

// Cleanup cannot be guaranteed after transport loss or a local delete failure.
// Keep the exact owned temporary path so the user can inspect it; never sweep
// similarly named files that might belong to another active transfer.
public sealed record TransferCleanupWarning(string Path,bool Remote);
