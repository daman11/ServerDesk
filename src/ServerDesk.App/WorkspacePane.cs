using Avalonia.Controls;

namespace ServerDesk.App;

// Shared lifecycle for connection and diagnostic tabs in the existing dock workspace.
internal abstract class WorkspacePane : Grid,IDisposable
{
    internal Guid WorkspaceId { get; }=Guid.NewGuid();
    public bool IsActive { get; set; }
    internal abstract string DisplayName { get; }
    internal virtual void BeginReparent() { }
    internal virtual void EndReparent() { }
    internal abstract void FocusContent();
    public abstract void Dispose();
}
