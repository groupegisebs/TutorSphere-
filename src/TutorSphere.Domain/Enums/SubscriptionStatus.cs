namespace TutorSphere.Domain.Enums;

public enum SubscriptionStatus
{
    /// <summary>Demande d'inscription en attente de validation par l'enseignant.</summary>
    Pending = 0,
    Active = 1,
    Paused = 2,
    Cancelled = 3,
    Expired = 4,
    /// <summary>Inscription refusée par l'enseignant.</summary>
    Rejected = 5,
    /// <summary>Acceptée par l'enseignant, en attente de paiement.</summary>
    AwaitingPayment = 6
}
