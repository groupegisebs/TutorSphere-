namespace TutorSphere.Infrastructure.WhatsApp;

/// <summary>
/// Corps attendu par POST /api/whatsapp/send de Mail Sender. Même esprit que l'envoi de courriel :
/// un code fonctionnel et des variables nommées, la passerelle résout le modèle approuvé chez Meta.
/// </summary>
public sealed record SendWhatsAppRequest(
    string ClientCode,
    string To,
    string TemplateCode,
    Dictionary<string, string>? BodyData = null,
    string? Language = null,
    string Kind = "template");

public sealed record SendWhatsAppResponse(
    bool Success,
    string? MessageCode,
    string? TrackingId,
    string? Status,
    string? To,
    string? Error);
