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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
}
