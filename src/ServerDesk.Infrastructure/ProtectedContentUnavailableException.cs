namespace ServerDesk.Infrastructure;

// A null vault result cannot prove deletion: Secret Service also uses null for
// some availability failures. Offer retry and recovery guidance without claiming
// that a database snapshot contains the separately protected content.
public sealed class ProtectedContentUnavailableException(string message) : InvalidOperationException(message);
