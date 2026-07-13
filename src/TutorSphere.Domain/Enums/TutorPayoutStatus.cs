namespace TutorSphere.Domain.Enums;

public enum TutorPayoutStatus
{
    /// <summary>Créée localement, en attente d'envoi PayGateway.</summary>
    Pending = 0,
    /// <summary>Versée / confirmée après rapprochement PayGateway.</summary>
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
    /// <summary>En file PayGateway (revue + rapprochement admin avant paiement).</summary>
    Processing = 4
}
