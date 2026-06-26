using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Entities;
using TutorSphere.Infrastructure.Identity;
using TutorSphere.Infrastructure.MultiTenancy;

namespace TutorSphere.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options) => _tenantContext = tenantContext;

    public DbSet<Tenant> TenantsSet => Set<Tenant>();
    public DbSet<Student> StudentsSet => Set<Student>();
    public DbSet<ParentProfile> ParentProfilesSet => Set<ParentProfile>();
    public DbSet<SubscriptionOffering> SubscriptionOfferingsSet => Set<SubscriptionOffering>();
    public DbSet<StudentSubscription> StudentSubscriptionsSet => Set<StudentSubscription>();
    public DbSet<Lesson> LessonsSet => Set<Lesson>();
    public DbSet<Unavailability> UnavailabilitiesSet => Set<Unavailability>();
    public DbSet<Holiday> HolidaysSet => Set<Holiday>();
    public DbSet<Vacation> VacationsSet => Set<Vacation>();
    public DbSet<LessonReport> LessonReportsSet => Set<LessonReport>();
    public DbSet<Homework> HomeworksSet => Set<Homework>();
    public DbSet<Invoice> InvoicesSet => Set<Invoice>();
    public DbSet<Payment> PaymentsSet => Set<Payment>();
    public DbSet<Document> DocumentsSet => Set<Document>();
    public DbSet<Message> MessagesSet => Set<Message>();
    public DbSet<TenantBranding> TenantBrandingsSet => Set<TenantBranding>();
    public DbSet<LessonAttendance> LessonAttendancesSet => Set<LessonAttendance>();

    IQueryable<Tenant> IApplicationDbContext.Tenants => TenantsSet;
    IQueryable<TenantBranding> IApplicationDbContext.TenantBrandings => TenantBrandingsSet;
    IQueryable<Student> IApplicationDbContext.Students => StudentsSet;
    IQueryable<ParentProfile> IApplicationDbContext.ParentProfiles => ParentProfilesSet;
    IQueryable<SubscriptionOffering> IApplicationDbContext.SubscriptionOfferings => SubscriptionOfferingsSet;
    IQueryable<StudentSubscription> IApplicationDbContext.StudentSubscriptions => StudentSubscriptionsSet;
    IQueryable<Lesson> IApplicationDbContext.Lessons => LessonsSet;
    IQueryable<Unavailability> IApplicationDbContext.Unavailabilities => UnavailabilitiesSet;
    IQueryable<Holiday> IApplicationDbContext.Holidays => HolidaysSet;
    IQueryable<Vacation> IApplicationDbContext.Vacations => VacationsSet;
    IQueryable<LessonReport> IApplicationDbContext.LessonReports => LessonReportsSet;
    IQueryable<Homework> IApplicationDbContext.Homeworks => HomeworksSet;
    IQueryable<Invoice> IApplicationDbContext.Invoices => InvoicesSet;
    IQueryable<Payment> IApplicationDbContext.Payments => PaymentsSet;
    IQueryable<Document> IApplicationDbContext.Documents => DocumentsSet;
    IQueryable<Message> IApplicationDbContext.Messages => MessagesSet;
    IQueryable<LessonAttendance> IApplicationDbContext.LessonAttendances => LessonAttendancesSet;

    public new void Add<T>(T entity) where T : class => Set<T>().Add(entity);
    public new void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.HasIndex(t => t.Subdomain).IsUnique();
            e.Property(t => t.PlatformCommissionPercent).HasPrecision(5, 2);
            e.HasOne(t => t.Branding).WithOne(b => b.Tenant).HasForeignKey<TenantBranding>(b => b.TenantId);
        });

        builder.Entity<SubscriptionOffering>(e =>
        {
            e.Property(o => o.Price).HasPrecision(18, 2);
        });

        builder.Entity<Invoice>(e =>
        {
            e.Property(i => i.Amount).HasPrecision(18, 2);
            e.HasIndex(i => i.InvoiceNumber);
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.PlatformFee).HasPrecision(18, 2);
            e.Property(p => p.TutorAmount).HasPrecision(18, 2);
        });

        builder.Entity<Homework>(e =>
        {
            e.Property(h => h.Grade).HasPrecision(5, 2);
        });

        builder.Entity<Unavailability>(e => e.HasIndex(u => u.TenantId));
        builder.Entity<Holiday>(e => e.HasIndex(h => h.TenantId));
        builder.Entity<Vacation>(e => e.HasIndex(v => v.TenantId));

        builder.Entity<ParentProfile>(e =>
        {
            e.HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Student>(e =>
        {
            e.HasOne(s => s.Tenant).WithMany(t => t.Students).HasForeignKey(s => s.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Parent).WithMany(p => p.Children).HasForeignKey(s => s.ParentProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.ClrType == typeof(Tenant) ||
                entityType.ClrType == typeof(TenantBranding) ||
                entityType.ClrType == typeof(ParentProfile) ||
                entityType.ClrType == typeof(Student))
                continue;

            var tenantFk = entityType.GetForeignKeys()
                .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Tenant));
            tenantFk?.DeleteBehavior = DeleteBehavior.Restrict;
        }

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
        });

        ApplyTenantFilters(builder);
    }

    private void ApplyTenantFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(ApplicationDbContext)
                .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [builder]);
        }
    }

    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantEntity
    {
        builder.Entity<T>().HasQueryFilter(e =>
            !_tenantContext.HasTenant || e.TenantId == _tenantContext.TenantId);
    }
}
