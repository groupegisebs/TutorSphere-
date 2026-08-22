using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.Payments;

namespace TutorSphere.UnitTests;

public class ParentPaymentMethodsTests
{
    [Theory]
    [InlineData("CA", "CAD", false, false)]
    [InlineData("US", "USD", false, false)]
    [InlineData("FR", "EUR", false, false)]
    [InlineData("GB", "GBP", false, false)]
    [InlineData("CM", "XAF", true, true)]
    [InlineData("SN", "XOF", true, true)]
    [InlineData("CI", "XOF", true, true)]
    [InlineData("NG", "NGN", true, false)]
    [InlineData("CM", "CAD", true, false)]
    [InlineData("CA", "XAF", false, false)]
    public void Methods_follow_parent_country(
        string country,
        string currency,
        bool paypal,
        bool momo)
    {
        Assert.True(ParentPaymentMethods.Allows(country, currency, PaymentMethodCodes.Card));
        Assert.Equal(paypal, ParentPaymentMethods.AllowsPayPal(country, currency));
        Assert.Equal(momo, ParentPaymentMethods.AllowsMobileMoney(country, currency));
        Assert.Equal(paypal, ParentPaymentMethods.Allows(country, currency, PaymentMethodCodes.PayPal));
        Assert.Equal(momo, ParentPaymentMethods.Allows(country, currency, PaymentMethodCodes.MtnMomo));
    }

    [Fact]
    public void Unknown_country_with_xaf_is_treated_as_africa()
    {
        Assert.True(ParentPaymentMethods.AllowsPayPal(null, "XAF"));
        Assert.True(ParentPaymentMethods.AllowsMobileMoney(null, "XAF"));
        Assert.False(ParentPaymentMethods.AllowsPayPal(null, "CAD"));
    }

    [Fact]
    public void Cameroon_offers_mtn_and_orange_collection()
    {
        var ops = ParentPaymentMethods.OperatorsFor("CM");
        Assert.Contains(ops, o => o.Code == "mtn" && o.Collectable);
        Assert.Contains(ops, o => o.Code == "orange" && o.Collectable);
        Assert.Equal("mtn", ParentPaymentMethods.DefaultCollectableOperator("CM"));
    }

    [Fact]
    public void Senegal_offers_orange_collection_and_wave_info()
    {
        var ops = ParentPaymentMethods.OperatorsFor("SN");
        Assert.Contains(ops, o => o.Code == "orange" && o.Collectable);
        Assert.Contains(ops, o => o.Code == "wave" && !o.Collectable);
        Assert.Equal("orange", ParentPaymentMethods.DefaultCollectableOperator("SN"));
    }

    [Fact]
    public void EnsureAllowed_rejects_paypal_outside_africa()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParentPaymentMethods.EnsureAllowed("CA", "CAD", PaymentMethodCodes.PayPal));
        Assert.Equal(ParentPaymentMethods.UnavailableInCountryMessage, ex.Message);
    }
}
