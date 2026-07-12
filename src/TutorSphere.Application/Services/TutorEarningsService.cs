using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.TutorEarnings;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ITutorEarningsService
{
    Task<TutorEarningsSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TutorPayoutDto>> ListPayoutsAsync(CancellationToken ct = default);
    Task<TutorPayoutDto> RequestPayoutAsync(RequestTutorPayoutRequest request, CancellationToken ct = default);
}

/// <summary>
/// Gains tuteur : encaissable uniquement pour les cours déjà donnés et terminés.
/// Les sommes liées aux séances restantes (forfait payé mais non dispensé) restent retenues.
/// </summary>
public class TutorEarningsService : ITutorEarningsService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TutorEarningsService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<TutorEarningsSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        RequireTenantId();
        var snapshot = ComputeSnapshot();
        var recent = _db.TutorPayouts
            .OrderByDescending(p => p.RequestedAt)
            .Take(20)
            .ToList()
            .Select(MapPayout)
            .ToList();

        return Task.FromResult(new TutorEarningsSummaryDto(
            snapshot.Collected,
            snapshot.Held,
            snapshot.Released,
            snapshot.Withdrawn,
            snapshot.Available,
            snapshot.Currency,
            snapshot.SessionsHeld,
            recent));
    }

    public Task<IReadOnlyList<TutorPayoutDto>> ListPayoutsAsync(CancellationToken ct = default)
    {
        RequireTenantId();
        var list = _db.TutorPayouts
            .OrderByDescending(p => p.RequestedAt)
            .ToList()
            .Select(MapPayout)
            .ToList();
        return Task.FromResult<IReadOnlyList<TutorPayoutDto>>(list);
    }

    public async Task<TutorPayoutDto> RequestPayoutAsync(
        RequestTutorPayoutRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var snapshot = ComputeSnapshot();

        if (snapshot.Available <= 0)
            throw new InvalidOperationException(
                "Aucun montant disponible à encaisser. Les sommes des cours non encore donnés restent retenues jusqu'à leur terminaison.");

        var amount = request.Amount.HasValue && request.Amount.Value > 0
            ? decimal.Round(request.Amount.Value, 2, MidpointRounding.AwayFromZero)
            : snapshot.Available;

        if (amount <= 0)
            throw new InvalidOperationException("Le montant à encaisser doit être supérieur à zéro.");

        if (amount > snapshot.Available)
            throw new InvalidOperationException(
                $"Montant trop élevé. Disponible à encaisser : {snapshot.Available:N2} {snapshot.Currency} " +
                $"(retenu pour cours non terminés : {snapshot.Held:N2} {snapshot.Currency}).");

        var payout = new TutorPayout
        {
            TenantId = tenantId,
            Amount = amount,
            Currency = snapshot.Currency,
            Status = TutorPayoutStatus.Completed,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            RequestedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _db.Add(payout);
        await _db.SaveChangesAsync(ct);
        return MapPayout(payout);
    }

    private EarningsSnapshot ComputeSnapshot()
    {
        var completedPayments = _db.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .ToList();

        var collected = completedPayments.Sum(p => p.TutorAmount);
        var currency = completedPayments
            .Select(p => p.Currency)
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))
            ?? "CAD";

        var subscriptionIds = completedPayments
            .Where(p => p.SubscriptionId.HasValue)
            .Select(p => p.SubscriptionId!.Value)
            .Distinct()
            .ToList();

        var subscriptions = _db.StudentSubscriptions
            .Where(s => subscriptionIds.Contains(s.Id))
            .ToList();

        var offeringIds = subscriptions.Select(s => s.OfferingId).Distinct().ToList();
        var offerings = _db.SubscriptionOfferings
            .Where(o => offeringIds.Contains(o.Id))
            .ToDictionary(o => o.Id);

        decimal held = 0m;
        var sessionsHeld = 0;

        foreach (var sub in subscriptions)
        {
            if (sub.SessionsRemaining <= 0)
                continue;

            var subPayments = completedPayments
                .Where(p => p.SubscriptionId == sub.Id)
                .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
                .ToList();

            if (subPayments.Count == 0)
                continue;

            offerings.TryGetValue(sub.OfferingId, out var offering);
            var sessionCount = Math.Max(1, offering?.SessionCount ?? 1);
            // Part tuteur d'une séance = dernier forfait payé / nombre de séances du forfait.
            var perSession = subPayments[0].TutorAmount / sessionCount;
            var holdSessions = Math.Min(sub.SessionsRemaining, sessionCount);
            held += decimal.Round(perSession * holdSessions, 2, MidpointRounding.AwayFromZero);
            sessionsHeld += holdSessions;
        }

        // Paiements sans abonnement : déjà « libérés » (pas de séances à retenir).
        held = Math.Min(held, collected);
        var released = decimal.Round(collected - held, 2, MidpointRounding.AwayFromZero);

        var withdrawn = _db.TutorPayouts
            .Where(p => p.Status == TutorPayoutStatus.Pending || p.Status == TutorPayoutStatus.Completed)
            .Sum(p => (decimal?)p.Amount) ?? 0m;

        var available = decimal.Round(Math.Max(0m, released - withdrawn), 2, MidpointRounding.AwayFromZero);

        return new EarningsSnapshot(collected, held, released, withdrawn, available, currency, sessionsHeld);
    }

    private Guid RequireTenantId() =>
        _tenantContext.TenantId
        ?? throw new InvalidOperationException("Tenant requis.");

    private static TutorPayoutDto MapPayout(TutorPayout p) => new(
        p.Id,
        p.Amount,
        p.Currency,
        TutorPayoutStatusNames.Of(p.Status),
        p.Note,
        p.RequestedAt,
        p.CompletedAt);

    private sealed record EarningsSnapshot(
        decimal Collected,
        decimal Held,
        decimal Released,
        decimal Withdrawn,
        decimal Available,
        string Currency,
        int SessionsHeld);
}
