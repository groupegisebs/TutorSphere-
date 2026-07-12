using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.TutorEarnings;
using TutorSphere.Application.DTOs.TutorPayouts;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Domain.Payouts;

namespace TutorSphere.Application.Services;

public interface ITutorEarningsService
{
    Task<TutorEarningsSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TutorPayoutDto>> ListPayoutsAsync(CancellationToken ct = default);
    Task<TutorPayoutDto> RequestPayoutAsync(RequestTutorPayoutRequest request, CancellationToken ct = default);
    Task<PayoutEligibilityDto> GetEligibilityAsync(CancellationToken ct = default);
}

/// <summary>
/// Gains tuteur : encaissable uniquement pour les cours déjà donnés et terminés.
/// Règles de retrait CAD :
/// ≥ 100 $ → immédiat ; &lt; 100 $ → délai 30 j ; &lt; 10 $ → aucun transfert.
/// </summary>
public class TutorEarningsService : ITutorEarningsService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITutorPayoutAccountService _payoutAccounts;

    public TutorEarningsService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        ITutorPayoutAccountService payoutAccounts)
    {
        _db = db;
        _tenantContext = tenantContext;
        _payoutAccounts = payoutAccounts;
    }

    public async Task<TutorEarningsSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var snapshot = ComputeSnapshot();
        await SyncHoldingClockAsync(tenantId, snapshot.Available, ct);

        var recent = _db.TutorPayouts
            .OrderByDescending(p => p.RequestedAt)
            .Take(20)
            .ToList()
            .Select(MapPayout)
            .ToList();

        var eligibility = await BuildEligibilityAsync(snapshot, ct);

        return new TutorEarningsSummaryDto(
            snapshot.Collected,
            snapshot.Held,
            snapshot.Released,
            snapshot.Withdrawn,
            snapshot.Available,
            snapshot.Currency,
            snapshot.SessionsHeld,
            recent,
            eligibility);
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

    public async Task<PayoutEligibilityDto> GetEligibilityAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var snapshot = ComputeSnapshot();
        await SyncHoldingClockAsync(tenantId, snapshot.Available, ct);
        return await BuildEligibilityAsync(snapshot, ct);
    }

    public async Task<TutorPayoutDto> RequestPayoutAsync(
        RequestTutorPayoutRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var snapshot = ComputeSnapshot();
        await SyncHoldingClockAsync(tenantId, snapshot.Available, ct);
        var eligibility = await BuildEligibilityAsync(snapshot, ct);

        if (!eligibility.CanWithdraw)
            throw new InvalidOperationException(
                eligibility.BlockReason ?? "Retrait non autorisé pour le moment.");

        var amount = request.Amount.HasValue && request.Amount.Value > 0
            ? decimal.Round(request.Amount.Value, 2, MidpointRounding.AwayFromZero)
            : eligibility.ClaimableNow;

        if (amount < TutorPayoutPolicy.MinimumTransferCad)
            throw new InvalidOperationException(
                $"Montant minimum de transfert : {TutorPayoutPolicy.MinimumTransferCad:N0} {TutorPayoutPolicy.PolicyCurrency}.");

        if (amount > eligibility.ClaimableNow)
            throw new InvalidOperationException(
                $"Montant trop élevé. Réclamable maintenant : {eligibility.ClaimableNow:N2} {eligibility.Currency}.");

        var primary = _db.TutorPayoutAccounts
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.IsPrimary)
            .FirstOrDefault();

        var payout = new TutorPayout
        {
            TenantId = tenantId,
            Amount = amount,
            Currency = TutorPayoutPolicy.PolicyCurrency,
            Status = TutorPayoutStatus.Pending,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            RequestedAt = DateTime.UtcNow,
            PayoutAccountId = primary?.Id,
            ProviderKind = primary?.ProviderKind
        };

        _db.Add(payout);
        await _db.SaveChangesAsync(ct);

        // Traitement immédiat côté ledger (le versement provider reste asynchrone / manuel).
        payout.Status = TutorPayoutStatus.Completed;
        payout.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Après un retrait, si le solde restant < 100 $, on repart le compteur 30 j.
        var after = ComputeSnapshot();
        await SyncHoldingClockAsync(tenantId, after.Available, ct, forceRestartIfBelowThreshold: true);

        return MapPayout(payout);
    }

    private async Task<PayoutEligibilityDto> BuildEligibilityAsync(EarningsSnapshot snapshot, CancellationToken ct)
    {
        var setup = await _payoutAccounts.GetSetupAsync(ct);
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == RequireTenantId());
        var holdingStarted = tenant?.PayoutHoldingStartedAt;

        var available = snapshot.Available;
        string? block = null;
        var can = true;
        int? elapsed = null;
        int? remaining = null;
        DateTime? eligibleAt = null;
        var claimable = 0m;

        if (!setup.SetupComplete)
        {
            can = false;
            block = setup.Region == nameof(PayoutRegionKind.Africa)
                ? "Configurez Wave et TapTap Send (détails complets) avant de demander un retrait."
                : "Configurez Stripe Connect et PayPal avant de demander un retrait.";
        }
        else if (available < TutorPayoutPolicy.MinimumTransferCad)
        {
            can = false;
            claimable = 0m;
            block =
                $"Aucun transfert sous {TutorPayoutPolicy.MinimumTransferCad:N0} {TutorPayoutPolicy.PolicyCurrency} " +
                "(y compris en fin de mois). Solde disponible : " +
                $"{available:N2} {TutorPayoutPolicy.PolicyCurrency}.";
        }
        else if (available >= TutorPayoutPolicy.InstantClaimThresholdCad)
        {
            claimable = available;
            can = true;
        }
        else
        {
            // < 100 $ : délai de 30 jours
            if (holdingStarted is null)
            {
                can = false;
                block = "Délai de détention en cours d'initialisation. Réessayez dans un instant.";
            }
            else
            {
                var since = holdingStarted.Value;
                elapsed = (int)Math.Floor((DateTime.UtcNow - since).TotalDays);
                remaining = Math.Max(0, TutorPayoutPolicy.HoldingDaysUnderThreshold - elapsed.Value);
                eligibleAt = since.AddDays(TutorPayoutPolicy.HoldingDaysUnderThreshold);

                if (elapsed < TutorPayoutPolicy.HoldingDaysUnderThreshold)
                {
                    can = false;
                    claimable = 0m;
                    block =
                        $"Solde inférieur à {TutorPayoutPolicy.InstantClaimThresholdCad:N0} {TutorPayoutPolicy.PolicyCurrency} : " +
                        $"délai de {TutorPayoutPolicy.HoldingDaysUnderThreshold} jours requis. " +
                        $"Encore {remaining} jour(s) (éligible le {eligibleAt:dd/MM/yyyy}).";
                }
                else
                {
                    claimable = available;
                    can = true;
                }
            }
        }

        if (can)
            claimable = available;

        return new PayoutEligibilityDto(
            can,
            available,
            can ? claimable : 0m,
            TutorPayoutPolicy.MinimumTransferCad,
            TutorPayoutPolicy.InstantClaimThresholdCad,
            TutorPayoutPolicy.HoldingDaysUnderThreshold,
            elapsed,
            remaining,
            holdingStarted,
            eligibleAt,
            setup.SetupComplete,
            block,
            TutorPayoutPolicy.PolicyCurrency);
    }

    private async Task SyncHoldingClockAsync(
        Guid tenantId,
        decimal available,
        CancellationToken ct,
        bool forceRestartIfBelowThreshold = false)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        if (tenant is null) return;

        if (available <= 0)
        {
            tenant.PayoutHoldingStartedAt = null;
        }
        else if (available >= TutorPayoutPolicy.InstantClaimThresholdCad)
        {
            // Au-dessus du seuil : pas de délai, on peut effacer le compteur.
            tenant.PayoutHoldingStartedAt = null;
        }
        else
        {
            // Sous 100 $ : démarrer / conserver le compteur 30 j.
            if (tenant.PayoutHoldingStartedAt is null || forceRestartIfBelowThreshold)
                tenant.PayoutHoldingStartedAt = DateTime.UtcNow;
        }

        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private EarningsSnapshot ComputeSnapshot()
    {
        var completedPayments = _db.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .ToList();

        var collected = completedPayments.Sum(p => p.TutorAmount);
        var currency = TutorPayoutPolicy.PolicyCurrency;

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
            var perSession = subPayments[0].TutorAmount / sessionCount;
            var holdSessions = Math.Min(sub.SessionsRemaining, sessionCount);
            held += decimal.Round(perSession * holdSessions, 2, MidpointRounding.AwayFromZero);
            sessionsHeld += holdSessions;
        }

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
        p.CompletedAt,
        p.ProviderKind?.ToString(),
        p.PayoutAccountId);

    private sealed record EarningsSnapshot(
        decimal Collected,
        decimal Held,
        decimal Released,
        decimal Withdrawn,
        decimal Available,
        string Currency,
        int SessionsHeld);
}
