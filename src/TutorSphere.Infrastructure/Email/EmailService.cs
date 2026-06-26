using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutorSphere.Application.Common.Interfaces;

namespace TutorSphere.Infrastructure.Email;

internal static class EmailTemplates
{
    public const string Welcome = "WELCOME";
    public const string ConfirmEmail = "CONFIRM_EMAIL";
    public const string LessonReport = "LESSON_REPORT";
    public const string SchoolCreated = "SCHOOL_CREATED";
}

public class EmailService : IEmailService
{
    private readonly MailGatewayClient _client;
    private readonly MailGatewaySettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        MailGatewayClient client,
        IOptions<MailGatewaySettings> settings,
        ILogger<EmailService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendWelcomeAsync(string email, string firstName, CancellationToken ct = default)
    {
        if (!_client.IsConfigured)
        {
            _logger.LogWarning("MailGateway non configuré — e-mail de bienvenue non envoyé à {Email}.", email);
            return;
        }

        await TrySendAsync(new SendMailRequest(
            ClientCode: _settings.ClientCode,
            TemplateCode: EmailTemplates.Welcome,
            To: [email],
            BodyData: new Dictionary<string, string>
            {
                ["FirstName"] = firstName
            }
        ), ct);
    }

    public async Task SendEmailConfirmationAsync(
        string email,
        string firstName,
        string confirmationUrl,
        CancellationToken ct = default)
    {
        if (!_client.IsConfigured)
        {
            _logger.LogWarning("MailGateway non configuré — confirmation non envoyée à {Email}.", email);
            return;
        }

        await TrySendAsync(new SendMailRequest(
            ClientCode: _settings.ClientCode,
            TemplateCode: EmailTemplates.ConfirmEmail,
            To: [email],
            BodyData: new Dictionary<string, string>
            {
                ["FirstName"] = firstName,
                ["ConfirmationUrl"] = confirmationUrl
            }
        ), ct);
    }

    public async Task SendLessonReportToParentAsync(
        string parentEmail,
        string parentFirstName,
        string studentName,
        string tutorName,
        CancellationToken ct = default)
    {
        if (!_client.IsConfigured)
        {
            _logger.LogWarning("MailGateway non configuré — rapport non envoyé à {Email}.", parentEmail);
            return;
        }

        await TrySendAsync(new SendMailRequest(
            ClientCode: _settings.ClientCode,
            TemplateCode: EmailTemplates.LessonReport,
            To: [parentEmail],
            BodyData: new Dictionary<string, string>
            {
                ["ParentFirstName"] = parentFirstName,
                ["StudentName"] = studentName,
                ["TutorName"] = tutorName
            }
        ), ct);
    }

    public async Task SendSchoolCreatedAsync(
        string ownerEmail,
        string ownerFirstName,
        string schoolName,
        CancellationToken ct = default)
    {
        if (!_client.IsConfigured)
        {
            _logger.LogWarning("MailGateway non configuré — confirmation école non envoyée à {Email}.", ownerEmail);
            return;
        }

        await TrySendAsync(new SendMailRequest(
            ClientCode: _settings.ClientCode,
            TemplateCode: EmailTemplates.SchoolCreated,
            To: [ownerEmail],
            BodyData: new Dictionary<string, string>
            {
                ["OwnerFirstName"] = ownerFirstName,
                ["SchoolName"] = schoolName
            }
        ), ct);
    }

    private async Task TrySendAsync(SendMailRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _client.SendAsync(request, ct);
            if (result.Success)
                _logger.LogInformation("E-mail {Template} envoyé → {MailCode}", request.TemplateCode, result.MailCode);
            else
                _logger.LogWarning("E-mail {Template} refusé : {Error}", request.TemplateCode, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec d'envoi d'e-mail {Template}.", request.TemplateCode);
        }
    }
}
