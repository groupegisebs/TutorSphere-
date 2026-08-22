using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<Tenant> Tenants { get; }
    IQueryable<TenantBranding> TenantBrandings { get; }
    IQueryable<Student> Students { get; }
    /// <summary>Students without tenant query filter (parent portal).</summary>
    IQueryable<Student> StudentsForAnyTenant { get; }
    IQueryable<ParentProfile> ParentProfiles { get; }
    /// <summary>Parent profiles without tenant query filter (parent portal).</summary>
    IQueryable<ParentProfile> ParentProfilesForAnyTenant { get; }
    IQueryable<SubscriptionOffering> SubscriptionOfferings { get; }
    /// <summary>Offerings without tenant query filter (parent search / public directory).</summary>
    IQueryable<SubscriptionOffering> SubscriptionOfferingsForAnyTenant { get; }
    IQueryable<StudentSubscription> StudentSubscriptions { get; }
    /// <summary>Subscriptions without tenant query filter (parent portal across schools).</summary>
    IQueryable<StudentSubscription> StudentSubscriptionsForAnyTenant { get; }
    IQueryable<Lesson> Lessons { get; }
    /// <summary>Lessons without tenant filter (parent portal across schools).</summary>
    IQueryable<Lesson> LessonsForAnyTenant { get; }
    IQueryable<Unavailability> Unavailabilities { get; }
    IQueryable<Unavailability> UnavailabilitiesForAnyTenant { get; }
    IQueryable<LessonCoverageAssignment> LessonCoverageAssignments { get; }
    IQueryable<TeacherAvailability> TeacherAvailabilities { get; }
    IQueryable<TeacherAvailability> TeacherAvailabilitiesForAnyTenant { get; }
    IQueryable<Holiday> Holidays { get; }
    IQueryable<Vacation> Vacations { get; }
    IQueryable<LessonReport> LessonReports { get; }
    /// <summary>Lesson reports without tenant filter (student/parent portal).</summary>
    IQueryable<LessonReport> LessonReportsForAnyTenant { get; }
    IQueryable<Homework> Homeworks { get; }
    /// <summary>Homework without tenant filter (student portal).</summary>
    IQueryable<Homework> HomeworksForAnyTenant { get; }
    IQueryable<Invoice> Invoices { get; }
    /// <summary>Invoices without tenant filter (parent portal).</summary>
    IQueryable<Invoice> InvoicesForAnyTenant { get; }
    IQueryable<Payment> Payments { get; }
    /// <summary>Payments without tenant filter (parent portal).</summary>
    IQueryable<Payment> PaymentsForAnyTenant { get; }
    IQueryable<Document> Documents { get; }
    /// <summary>Documents without tenant filter (student portal).</summary>
    IQueryable<Document> DocumentsForAnyTenant { get; }
    IQueryable<Message> Messages { get; }
    IQueryable<LessonAttendance> LessonAttendances { get; }
    /// <summary>Attendances without tenant filter (parent portal across schools).</summary>
    IQueryable<LessonAttendance> LessonAttendancesForAnyTenant { get; }
    IQueryable<TutorPayout> TutorPayouts { get; }
    IQueryable<TutorPayout> TutorPayoutsForAnyTenant { get; }
    IQueryable<TutorPayoutAccount> TutorPayoutAccounts { get; }
    IQueryable<PlatformLicensePayment> PlatformLicensePayments { get; }
    IQueryable<PlatformLicensePayment> PlatformLicensePaymentsForAnyTenant { get; }
    IQueryable<PlatformPromoCode> PlatformPromoCodes { get; }
    IQueryable<ExpertGroup> ExpertGroups { get; }
    IQueryable<ExpertGroupMember> ExpertGroupMembers { get; }
    IQueryable<ExpertGroupDefinedRole> ExpertGroupDefinedRoles { get; }
    IQueryable<ExpertGroupManagerMandate> ExpertGroupManagerMandates { get; }
    IQueryable<GroupOffer> GroupOffers { get; }
    IQueryable<GroupOfferTeacher> GroupOfferTeachers { get; }
    IQueryable<GroupAdminConversation> GroupAdminConversations { get; }
    IQueryable<GroupAdminMessage> GroupAdminMessages { get; }
    IQueryable<TeacherInterestRequest> TeacherInterestRequests { get; }
    IQueryable<ExpertDelegatedTask> ExpertDelegatedTasks { get; }
    IQueryable<ExpertWorkspaceItem> ExpertWorkspaceItems { get; }
    IQueryable<ExpertGovernanceEvent> ExpertGovernanceEvents { get; }
    IQueryable<TeacherDocument> TeacherDocuments { get; }
    /// <summary>Teacher verification documents without tenant query filter (expert / admin review).</summary>
    IQueryable<TeacherDocument> TeacherDocumentsForAnyTenant { get; }
    IQueryable<TeacherApplicationInvite> TeacherApplicationInvites { get; }
    IQueryable<ExpertMembershipInvite> ExpertMembershipInvites { get; }
    IQueryable<ExpertMembershipVote> ExpertMembershipVotes { get; }
    IQueryable<ExpertRemark> ExpertRemarks { get; }
    /// <summary>Remarques expert sans filtre tenant (portail expert / enseignant).</summary>
    IQueryable<ExpertRemark> ExpertRemarksForAnyTenant { get; }
    IQueryable<Discipline> Disciplines { get; }
    IQueryable<DisciplineServiceItem> DisciplineServiceItems { get; }
    IQueryable<TeacherDisciplineAssignment> TeacherDisciplineAssignments { get; }
    IQueryable<ParentSupportRequest> ParentSupportRequests { get; }
    IQueryable<Meeting> Meetings { get; }
    IQueryable<MeetingRecurrence> MeetingRecurrences { get; }
    IQueryable<MeetingGroup> MeetingGroups { get; }
    IQueryable<MeetingParticipant> MeetingParticipants { get; }
    IQueryable<MeetingExternalGuest> MeetingExternalGuests { get; }
    IQueryable<MeetingInvitation> MeetingInvitations { get; }
    IQueryable<MeetingSession> MeetingSessions { get; }
    IQueryable<MeetingAttendance> MeetingAttendances { get; }
    IQueryable<MeetingMessage> MeetingMessages { get; }
    IQueryable<MeetingFile> MeetingFiles { get; }
    IQueryable<MeetingRecording> MeetingRecordings { get; }
    IQueryable<MeetingTranscript> MeetingTranscripts { get; }
    IQueryable<MeetingAIConsent> MeetingAiConsents { get; }
    IQueryable<MeetingAISummary> MeetingAiSummaries { get; }
    IQueryable<MeetingDecision> MeetingDecisions { get; }
    IQueryable<MeetingActionItem> MeetingActionItems { get; }
    IQueryable<MeetingNotification> MeetingNotifications { get; }
    IQueryable<MeetingAuditLog> MeetingAuditLogs { get; }
    IQueryable<TeacherContract> TeacherContracts { get; }
    IQueryable<TeacherContractSectionDecision> TeacherContractSectionDecisions { get; }
    IQueryable<TeacherContractAuditEvent> TeacherContractAuditEvents { get; }
    IQueryable<PlatformPaymentSettings> PlatformPaymentSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    void RemoveRange<T>(IEnumerable<T> entities) where T : class;
}
