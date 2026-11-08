namespace ServerDesk.Core;

public sealed record ProfileTagChange(Guid ProfileId,string OriginalTags,string UpdatedTags);

public sealed class ProfileTagConflictException:InvalidOperationException
{
    public ProfileTagConflictException():base(L.T("Połączenie usunięto lub jego tagi zmieniły się od otwarcia podglądu. Nic nie zapisano. Zamknij okno i otwórz je ponownie.")) { }
}
