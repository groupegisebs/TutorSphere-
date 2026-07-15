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

    // Auth
    public const string ConfirmEmailSimple = "CONFIRM_EMAIL_SIMPLE";
    public const string ResetPassword = "RESET_PASSWORD";
    public const string PasswordChanged = "PASSWORD_CHANGED";

    // Tutor billing
    public const string TutorTrialStarted = "TUTOR_TRIAL_STARTED";
    public const string TutorPaymentReceipt = "TUTOR_PAYMENT_RECEIPT";
    public const string TutorRenewalReminder = "TUTOR_RENEWAL_REMINDER";
    public const string TutorPaymentFailed = "TUTOR_PAYMENT_FAILED";
    public const string TutorSubCancelled = "TUTOR_SUB_CANCELLED";

    // Account lifecycle
    public const string AccountActivated = "ACCOUNT_ACTIVATED";
    public const string AccountDeactivated = "ACCOUNT_DEACTIVATED";
    public const string SchoolApproved = "SCHOOL_APPROVED";

    // Lessons
    public const string LessonScheduled = "LESSON_SCHEDULED";
    public const string LessonReminder = "LESSON_REMINDER";
    public const string LessonCancelled = "LESSON_CANCELLED";

    // Parent billing
    public const string ParentPaymentReceipt = "PARENT_PAYMENT_RECEIPT";
    public const string ParentPaymentFailed = "PARENT_PAYMENT_FAILED";
    public const string InvoiceReady = "INVOICE_READY";
    public const string ParentPaymentOverdue = "PARENT_PAYMENT_OVERDUE";

    // Course enrollment / tutor receipt
    public const string CourseEnrollmentRequest = "COURSE_ENROLLMENT_REQUEST";
    public const string CourseEnrollmentAccepted = "COURSE_ENROLLMENT_ACCEPTED";
    public const string TutorStudentPaymentReceived = "TUTOR_STUDENT_PAYMENT_RECEIVED";
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

    public async Task SendEmailConfirmationSimpleAsync(string to, string firstName, string confirmUrl, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — CONFIRM_EMAIL_SIMPLE non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.ConfirmEmailSimple, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName, ["ConfirmationUrl"] = confirmUrl }), ct);
    }

    public async Task SendResetPasswordAsync(string to, string firstName, string resetUrl, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — RESET_PASSWORD non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.ResetPassword, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName, ["ResetUrl"] = resetUrl }), ct);
    }

    public async Task SendPasswordChangedAsync(string to, string firstName, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — PASSWORD_CHANGED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.PasswordChanged, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName }), ct);
    }

    public async Task SendTutorTrialStartedAsync(string to, string firstName, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — TUTOR_TRIAL_STARTED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.TutorTrialStarted, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName }), ct);
    }

    public async Task SendTutorPaymentReceiptAsync(string to, string firstName, decimal amount, string invoiceUrl, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — TUTOR_PAYMENT_RECEIPT non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.TutorPaymentReceipt, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName, ["Amount"] = amount.ToString("C"), ["InvoiceUrl"] = invoiceUrl }), ct);
    }

    public async Task SendTutorRenewalReminderAsync(string to, string firstName, DateTime renewalDate, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — TUTOR_RENEWAL_REMINDER non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.TutorRenewalReminder, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName, ["RenewalDate"] = renewalDate.ToString("d MMMM yyyy") }), ct);
    }

    public async Task SendTutorPaymentFailedAsync(string to, string firstName, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — TUTOR_PAYMENT_FAILED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.TutorPaymentFailed, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName }), ct);
    }

    public async Task SendTutorSubscriptionCancelledAsync(string to, string firstName, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — TUTOR_SUB_CANCELLED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.TutorSubCancelled, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName }), ct);
    }

    public async Task SendAccountActivatedAsync(string to, string firstName, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — ACCOUNT_ACTIVATED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.AccountActivated, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName }), ct);
    }

    public async Task SendAccountDeactivatedAsync(string to, string firstName, string reason, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — ACCOUNT_DEACTIVATED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.AccountDeactivated, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName, ["Reason"] = reason }), ct);
    }

    public async Task SendSchoolApprovedAsync(string to, string firstName, string schoolName, string loginUrl, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — SCHOOL_APPROVED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.SchoolApproved, [to],
            new Dictionary<string, string> { ["FirstName"] = firstName, ["SchoolName"] = schoolName, ["LoginUrl"] = loginUrl }), ct);
    }

    public async Task SendLessonScheduledAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — LESSON_SCHEDULED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.LessonScheduled, [to],
            new Dictionary<string, string> { ["RecipientName"] = recipientName, ["TutorName"] = tutorName, ["Subject"] = subject, ["LessonDate"] = lessonDate.ToString("dddd d MMMM yyyy à HH:mm") }), ct);
    }

    public async Task SendLessonReminderAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — LESSON_REMINDER non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.LessonReminder, [to],
            new Dictionary<string, string> { ["RecipientName"] = recipientName, ["TutorName"] = tutorName, ["Subject"] = subject, ["LessonDate"] = lessonDate.ToString("dddd d MMMM yyyy à HH:mm") }), ct);
    }

    public async Task SendLessonCancelledAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — LESSON_CANCELLED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.LessonCancelled, [to],
            new Dictionary<string, string> { ["RecipientName"] = recipientName, ["TutorName"] = tutorName, ["Subject"] = subject, ["LessonDate"] = lessonDate.ToString("dddd d MMMM yyyy à HH:mm") }), ct);
    }

    public async Task SendParentPaymentReceiptAsync(string to, string parentName, string studentName, decimal amount, string invoiceUrl, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — PARENT_PAYMENT_RECEIPT non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.ParentPaymentReceipt, [to],
            new Dictionary<string, string> { ["ParentName"] = parentName, ["StudentName"] = studentName, ["Amount"] = amount.ToString("C"), ["InvoiceUrl"] = invoiceUrl }), ct);
    }

    public async Task SendParentPaymentFailedAsync(string to, string parentName, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — PARENT_PAYMENT_FAILED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.ParentPaymentFailed, [to],
            new Dictionary<string, string> { ["ParentName"] = parentName }), ct);
    }

    public async Task SendInvoiceReadyAsync(string to, string parentName, string invoiceUrl, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — INVOICE_READY non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.InvoiceReady, [to],
            new Dictionary<string, string> { ["ParentName"] = parentName, ["InvoiceUrl"] = invoiceUrl }), ct);
    }

    public async Task SendParentPaymentOverdueAsync(string to, string parentName, string studentName, string courseTitle, string payUrl, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — PARENT_PAYMENT_OVERDUE non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.ParentPaymentOverdue, [to],
            new Dictionary<string, string>
            {
                ["ParentName"] = parentName,
                ["StudentName"] = studentName,
                ["CourseTitle"] = courseTitle,
                ["PayUrl"] = payUrl
            }), ct);
    }

    public async Task SendCourseEnrollmentRequestAsync(string to, string tutorName, string studentName, string courseTitle, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — COURSE_ENROLLMENT_REQUEST non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.CourseEnrollmentRequest, [to],
            new Dictionary<string, string>
            {
                ["TutorName"] = tutorName,
                ["StudentName"] = studentName,
                ["CourseTitle"] = courseTitle
            }), ct);
    }

    public async Task SendCourseEnrollmentAcceptedAsync(string to, string parentName, string studentName, string courseTitle, string statusNote, string actionUrl, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — COURSE_ENROLLMENT_ACCEPTED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.CourseEnrollmentAccepted, [to],
            new Dictionary<string, string>
            {
                ["ParentName"] = parentName,
                ["StudentName"] = studentName,
                ["CourseTitle"] = courseTitle,
                ["StatusNote"] = statusNote,
                ["ActionUrl"] = actionUrl
            }), ct);
    }

    public async Task SendTutorStudentPaymentReceivedAsync(string to, string tutorName, string studentName, string courseTitle, decimal amount, CancellationToken ct = default)
    {
        if (!_client.IsConfigured) { _logger.LogWarning("MailGateway non configuré — TUTOR_STUDENT_PAYMENT_RECEIVED non envoyé à {Email}.", to); return; }
        await TrySendAsync(new SendMailRequest(_settings.ClientCode, EmailTemplates.TutorStudentPaymentReceived, [to],
            new Dictionary<string, string>
            {
                ["TutorName"] = tutorName,
                ["StudentName"] = studentName,
                ["CourseTitle"] = courseTitle,
                ["Amount"] = amount.ToString("C")
            }), ct);
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
