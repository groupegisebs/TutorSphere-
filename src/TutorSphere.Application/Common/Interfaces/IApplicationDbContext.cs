using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<Tenant> Tenants { get; }
    IQueryable<TenantBranding> TenantBrandings { get; }
    IQueryable<Student> Students { get; }
    IQueryable<ParentProfile> ParentProfiles { get; }
    IQueryable<SubscriptionOffering> SubscriptionOfferings { get; }
    IQueryable<StudentSubscription> StudentSubscriptions { get; }
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
}
