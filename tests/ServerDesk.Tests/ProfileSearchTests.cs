using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class ProfileSearchTests
{
    private static readonly ServerProfile Profile=new() { Name="Baza główna",Host="db.internal",Port=2222,Username="deploy",Domain="ACME",Tags="postgres, ważny",Folder="Produkcja/Linux",Description="Kopia zapasowa",Protocol=Protocol.SSH };
    [Theory]
    [InlineData("deploy linux")][InlineData(" SSH   2222 ")][InlineData("GŁÓWNA postgres")][InlineData("kopia ACME")][InlineData("db.internal ważny")][InlineData("")][InlineData("  \t\n ")]
    public void TermsMatchAcrossMetadataWithoutDependingOnTheirOrder(string query)=>Assert.True(ProfileSearch.Matches(Profile,query));
    [Theory][InlineData("linux RDP")][InlineData("deploy unknown")][InlineData("22222")]
    public void AllTermsMustMatch(string query)=>Assert.False(ProfileSearch.Matches(Profile,query));
}
