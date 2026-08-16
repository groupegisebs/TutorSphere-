using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ISubscriptionOfferingService
{
    Task<IReadOnlyList<SubscriptionOfferingDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionOfferingDto>> GetForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<SubscriptionOfferingDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionOfferingDto> CreateAsync(CreateSubscriptionOfferingRequest request, CancellationToken ct = default);
    /// <summary>Crée une offre pour une école (admin / expert, hors contexte tuteur).</summary>
    Task<SubscriptionOfferingDto> CreateForTenantAsync(
        Guid tenantId,
        CreateSubscriptionOfferingRequest request,
        CancellationToken ct = default);
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

        var owners = ResolveOwnerNames(offerings.Select(o => o.TenantId));
        var result = offerings
            .Select(o => MapToDto(o, counts.GetValueOrDefault(o.Id), owners.GetValueOrDefault(o.TenantId)))
            .ToList();
        return Task.FromResult<IReadOnlyList<SubscriptionOfferingDto>>(result);
    }

    public Task<IReadOnlyList<SubscriptionOfferingDto>> GetForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.Title)
            .ToList();

        var ids = offerings.Select(o => o.Id).ToList();
        var counts = _db.StudentSubscriptionsForAnyTenant
            .Where(s => ids.Contains(s.OfferingId)
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Paused))
            .GroupBy(s => s.OfferingId)
            .Select(g => new { OfferingId = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.OfferingId, x => x.Count);

        var owners = ResolveOwnerNames(offerings.Select(o => o.TenantId));
        IReadOnlyList<SubscriptionOfferingDto> result = offerings
            .Select(o => MapToDto(o, counts.GetValueOrDefault(o.Id), owners.GetValueOrDefault(o.TenantId)))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<SubscriptionOfferingDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id);
        if (offering is null)
            return Task.FromResult<SubscriptionOfferingDto?>(null);

        var subscribers = _db.StudentSubscriptions.Count(s =>
            s.OfferingId == id
            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Paused));
        var ownerName = ResolveOwnerNames([offering.TenantId]).GetValueOrDefault(offering.TenantId);
        return Task.FromResult<SubscriptionOfferingDto?>(MapToDto(offering, subscribers, ownerName));
    }

    public Task<SubscriptionOfferingDto> CreateAsync(CreateSubscriptionOfferingRequest request, CancellationToken ct = default)
        => CreateForTenantAsync(RequireTenantId(), request, ct);

    public async Task<SubscriptionOfferingDto> CreateForTenantAsync(
        Guid tenantId,
        CreateSubscriptionOfferingRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Profil introuvable.");
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Profil introuvable.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre de l'offre est obligatoire.");
        if (request.Price < 0)
            throw new InvalidOperationException("Le prix de l'offre ne peut pas être négatif.");
        if (request.IsInternational && string.IsNullOrWhiteSpace(request.MarketCountryCode))
            throw new InvalidOperationException("Sélectionnez un pays de marché pour une offre internationale.");

        var ownerName = tenant.Name.Trim();
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? GroupOfferCurrencyRules.ResolveOfferCurrency(
                request.IsInternational, request.MarketCountryCode, tenant.Country)
            : request.Currency.Trim();
        if (!string.IsNullOrWhiteSpace(request.Code) || request.IsInternational || !string.IsNullOrWhiteSpace(request.MarketCountryCode))
        {
            currency = GroupOfferCurrencyRules.ResolveOfferCurrency(
                request.IsInternational, request.MarketCountryCode, tenant.Country);
        }

        var durationDays = request.DurationDays > 0 ? request.DurationDays : 30;
        var (frequency, conditions, mode, sessionCount) = NormalizeSchedule(
            request with { Currency = currency, DurationDays = durationDays });

        conditions = MergePlanCatalog(conditions, request.Code, request.IsInternational, request.MarketCountryCode, ownerName);

        var offering = new SubscriptionOffering
        {
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Description = TeacherContactPrivacy.RedactFromPublicText(request.Description?.Trim()),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? request.Title.Trim() : request.Subject.Trim(),
            Price = request.Price,
            Currency = currency,
            DurationDays = durationDays,
            SessionCount = Math.Max(1, sessionCount),
            Frequency = frequency,
            Conditions = conditions,
            Mode = mode,
            MaxCapacity = Math.Clamp(request.MaxCapacity <= 0 ? 20 : request.MaxCapacity, 1, 500),
            IsActive = true
        };

        _db.Add(offering);
        PublishTenantProfile(tenantId);
        await _db.SaveChangesAsync(ct);
        try
        {
            await _payments.SyncOfferingCatalogAsync(offering.Id, ct);
        }
        catch
        {
            // L'offre reste créée même si la sync catalogue paiement échoue (gateway indisponible).
        }

        return MapToDto(offering, 0, ownerName);
    }

    public async Task<SubscriptionOfferingDto> UpdateAsync(Guid id, UpdateSubscriptionOfferingRequest request, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == id)
            ?? throw new InvalidOperationException("Offre introuvable.");

        var (frequency, conditions, mode, sessionCount) = NormalizeSchedule(request);

        offering.Title = request.Title.Trim();
        offering.Description = TeacherContactPrivacy.RedactFromPublicText(request.Description?.Trim());
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
        var ownerName = ResolveOwnerNames([offering.TenantId]).GetValueOrDefault(offering.TenantId);
        return MapToDto(offering, 0, ownerName);
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
        var ownerName = ResolveOwnerNames([offering.TenantId]).GetValueOrDefault(offering.TenantId);
        return MapToDto(offering, 0, ownerName);
    }

    public async Task<SubscriptionOfferingDto> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == id)
            ?? throw new InvalidOperationException("Offre introuvable.");

        offering.IsActive = false;
        offering.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var ownerName = ResolveOwnerNames([offering.TenantId]).GetValueOrDefault(offering.TenantId);
        return MapToDto(offering, 0, ownerName);
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");
        return _tenantContext.TenantId.Value;
    }

    /// <summary>
    /// Publishing an offer makes the school discoverable only when the annual platform license is valid.
    /// Does NOT activate the tenant (payment-gated).
    /// </summary>
    private void PublishTenantProfile(Guid tenantId)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        if (tenant is null)
            return;

        if (!tenant.HasValidLicense())
            return;

        if (!tenant.IsPublicProfile)
        {
            tenant.IsPublicProfile = true;
            tenant.UpdatedAt = DateTime.UtcNow;
        }
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
            .Select(s => new OfferingScheduleSlotDto(
                s.Day.Trim(),
                s.Time.Trim(),
                string.IsNullOrWhiteSpace(s.EndTime) ? null : s.EndTime.Trim()))
            .DistinctBy(s => $"{s.Day}|{s.Time}|{s.EndTime}")
            .ToList();

        var sessionMin = schedule.SessionDurationMin > 0 ? schedule.SessionDurationMin : 60;
        var windowsPerWeek = slots.Sum(s => CountAvailabilityWindows(s, sessionMin));

        var normalized = schedule with
        {
            BillingPeriod = string.IsNullOrWhiteSpace(schedule.BillingPeriod) ? "mois" : schedule.BillingPeriod.Trim().ToLowerInvariant(),
            Cadence = string.IsNullOrWhiteSpace(schedule.Cadence) ? "weekly" : schedule.Cadence.Trim().ToLowerInvariant(),
            SessionDurationMin = sessionMin,
            TimeZone = string.IsNullOrWhiteSpace(schedule.TimeZone) ? schedule.TimeZone : TimeZoneCatalog.Normalize(schedule.TimeZone),
            Slots = slots
        };

        var computedCount = sessionCount > 0
            ? sessionCount
            : windowsPerWeek == 0
                ? Math.Max(1, sessionCount)
                : EstimateSessionCount(normalized.BillingPeriod, normalized.Cadence, windowsPerWeek, durationDays);

        var summary = slots.Count == 0
            ? $"{normalized.BillingPeriod} · {normalized.BillingMode ?? "horaire"}"
            : BuildFrequencySummary(normalized);
        var json = JsonSerializer.Serialize(normalized, ScheduleJson);
        return (summary, json, mode, Math.Max(1, computedCount));
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

    private static int CountAvailabilityWindows(OfferingScheduleSlotDto slot, int sessionMin)
    {
        if (!AvailabilityWindows.TryParseTime(slot.Time, out var start))
            return 0;
        if (AvailabilityWindows.TryParseTime(slot.EndTime, out var end) && end > start)
            return AvailabilityWindows.CountBookingSlots(start, end, sessionMin);
        return 1;
    }

    private static string BuildFrequencySummary(OfferingScheduleDto schedule)
    {
        var mode = (schedule.BillingMode ?? "").Trim().ToLowerInvariant();
        var modeLabel = mode switch
        {
            "monthly" => "Taux mensuel",
            "hourly" => "Taux horaire",
            _ => schedule.BillingPeriod
        };

        if (mode == "monthly" && schedule.HoursPerWeek is > 0)
            modeLabel = $"{modeLabel} · {schedule.HoursPerWeek:0.##} h/semaine";

        var days = schedule.Slots.Count == 0
            ? ""
            : " · " + string.Join(", ", schedule.Slots.Select(s =>
            {
                var shortDay = s.Day.Length <= 3 ? s.Day : s.Day[..Math.Min(3, s.Day.Length)];
                return string.IsNullOrWhiteSpace(s.EndTime)
                    ? $"{shortDay} {s.Time}"
                    : $"{shortDay} {s.Time}–{s.EndTime}";
            }));

        return $"{modeLabel}{days}";
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

    private Dictionary<Guid, string> ResolveOwnerNames(IEnumerable<Guid> tenantIds)
    {
        var ids = tenantIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return _db.Tenants
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToList()
            .ToDictionary(t => t.Id, t => t.Name);
    }

    private static string? MergePlanCatalog(
        string? conditions,
        string? code,
        bool isInternational,
        string? marketCountryCode,
        string ownerTeacherName)
    {
        JsonObject node;
        try
        {
            node = string.IsNullOrWhiteSpace(conditions)
                ? new JsonObject()
                : JsonNode.Parse(conditions) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            node = new JsonObject();
        }

        node["ownerTeacherName"] = ownerTeacherName;
        if (!string.IsNullOrWhiteSpace(code))
            node["code"] = code.Trim();
        node["isInternational"] = isInternational;
        if (isInternational && !string.IsNullOrWhiteSpace(marketCountryCode))
            node["marketCountryCode"] = marketCountryCode.Trim().ToUpperInvariant();
        else
            node.Remove("marketCountryCode");

        return node.ToJsonString(ScheduleJson);
    }

    private static (string? Code, bool IsInternational, string? MarketCountryCode, string? OwnerTeacherName)
        ParsePlanCatalog(string? conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions))
            return (null, false, null, null);

        try
        {
            using var doc = JsonDocument.Parse(conditions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return (null, false, null, null);

            var root = doc.RootElement;
            string? code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
            var intl = root.TryGetProperty("isInternational", out var i) && i.ValueKind == JsonValueKind.True;
            string? market = root.TryGetProperty("marketCountryCode", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
            string? owner = root.TryGetProperty("ownerTeacherName", out var o) && o.ValueKind == JsonValueKind.String
                ? o.GetString()
                : null;
            return (code, intl, market, owner);
        }
        catch (JsonException)
        {
            return (null, false, null, null);
        }
    }

    private static SubscriptionOfferingDto MapToDto(
        SubscriptionOffering o,
        int activeSubscribers = 0,
        string? ownerTeacherName = null)
    {
        var monthlyUnit = ToMonthlyAmount(o.Price, o.DurationDays, o.Frequency, o.Conditions);
        var catalog = ParsePlanCatalog(o.Conditions);
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
            o.MaxCapacity,
            catalog.Code,
            catalog.IsInternational,
            catalog.MarketCountryCode,
            string.IsNullOrWhiteSpace(catalog.OwnerTeacherName) ? ownerTeacherName : catalog.OwnerTeacherName);
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
