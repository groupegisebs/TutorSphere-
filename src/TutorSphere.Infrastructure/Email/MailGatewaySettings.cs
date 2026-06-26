namespace TutorSphere.Infrastructure.Email;

public class MailGatewaySettings
{
    public const string SectionName = "Email";

    public string BaseUrl { get; set; } = "https://gisemailsender.gisebs.com";
    public string ApiKey { get; set; } = string.Empty;
    public string ClientCode { get; set; } = "TUTORSPHERE";
}
