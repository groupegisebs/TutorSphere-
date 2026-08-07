namespace TutorSphere.Infrastructure.Email;

/// <summary>
/// Configuration client pour <b>Mail Sender</b> (projet GiseMailSender / SecureMailGateway).
/// Endpoint : POST {BaseUrl}/api/mail/send — Bearer ApiKey — ClientCode TUTORSPHERE.
/// </summary>
public class MailGatewaySettings
{
    public const string SectionName = "Email";

    /// <summary>URL publique Mail Sender (défaut production).</summary>
    public string BaseUrl { get; set; } = "https://gisemailsender.gisebs.com";

    /// <summary>Jeton API du client TUTORSPHERE (admin GiseMailSender).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string ClientCode { get; set; } = "TUTORSPHERE";
}
