using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Domain.Enums;
using TutorSphere.Domain.Payouts;

namespace TutorSphere.UnitTests;

public class MtnOrangeMoneyEnabledTests
{
    [Theory]
    [InlineData("orange")]
    [InlineData("orange_money")]
    [InlineData("om")]
    [InlineData("mtn")]
    [InlineData("mtn_momo")]
    [InlineData("momo")]
    public void Collection_accepts_mtn_and_orange_money(string channel)
    {
        Assert.True(PaymentMethodCodes.MobileMoneyCollectionEnabled);
        Assert.True(PaymentMethodCodes.IsMobileMoney(channel));
        Assert.False(PaymentMethodCodes.IsDisabledCollectionChannel(channel));
    }

    [Fact]
    public void Collection_keeps_card_and_paypal()
    {
        Assert.False(PaymentMethodCodes.IsDisabledCollectionChannel("card"));
        Assert.False(PaymentMethodCodes.IsDisabledCollectionChannel("paypal"));
    }

    [Fact]
    public void Payout_catalog_offers_mtn_and_orange_money()
    {
        Assert.Contains(PayoutProviderKind.OrangeMoney, TutorPayoutPolicy.AfricaMobileMoneyProviders);
        Assert.Contains(PayoutProviderKind.MtnMomo, TutorPayoutPolicy.AfricaMobileMoneyProviders);
        Assert.Contains(PayoutProviderKind.Wave, TutorPayoutPolicy.AfricaMobileMoneyProviders);
    }

    [Theory]
    [InlineData(PayoutProviderKind.OrangeMoney)]
    [InlineData(PayoutProviderKind.MtnMomo)]
    [InlineData(PayoutProviderKind.Wave)]
    [InlineData(PayoutProviderKind.Airtel)]
    [InlineData(PayoutProviderKind.Mpesa)]
    [InlineData(PayoutProviderKind.PayPal)]
    public void Payout_providers_are_available(PayoutProviderKind kind)
    {
        Assert.False(PayoutProviderCodes.IsDiscontinued(kind));
    }
}
