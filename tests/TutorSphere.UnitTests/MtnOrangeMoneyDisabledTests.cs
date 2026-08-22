using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Domain.Enums;
using TutorSphere.Domain.Payouts;

namespace TutorSphere.UnitTests;

public class MtnOrangeMoneyDisabledTests
{
    [Theory]
    [InlineData("orange")]
    [InlineData("orange_money")]
    [InlineData("om")]
    [InlineData("mtn")]
    [InlineData("mtn_momo")]
    [InlineData("momo")]
    public void Collection_rejects_mtn_and_orange_money(string channel)
    {
        Assert.False(PaymentMethodCodes.MobileMoneyCollectionEnabled);
        Assert.True(PaymentMethodCodes.IsDisabledCollectionChannel(channel));
    }

    [Fact]
    public void Collection_keeps_card_and_paypal()
    {
        Assert.False(PaymentMethodCodes.IsDisabledCollectionChannel("card"));
        Assert.False(PaymentMethodCodes.IsDisabledCollectionChannel("paypal"));
    }

    [Fact]
    public void Payout_catalog_no_longer_offers_mtn_or_orange_money()
    {
        Assert.DoesNotContain(PayoutProviderKind.OrangeMoney, TutorPayoutPolicy.AfricaMobileMoneyProviders);
        Assert.DoesNotContain(PayoutProviderKind.MtnMomo, TutorPayoutPolicy.AfricaMobileMoneyProviders);
        Assert.Contains(PayoutProviderKind.Wave, TutorPayoutPolicy.AfricaMobileMoneyProviders);
    }

    [Theory]
    [InlineData(PayoutProviderKind.OrangeMoney)]
    [InlineData(PayoutProviderKind.MtnMomo)]
    public void New_payout_accounts_mark_mtn_and_orange_as_discontinued(PayoutProviderKind kind)
    {
        Assert.True(PayoutProviderCodes.IsDiscontinued(kind));
    }

    [Theory]
    [InlineData(PayoutProviderKind.Wave)]
    [InlineData(PayoutProviderKind.Airtel)]
    [InlineData(PayoutProviderKind.Mpesa)]
    [InlineData(PayoutProviderKind.PayPal)]
    public void Other_payout_providers_remain_available(PayoutProviderKind kind)
    {
        Assert.False(PayoutProviderCodes.IsDiscontinued(kind));
    }
}
