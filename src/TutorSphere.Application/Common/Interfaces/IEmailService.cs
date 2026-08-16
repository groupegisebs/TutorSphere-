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
    /// <summary>Invitation parent : valider l'e-mail pour activer l'espace parent (pas de WELCOME générique).</summary>
    Task SendParentAccessConfirmationAsync(string to, string firstName, string confirmUrl, CancellationToken ct = default);
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
    Task SendParentPaymentRefundedAsync(
        string to,
        string parentName,
        string studentName,
        string tutorName,
        decimal amount,
        CancellationToken ct = default);
    Task SendParentPaymentFailedAsync(string to, string parentName, CancellationToken ct = default);
    Task SendInvoiceReadyAsync(string to, string parentName, string invoiceUrl, CancellationToken ct = default);
    Task SendParentPaymentOverdueAsync(string to, string parentName, string studentName, string courseTitle, string payUrl, CancellationToken ct = default);

    Task SendParentSubscriptionRenewalReminderAsync(
        string to,
        string parentName,
        string studentName,
        string courseTitle,
        DateTime endDate,
        string payUrl,
        CancellationToken ct = default);

    // Course enrollment
    Task SendCourseEnrollmentRequestAsync(string to, string tutorName, string studentName, string courseTitle, CancellationToken ct = default, string? actionUrl = null);
    Task SendCourseEnrollmentAcceptedAsync(string to, string parentName, string studentName, string courseTitle, string statusNote, string actionUrl, CancellationToken ct = default);

    /// <summary>Notifie le tuteur qu'un paiement parent a été reçu pour un cours.</summary>
    Task SendTutorStudentPaymentReceivedAsync(string to, string tutorName, string studentName, string courseTitle, decimal amount, CancellationToken ct = default);

    /// <summary>Notifie un expert qu'un enseignant est en attente de validation.</summary>
    Task SendExpertTeacherPendingReviewAsync(
        string to,
        string expertFirstName,
        string schoolName,
        string? country,
        string reviewUrl,
        CancellationToken ct = default);

    /// <summary>Invitation expert : identifiants + mot de passe temporaire à changer.</summary>
    Task SendExpertInviteAsync(
        string to,
        string firstName,
        string temporaryPassword,
        string loginUrl,
        string groupName,
        CancellationToken ct = default);

    /// <summary>
    /// Identifiants de connexion enseignant (login généré groupe.XXXX@domaine).
    /// <paramref name="to"/> = destinataire de la notification ; <paramref name="loginEmail"/> = compte de connexion.
    /// </summary>
    Task SendTeacherAccountCredentialsAsync(
        string to,
        string teacherFirstName,
        string loginEmail,
        string temporaryPassword,
        string loginUrl,
        string groupName,
        CancellationToken ct = default);

    /// <summary>Notification : expert existant ajouté à un groupe (sans mot de passe).</summary>
    Task SendExpertAddedToGroupAsync(
        string to,
        string firstName,
        string loginUrl,
        string groupName,
        CancellationToken ct = default);

    /// <summary>Notifie l'enseignant que son dossier a été approuvé par un groupe d'experts.</summary>
    Task SendExpertTeacherApprovedAsync(
        string to,
        string firstName,
        string schoolName,
        string groupName,
        string notes,
        string loginUrl,
        string? loginEmail = null,
        string? temporaryPassword = null,
        string? loginInstructions = null,
        CancellationToken ct = default);

    /// <summary>Notifie l'enseignant que son dossier a été rejeté par un groupe d'experts.</summary>
    Task SendExpertTeacherRejectedAsync(
        string to,
        string firstName,
        string schoolName,
        string groupName,
        string notes,
        string loginUrl,
        CancellationToken ct = default);

    /// <summary>Invite un enseignant à déposer sa candidature (URL + bouton).</summary>
    Task SendExpertTeacherApplyInviteAsync(
        string to,
        string firstName,
        string expertName,
        string groupName,
        string personalMessage,
        string applyUrl,
        CancellationToken ct = default);

    /// <summary>Notifie l'enseignant qu'un expert a laissé une nouvelle remarque de suivi qualité.</summary>
    Task SendExpertRemarkNotificationAsync(
        string to,
        string firstName,
        string schoolName,
        string category,
        string excerpt,
        string remarksUrl,
        CancellationToken ct = default);

    Task SendExpertMembershipInviteAsync(
        string to,
        string firstName,
        string inviterName,
        string groupName,
        string personalMessage,
        string joinUrl,
        CancellationToken ct = default);

    Task SendExpertMembershipVoteOpenedAsync(
        string to,
        string voterName,
        string candidateName,
        string groupName,
        string voteUrl,
        CancellationToken ct = default);

    Task SendExpertMembershipRejectedAsync(
        string to,
        string firstName,
        string reason,
        CancellationToken ct = default);

    Task SendSupportContactAsync(
        string to,
        string parentFirstName,
        string parentLastName,
        string replyToEmail,
        string subject,
        string message,
        CancellationToken ct = default);

    /// <summary>Copie e-mail d'un message interne envoyé par un admin plateforme.</summary>
    Task SendAdminDirectMessageAsync(
        string to,
        string firstName,
        string adminName,
        string subject,
        string messageBody,
        string inboxUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Invitation à une réunion. Les invités externes reçoivent un modèle distinct :
    /// lien personnel temporaire et vérification par code, sans accès au reste de la plateforme.
    /// </summary>
    Task SendMeetingInvitationAsync(
        string to,
        string recipientName,
        string title,
        DateTime startAtUtc,
        string timeZoneId,
        string organizerName,
        string? agenda,
        string joinUrl,
        bool recordingEnabled,
        bool aiEnabled,
        bool isExternal,
        DateTime? linkExpiresAtUtc = null,
        CancellationToken ct = default);

    Task SendMeetingCancelledAsync(string to, string title, DateTime startAtUtc, CancellationToken ct = default);

    Task SendMeetingGuestCodeAsync(string to, string recipientName, string title, string code, CancellationToken ct = default);

    Task SendMeetingReminderAsync(string to, string recipientName, string title, DateTime startAtUtc, string joinUrl, CancellationToken ct = default);

    Task SendMeetingMinutesAsync(string to, string recipientName, string title, string minutesUrl, CancellationToken ct = default);
}
