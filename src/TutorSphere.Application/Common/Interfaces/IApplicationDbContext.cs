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
    IQueryable<Unavailability> Unavailabilities { get; }
    IQueryable<Holiday> Holidays { get; }
    IQueryable<Vacation> Vacations { get; }
    IQueryable<LessonReport> LessonReports { get; }
    IQueryable<Homework> Homeworks { get; }
    IQueryable<Invoice> Invoices { get; }
    IQueryable<Payment> Payments { get; }
    IQueryable<Document> Documents { get; }
    IQueryable<Message> Messages { get; }
    IQueryable<LessonAttendance> LessonAttendances { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
}
