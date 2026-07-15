namespace TutorSphere.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendWelcomeAsync(string email, string firstName, CancellationToken ct = default);

    Task SendEmailConfirmationAsync(
        string email,
        string firstName,
        string confirmationUrl,
        CancellationToken ct = default);

    Task SendLessonReportToParentAsync(
        string parentEmail,
        string parentFirstName,
        string studentName,
        string tutorName,
        CancellationToken ct = default);

    Task SendSchoolCreatedAsync(
        string ownerEmail,
        string ownerFirstName,
        string schoolName,
        CancellationToken ct = default);

    // Auth
    Task SendEmailConfirmationSimpleAsync(string to, string firstName, string confirmUrl, CancellationToken ct = default);
    Task SendResetPasswordAsync(string to, string firstName, string resetUrl, CancellationToken ct = default);
    Task SendPasswordChangedAsync(string to, string firstName, CancellationToken ct = default);

    // Tutor billing
    Task SendTutorTrialStartedAsync(string to, string firstName, CancellationToken ct = default);
    Task SendTutorPaymentReceiptAsync(string to, string firstName, decimal amount, string invoiceUrl, CancellationToken ct = default);
    Task SendTutorRenewalReminderAsync(string to, string firstName, DateTime renewalDate, CancellationToken ct = default);
    Task SendTutorPaymentFailedAsync(string to, string firstName, CancellationToken ct = default);
    Task SendTutorSubscriptionCancelledAsync(string to, string firstName, CancellationToken ct = default);

    // Account lifecycle
    Task SendAccountActivatedAsync(string to, string firstName, CancellationToken ct = default);
    Task SendAccountDeactivatedAsync(string to, string firstName, string reason, CancellationToken ct = default);
    Task SendSchoolApprovedAsync(string to, string firstName, string schoolName, string loginUrl, CancellationToken ct = default);

    // Lessons
    Task SendLessonScheduledAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default);
    Task SendLessonReminderAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default);
    Task SendLessonCancelledAsync(string to, string recipientName, string tutorName, string subject, DateTime lessonDate, CancellationToken ct = default);

    // Parent billing
    Task SendParentPaymentReceiptAsync(string to, string parentName, string studentName, decimal amount, string invoiceUrl, CancellationToken ct = default);
    Task SendParentPaymentFailedAsync(string to, string parentName, CancellationToken ct = default);
    Task SendInvoiceReadyAsync(string to, string parentName, string invoiceUrl, CancellationToken ct = default);
    Task SendParentPaymentOverdueAsync(string to, string parentName, string studentName, string courseTitle, string payUrl, CancellationToken ct = default);

    // Course enrollment
    Task SendCourseEnrollmentRequestAsync(string to, string tutorName, string studentName, string courseTitle, CancellationToken ct = default);
    Task SendCourseEnrollmentAcceptedAsync(string to, string parentName, string studentName, string courseTitle, string statusNote, string actionUrl, CancellationToken ct = default);

    /// <summary>Notifie le tuteur qu'un paiement parent a été reçu pour un cours.</summary>
    Task SendTutorStudentPaymentReceivedAsync(string to, string tutorName, string studentName, string courseTitle, decimal amount, CancellationToken ct = default);
}
