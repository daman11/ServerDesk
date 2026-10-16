namespace ServerDesk.Core;

public enum SerialParity { None,Odd,Even,Mark,Space }
public enum SerialStopBits { One=1,Two=2,OnePointFive=3 }
public enum SerialHandshake { None,XOnXOff,RequestToSend,RequestToSendXOnXOff }
public sealed record SerialOptions
{
    public string PortName { get; init; }="";
    public int BaudRate { get; init; }=115200;
    public int DataBits { get; init; }=8;
    public SerialParity Parity { get; init; }
    public SerialStopBits StopBits { get; init; }=SerialStopBits.One;
    public SerialHandshake Handshake { get; init; }
    public bool DtrEnable { get; init; }
    public bool RtsEnable { get; init; }
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(PortName)||PortName.Any(char.IsControl)||PortName!=PortName.Trim())throw new ArgumentException(L.T("Wskaż port COM lub urządzenie szeregowe, np. COM3 albo /dev/ttyUSB0."));
        if(BaudRate is <50 or >4000000 || DataBits is <5 or >8 || !Enum.IsDefined(Parity)||!Enum.IsDefined(StopBits)||!Enum.IsDefined(Handshake))throw new ArgumentException(L.T("Niepoprawne parametry portu szeregowego."));
    }
    public string Summary=>$"{PortName} · {BaudRate} · {DataBits}{Parity.ToString()[0]}{(StopBits==SerialStopBits.OnePointFive?"1.5":((int)StopBits).ToString())}";
}
