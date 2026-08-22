using TutorSphere.Application.Common;

namespace TutorSphere.UnitTests;

public class FamilyResidenceTests
{
    [Fact]
    public void RequireIso_accepts_cameroon()
    {
        Assert.Equal("CM", FamilyResidence.RequireIso("cm"));
        Assert.Equal("CM", FamilyResidence.RequireIso("CM"));
    }

    [Fact]
    public void RequireIso_rejects_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => FamilyResidence.RequireIso(null));
        Assert.Equal(FamilyResidence.RequiredMessage, ex.Message);
        Assert.Throws<InvalidOperationException>(() => FamilyResidence.RequireIso(" "));
        Assert.Throws<InvalidOperationException>(() => FamilyResidence.RequireIso("xyz"));
    }

    [Fact]
    public void Child_country_falls_back_to_parent()
    {
        Assert.Equal("CM", FamilyResidence.EffectiveChildCountry(null, "CM"));
        Assert.Equal("FR", FamilyResidence.EffectiveChildCountry("FR", "CM"));
        Assert.Null(FamilyResidence.EffectiveChildCountry(null, null));
    }

    [Theory]
    [InlineData("CM", "XAF", false, true, true)]
    [InlineData("CA", "CAD", true, false, false)]
    [InlineData("FR", "EUR", true, false, false)]
    public void Payment_methods_follow_parent_country(
        string country,
        string currency,
        bool card,
        bool paypal,
        bool momo)
    {
        Assert.Equal(card, ParentPaymentMethods.AllowsCard(country, currency));
        Assert.Equal(paypal, ParentPaymentMethods.AllowsPayPal(country, currency));
        Assert.Equal(momo, ParentPaymentMethods.AllowsMobileMoney(country, currency));
    }
}
