namespace TutorSphere.Infrastructure.Email;

public sealed record SendMailRequest(
    string ClientCode,
    string TemplateCode,
    List<string> To,
    Dictionary<string, string>? BodyData = null,
    int Priority = 1);

public sealed record SendMailResponse(
    bool Success,
    string? MailCode,
    string? TrackingId,
    string? Status,
    string? Error);
