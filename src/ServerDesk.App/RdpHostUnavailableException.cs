namespace ServerDesk.App;

internal sealed class RdpHostUnavailableException(Exception inner) : InvalidOperationException(
    L.T("Nie udało się otworzyć pulpitu RDP w zakładce na tym komputerze. Możesz ponowić próbę lub otworzyć tę sesję w osobnym oknie."),inner);
