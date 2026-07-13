namespace TutorSphere.Infrastructure.PayGateway;

public class PayGatewaySettings
{
    public const string SectionName = "PayGateway";

    public string BaseUrl { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Secret HMAC pour les callbacks décaissement (header X-PayGateway-Signature).</summary>
    public string? PayoutWebhookSecret { get; set; }

    /// <summary>
    /// Contrôle le header <c>X-Stripe-Env: DEV</c> (bac à sable Stripe côté Pay Gateway).
    /// <list type="bullet">
    /// <item><c>null</c> (défaut) — auto : Development / Staging → sandbox ; sinon Live.</item>
    /// <item><c>true</c> — force le bac à sable (QA uniquement).</item>
    /// <item><c>false</c> — force Stripe Live (ne jamais forcer <c>true</c> en production utilisateurs).</item>
    /// </list>
    /// </summary>
    public bool? UseSandbox { get; set; }
}
