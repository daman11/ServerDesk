using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class SessionSearchTests
{
    private readonly SessionSearchEntry[] entries=[new(Guid.NewGuid(),"Produkcja Baza", "db.example", "admin", "SSH", "Panel 2", "Połączono"),new(Guid.NewGuid(),"Łódź staging", "10.2.3.4", "deploy", "RDP", "Osobne okno", "Błąd")];
    [Theory]
    [InlineData("baza ADMIN panel 2",0)] [InlineData("10.2 RDP błąd",1)] [InlineData("produkcja ssh",0)]
    public void All_query_terms_can_match_different_fields(string query,int index)=>Assert.Equal(entries[index],Assert.Single(SessionSearch.Filter(entries,query)));
    [Fact] public void Empty_query_preserves_order_and_nonmatching_query_is_empty() { Assert.Equal(entries,SessionSearch.Filter(entries,"  ")); Assert.Empty(SessionSearch.Filter(entries,"missing")); }
}
