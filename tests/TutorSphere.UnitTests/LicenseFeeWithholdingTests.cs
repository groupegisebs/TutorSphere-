using TutorSphere.Application.Common;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.UnitTests;

public class LicenseFeeWithholdingTests
{
    [Theory]
    [InlineData(10, "USD", 10)]
    [InlineData(6000, "XAF", 10)]
    [InlineData(13.89, "CAD", 10)]
    public void ToUsd_converts_annual_fee_equivalent(decimal local, string currency, decimal expectedUsd)
    {
        var usd = LicenseFeeWithholding.ToUsd(local, currency);
        Assert.InRange(usd, expectedUsd - 0.2m, expectedUsd + 0.2m);
    }

    [Fact]
    public void TakeFromTutorShare_withholds_until_ten_usd_is_collected()
    {
        var tenant = new Tenant
        {
            LicenseFeeWithholdingRemainingUsd = 10m,
            LicenseSettlementKind = LicenseFeeWithholding.SettlementWithhold
        };

        var first = new Payment { TutorAmount = 8m, PlatformFee = 1m, Amount = 9m, Currency = "USD" };
        var taken1 = LicenseFeeWithholding.TakeFromTutorShare(tenant, first);

        Assert.Equal(8m, taken1);
        Assert.Equal(0m, first.TutorAmount);
        Assert.Equal(9m, first.PlatformFee);
        Assert.Equal(2m, tenant.LicenseFeeWithholdingRemainingUsd);

        var second = new Payment { TutorAmount = 5m, PlatformFee = 0.5m, Amount = 5.5m, Currency = "USD" };
        var taken2 = LicenseFeeWithholding.TakeFromTutorShare(tenant, second);

        Assert.Equal(2m, taken2);
        Assert.Equal(3m, second.TutorAmount);
        Assert.Equal(0m, tenant.LicenseFeeWithholdingRemainingUsd);

        var third = new Payment { TutorAmount = 4m, PlatformFee = 0.4m, Amount = 4.4m, Currency = "USD" };
        Assert.Equal(0m, LicenseFeeWithholding.TakeFromTutorShare(tenant, third));
        Assert.Equal(4m, third.TutorAmount);
    }

    [Fact]
    public void GrantLicenseYears_extends_from_current_expiry_and_requires_onboarding()
    {
        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            Status = TenantStatus.PendingValidation,
            LicenseExpiresAt = now.AddMonths(2)
        };

        LicenseFeeWithholding.GrantLicenseYears(tenant, 1, now);

        Assert.True(tenant.LicenseExpiresAt > now.AddMonths(13));
        Assert.Equal(TenantStatus.AwaitingOnboarding, tenant.Status);
        Assert.False(tenant.IsPublicProfile);
    }
}
