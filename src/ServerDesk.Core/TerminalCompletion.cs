namespace ServerDesk.Core;

public enum TerminalEndReason { RemoteClosed, TransportError, Disposed }
public sealed record TerminalCompletion(TerminalEndReason Reason, string? ErrorType = null);
