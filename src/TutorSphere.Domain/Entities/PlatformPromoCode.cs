using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Code promo plateforme pour activer gratuitement la licence enseignant (usage unique).
/// </summary>
public class PlatformPromoCode : BaseEntity
{
    /// <summary>Code normalisé (majuscules, sans espaces).</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Durée de licence accordée en années (défaut 1).</summary>
    public int LicenseYears { get; set; } = 1;

    public DateTime? ExpiresAt { get; set; }

    public string? Notes { get; set; }

    /// <summary>Null tant que le code n'a pas été utilisé (usage unique).</summary>
    public DateTime? RedeemedAt { get; set; }

    public Guid? RedeemedByTenantId { get; set; }

    public string? RedeemedByUserId { get; set; }

    public bool IsAvailable(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return IsActive
               && RedeemedAt is null
               && (ExpiresAt is null || ExpiresAt > now);
    }
}
