using TutorSphere.Application.Common;
using TutorSphere.Domain.Entities;

namespace TutorSphere.UnitTests;

public class ParentPaymentSplitCalculatorTests
{
    [Fact]
    public void Card_ten_dollars_deducts_processor_then_thirty_percent_to_group()
    {
        var groupId = Guid.NewGuid();
        var split = ParentPaymentSplitCalculator.Compute(
            10m,
            ParentPaymentSplitCalculator.DefaultCardFeePercent,
            ParentPaymentSplitCalculator.DefaultCardFeeFixed,
            30m,
            groupId);

        Assert.Equal(10m, split.Gross);
        Assert.Equal(0.59m, split.ProcessorFee);
        Assert.Equal(9.41m, split.Net);
        Assert.Equal(2.82m, split.PlatformFee);
        Assert.Equal(6.59m, split.Remainder);
        Assert.Equal(0m, split.TutorAmount);
        Assert.Equal(6.59m, split.GroupAmount);
        Assert.Equal(groupId, split.ExpertGroupId);
        Assert.Equal(30m, split.CommissionPercent);
    }

    [Fact]
    public void Independent_teacher_receives_remainder()
    {
        var split = ParentPaymentSplitCalculator.Compute(10m, 2.9m, 0.30m, 30m, null);

        Assert.Equal(6.59m, split.TutorAmount);
        Assert.Equal(0m, split.GroupAmount);
        Assert.Null(split.ExpertGroupId);
    }

    [Fact]
    public void TakeFromTutorShare_does_not_touch_group_amount()
    {
        var tenant = new Tenant
        {
            LicenseFeeWithholdingRemainingUsd = 10m,
            LicenseSettlementKind = LicenseFeeWithholding.SettlementWithhold
        };
        var payment = new Payment
        {
            TutorAmount = 0m,
            GroupAmount = 6.59m,
            PlatformFee = 2.82m,
            Amount = 10m,
            Currency = "USD"
        };

        Assert.Equal(0m, LicenseFeeWithholding.TakeFromTutorShare(tenant, payment));
        Assert.Equal(0m, payment.TutorAmount);
        Assert.Equal(6.59m, payment.GroupAmount);
        Assert.Equal(2.82m, payment.PlatformFee);
        Assert.Equal(10m, tenant.LicenseFeeWithholdingRemainingUsd);
    }

    [Fact]
    public void Mtn_six_thousand_xaf_deducts_two_percent_then_thirty_to_group()
    {
        var groupId = Guid.NewGuid();
        var split = ParentPaymentSplitCalculator.Compute(
            6000m,
            ParentPaymentSplitCalculator.DefaultMobileMoneyFeePercent,
            ParentPaymentSplitCalculator.DefaultMobileMoneyFeeFixed,
            30m,
            groupId);

        Assert.Equal(120m, split.ProcessorFee);
        Assert.Equal(5880m, split.Net);
        Assert.Equal(1764m, split.PlatformFee);
        Assert.Equal(4116m, split.Remainder);
        Assert.Equal(0m, split.TutorAmount);
        Assert.Equal(4116m, split.GroupAmount);
    }
}
