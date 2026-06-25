namespace TutorSphere.Infrastructure.PayGateway;

public class PayGatewaySettings
{
    public const string SectionName = "PayGateway";

    public string BaseUrl { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
