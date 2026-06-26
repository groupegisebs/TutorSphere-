using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface ISubscriptionOfferingService
{
    Task<IReadOnlyList<SubscriptionOfferingDto>> GetAllAsync(CancellationToken ct = default);
    Task<SubscriptionOfferingDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionOfferingDto> CreateAsync(CreateSubscriptionOfferingRequest request, CancellationToken ct = default);
    Task<SubscriptionOfferingDto> UpdateAsync(Guid id, UpdateSubscriptionOfferingRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionOfferingDto> ActivateAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionOfferingDto> DeactivateAsync(Guid id, CancellationToken ct = default);
}

public class SubscriptionOfferingService : ISubscriptionOfferingService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SubscriptionOfferingService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<IReadOnlyList<SubscriptionOfferingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var offerings = _db.SubscriptionOfferings
            .OrderBy(o => o.Title)
            .ToList()
            .Select(MapToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<SubscriptionOfferingDto>>(offerings);
    }

    public Task<SubscriptionOfferingDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id);
        return Task.FromResult(offering is null ? null : MapToDto(offering));
    }

    public async Task<SubscriptionOfferingDto> CreateAsync(CreateSubscriptionOfferingRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var offering = new SubscriptionOffering
        {
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Subject = request.Subject?.Trim(),
            Price = request.Price,
            Currency = request.Currency.Trim(),
            DurationDays = request.DurationDays,
            SessionCount = request.SessionCount,
            Frequency = request.Frequency?.Trim(),
            IsActive = true
        };

        _db.Add(offering);
        await _db.SaveChangesAsync(ct);
        return MapToDto(offering);
    }

    public async Task<SubscriptionOfferingDto> UpdateAsync(Guid id, UpdateSubscriptionOfferingRequest request, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id)
            ?? throw new InvalidOperationException("Offre introuvable.");

        offering.Title = request.Title.Trim();
        offering.Description = request.Description?.Trim();
        offering.Subject = request.Subject?.Trim();
        offering.Price = request.Price;
        offering.Currency = request.Currency.Trim();
        offering.DurationDays = request.DurationDays;
        offering.SessionCount = request.SessionCount;
        offering.Frequency = request.Frequency?.Trim();
        offering.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(offering);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id)
            ?? throw new InvalidOperationException("Offre introuvable.");

        _db.Remove(offering);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SubscriptionOfferingDto> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id)
            ?? throw new InvalidOperationException("Offre introuvable.");

        offering.IsActive = true;
        offering.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapToDto(offering);
    }

    public async Task<SubscriptionOfferingDto> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id)
            ?? throw new InvalidOperationException("Offre introuvable.");

        offering.IsActive = false;
        offering.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapToDto(offering);
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");
        return _tenantContext.TenantId.Value;
    }

    private static SubscriptionOfferingDto MapToDto(SubscriptionOffering o) => new(
        o.Id,
        o.Title,
        o.Description,
        o.Subject,
        o.Price,
        o.Currency,
        o.DurationDays,
        o.SessionCount,
        o.Frequency,
        o.IsActive);
}
