using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IPlatformPaymentSettingsService
{
    Task<PlatformPaymentSettingsDto> GetAsync(CancellationToken ct = default);
    Task<PlatformPaymentSettingsDto> UpdateAsync(UpdatePlatformPaymentSettingsRequest request, CancellationToken ct = default);
    Task<PlatformPaymentSettings> GetEntityAsync(CancellationToken ct = default);
}

public sealed class PlatformPaymentSettingsService(IApplicationDbContext db) : IPlatformPaymentSettingsService
{
    public async Task<PlatformPaymentSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var entity = await GetEntityAsync(ct);
        return Map(entity);
    }

    public async Task<PlatformPaymentSettingsDto> UpdateAsync(
        UpdatePlatformPaymentSettingsRequest request, CancellationToken ct = default)
    {
        var entity = await GetEntityAsync(ct);
        entity.DefaultCommissionPercent = ParentPaymentSplitCalculator.ClampCommission(request.DefaultCommissionPercent);
        entity.CardFeePercent = ParentPaymentSplitCalculator.ClampFeePercent(request.CardFeePercent);
        entity.CardFeeFixed = ParentPaymentSplitCalculator.ClampFeeFixed(request.CardFeeFixed);
        entity.PayPalFeePercent = ParentPaymentSplitCalculator.ClampFeePercent(request.PayPalFeePercent);
        entity.PayPalFeeFixed = ParentPaymentSplitCalculator.ClampFeeFixed(request.PayPalFeeFixed);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<PlatformPaymentSettings> GetEntityAsync(CancellationToken ct = default)
    {
        var existing = db.PlatformPaymentSettings.OrderBy(s => s.CreatedAt).FirstOrDefault();
        if (existing is not null)
            return existing;

        var created = new PlatformPaymentSettings();
        db.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    private static PlatformPaymentSettingsDto Map(PlatformPaymentSettings s) =>
        new(s.DefaultCommissionPercent, s.CardFeePercent, s.CardFeeFixed, s.PayPalFeePercent, s.PayPalFeeFixed);
}
