using System.Text.Json;
using System.Text.Json.Serialization;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

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
    private static readonly JsonSerializerOptions ScheduleJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPaymentGatewayService _payments;

    public SubscriptionOfferingService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        IPaymentGatewayService payments)
    {
        _db = db;
        _tenantContext = tenantContext;
        _payments = payments;
    }

    public Task<IReadOnlyList<SubscriptionOfferingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var offerings = _db.SubscriptionOfferings
            .OrderBy(o => o.Title)
            .ToList();

        var counts = _db.StudentSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Paused)
            .GroupBy(s => s.OfferingId)
            .Select(g => new { OfferingId = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.OfferingId, x => x.Count);

        var result = offerings
            .Select(o => MapToDto(o, counts.GetValueOrDefault(o.Id)))
            .ToList();
        return Task.FromResult<IReadOnlyList<SubscriptionOfferingDto>>(result);
    }

    public Task<SubscriptionOfferingDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id);
        if (offering is null)
            return Task.FromResult<SubscriptionOfferingDto?>(null);

        var subscribers = _db.StudentSubscriptions.Count(s =>
            s.OfferingId == id
            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Paused));
        return Task.FromResult<SubscriptionOfferingDto?>(MapToDto(offering, subscribers));
    }

    public async Task<SubscriptionOfferingDto> CreateAsync(CreateSubscriptionOfferingRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var (frequency, conditions, mode, sessionCount) = NormalizeSchedule(request);

        var offering = new SubscriptionOffering
        {
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Subject = request.Subject?.Trim(),
            Price = request.Price,
            Currency = request.Currency.Trim(),
            DurationDays = request.DurationDays,
            SessionCount = sessionCount,
            Frequency = frequency,
            Conditions = conditions,
            Mode = mode,
            MaxCapacity = Math.Clamp(request.MaxCapacity, 1, 500),
            IsActive = true
        };

        _db.Add(offering);
        PublishTenantProfile(tenantId);
        await _db.SaveChangesAsync(ct);
        await _payments.SyncOfferingCatalogAsync(offering.Id, ct);
        return MapToDto(offering);
    }

    public async Task<SubscriptionOfferingDto> UpdateAsync(Guid id, UpdateSubscriptionOfferingRequest request, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id)
            ?? throw new InvalidOperationException("Offre introuvable.");

        var (frequency, conditions, mode, sessionCount) = NormalizeSchedule(request);

        offering.Title = request.Title.Trim();
        offering.Description = request.Description?.Trim();
        offering.Subject = request.Subject?.Trim();
        offering.Price = request.Price;
        offering.Currency = request.Currency.Trim();
        offering.DurationDays = request.DurationDays;
        offering.SessionCount = sessionCount;
        offering.Frequency = frequency;
        offering.Conditions = conditions;
        offering.Mode = mode;
        offering.MaxCapacity = Math.Clamp(request.MaxCapacity, 1, 500);
        offering.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _payments.SyncOfferingCatalogAsync(offering.Id, ct);
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
        PublishTenantProfile(offering.TenantId);
        await _db.SaveChangesAsync(ct);
        await _payments.SyncOfferingCatalogAsync(offering.Id, ct);
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

    /// <summary>
    /// Publishing an offer makes the school discoverable in parent search
    /// (search requires Active + IsPublicProfile + at least one active offering).
    /// </summary>
    private void PublishTenantProfile(Guid tenantId)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        if (tenant is null)
            return;

        var changed = false;
        if (!tenant.IsPublicProfile)
        {
            tenant.IsPublicProfile = true;
            changed = true;
        }

        // Tutor already published an offer — make them searchable without waiting on admin.
        if (tenant.Status != TenantStatus.Active)
        {
            tenant.Status = TenantStatus.Active;
            changed = true;
        }

        if (changed)
            tenant.UpdatedAt = DateTime.UtcNow;
    }

    private static (string? Frequency, string? Conditions, LessonMode Mode, int SessionCount) NormalizeSchedule(
        CreateSubscriptionOfferingRequest request)
        => NormalizeScheduleCore(
            request.Frequency,
            request.Conditions,
            request.Mode,
            request.SessionCount,
            request.Schedule,
            request.DurationDays);

    private static (string? Frequency, string? Conditions, LessonMode Mode, int SessionCount) NormalizeSchedule(
        UpdateSubscriptionOfferingRequest request)
        => NormalizeScheduleCore(
            request.Frequency,
            request.Conditions,
            request.Mode,
            request.SessionCount,
            request.Schedule,
            request.DurationDays);

    private static (string? Frequency, string? Conditions, LessonMode Mode, int SessionCount) NormalizeScheduleCore(
        string? frequency,
        string? conditions,
        string? modeDisplay,
        int sessionCount,
        OfferingScheduleDto? schedule,
        int durationDays)
    {
        var mode = ParseMode(modeDisplay);
        if (schedule is null)
            return (frequency?.Trim(), conditions?.Trim(), mode, sessionCount);

        var slots = schedule.Slots
            .Where(s => !string.IsNullOrWhiteSpace(s.Day) && !string.IsNullOrWhiteSpace(s.Time))
            .Select(s => new OfferingScheduleSlotDto(s.Day.Trim(), s.Time.Trim()))
            .DistinctBy(s => $"{s.Day}|{s.Time}")
            .ToList();

        if (slots.Count == 0)
            throw new InvalidOperationException("Sélectionnez au moins un jour avec une heure de cours.");

        var normalized = schedule with
        {
            BillingPeriod = string.IsNullOrWhiteSpace(schedule.BillingPeriod) ? "mois" : schedule.BillingPeriod.Trim().ToLowerInvariant(),
            Cadence = string.IsNullOrWhiteSpace(schedule.Cadence) ? "weekly" : schedule.Cadence.Trim().ToLowerInvariant(),
            SessionDurationMin = schedule.SessionDurationMin > 0 ? schedule.SessionDurationMin : 60,
            Slots = slots
        };

        var computedCount = sessionCount > 0
            ? sessionCount
            : EstimateSessionCount(normalized.BillingPeriod, normalized.Cadence, slots.Count, durationDays);

        var summary = BuildFrequencySummary(normalized);
        var json = JsonSerializer.Serialize(normalized, ScheduleJson);
        return (summary, json, mode, computedCount);
    }

    private static int EstimateSessionCount(string billingPeriod, string cadence, int slotsPerWeek, int durationDays)
    {
        var weeks = billingPeriod switch
        {
            "semaine" => 1,
            "trimestre" => 12,
            "semestre" => 26,
            "an" => 52,
            _ => Math.Max(1, durationDays / 7)
        };

        if (cadence is "biweekly" or "fortnightly")
            weeks = Math.Max(1, weeks / 2);

        return Math.Max(1, weeks * Math.Max(1, slotsPerWeek));
    }

    private static string BuildFrequencySummary(OfferingScheduleDto schedule)
    {
        var cadenceLabel = schedule.Cadence switch
        {
            "biweekly" or "fortnightly" => "Toutes les 2 semaines",
            _ => "Chaque semaine"
        };

        var days = string.Join(", ", schedule.Slots.Select(s =>
        {
            var shortDay = s.Day.Length <= 3 ? s.Day : s.Day[..3];
            return $"{shortDay} {s.Time}";
        }));

        return $"{schedule.BillingPeriod} · {cadenceLabel} · {days}";
    }

    private static LessonMode ParseMode(string? mode) => mode?.Trim() switch
    {
        "Présentiel" or "InPerson" => LessonMode.InPerson,
        "Hybride" or "Hybrid" => LessonMode.Hybrid,
        _ => LessonMode.Online
    };

    private static string FormatMode(LessonMode mode) => mode switch
    {
        LessonMode.InPerson => "Présentiel",
        LessonMode.Hybrid => "Hybride",
        _ => "En ligne"
    };

    private static OfferingScheduleDto? TryParseSchedule(string? conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(conditions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (!doc.RootElement.TryGetProperty("slots", out _))
                return null;

            return JsonSerializer.Deserialize<OfferingScheduleDto>(conditions, ScheduleJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SubscriptionOfferingDto MapToDto(SubscriptionOffering o, int activeSubscribers = 0)
    {
        var monthlyUnit = ToMonthlyAmount(o.Price, o.DurationDays, o.Frequency, o.Conditions);
        return new(
            o.Id,
            o.Title,
            o.Description,
            o.Subject,
            o.Price,
            o.Currency,
            o.DurationDays,
            o.SessionCount,
            o.Frequency,
            o.IsActive,
            FormatMode(o.Mode),
            o.Conditions,
            TryParseSchedule(o.Conditions),
            activeSubscribers,
            Math.Round(monthlyUnit * activeSubscribers, 2),
            o.MaxCapacity);
    }

    /// <summary>Normalise le prix de l'offre en revenu mensuel récurrent (MRR unitaire).</summary>
    private static decimal ToMonthlyAmount(
        decimal price,
        int durationDays,
        string? frequency,
        string? conditions)
    {
        var period = TryParseSchedule(conditions)?.BillingPeriod
            ?? frequency
            ?? "";
        period = period.Trim().ToLowerInvariant();

        if (period.Contains("semaine") || period is "week" or "weekly" || durationDays is > 0 and <= 8)
            return price * 52m / 12m;
        if (period.Contains("trimestre") || period is "quarter" || durationDays is > 60 and <= 100)
            return price / 3m;
        if (period.Contains("semestre") || period is "semester" || durationDays is > 100 and <= 200)
            return price / 6m;
        if (period.Contains("an") || period is "year" or "yearly" or "annual" || durationDays > 200)
            return price / 12m;

        return price;
    }
}
