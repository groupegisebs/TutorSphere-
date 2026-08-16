using System.Text.Json;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.TutorEarnings;
using TutorSphere.Application.DTOs.TutorPayouts;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IGroupTeacherPayoutService
{
    Task<IReadOnlyList<GroupTeacherPayoutInvoiceDto>> ListForGroupAsync(
        Guid expertGroupId, string? tab, CancellationToken ct = default);
    Task<GroupTeacherPayoutInvoiceDto> MarkProcessingAsync(
        Guid expertGroupId, Guid payoutId, string actorUserId, CancellationToken ct = default);
    Task<GroupTeacherPayoutInvoiceDto> MarkPaidAsync(
        Guid expertGroupId, Guid payoutId, string actorUserId, CancellationToken ct = default);
    Task<(byte[] Content, string FileName)> BuildPdfAsync(
        Guid payoutId, Guid? expertGroupId, Guid? tenantId, CancellationToken ct = default);
}

public sealed class GroupTeacherPayoutService(IApplicationDbContext db) : IGroupTeacherPayoutService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<GroupTeacherPayoutInvoiceDto>> ListForGroupAsync(
        Guid expertGroupId, string? tab, CancellationToken ct = default)
    {
        var query = db.TutorPayoutsForAnyTenant.Where(p => p.ExpertGroupId == expertGroupId);
        var status = NormalizeTab(tab);
        if (status is { } s)
            query = query.Where(p => p.Status == s);

        var rows = query
            .OrderByDescending(p => p.RequestedAt)
            .ToList();

        var tenantIds = rows.Select(r => r.TenantId).Distinct().ToList();
        var names = db.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToList()
            .ToDictionary(t => t.Id, t => t.Name);

        IReadOnlyList<GroupTeacherPayoutInvoiceDto> result = rows
            .Select(p => Map(p, names.GetValueOrDefault(p.TenantId, "Enseignant")))
            .ToList();
        return Task.FromResult(result);
    }

    public async Task<GroupTeacherPayoutInvoiceDto> MarkProcessingAsync(
        Guid expertGroupId, Guid payoutId, string actorUserId, CancellationToken ct = default)
    {
        var payout = RequireGroupPayout(expertGroupId, payoutId);
        if (payout.Status == TutorPayoutStatus.Completed)
            throw new InvalidOperationException("Cette facture est déjà payée.");
        if (payout.Status == TutorPayoutStatus.Cancelled)
            throw new InvalidOperationException("Cette demande a été annulée.");

        payout.Status = TutorPayoutStatus.Processing;
        payout.ProcessingAt ??= DateTime.UtcNow;
        payout.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(payout, TeacherName(payout.TenantId));
    }

    public async Task<GroupTeacherPayoutInvoiceDto> MarkPaidAsync(
        Guid expertGroupId, Guid payoutId, string actorUserId, CancellationToken ct = default)
    {
        var payout = RequireGroupPayout(expertGroupId, payoutId);
        if (payout.Status == TutorPayoutStatus.Cancelled)
            throw new InvalidOperationException("Cette demande a été annulée.");

        payout.Status = TutorPayoutStatus.Completed;
        payout.CompletedAt = DateTime.UtcNow;
        payout.ProcessingAt ??= DateTime.UtcNow;
        payout.PaidByUserId = actorUserId;
        payout.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(payout, TeacherName(payout.TenantId));
    }

    public Task<(byte[] Content, string FileName)> BuildPdfAsync(
        Guid payoutId, Guid? expertGroupId, Guid? tenantId, CancellationToken ct = default)
    {
        var payout = db.TutorPayoutsForAnyTenant.FirstOrDefault(p => p.Id == payoutId)
            ?? throw new InvalidOperationException("Facture introuvable.");

        if (expertGroupId is Guid gid && payout.ExpertGroupId != gid)
            throw new InvalidOperationException("Facture hors de ce groupe.");
        if (tenantId is Guid tid && payout.TenantId != tid)
            throw new InvalidOperationException("Facture hors de ce compte.");

        var teacher = TeacherName(payout.TenantId);
        var method = ParseSnapshot(payout) ?? FromAccount(payout);
        var invoiceNo = string.IsNullOrWhiteSpace(payout.InvoiceNumber)
            ? payout.Id.ToString("N")[..8].ToUpperInvariant()
            : payout.InvoiceNumber;
        var status = payout.Status switch
        {
            TutorPayoutStatus.Pending => "Demande de paiement",
            TutorPayoutStatus.Processing => "Paiement en cours",
            TutorPayoutStatus.Completed => "Facture payee",
            TutorPayoutStatus.Failed => "Echec",
            TutorPayoutStatus.Cancelled => "Annulee",
            _ => payout.Status.ToString()
        };

        var lines = new List<string>
        {
            "TutorSphere — Facture de versement enseignant",
            "",
            $"N {invoiceNo}",
            $"Date de demande : {payout.RequestedAt:dd/MM/yyyy HH:mm} UTC",
            payout.ProcessingAt is null ? "" : $"Prise en charge : {payout.ProcessingAt:dd/MM/yyyy HH:mm} UTC",
            payout.CompletedAt is null ? "" : $"Payee le : {payout.CompletedAt:dd/MM/yyyy}",
            $"Statut : {status}",
            "",
            $"Enseignant : {teacher}",
            $"Montant : {payout.Amount:N2} {payout.Currency}",
            "",
            "Moyen de paiement de l'enseignant",
            "--------------------------------",
            $"Canal : {method.ProviderKind}",
            $"Libelle : {method.Label}",
            $"Titulaire : {method.AccountHolderName}",
            $"Pays : {method.CountryCode}",
            $"Devise du compte : {method.Currency}",
            string.IsNullOrWhiteSpace(method.PhoneNumber) ? "" : $"Telephone : {method.PhoneNumber}",
            string.IsNullOrWhiteSpace(method.EmailOrAccountId) ? "" : $"Email / identifiant : {method.EmailOrAccountId}",
            string.IsNullOrWhiteSpace(method.PaymentDetails) ? "" : $"Details : {CompactDetails(method.PaymentDetails)}",
            method.IsPrimary ? "Compte principal : oui" : "Compte principal : non",
            "",
            "Document generable a tout moment. Aucun PIN ni mot de passe n'apparait sur cette facture."
        };

        var bytes = InvoicePdfGenerator.FromTextLines(lines);
        return Task.FromResult((bytes, $"facture-{invoiceNo}.pdf"));
    }

    public static string SnapshotJson(TutorPayoutAccount account) =>
        JsonSerializer.Serialize(FromAccountEntity(account), JsonOpts);

    public static string NewInvoiceNumber() =>
        $"TSG-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    public static string Summarize(TutorPayoutMethodSnapshotDto method)
    {
        var dest = method.PhoneNumber
            ?? method.EmailOrAccountId
            ?? CompactDetails(method.PaymentDetails)
            ?? method.Label;
        return $"{method.ProviderKind} · {method.AccountHolderName} · {dest}";
    }

    private TutorPayout RequireGroupPayout(Guid expertGroupId, Guid payoutId)
    {
        var payout = db.TutorPayoutsForAnyTenant.FirstOrDefault(p => p.Id == payoutId)
            ?? throw new InvalidOperationException("Facture introuvable.");
        if (payout.ExpertGroupId != expertGroupId)
            throw new InvalidOperationException("Facture hors de ce groupe.");
        return payout;
    }

    private string TeacherName(Guid tenantId) =>
        db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).FirstOrDefault() ?? "Enseignant";

    private static TutorPayoutStatus? NormalizeTab(string? tab) => (tab ?? "").Trim().ToLowerInvariant() switch
    {
        "request" or "pending" or "demandes" => TutorPayoutStatus.Pending,
        "processing" or "encours" or "inprogress" => TutorPayoutStatus.Processing,
        "paid" or "completed" or "payee" => TutorPayoutStatus.Completed,
        _ => null
    };

    private static GroupTeacherPayoutInvoiceDto Map(TutorPayout p, string teacherName) =>
        new(
            p.Id,
            string.IsNullOrWhiteSpace(p.InvoiceNumber) ? p.Id.ToString("N")[..8].ToUpperInvariant() : p.InvoiceNumber!,
            p.TenantId,
            teacherName,
            p.Amount,
            p.Currency,
            TutorPayoutStatusNames.Of(p.Status),
            p.RequestedAt,
            p.ProcessingAt,
            p.CompletedAt,
            ParseSnapshot(p) ?? FromAccount(p),
            p.Note);

    private static TutorPayoutMethodSnapshotDto? ParseSnapshot(TutorPayout p)
    {
        if (string.IsNullOrWhiteSpace(p.PaymentMethodSnapshot))
            return null;
        try
        {
            return JsonSerializer.Deserialize<TutorPayoutMethodSnapshotDto>(p.PaymentMethodSnapshot, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TutorPayoutMethodSnapshotDto FromAccount(TutorPayout p) =>
        p.PayoutAccount is { } a
            ? FromAccountEntity(a)
            : new TutorPayoutMethodSnapshotDto(
                p.ProviderKind?.ToString() ?? "—",
                "Compte",
                "—",
                "",
                p.Currency,
                null, null, null, false);

    private static TutorPayoutMethodSnapshotDto FromAccountEntity(TutorPayoutAccount a) => new(
        a.ProviderKind.ToString(),
        a.Label,
        a.AccountHolderName,
        a.CountryCode,
        a.Currency,
        a.EmailOrAccountId,
        a.PhoneNumber,
        a.PaymentDetails,
        a.IsPrimary);

    private static string CompactDetails(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return "";
        var t = details.Trim();
        if (!t.StartsWith('{'))
            return t.Length > 180 ? t[..180] + "…" : t;
        try
        {
            using var doc = JsonDocument.Parse(t);
            var parts = new List<string>();
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind is JsonValueKind.Null or JsonValueKind.False)
                    continue;
                var v = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
                if (!string.IsNullOrWhiteSpace(v))
                    parts.Add($"{p.Name}: {v}");
            }
            var s = string.Join(" | ", parts);
            return s.Length > 220 ? s[..220] + "…" : s;
        }
        catch (JsonException)
        {
            return t.Length > 180 ? t[..180] + "…" : t;
        }
    }
}
