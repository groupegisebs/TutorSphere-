namespace TutorSphere.Infrastructure.Identity;

/// <summary>Connexion refusée car l'adresse e-mail n'est pas encore confirmée.</summary>
public sealed class EmailNotConfirmedException : UnauthorizedAccessException
{
    public const string ErrorCode = "email_not_confirmed";

    public EmailNotConfirmedException()
        : base("Veuillez confirmer votre adresse e-mail avant de vous connecter. Consultez votre boîte de réception.")
    {
    }
}
