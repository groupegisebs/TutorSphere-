using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Email;

internal static class EmailTemplates
{
    public const string Welcome = "WELCOME";
    public const string ConfirmEmail = "CONFIRM_EMAIL";
    public const string LessonReport = "LESSON_REPORT";
    public const string SchoolCreated = "SCHOOL_CREATED";

    public const string ConfirmEmailSimple = "CONFIRM_EMAIL_SIMPLE";
    public const string ResetPassword = "RESET_PASSWORD";
    public const string PasswordChanged = "PASSWORD_CHANGED";

    public const string TutorTrialStarted = "TUTOR_TRIAL_STARTED";
    public const string TutorPaymentReceipt = "TUTOR_PAYMENT_RECEIPT";
    public const string TutorRenewalReminder = "TUTOR_RENEWAL_REMINDER";
    public const string TutorPaymentFailed = "TUTOR_PAYMENT_FAILED";
    public const string TutorSubCancelled = "TUTOR_SUB_CANCELLED";

    public const string AccountActivated = "ACCOUNT_ACTIVATED";
    public const string AccountDeactivated = "ACCOUNT_DEACTIVATED";
    public const string SchoolApproved = "SCHOOL_APPROVED";

    public const string LessonScheduled = "LESSON_SCHEDULED";
    public const string LessonReminder = "LESSON_REMINDER";
    public const string LessonCancelled = "LESSON_CANCELLED";

    public const string ParentPaymentReceipt = "PARENT_PAYMENT_RECEIPT";
    public const string ParentPaymentFailed = "PARENT_PAYMENT_FAILED";
    public const string InvoiceReady = "INVOICE_READY";
    public const string ParentPaymentOverdue = "PARENT_PAYMENT_OVERDUE";
    public const string ParentSubscriptionRenewal = "PARENT_SUBSCRIPTION_RENEWAL";

    public const string CourseEnrollmentRequest = "COURSE_ENROLLMENT_REQUEST";
    public const string CourseEnrollmentAccepted = "COURSE_ENROLLMENT_ACCEPTED";
    public const string TutorStudentPaymentReceived = "TUTOR_STUDENT_PAYMENT_RECEIVED";
    public const string ExpertTeacherPending = "EXPERT_TEACHER_PENDING";
}

public class EmailService : IEmailService
{
    private readonly MailGatewayClient _client;
    private readonly MailGatewaySettings _settings;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        MailGatewayClient client,
        IOptions<MailGatewaySettings> settings,
        UserManager<ApplicationUser> users,
        ILogger<EmailService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _users = users;
        _logger = logger;
    }

    public Task SendWelcomeAsync(string email, string firstName, CancellationToken ct = default) =>
        SendAsync(email, EmailTemplates.Welcome, new Dictionary<string, string> { ["FirstName"] = firstName }, ct);

    public Task SendEmailConfirmationAsync(
        string email,
        string firstName,
        string confirmationUrl,
        CancellationToken ct = default) =>
        SendAsync(email, EmailTemplates.ConfirmEmail, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["ConfirmationUrl"] = confirmationUrl
        }, ct);

    public Task SendLessonReportToParentAsync(
        string parentEmail,
        string parentFirstName,
        string studentName,
        string tutorName,
        CancellationToken ct = default) =>
        SendAsync(parentEmail, EmailTemplates.LessonReport, new Dictionary<string, string>
        {
            ["ParentFirstName"] = parentFirstName,
            ["StudentName"] = studentName,
            ["TutorName"] = tutorName
        }, ct);

    public Task SendSchoolCreatedAsync(
        string ownerEmail,
        string ownerFirstName,
        string schoolName,
        CancellationToken ct = default) =>
        SendAsync(ownerEmail, EmailTemplates.SchoolCreated, new Dictionary<string, string>
        {
            ["OwnerFirstName"] = ownerFirstName,
            ["SchoolName"] = schoolName
        }, ct);

    public Task SendEmailConfirmationSimpleAsync(string to, string firstName, string confirmUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ConfirmEmailSimple, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["ConfirmationUrl"] = confirmUrl
        }, ct);

    public Task SendResetPasswordAsync(string to, string firstName, string resetUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ResetPassword, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["ResetUrl"] = resetUrl
        }, ct);

    public Task SendPasswordChangedAsync(string to, string firstName, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.PasswordChanged, new Dictionary<string, string> { ["FirstName"] = firstName }, ct);

    public Task SendTutorTrialStartedAsync(string to, string firstName, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.TutorTrialStarted, new Dictionary<string, string> { ["FirstName"] = firstName }, ct);

    public async Task SendTutorPaymentReceiptAsync(string to, string firstName, decimal amount, string invoiceUrl, CancellationToken ct = default)
    {
        var culture = await ResolveCultureAsync(to, ct);
        await SendAsync(to, EmailTemplates.TutorPaymentReceipt, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["Amount"] = amount.ToString("C", culture),
            ["InvoiceUrl"] = invoiceUrl
        }, ct, culture.Name);
    }

    public async Task SendTutorRenewalReminderAsync(string to, string firstName, DateTime renewalDate, CancellationToken ct = default)
    {
        var culture = await ResolveCultureAsync(to, ct);
        await SendAsync(to, EmailTemplates.TutorRenewalReminder, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["RenewalDate"] = renewalDate.ToString("D", culture)
        }, ct, culture.Name);
    }

    public Task SendTutorPaymentFailedAsync(string to, string firstName, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.TutorPaymentFailed, new Dictionary<string, string> { ["FirstName"] = firstName }, ct);

    public Task SendTutorSubscriptionCancelledAsync(string to, string firstName, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.TutorSubCancelled, new Dictionary<string, string> { ["FirstName"] = firstName }, ct);

    public Task SendAccountActivatedAsync(string to, string firstName, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.AccountActivated, new Dictionary<string, string> { ["FirstName"] = firstName }, ct);

    public Task SendAccountDeactivatedAsync(string to, string firstName, string reason, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.AccountDeactivated, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["Reason"] = reason
        }, ct);

    public Task SendSchoolApprovedAsync(string to, string firstName, string schoolName, string loginUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.SchoolApproved, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["SchoolName"] = schoolName,
            ["LoginUrl"] = loginUrl
        }, ct);

    public async Task SendLessonScheduledAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default)
    {
        var culture = await ResolveCultureAsync(to, ct);
        await SendAsync(to, EmailTemplates.LessonScheduled, LessonBody(recipientName, tutorName, subject, lessonDate, culture), ct, culture.Name);
    }

    public async Task SendLessonReminderAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default)
    {
        var culture = await ResolveCultureAsync(to, ct);
        await SendAsync(to, EmailTemplates.LessonReminder, LessonBody(recipientName, tutorName, subject, lessonDate, culture), ct, culture.Name);
    }

    public async Task SendLessonCancelledAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default)
    {
        var culture = await ResolveCultureAsync(to, ct);
        await SendAsync(to, EmailTemplates.LessonCancelled, LessonBody(recipientName, tutorName, subject, lessonDate, culture), ct, culture.Name);
    }

    public async Task SendParentPaymentReceiptAsync(string to, string parentName, string studentName, decimal amount, string invoiceUrl, CancellationToken ct = default)
    {
        var culture = await ResolveCultureAsync(to, ct);
        await SendAsync(to, EmailTemplates.ParentPaymentReceipt, new Dictionary<string, string>
        {
            ["ParentName"] = parentName,
            ["StudentName"] = studentName,
            ["Amount"] = amount.ToString("C", culture),
            ["InvoiceUrl"] = invoiceUrl
        }, ct, culture.Name);
    }

    public Task SendParentPaymentFailedAsync(string to, string parentName, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ParentPaymentFailed, new Dictionary<string, string> { ["ParentName"] = parentName }, ct);

    public Task SendInvoiceReadyAsync(string to, string parentName, string invoiceUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.InvoiceReady, new Dictionary<string, string>
        {
            ["ParentName"] = parentName,
            ["InvoiceUrl"] = invoiceUrl
        }, ct);

    public Task SendParentPaymentOverdueAsync(string to, string parentName, string studentName, string courseTitle, string payUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ParentPaymentOverdue, new Dictionary<string, string>
        {
            ["ParentName"] = parentName,
            ["StudentName"] = studentName,
            ["CourseTitle"] = courseTitle,
            ["PayUrl"] = payUrl
        }, ct);

    public async Task SendParentSubscriptionRenewalReminderAsync(
        string to,
        string parentName,
        string studentName,
        string courseTitle,
        DateTime endDate,
        string payUrl,
        CancellationToken ct = default)
    {
        var culture = await ResolveCultureAsync(to, ct);
        await SendAsync(to, EmailTemplates.ParentSubscriptionRenewal, new Dictionary<string, string>
        {
            ["ParentName"] = parentName,
            ["StudentName"] = studentName,
            ["CourseTitle"] = courseTitle,
            ["EndDate"] = endDate.ToString("D", culture),
            ["PayUrl"] = payUrl
        }, ct, culture.Name);
    }

    public Task SendCourseEnrollmentRequestAsync(string to, string tutorName, string studentName, string courseTitle, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.CourseEnrollmentRequest, new Dictionary<string, string>
        {
            ["TutorName"] = tutorName,
            ["StudentName"] = studentName,
            ["CourseTitle"] = courseTitle
        }, ct);

    public Task SendCourseEnrollmentAcceptedAsync(string to, string parentName, string studentName, string courseTitle, string statusNote, string actionUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.CourseEnrollmentAccepted, new Dictionary<string, string>
        {
            ["ParentName"] = parentName,
            ["StudentName"] = studentName,
            ["CourseTitle"] = courseTitle,
            ["StatusNote"] = statusNote,
            ["ActionUrl"] = actionUrl
        }, ct);

    public async Task SendTutorStudentPaymentReceivedAsync(string to, string tutorName, string studentName, string courseTitle, decimal amount, CancellationToken ct = default)
    {
        var culture = await ResolveCultureAsync(to, ct);
        await SendAsync(to, EmailTemplates.TutorStudentPaymentReceived, new Dictionary<string, string>
        {
            ["TutorName"] = tutorName,
            ["StudentName"] = studentName,
            ["CourseTitle"] = courseTitle,
            ["Amount"] = amount.ToString("C", culture)
        }, ct, culture.Name);
    }

    public Task SendExpertTeacherPendingReviewAsync(
        string to,
        string expertFirstName,
        string schoolName,
        string? country,
        string reviewUrl,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertTeacherPending, new Dictionary<string, string>
        {
            ["ExpertFirstName"] = expertFirstName,
            ["SchoolName"] = schoolName,
            ["Country"] = string.IsNullOrWhiteSpace(country) ? "—" : country.Trim(),
            ["ReviewUrl"] = reviewUrl
        }, ct);

    private static Dictionary<string, string> LessonBody(
        string recipientName,
        string tutorName,
        string subject,
        DateTime lessonDate,
        CultureInfo culture) =>
        new()
        {
            ["RecipientName"] = recipientName,
            ["TutorName"] = tutorName,
            ["Subject"] = subject,
            ["LessonDate"] = lessonDate.ToString("f", culture)
        };

    private async Task SendAsync(
        string to,
        string templateCode,
        Dictionary<string, string> bodyData,
        CancellationToken ct,
        string? language = null)
    {
        if (!_client.IsConfigured)
        {
            _logger.LogWarning("Mail Sender non configuré — {Template} non envoyé à {Email}.", templateCode, to);
            return;
        }

        var lang = SupportedLanguageCodes.Normalize(language ?? await ResolveLanguageAsync(to, ct));
        await TrySendAsync(new SendMailRequest(
            ClientCode: _settings.ClientCode,
            TemplateCode: templateCode,
            To: [to],
            BodyData: bodyData,
            Language: lang
        ), ct);
    }

    private async Task<string> ResolveLanguageAsync(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return SupportedLanguageCodes.Default;

        var user = await _users.FindByEmailAsync(email.Trim());
        return SupportedLanguageCodes.Normalize(user?.PreferredLanguage);
    }

    private async Task<CultureInfo> ResolveCultureAsync(string email, CancellationToken ct)
    {
        var lang = await ResolveLanguageAsync(email, ct);
        try
        {
            return CultureInfo.GetCultureInfo(lang);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(SupportedLanguageCodes.French);
        }
    }

    private async Task TrySendAsync(SendMailRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _client.SendAsync(request, ct);
            if (result.Success)
                _logger.LogInformation(
                    "E-mail {Template} ({Language}) envoyé → {MailCode}",
                    request.TemplateCode,
                    request.Language,
                    result.MailCode);
            else
                _logger.LogWarning(
                    "E-mail {Template} ({Language}) refusé : {Error}",
                    request.TemplateCode,
                    request.Language,
                    result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec d'envoi d'e-mail {Template} ({Language}).", request.TemplateCode, request.Language);
        }
    }
}
