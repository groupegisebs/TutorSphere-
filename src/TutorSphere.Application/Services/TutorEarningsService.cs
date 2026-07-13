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
    Task<(bool Success, string? Error)> CompleteFromGatewayAsync(string idempotencyKeyOrRef, string? providerPayoutId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> RejectFromGatewayAsync(string idempotencyKeyOrRef, string? reason, CancellationToken ct = default);
}

/// <summary>
/// Gains tuteur : encaissable uniquement pour les cours déjà donnés et terminés.
/// Règles CAD : ≥ 100 $ immédiat ; &lt; 100 $ délai 30 j ; &lt; 10 $ aucun transfert.
/// Paiement réel via file PayGateway (revue + rapprochement admin).
/// </summary>
public class TutorEarningsService : ITutorEarningsService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITutorPayoutAccountService _payoutAccounts;
    private readonly ITutorDisbursementGateway _disbursements;

    public TutorEarningsService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        ITutorPayoutAccountService payoutAccounts,
        ITutorDisbursementGateway disbursements)
    {
        _db = db;
        _tenantContext = tenantContext;
        _payoutAccounts = payoutAccounts;
        _disbursements = disbursements;
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
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Aucun moyen de versement configuré.");

        var destinationToken = ResolveDestinationToken(primary);
        if (string.IsNullOrWhiteSpace(destinationToken))
            throw new InvalidOperationException("Destination de versement incomplète.");

        var idempotencyKey = $"tutor-payout-{tenantId:N}-{Guid.NewGuid():N}"[..64];
        var payout = new TutorPayout
        {
            TenantId = tenantId,
            Amount = amount,
            Currency = TutorPayoutPolicy.PolicyCurrency,
            Status = TutorPayoutStatus.Pending,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            RequestedAt = DateTime.UtcNow,
            PayoutAccountId = primary.Id,
            ProviderKind = primary.ProviderKind,
            IdempotencyKey = idempotencyKey
        };

        _db.Add(payout);
        await _db.SaveChangesAsync(ct);

        if (_disbursements.IsConfigured)
        {
            var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
            var amountMinor = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
            var enqueued = await _disbursements.EnqueueAsync(new TutorDisbursementEnqueueRequest(
                ExternalReference: $"tutor-payout-{payout.Id:N}",
                IdempotencyKey: idempotencyKey,
                SellerExternalId: $"tutor-{tenantId:N}",
                SellerDisplayName: tenant?.Name,
                ProviderCode: PayoutProviderCodes.ToPayGatewayCode(primary.ProviderKind),
                DestinationMasked: MaskDestination(primary),
                DestinationToken: destinationToken,
                AmountMinor: amountMinor,
                Currency: TutorPayoutPolicy.PolicyCurrency,
                CountryCode: primary.CountryCode), ct);

            payout.ExternalDisbursementId = enqueued.Id.ToString();
            payout.Status = TutorPayoutStatus.Processing;
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Sans PayGateway : reste en attente manuelle (admin / ops).
            payout.Status = TutorPayoutStatus.Processing;
            payout.Note = (payout.Note ?? "") + " | PayGateway non configuré — traitement manuel.";
            await _db.SaveChangesAsync(ct);
        }

        var after = ComputeSnapshot();
        await SyncHoldingClockAsync(tenantId, after.Available, ct, forceRestartIfBelowThreshold: true);

        return MapPayout(payout);
    }

    public async Task<(bool Success, string? Error)> CompleteFromGatewayAsync(
        string idempotencyKeyOrRef, string? providerPayoutId, CancellationToken ct = default)
    {
        var payout = FindPayout(idempotencyKeyOrRef);
        if (payout is null) return (false, "not_found");
        if (payout.Status == TutorPayoutStatus.Completed) return (true, null);

        payout.Status = TutorPayoutStatus.Completed;
        payout.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(providerPayoutId))
            payout.ProviderPayoutId = providerPayoutId;
        payout.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var after = ComputeSnapshotForTenant(payout.TenantId);
        await SyncHoldingClockForTenantAsync(payout.TenantId, after.Available, ct, forceRestartIfBelowThreshold: true);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RejectFromGatewayAsync(
        string idempotencyKeyOrRef, string? reason, CancellationToken ct = default)
    {
        var payout = FindPayout(idempotencyKeyOrRef);
        if (payout is null) return (false, "not_found");
        if (payout.Status is TutorPayoutStatus.Completed or TutorPayoutStatus.Cancelled)
            return (true, null);

        payout.Status = TutorPayoutStatus.Failed;
        payout.FailureMessage = reason ?? "rejected_by_paygateway";
        payout.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var after = ComputeSnapshotForTenant(payout.TenantId);
        await SyncHoldingClockForTenantAsync(payout.TenantId, after.Available, ct);
        return (true, null);
    }

    private TutorPayout? FindPayout(string key)
    {
        var payout = _db.TutorPayoutsForAnyTenant.FirstOrDefault(p => p.IdempotencyKey == key);
        if (payout is not null) return payout;

        if (key.StartsWith("tutor-payout-", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(key["tutor-payout-".Length..], "N", out var id))
            return _db.TutorPayoutsForAnyTenant.FirstOrDefault(p => p.Id == id);

        return _db.TutorPayoutsForAnyTenant.FirstOrDefault(p =>
            p.ExternalDisbursementId == key || p.ProviderPayoutId == key);
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
                ? "Configurez un portefeuille Mobile Money (titulaire + numéro public) avant de demander un retrait."
                : "Configurez Stripe Connect (onboarding) et PayPal avant de demander un retrait.";
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

    private Task SyncHoldingClockAsync(
        Guid tenantId,
        decimal available,
        CancellationToken ct,
        bool forceRestartIfBelowThreshold = false)
        => SyncHoldingClockForTenantAsync(tenantId, available, ct, forceRestartIfBelowThreshold);

    private async Task SyncHoldingClockForTenantAsync(
        Guid tenantId,
        decimal available,
        CancellationToken ct,
        bool forceRestartIfBelowThreshold = false)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        if (tenant is null) return;

        if (available <= 0)
            tenant.PayoutHoldingStartedAt = null;
        else if (available >= TutorPayoutPolicy.InstantClaimThresholdCad)
            tenant.PayoutHoldingStartedAt = null;
        else if (tenant.PayoutHoldingStartedAt is null || forceRestartIfBelowThreshold)
            tenant.PayoutHoldingStartedAt = DateTime.UtcNow;

        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private EarningsSnapshot ComputeSnapshot() => ComputeSnapshotForTenant(RequireTenantId());

    private EarningsSnapshot ComputeSnapshotForTenant(Guid tenantId)
    {
        // Query filters already scope to current tenant when set; for webhook use ForAnyTenant + explicit filter.
        var completedPayments = (_tenantContext.HasTenant && _tenantContext.TenantId == tenantId
                ? _db.Payments
                : _db.PaymentsForAnyTenant.Where(p => p.TenantId == tenantId))
            .Where(p => p.Status == PaymentStatus.Completed)
            .ToList();

        var collected = completedPayments.Sum(p => p.TutorAmount);
        var currency = TutorPayoutPolicy.PolicyCurrency;

        var subscriptionIds = completedPayments
            .Where(p => p.SubscriptionId.HasValue)
            .Select(p => p.SubscriptionId!.Value)
            .Distinct()
            .ToList();

        var subscriptions = (_tenantContext.HasTenant && _tenantContext.TenantId == tenantId
                ? _db.StudentSubscriptions
                : _db.StudentSubscriptionsForAnyTenant.Where(s => s.TenantId == tenantId))
            .Where(s => subscriptionIds.Contains(s.Id))
            .ToList();

        var offeringIds = subscriptions.Select(s => s.OfferingId).Distinct().ToList();
        var offerings = (_tenantContext.HasTenant && _tenantContext.TenantId == tenantId
                ? _db.SubscriptionOfferings
                : _db.SubscriptionOfferingsForAnyTenant.Where(o => o.TenantId == tenantId))
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

        var withdrawn = (_tenantContext.HasTenant && _tenantContext.TenantId == tenantId
                ? _db.TutorPayouts
                : _db.TutorPayoutsForAnyTenant.Where(p => p.TenantId == tenantId))
            .Where(p => p.Status == TutorPayoutStatus.Pending
                        || p.Status == TutorPayoutStatus.Processing
                        || p.Status == TutorPayoutStatus.Completed)
            .Sum(p => (decimal?)p.Amount) ?? 0m;

        var available = decimal.Round(Math.Max(0m, released - withdrawn), 2, MidpointRounding.AwayFromZero);

        return new EarningsSnapshot(collected, held, released, withdrawn, available, currency, sessionsHeld);
    }

    private static string? ResolveDestinationToken(TutorPayoutAccount account) =>
        account.ProviderKind switch
        {
            PayoutProviderKind.StripeConnect => account.EmailOrAccountId,
            PayoutProviderKind.PayPal => account.EmailOrAccountId,
            _ when PayoutProviderCodes.IsMobileMoney(account.ProviderKind)
                => account.EmailOrAccountId ?? account.PhoneNumber,
            _ => account.EmailOrAccountId ?? account.PhoneNumber
        };

    private static string MaskDestination(TutorPayoutAccount account)
    {
        if (!string.IsNullOrWhiteSpace(account.PhoneNumber))
        {
            var digits = new string(account.PhoneNumber.Where(char.IsDigit).ToArray());
            if (digits.Length >= 4)
                return $"{account.ProviderKind} +{digits[..Math.Min(3, digits.Length)]} ••• {digits[^2..]}";
        }

        if (!string.IsNullOrWhiteSpace(account.EmailOrAccountId) && account.EmailOrAccountId.Contains('@'))
        {
            var parts = account.EmailOrAccountId.Split('@', 2);
            var local = parts[0];
            var masked = local.Length <= 2 ? "••" : $"{local[0]}••••{local[^1]}";
            return $"{masked}@{parts[1]}";
        }

        if (!string.IsNullOrWhiteSpace(account.EmailOrAccountId) && account.EmailOrAccountId.StartsWith("acct_"))
            return "Stripe ••••" + account.EmailOrAccountId[^4..];

        return account.Label;
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
