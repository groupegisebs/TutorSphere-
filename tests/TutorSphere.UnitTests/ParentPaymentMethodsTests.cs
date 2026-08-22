using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Domain.Payments;

namespace TutorSphere.UnitTests;

public class ParentPaymentMethodsTests
{
    [Theory]
    [InlineData("CA", "CAD", true, false, false)]
    [InlineData("US", "USD", true, false, false)]
    [InlineData("FR", "EUR", true, false, false)]
    [InlineData("GB", "GBP", true, false, false)]
    [InlineData("JP", "JPY", true, false, false)]
    [InlineData("BR", "BRL", true, false, false)]
    [InlineData("CM", "XAF", false, true, true)]
    [InlineData("SN", "XOF", false, true, true)]
    [InlineData("CI", "XOF", true, true, true)]
    [InlineData("NG", "NGN", true, true, false)]
    [InlineData("GH", "GHS", true, true, false)]
    [InlineData("ZA", "ZAR", true, true, false)]
    [InlineData("KE", "KES", true, true, false)]
    [InlineData("CM", "CAD", false, true, false)]
    [InlineData("CA", "XAF", true, false, false)]
    public void Methods_follow_stripe_list_and_africa(
        string country,
        string currency,
        bool card,
        bool paypal,
        bool momo)
    {
        Assert.Equal(card, ParentPaymentMethods.AllowsCard(country, currency));
        Assert.Equal(paypal, ParentPaymentMethods.AllowsPayPal(country, currency));
        Assert.Equal(momo, ParentPaymentMethods.AllowsMobileMoney(country, currency));
        Assert.Equal(card, ParentPaymentMethods.Allows(country, currency, PaymentMethodCodes.Card));
        Assert.Equal(paypal, ParentPaymentMethods.Allows(country, currency, PaymentMethodCodes.PayPal));
        Assert.Equal(momo, ParentPaymentMethods.Allows(country, currency, PaymentMethodCodes.MtnMomo));
    }

    [Fact]
    public void Unknown_country_with_xaf_skips_stripe_uses_africa_methods()
    {
        Assert.False(ParentPaymentMethods.AllowsCard(null, "XAF"));
        Assert.True(ParentPaymentMethods.AllowsPayPal(null, "XAF"));
        Assert.True(ParentPaymentMethods.AllowsMobileMoney(null, "XAF"));
        Assert.Equal("paypal", ParentPaymentMethods.DefaultFamily(null, "XAF"));
    }

    [Fact]
    public void Stripe_official_list_covers_extended_africa_and_excludes_cameroon()
    {
        Assert.True(StripePaymentsCountries.Contains("CI"));
        Assert.True(StripePaymentsCountries.Contains("GH"));
        Assert.True(StripePaymentsCountries.Contains("NG"));
        Assert.True(StripePaymentsCountries.Contains("KE"));
        Assert.True(StripePaymentsCountries.Contains("ZA"));
        Assert.True(StripePaymentsCountries.Contains("UK"));
        Assert.False(StripePaymentsCountries.Contains("CM"));
        Assert.False(StripePaymentsCountries.Contains("SN"));
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
    public void EnsureAllowed_rejects_card_where_stripe_is_unavailable()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParentPaymentMethods.EnsureAllowed("CM", "XAF", PaymentMethodCodes.Card));
        Assert.Equal(ParentPaymentMethods.UnavailableInCountryMessage, ex.Message);
    }

    [Fact]
    public void EnsureAllowed_rejects_paypal_outside_africa()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParentPaymentMethods.EnsureAllowed("CA", "CAD", PaymentMethodCodes.PayPal));
        Assert.Equal(ParentPaymentMethods.UnavailableInCountryMessage, ex.Message);
    }
}
