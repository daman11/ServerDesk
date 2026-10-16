namespace ServerDesk.Core;

public enum VncAuthentication { Password,None }
public sealed record VncOptions
{
    public VncAuthentication Authentication { get; init; }
    public bool Shared { get; init; }=true;
    public bool ViewOnly { get; init; }
    public bool FitToWindow { get; init; }=true;
    public void Validate()
    { if(!Enum.IsDefined(Authentication))throw new ArgumentException(L.T("Niepoprawna metoda uwierzytelniania VNC.")); }
}
