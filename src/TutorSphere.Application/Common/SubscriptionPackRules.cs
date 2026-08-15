namespace TutorSphere.Application.Common;

/// <summary>
/// Règles du forfait (durée + séances), pas d'un portefeuille.
/// </summary>
public static class SubscriptionPackRules
{
    /// <summary>
    /// Fenêtre de relance / renouvellement avant <c>EndDate</c> :
    /// un pack 7 j ne doit pas relancer 30 j à l'avance.
    /// </summary>
    public static int RenewalWindowDays(int durationDays)
    {
        var days = Math.Max(1, durationDays);
        return Math.Clamp(days / 3, 1, 30);
    }

    /// <summary>
    /// Mapping catalogue Pay Gateway / Stripe.
    /// Semaine et trimestre : paiement unique (évite de facturer un trimestre comme un Yearly).
    /// </summary>
    public static string ResolvePlanCode(int durationDays) => durationDays switch
    {
        <= 0 => "ONE-TIME",
        <= 31 => "MONTHLY",
        <= 180 => "ONE-TIME",
        _ => "YEARLY"
    };

    public static string ResolveBillingInterval(int durationDays) => durationDays switch
    {
        <= 0 => "OneTime",
        <= 31 => "Monthly",
        <= 180 => "OneTime",
        _ => "Yearly"
    };
}
