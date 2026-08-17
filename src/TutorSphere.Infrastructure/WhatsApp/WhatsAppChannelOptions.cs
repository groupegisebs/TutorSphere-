namespace TutorSphere.Infrastructure.WhatsApp;

/// <summary>Réglages du canal WhatsApp côté TutorSphere (section « WhatsApp » de la configuration).</summary>
public sealed class WhatsAppChannelOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>
    /// Indicatif ajouté aux numéros saisis sans indicatif. « 1 » couvre le Canada et les États-Unis,
    /// où se trouve l'essentiel des familles ; tout autre pays doit saisir le format international.
    /// </summary>
    public string DefaultCountryCode { get; set; } = "1";

    public int CodeLifetimeMinutes { get; set; } = 10;

    /// <summary>Saisies erronées tolérées avant d'exiger un nouveau code.</summary>
    public int MaxVerificationAttempts { get; set; } = 5;

    /// <summary>Délai imposé entre deux demandes de code, contre l'usage du canal comme robot d'appel.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;
}
