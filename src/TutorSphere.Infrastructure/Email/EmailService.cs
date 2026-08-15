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
    public const string ParentConfirmAccess = "PARENT_CONFIRM_ACCESS";
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
    public const string ExpertInvite = "EXPERT_INVITE";
    public const string ExpertAddedToGroup = "EXPERT_ADDED_TO_GROUP";
    public const string ExpertTeacherApproved = "EXPERT_TEACHER_APPROVED";
    public const string ExpertTeacherRejected = "EXPERT_TEACHER_REJECTED";
    public const string ExpertTeacherApplyInvite = "EXPERT_TEACHER_APPLY_INVITE";
    public const string ExpertRemarkNotification = "EXPERT_REMARK_NOTIFICATION";
    public const string ExpertMembershipInvite = "EXPERT_MEMBERSHIP_INVITE";
    public const string ExpertMembershipVoteOpened = "EXPERT_MEMBERSHIP_VOTE_OPENED";
    public const string ExpertMembershipRejected = "EXPERT_MEMBERSHIP_REJECTED";
    public const string SupportContact = "SUPPORT_CONTACT";
    public const string AdminDirectMessage = "ADMIN_DIRECT_MESSAGE";
    public const string MeetingInvitation = "MEETING_INVITATION";
    public const string MeetingCancelled = "MEETING_CANCELLED";
    public const string MeetingGuestCode = "MEETING_GUEST_CODE";
    public const string MeetingReminder = "MEETING_REMINDER";
    public const string MeetingMinutes = "MEETING_MINUTES";
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

    public Task SendParentAccessConfirmationAsync(string to, string firstName, string confirmUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ParentConfirmAccess, new Dictionary<string, string>
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

    public Task SendExpertInviteAsync(
        string to,
        string firstName,
        string temporaryPassword,
        string loginUrl,
        string groupName,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertInvite, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["Email"] = to,
            ["TemporaryPassword"] = temporaryPassword,
            ["LoginUrl"] = loginUrl,
            ["GroupName"] = groupName
        }, ct);

    public Task SendTeacherAccountCredentialsAsync(
        string to,
        string teacherFirstName,
        string loginEmail,
        string temporaryPassword,
        string loginUrl,
        string groupName,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertInvite, new Dictionary<string, string>
        {
            ["FirstName"] = teacherFirstName,
            ["Email"] = loginEmail,
            ["TemporaryPassword"] = temporaryPassword,
            ["LoginUrl"] = loginUrl,
            ["GroupName"] = groupName
        }, ct);

    public Task SendExpertAddedToGroupAsync(
        string to,
        string firstName,
        string loginUrl,
        string groupName,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertAddedToGroup, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["Email"] = to,
            ["LoginUrl"] = loginUrl,
            ["GroupName"] = groupName
        }, ct);

    public Task SendExpertTeacherApprovedAsync(
        string to,
        string firstName,
        string schoolName,
        string groupName,
        string notes,
        string loginUrl,
        string? loginEmail = null,
        string? temporaryPassword = null,
        string? loginInstructions = null,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertTeacherApproved, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["SchoolName"] = schoolName,
            ["GroupName"] = groupName,
            ["Notes"] = string.IsNullOrWhiteSpace(notes) ? "—" : notes.Trim(),
            ["LoginUrl"] = loginUrl,
            ["Email"] = string.IsNullOrWhiteSpace(loginEmail) ? to : loginEmail.Trim(),
            ["TemporaryPassword"] = temporaryPassword ?? "",
            ["LoginInstructions"] = loginInstructions ?? ""
        }, ct);

    public Task SendExpertTeacherRejectedAsync(
        string to,
        string firstName,
        string schoolName,
        string groupName,
        string notes,
        string loginUrl,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertTeacherRejected, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["SchoolName"] = schoolName,
            ["GroupName"] = groupName,
            ["Notes"] = string.IsNullOrWhiteSpace(notes) ? "—" : notes.Trim(),
            ["LoginUrl"] = loginUrl
        }, ct);

    public Task SendExpertTeacherApplyInviteAsync(
        string to,
        string firstName,
        string expertName,
        string groupName,
        string personalMessage,
        string applyUrl,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertTeacherApplyInvite, new Dictionary<string, string>
        {
            ["FirstName"] = string.IsNullOrWhiteSpace(firstName) ? "enseignant" : firstName.Trim(),
            ["ExpertName"] = string.IsNullOrWhiteSpace(expertName) ? "un expert TutorSphere" : expertName.Trim(),
            ["GroupName"] = string.IsNullOrWhiteSpace(groupName) ? "TutorSphere" : groupName.Trim(),
            ["PersonalMessage"] = string.IsNullOrWhiteSpace(personalMessage)
                ? "Nous vous invitons à créer votre profil enseignant et à déposer votre candidature pour examen."
                : personalMessage.Trim(),
            ["ApplyUrl"] = applyUrl
        }, ct);

    public Task SendExpertRemarkNotificationAsync(
        string to,
        string firstName,
        string schoolName,
        string category,
        string excerpt,
        string remarksUrl,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertRemarkNotification, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["SchoolName"] = schoolName,
            ["Category"] = category,
            ["Excerpt"] = excerpt,
            ["RemarksUrl"] = remarksUrl
        }, ct);

    public Task SendExpertMembershipInviteAsync(
        string to,
        string firstName,
        string inviterName,
        string groupName,
        string personalMessage,
        string joinUrl,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertMembershipInvite, new Dictionary<string, string>
        {
            ["FirstName"] = string.IsNullOrWhiteSpace(firstName) ? "candidat" : firstName.Trim(),
            ["InviterName"] = string.IsNullOrWhiteSpace(inviterName) ? "un Responsable" : inviterName.Trim(),
            ["GroupName"] = groupName,
            ["PersonalMessage"] = string.IsNullOrWhiteSpace(personalMessage)
                ? "—"
                : personalMessage.Trim(),
            ["JoinUrl"] = joinUrl
        }, ct);

    public Task SendExpertMembershipVoteOpenedAsync(
        string to,
        string voterName,
        string candidateName,
        string groupName,
        string voteUrl,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertMembershipVoteOpened, new Dictionary<string, string>
        {
            ["FirstName"] = string.IsNullOrWhiteSpace(voterName) ? "expert" : voterName.Trim(),
            ["CandidateName"] = candidateName,
            ["GroupName"] = groupName,
            ["VoteUrl"] = voteUrl
        }, ct);

    public Task SendExpertMembershipRejectedAsync(
        string to,
        string firstName,
        string reason,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.ExpertMembershipRejected, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["Reason"] = string.IsNullOrWhiteSpace(reason) ? "Candidature non retenue." : reason.Trim()
        }, ct);

    public Task SendSupportContactAsync(
        string to,
        string parentFirstName,
        string parentLastName,
        string replyToEmail,
        string subject,
        string message,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.SupportContact, new Dictionary<string, string>
        {
            ["ParentName"] = $"{parentFirstName} {parentLastName}".Trim(),
            ["ReplyTo"] = replyToEmail,
            ["Subject"] = subject,
            ["Message"] = message
        }, ct);

    public Task SendAdminDirectMessageAsync(
        string to,
        string firstName,
        string adminName,
        string subject,
        string messageBody,
        string inboxUrl,
        CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.AdminDirectMessage, new Dictionary<string, string>
        {
            ["FirstName"] = firstName,
            ["AdminName"] = adminName,
            ["Subject"] = subject,
            ["Message"] = messageBody,
            ["InboxUrl"] = inboxUrl
        }, ct);

    public Task SendMeetingInvitationAsync(
        string to, string recipientName, string title, DateTime startAtUtc, string timeZoneId,
        string organizerName, string? agenda, string joinUrl, bool recordingEnabled, bool aiEnabled,
        bool isExternal, CancellationToken ct = default)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var privacy = isExternal
            ? "Ce lien est personnel, temporaire et non transférable. Il ne donne pas accès au reste de TutorSphere."
            : "Réunion privée du groupe d’experts. Ne transférez pas le lien.";
        var flags = new List<string>();
        if (recordingEnabled) flags.Add("un enregistrement pourra être réalisé");
        if (aiEnabled) flags.Add("un assistant IA pourra analyser la discussion (avec votre consentement)");
        var notice = flags.Count == 0
            ? "Aucun enregistrement ni assistant IA n’est prévu pour le moment."
            : "Attention : " + string.Join(" et ", flags) + ".";
        return SendAsync(to, EmailTemplates.MeetingInvitation, new Dictionary<string, string>
        {
            ["RecipientName"] = recipientName,
            ["Title"] = title,
            ["StartLocal"] = startAtUtc.ToString("f", culture),
            ["TimeZone"] = timeZoneId,
            ["OrganizerName"] = organizerName,
            ["Agenda"] = agenda ?? "",
            ["JoinUrl"] = joinUrl,
            ["CalendarUrl"] = joinUrl,
            ["Privacy"] = privacy,
            ["RecordingAndAi"] = notice
        }, ct, culture.Name);
    }

    public Task SendMeetingCancelledAsync(string to, string title, DateTime startAtUtc, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.MeetingCancelled, new Dictionary<string, string>
        {
            ["Title"] = title,
            ["StartLocal"] = startAtUtc.ToString("f", CultureInfo.GetCultureInfo("fr-FR"))
        }, ct);

    public Task SendMeetingGuestCodeAsync(string to, string recipientName, string title, string code, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.MeetingGuestCode, new Dictionary<string, string>
        {
            ["RecipientName"] = recipientName,
            ["Title"] = title,
            ["Code"] = code
        }, ct);

    public Task SendMeetingReminderAsync(string to, string recipientName, string title, DateTime startAtUtc, string joinUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.MeetingReminder, new Dictionary<string, string>
        {
            ["RecipientName"] = recipientName,
            ["Title"] = title,
            ["StartLocal"] = startAtUtc.ToString("f", CultureInfo.GetCultureInfo("fr-FR")),
            ["JoinUrl"] = joinUrl
        }, ct);

    public Task SendMeetingMinutesAsync(string to, string recipientName, string title, string minutesUrl, CancellationToken ct = default) =>
        SendAsync(to, EmailTemplates.MeetingMinutes, new Dictionary<string, string>
        {
            ["RecipientName"] = recipientName,
            ["Title"] = title,
            ["MinutesUrl"] = minutesUrl
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
        // Les coordonnées enseignants ne sont jamais exposées dans les templates (uniquement TutorName).
        TeacherContactPrivacy.StripTeacherContactKeys(bodyData);
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
