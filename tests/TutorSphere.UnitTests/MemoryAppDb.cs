using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Entities;

namespace TutorSphere.UnitTests;

/// <summary>DbContext en mémoire pour tester le forfait / l'accès salle sans EF.</summary>
internal sealed class MemoryAppDb : IApplicationDbContext
{
    public List<Student> StudentsList { get; } = [];
    public List<ParentProfile> ParentsList { get; } = [];
    public List<Lesson> LessonsList { get; } = [];
    public List<LessonAttendance> AttendancesList { get; } = [];
    public List<StudentSubscription> SubscriptionsList { get; } = [];
    public List<SubscriptionOffering> OfferingsList { get; } = [];
    public List<Payment> PaymentsList { get; } = [];
    public List<LessonCoverageAssignment> CoverageList { get; } = [];
    public List<Tenant> TenantsList { get; } = [];

    public IQueryable<Student> Students => StudentsList.AsQueryable();
    public IQueryable<Student> StudentsForAnyTenant => StudentsList.AsQueryable();
    public IQueryable<ParentProfile> ParentProfiles => ParentsList.AsQueryable();
    public IQueryable<ParentProfile> ParentProfilesForAnyTenant => ParentsList.AsQueryable();
    public IQueryable<Lesson> Lessons => LessonsList.AsQueryable();
    public IQueryable<Lesson> LessonsForAnyTenant => LessonsList.AsQueryable();
    public IQueryable<LessonAttendance> LessonAttendances => AttendancesList.AsQueryable();
    public IQueryable<LessonAttendance> LessonAttendancesForAnyTenant => AttendancesList.AsQueryable();
    public IQueryable<StudentSubscription> StudentSubscriptions => SubscriptionsList.AsQueryable();
    public IQueryable<StudentSubscription> StudentSubscriptionsForAnyTenant => SubscriptionsList.AsQueryable();
    public IQueryable<SubscriptionOffering> SubscriptionOfferings => OfferingsList.AsQueryable();
    public IQueryable<SubscriptionOffering> SubscriptionOfferingsForAnyTenant => OfferingsList.AsQueryable();
    public IQueryable<Payment> Payments => PaymentsList.AsQueryable();
    public IQueryable<Payment> PaymentsForAnyTenant => PaymentsList.AsQueryable();
    public IQueryable<Tenant> Tenants => TenantsList.AsQueryable();

    IQueryable<TenantBranding> IApplicationDbContext.TenantBrandings => None<TenantBranding>();
    IQueryable<Unavailability> IApplicationDbContext.Unavailabilities => None<Unavailability>();
    IQueryable<Unavailability> IApplicationDbContext.UnavailabilitiesForAnyTenant => None<Unavailability>();
    IQueryable<LessonCoverageAssignment> IApplicationDbContext.LessonCoverageAssignments => CoverageList.AsQueryable();
    IQueryable<TeacherAvailability> IApplicationDbContext.TeacherAvailabilities => None<TeacherAvailability>();
    IQueryable<TeacherAvailability> IApplicationDbContext.TeacherAvailabilitiesForAnyTenant => None<TeacherAvailability>();
    IQueryable<Holiday> IApplicationDbContext.Holidays => None<Holiday>();
    IQueryable<Vacation> IApplicationDbContext.Vacations => None<Vacation>();
    IQueryable<LessonReport> IApplicationDbContext.LessonReports => None<LessonReport>();
    IQueryable<LessonReport> IApplicationDbContext.LessonReportsForAnyTenant => None<LessonReport>();
    IQueryable<Homework> IApplicationDbContext.Homeworks => None<Homework>();
    IQueryable<Homework> IApplicationDbContext.HomeworksForAnyTenant => None<Homework>();
    IQueryable<Invoice> IApplicationDbContext.Invoices => None<Invoice>();
    IQueryable<Invoice> IApplicationDbContext.InvoicesForAnyTenant => None<Invoice>();
    IQueryable<Document> IApplicationDbContext.Documents => None<Document>();
    IQueryable<Document> IApplicationDbContext.DocumentsForAnyTenant => None<Document>();
    IQueryable<Message> IApplicationDbContext.Messages => None<Message>();
    IQueryable<TutorPayout> IApplicationDbContext.TutorPayouts => None<TutorPayout>();
    IQueryable<TutorPayout> IApplicationDbContext.TutorPayoutsForAnyTenant => None<TutorPayout>();
    IQueryable<TutorPayoutAccount> IApplicationDbContext.TutorPayoutAccounts => None<TutorPayoutAccount>();
    IQueryable<PlatformLicensePayment> IApplicationDbContext.PlatformLicensePayments => None<PlatformLicensePayment>();
    IQueryable<PlatformLicensePayment> IApplicationDbContext.PlatformLicensePaymentsForAnyTenant => None<PlatformLicensePayment>();
    IQueryable<PlatformPromoCode> IApplicationDbContext.PlatformPromoCodes => None<PlatformPromoCode>();
    IQueryable<ExpertGroup> IApplicationDbContext.ExpertGroups => None<ExpertGroup>();
    IQueryable<ExpertGroupMember> IApplicationDbContext.ExpertGroupMembers => None<ExpertGroupMember>();
    IQueryable<ExpertGroupDefinedRole> IApplicationDbContext.ExpertGroupDefinedRoles => None<ExpertGroupDefinedRole>();
    IQueryable<ExpertGroupManagerMandate> IApplicationDbContext.ExpertGroupManagerMandates => None<ExpertGroupManagerMandate>();
    IQueryable<GroupOffer> IApplicationDbContext.GroupOffers => None<GroupOffer>();
    IQueryable<GroupOfferTeacher> IApplicationDbContext.GroupOfferTeachers => None<GroupOfferTeacher>();
    IQueryable<GroupAdminConversation> IApplicationDbContext.GroupAdminConversations => None<GroupAdminConversation>();
    IQueryable<GroupAdminMessage> IApplicationDbContext.GroupAdminMessages => None<GroupAdminMessage>();
    IQueryable<TeacherInterestRequest> IApplicationDbContext.TeacherInterestRequests => None<TeacherInterestRequest>();
    IQueryable<ExpertDelegatedTask> IApplicationDbContext.ExpertDelegatedTasks => None<ExpertDelegatedTask>();
    IQueryable<ExpertWorkspaceItem> IApplicationDbContext.ExpertWorkspaceItems => None<ExpertWorkspaceItem>();
    IQueryable<ExpertGovernanceEvent> IApplicationDbContext.ExpertGovernanceEvents => None<ExpertGovernanceEvent>();
    IQueryable<TeacherDocument> IApplicationDbContext.TeacherDocuments => None<TeacherDocument>();
    IQueryable<TeacherDocument> IApplicationDbContext.TeacherDocumentsForAnyTenant => None<TeacherDocument>();
    IQueryable<TeacherApplicationInvite> IApplicationDbContext.TeacherApplicationInvites => None<TeacherApplicationInvite>();
    IQueryable<ExpertMembershipInvite> IApplicationDbContext.ExpertMembershipInvites => None<ExpertMembershipInvite>();
    IQueryable<ExpertMembershipVote> IApplicationDbContext.ExpertMembershipVotes => None<ExpertMembershipVote>();
    IQueryable<ExpertRemark> IApplicationDbContext.ExpertRemarks => None<ExpertRemark>();
    IQueryable<ExpertRemark> IApplicationDbContext.ExpertRemarksForAnyTenant => None<ExpertRemark>();
    IQueryable<Discipline> IApplicationDbContext.Disciplines => None<Discipline>();
    IQueryable<DisciplineServiceItem> IApplicationDbContext.DisciplineServiceItems => None<DisciplineServiceItem>();
    IQueryable<TeacherDisciplineAssignment> IApplicationDbContext.TeacherDisciplineAssignments => None<TeacherDisciplineAssignment>();
    IQueryable<ParentSupportRequest> IApplicationDbContext.ParentSupportRequests => None<ParentSupportRequest>();
    IQueryable<Meeting> IApplicationDbContext.Meetings => None<Meeting>();
    IQueryable<MeetingRecurrence> IApplicationDbContext.MeetingRecurrences => None<MeetingRecurrence>();
    IQueryable<MeetingGroup> IApplicationDbContext.MeetingGroups => None<MeetingGroup>();
    IQueryable<MeetingParticipant> IApplicationDbContext.MeetingParticipants => None<MeetingParticipant>();
    IQueryable<MeetingExternalGuest> IApplicationDbContext.MeetingExternalGuests => None<MeetingExternalGuest>();
    IQueryable<MeetingInvitation> IApplicationDbContext.MeetingInvitations => None<MeetingInvitation>();
    IQueryable<MeetingSession> IApplicationDbContext.MeetingSessions => None<MeetingSession>();
    IQueryable<MeetingAttendance> IApplicationDbContext.MeetingAttendances => None<MeetingAttendance>();
    IQueryable<MeetingMessage> IApplicationDbContext.MeetingMessages => None<MeetingMessage>();
    IQueryable<MeetingFile> IApplicationDbContext.MeetingFiles => None<MeetingFile>();
    IQueryable<MeetingRecording> IApplicationDbContext.MeetingRecordings => None<MeetingRecording>();
    IQueryable<MeetingTranscript> IApplicationDbContext.MeetingTranscripts => None<MeetingTranscript>();
    IQueryable<MeetingAIConsent> IApplicationDbContext.MeetingAiConsents => None<MeetingAIConsent>();
    IQueryable<MeetingAISummary> IApplicationDbContext.MeetingAiSummaries => None<MeetingAISummary>();
    IQueryable<MeetingDecision> IApplicationDbContext.MeetingDecisions => None<MeetingDecision>();
    IQueryable<MeetingActionItem> IApplicationDbContext.MeetingActionItems => None<MeetingActionItem>();
    IQueryable<MeetingNotification> IApplicationDbContext.MeetingNotifications => None<MeetingNotification>();
    IQueryable<MeetingAuditLog> IApplicationDbContext.MeetingAuditLogs => None<MeetingAuditLog>();
    IQueryable<TeacherContract> IApplicationDbContext.TeacherContracts => None<TeacherContract>();
    IQueryable<TeacherContractSectionDecision> IApplicationDbContext.TeacherContractSectionDecisions => None<TeacherContractSectionDecision>();
    IQueryable<TeacherContractAuditEvent> IApplicationDbContext.TeacherContractAuditEvents => None<TeacherContractAuditEvent>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public void Add<T>(T entity) where T : class
    {
        switch (entity)
        {
            case Lesson lesson: LessonsList.Add(lesson); break;
            case LessonAttendance attendance: AttendancesList.Add(attendance); break;
            case StudentSubscription sub: SubscriptionsList.Add(sub); break;
            case LessonCoverageAssignment coverage: CoverageList.Add(coverage); break;
            case Student student: StudentsList.Add(student); break;
            default: throw new NotSupportedException(typeof(T).Name);
        }
    }

    public void Remove<T>(T entity) where T : class
    {
        switch (entity)
        {
            case Lesson lesson: LessonsList.Remove(lesson); break;
            case LessonAttendance attendance: AttendancesList.Remove(attendance); break;
            case StudentSubscription sub: SubscriptionsList.Remove(sub); break;
            case Payment payment: PaymentsList.Remove(payment); break;
            default: throw new NotSupportedException(typeof(T).Name);
        }
    }

    public void RemoveRange<T>(IEnumerable<T> entities) where T : class
    {
        foreach (var entity in entities)
            Remove(entity);
    }

    private static IQueryable<T> None<T>() => Array.Empty<T>().AsQueryable();
}

internal sealed class StubUrls : IAppUrlProvider
{
    public string WebBaseUrl { get; init; } = "https://app.test";
    public string ApiPublicBaseUrl => "https://api.test";
    public string BuildEmailConfirmUrl(string userId, string token, string? returnPath = null) =>
        $"{WebBaseUrl}/confirm";
}
