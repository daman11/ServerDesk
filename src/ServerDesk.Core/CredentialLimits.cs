using System.Text;

namespace ServerDesk.Core;

public static class CredentialLimits
{
    public const int MaximumSecretBytes=64*1024;
    public const int MaximumProtectedBytes=128*1024;
    public static void Validate(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if(Encoding.UTF8.GetByteCount(secret)>MaximumSecretBytes)throw new ArgumentException(L.T("Poświadczenie przekracza limit 64 KiB."));
    }
}

public sealed class CredentialReadException(Exception? inner=null) : InvalidOperationException(L.T("Nie można odczytać zapisanego hasła. Wpisz je ponownie; zapisany wpis pozostanie bez zmian, dopóki nie wybierzesz zapamiętania nowego hasła."),inner);
