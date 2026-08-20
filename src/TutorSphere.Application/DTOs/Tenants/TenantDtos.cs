namespace TutorSphere.Application.DTOs.Tenants;

public record CreateTenantRequest(
    string Name,
    string Slug,
    string OwnerEmail,
    string OwnerPassword,
    string OwnerFirstName,
    string OwnerLastName,
    string? City,
    string? Country,
    /// <summary>E-mail PayPal obligatoire pour CA/US/UK/EEA/CH.</summary>
    string? PayPalEmail = null,
    /// <summary>Identifiant Stripe Connect (acct_…) pour les zones concernées.</summary>
    string? StripeAccountId = null);

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string? Subdomain,
    string Status,
    string Plan,
    string Currency,
    string Language);

/// <param name="MonthlyRevenueTotals">
/// Recette du mois, un total par devise. Un enseignant peut vendre une offre locale en XAF et une
/// offre internationale en CAD : un montant scalaire unique mélangerait les deux.
/// </param>
public record TenantDashboardDto(
    int ActiveStudents,
    int ActiveSubscriptions,
    int UpcomingLessons,
    int PendingPayments,
    IReadOnlyList<Common.MoneyTotal> MonthlyRevenueTotals);

public record TutorProfileDto(
    Guid Id,
    string Name,
    string? Description,
    string? City,
    string? Country,
    string Language,
    string Currency,
    string Slug,
    IReadOnlyList<string> VisibleCountryCodes);

public record UpdateTutorProfileRequest(
    string? Name,
    string? Description,
    string? City,
    string? Country,
    string? Language,
    string? Currency,
    /// <summary>Pays où la fiche est visible (ISO). Null = ne pas modifier. Vide = reset au pays d'origine.</summary>
    IReadOnlyList<string>? VisibleCountryCodes = null);
