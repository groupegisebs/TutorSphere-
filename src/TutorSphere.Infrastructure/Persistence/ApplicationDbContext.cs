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
    public DbSet<TutorPayout> TutorPayoutsSet => Set<TutorPayout>();
    public DbSet<TutorPayoutAccount> TutorPayoutAccountsSet => Set<TutorPayoutAccount>();
    public DbSet<PlatformLicensePayment> PlatformLicensePaymentsSet => Set<PlatformLicensePayment>();
    public DbSet<PlatformPromoCode> PlatformPromoCodesSet => Set<PlatformPromoCode>();
    public DbSet<ExpertGroup> ExpertGroupsSet => Set<ExpertGroup>();
    public DbSet<ExpertGroupMember> ExpertGroupMembersSet => Set<ExpertGroupMember>();
    public DbSet<ExpertGroupManagerMandate> ExpertGroupManagerMandatesSet => Set<ExpertGroupManagerMandate>();
    public DbSet<GroupOffer> GroupOffersSet => Set<GroupOffer>();
    public DbSet<GroupOfferTeacher> GroupOfferTeachersSet => Set<GroupOfferTeacher>();
    public DbSet<GroupAdminConversation> GroupAdminConversationsSet => Set<GroupAdminConversation>();
    public DbSet<GroupAdminMessage> GroupAdminMessagesSet => Set<GroupAdminMessage>();
    public DbSet<TeacherInterestRequest> TeacherInterestRequestsSet => Set<TeacherInterestRequest>();
    public DbSet<ExpertDelegatedTask> ExpertDelegatedTasksSet => Set<ExpertDelegatedTask>();
    public DbSet<ExpertWorkspaceItem> ExpertWorkspaceItemsSet => Set<ExpertWorkspaceItem>();
    public DbSet<ExpertGovernanceEvent> ExpertGovernanceEventsSet => Set<ExpertGovernanceEvent>();
    public DbSet<TeacherDocument> TeacherDocumentsSet => Set<TeacherDocument>();
    public DbSet<TeacherApplicationInvite> TeacherApplicationInvitesSet => Set<TeacherApplicationInvite>();
    public DbSet<ExpertMembershipInvite> ExpertMembershipInvitesSet => Set<ExpertMembershipInvite>();
    public DbSet<ExpertMembershipVote> ExpertMembershipVotesSet => Set<ExpertMembershipVote>();
    public DbSet<ExpertRemark> ExpertRemarksSet => Set<ExpertRemark>();
    public DbSet<Discipline> DisciplinesSet => Set<Discipline>();
    public DbSet<DisciplineServiceItem> DisciplineServiceItemsSet => Set<DisciplineServiceItem>();
    public DbSet<TeacherDisciplineAssignment> TeacherDisciplineAssignmentsSet => Set<TeacherDisciplineAssignment>();

    IQueryable<Tenant> IApplicationDbContext.Tenants => TenantsSet;
    IQueryable<TenantBranding> IApplicationDbContext.TenantBrandings => TenantBrandingsSet;
    IQueryable<Student> IApplicationDbContext.Students => StudentsSet;
    IQueryable<Student> IApplicationDbContext.StudentsForAnyTenant => StudentsSet.IgnoreQueryFilters();
    IQueryable<ParentProfile> IApplicationDbContext.ParentProfiles => ParentProfilesSet;
    IQueryable<ParentProfile> IApplicationDbContext.ParentProfilesForAnyTenant => ParentProfilesSet.IgnoreQueryFilters();
    IQueryable<SubscriptionOffering> IApplicationDbContext.SubscriptionOfferings => SubscriptionOfferingsSet;
    IQueryable<SubscriptionOffering> IApplicationDbContext.SubscriptionOfferingsForAnyTenant =>
        SubscriptionOfferingsSet.IgnoreQueryFilters();
    IQueryable<StudentSubscription> IApplicationDbContext.StudentSubscriptions => StudentSubscriptionsSet;
    IQueryable<StudentSubscription> IApplicationDbContext.StudentSubscriptionsForAnyTenant =>
        StudentSubscriptionsSet.IgnoreQueryFilters();
    IQueryable<Lesson> IApplicationDbContext.Lessons => LessonsSet;
    IQueryable<Lesson> IApplicationDbContext.LessonsForAnyTenant => LessonsSet.IgnoreQueryFilters();
    IQueryable<Unavailability> IApplicationDbContext.Unavailabilities => UnavailabilitiesSet;
    IQueryable<Holiday> IApplicationDbContext.Holidays => HolidaysSet;
    IQueryable<Vacation> IApplicationDbContext.Vacations => VacationsSet;
    IQueryable<LessonReport> IApplicationDbContext.LessonReports => LessonReportsSet;
    IQueryable<LessonReport> IApplicationDbContext.LessonReportsForAnyTenant =>
        LessonReportsSet.IgnoreQueryFilters();
    IQueryable<Homework> IApplicationDbContext.Homeworks => HomeworksSet;
    IQueryable<Homework> IApplicationDbContext.HomeworksForAnyTenant =>
        HomeworksSet.IgnoreQueryFilters();
    IQueryable<Invoice> IApplicationDbContext.Invoices => InvoicesSet;
    IQueryable<Invoice> IApplicationDbContext.InvoicesForAnyTenant => InvoicesSet.IgnoreQueryFilters();
    IQueryable<Payment> IApplicationDbContext.Payments => PaymentsSet;
    IQueryable<Payment> IApplicationDbContext.PaymentsForAnyTenant => PaymentsSet.IgnoreQueryFilters();
    IQueryable<Document> IApplicationDbContext.Documents => DocumentsSet;
    IQueryable<Document> IApplicationDbContext.DocumentsForAnyTenant =>
        DocumentsSet.IgnoreQueryFilters();
    IQueryable<Message> IApplicationDbContext.Messages => MessagesSet;
    IQueryable<LessonAttendance> IApplicationDbContext.LessonAttendances => LessonAttendancesSet;
    IQueryable<LessonAttendance> IApplicationDbContext.LessonAttendancesForAnyTenant =>
        LessonAttendancesSet.IgnoreQueryFilters();
    IQueryable<TutorPayout> IApplicationDbContext.TutorPayouts => TutorPayoutsSet;
    IQueryable<TutorPayout> IApplicationDbContext.TutorPayoutsForAnyTenant => TutorPayoutsSet.IgnoreQueryFilters();
    IQueryable<TutorPayoutAccount> IApplicationDbContext.TutorPayoutAccounts => TutorPayoutAccountsSet;
    IQueryable<PlatformLicensePayment> IApplicationDbContext.PlatformLicensePayments => PlatformLicensePaymentsSet;
    IQueryable<PlatformLicensePayment> IApplicationDbContext.PlatformLicensePaymentsForAnyTenant =>
        PlatformLicensePaymentsSet.IgnoreQueryFilters();
    IQueryable<PlatformPromoCode> IApplicationDbContext.PlatformPromoCodes => PlatformPromoCodesSet;
    IQueryable<ExpertGroup> IApplicationDbContext.ExpertGroups => ExpertGroupsSet;
    IQueryable<ExpertGroupMember> IApplicationDbContext.ExpertGroupMembers => ExpertGroupMembersSet;
    IQueryable<ExpertGroupManagerMandate> IApplicationDbContext.ExpertGroupManagerMandates =>
        ExpertGroupManagerMandatesSet;
    IQueryable<GroupOffer> IApplicationDbContext.GroupOffers => GroupOffersSet;
    IQueryable<GroupOfferTeacher> IApplicationDbContext.GroupOfferTeachers => GroupOfferTeachersSet;
    IQueryable<GroupAdminConversation> IApplicationDbContext.GroupAdminConversations =>
        GroupAdminConversationsSet;
    IQueryable<GroupAdminMessage> IApplicationDbContext.GroupAdminMessages => GroupAdminMessagesSet;
    IQueryable<TeacherInterestRequest> IApplicationDbContext.TeacherInterestRequests =>
        TeacherInterestRequestsSet;
    IQueryable<ExpertDelegatedTask> IApplicationDbContext.ExpertDelegatedTasks => ExpertDelegatedTasksSet;
    IQueryable<ExpertWorkspaceItem> IApplicationDbContext.ExpertWorkspaceItems => ExpertWorkspaceItemsSet;
    IQueryable<ExpertGovernanceEvent> IApplicationDbContext.ExpertGovernanceEvents => ExpertGovernanceEventsSet;
    IQueryable<TeacherDocument> IApplicationDbContext.TeacherDocuments => TeacherDocumentsSet;
    IQueryable<TeacherDocument> IApplicationDbContext.TeacherDocumentsForAnyTenant =>
        TeacherDocumentsSet.IgnoreQueryFilters();
    IQueryable<TeacherApplicationInvite> IApplicationDbContext.TeacherApplicationInvites =>
        TeacherApplicationInvitesSet;
    IQueryable<ExpertMembershipInvite> IApplicationDbContext.ExpertMembershipInvites =>
        ExpertMembershipInvitesSet;
    IQueryable<ExpertMembershipVote> IApplicationDbContext.ExpertMembershipVotes =>
        ExpertMembershipVotesSet;
    IQueryable<ExpertRemark> IApplicationDbContext.ExpertRemarks => ExpertRemarksSet;
    IQueryable<ExpertRemark> IApplicationDbContext.ExpertRemarksForAnyTenant =>
        ExpertRemarksSet.IgnoreQueryFilters();
    IQueryable<Discipline> IApplicationDbContext.Disciplines => DisciplinesSet;
    IQueryable<DisciplineServiceItem> IApplicationDbContext.DisciplineServiceItems => DisciplineServiceItemsSet;
    IQueryable<TeacherDisciplineAssignment> IApplicationDbContext.TeacherDisciplineAssignments =>
        TeacherDisciplineAssignmentsSet;

    public new void Add<T>(T entity) where T : class => Set<T>().Add(entity);
    public new void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);
    public void RemoveRange<T>(IEnumerable<T> entities) where T : class => Set<T>().RemoveRange(entities);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.HasIndex(t => t.Subdomain).IsUnique();
            e.Property(t => t.PlatformCommissionPercent).HasPrecision(5, 2);
            e.HasOne(t => t.Branding).WithOne(b => b.Tenant).HasForeignKey<TenantBranding>(b => b.TenantId);
            e.HasOne(t => t.ApprovedByExpertGroup)
                .WithMany()
                .HasForeignKey(t => t.ApprovedByExpertGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            e.Property(t => t.ApprovedByUserId).HasMaxLength(450);
            e.Property(t => t.ExpertApprovalNotes).HasMaxLength(2000);
            e.Property(t => t.ReviewAssignedToUserId).HasMaxLength(450);
            e.Property(t => t.ReviewRequestNotes).HasMaxLength(2000);
            e.HasIndex(t => t.ExpertApprovalStatus);
            e.HasIndex(t => t.ReviewAssignedToUserId);
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
            e.Property(p => p.Channel).HasMaxLength(20);
            e.Property(p => p.PhoneMasked).HasMaxLength(40);
        });

        builder.Entity<TutorPayout>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.IdempotencyKey).HasMaxLength(80);
            e.Property(p => p.ExternalDisbursementId).HasMaxLength(80);
            e.Property(p => p.ProviderPayoutId).HasMaxLength(120);
            e.HasIndex(p => p.TenantId);
            e.HasIndex(p => p.RequestedAt);
            e.HasIndex(p => p.IdempotencyKey);
            e.HasOne(p => p.PayoutAccount).WithMany().HasForeignKey(p => p.PayoutAccountId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TutorPayoutAccount>(e =>
        {
            e.HasIndex(a => a.TenantId);
            e.HasIndex(a => new { a.TenantId, a.IsPrimary });
            e.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlatformLicensePayment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.Currency).HasMaxLength(8);
            e.Property(p => p.GatewayPaymentCode).HasMaxLength(120);
            e.HasIndex(p => p.TenantId);
            e.HasIndex(p => p.GatewayPaymentCode);
            e.HasOne(p => p.Tenant).WithMany(t => t.LicensePayments).HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlatformPromoCode>(e =>
        {
            e.Property(p => p.Code).HasMaxLength(32).IsRequired();
            e.Property(p => p.Notes).HasMaxLength(500);
            e.Property(p => p.RedeemedByUserId).HasMaxLength(450);
            e.HasIndex(p => p.Code).IsUnique();
            e.HasIndex(p => p.RedeemedAt);
            e.HasIndex(p => p.IsActive);
        });

        builder.Entity<ExpertGroup>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(200).IsRequired();
            e.Property(g => g.LogoUrl).HasMaxLength(500);
            e.Property(g => g.Description).HasMaxLength(4000);
            e.Property(g => g.ContactName).HasMaxLength(200);
            e.Property(g => g.ContactEmail).HasMaxLength(256);
            e.Property(g => g.ContactPhone).HasMaxLength(50);
            e.Property(g => g.CountryCode).HasMaxLength(8);
            e.Property(g => g.ManagerAssignedByAdminId).HasMaxLength(450);
            e.HasIndex(g => g.IsInternational);
            e.HasIndex(g => g.CountryCode);
            e.HasIndex(g => g.ActiveManagerMandateId);
            e.HasIndex(g => g.GroupManagerMembershipId);
            // Unicité logique : un international + un par pays (appliquée aussi en service).
            e.HasIndex(g => g.CountryCode)
                .IsUnique()
                .HasFilter("\"IsInternational\" = FALSE AND \"CountryCode\" IS NOT NULL");
            e.HasIndex(g => g.IsInternational)
                .IsUnique()
                .HasFilter("\"IsInternational\" = TRUE");
        });

        builder.Entity<ExpertGroupMember>(e =>
        {
            e.Property(m => m.UserId).HasMaxLength(450).IsRequired();
            e.Property(m => m.Specialty).HasMaxLength(200);
            e.Property(m => m.ApprovedByAdminId).HasMaxLength(450);
            e.HasIndex(m => new { m.ExpertGroupId, m.UserId }).IsUnique();
            e.HasIndex(m => new { m.ExpertGroupId, m.MemberRole });
            e.HasOne(m => m.ExpertGroup).WithMany(g => g.Members).HasForeignKey(m => m.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExpertGroupManagerMandate>(e =>
        {
            e.Property(m => m.UserId).HasMaxLength(450).IsRequired();
            e.Property(m => m.FunctionTitle).HasMaxLength(200);
            e.Property(m => m.Phone).HasMaxLength(50);
            e.Property(m => m.AppointedByAdminId).HasMaxLength(450).IsRequired();
            e.Property(m => m.EndedByAdminId).HasMaxLength(450);
            e.Property(m => m.EndReason).HasMaxLength(2000);
            e.HasIndex(m => m.ExpertGroupId);
            e.HasIndex(m => new { m.ExpertGroupId, m.Status });
            e.HasOne(m => m.ExpertGroup).WithMany(g => g.ManagerMandates).HasForeignKey(m => m.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Membership).WithMany().HasForeignKey(m => m.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GroupOffer>(e =>
        {
            e.Property(o => o.Name).HasMaxLength(200).IsRequired();
            e.Property(o => o.Code).HasMaxLength(64);
            e.Property(o => o.ShortDescription).HasMaxLength(500);
            e.Property(o => o.FullDescription).HasMaxLength(8000);
            e.Property(o => o.ImageUrl).HasMaxLength(500);
            e.Property(o => o.SchoolCycle).HasMaxLength(120);
            e.Property(o => o.LevelsCsv).HasMaxLength(500);
            e.Property(o => o.LanguagesCsv).HasMaxLength(200);
            e.Property(o => o.VisibleCountryCodes).HasMaxLength(500);
            e.Property(o => o.Currency).HasMaxLength(8).IsRequired();
            e.Property(o => o.CreatedByUserId).HasMaxLength(450).IsRequired();
            e.Property(o => o.ApprovedByManagerUserId).HasMaxLength(450);
            e.HasIndex(o => o.ExpertGroupId);
            e.HasIndex(o => new { o.ExpertGroupId, o.Status });
            e.HasOne(o => o.ExpertGroup).WithMany(g => g.Offers).HasForeignKey(o => o.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.Discipline).WithMany().HasForeignKey(o => o.DisciplineId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<GroupOfferTeacher>(e =>
        {
            e.Property(t => t.ApprovedByUserId).HasMaxLength(450);
            e.HasIndex(t => new { t.GroupOfferId, t.TeacherTenantId }).IsUnique();
            e.HasOne(t => t.GroupOffer).WithMany(o => o.Teachers).HasForeignKey(t => t.GroupOfferId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.TeacherTenant).WithMany().HasForeignKey(t => t.TeacherTenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GroupAdminConversation>(e =>
        {
            e.Property(c => c.Reference).HasMaxLength(64).IsRequired();
            e.Property(c => c.Subject).HasMaxLength(300).IsRequired();
            e.Property(c => c.CreatedByManagerUserId).HasMaxLength(450).IsRequired();
            e.Property(c => c.AssignedAdminUserId).HasMaxLength(450);
            e.HasIndex(c => c.Reference).IsUnique();
            e.HasIndex(c => c.ExpertGroupId);
            e.HasIndex(c => c.Status);
            e.HasOne(c => c.ExpertGroup).WithMany().HasForeignKey(c => c.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GroupAdminMessage>(e =>
        {
            e.Property(m => m.SenderUserId).HasMaxLength(450).IsRequired();
            e.Property(m => m.Body).HasMaxLength(8000).IsRequired();
            e.Property(m => m.AttachmentReference).HasMaxLength(500);
            e.Property(m => m.PreviousBody).HasMaxLength(8000);
            e.HasIndex(m => m.ConversationId);
            e.HasOne(m => m.Conversation).WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeacherInterestRequest>(e =>
        {
            e.Property(r => r.FullName).HasMaxLength(200).IsRequired();
            e.Property(r => r.Email).HasMaxLength(256).IsRequired();
            e.Property(r => r.CountryCode).HasMaxLength(8);
            e.Property(r => r.City).HasMaxLength(120);
            e.Property(r => r.Disciplines).HasMaxLength(500);
            e.Property(r => r.Experience).HasMaxLength(2000);
            e.Property(r => r.Message).HasMaxLength(4000);
            e.Property(r => r.HandledByUserId).HasMaxLength(450);
            e.HasIndex(r => r.Email);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.RoutedExpertGroupId);
            e.HasOne(r => r.RoutedExpertGroup).WithMany().HasForeignKey(r => r.RoutedExpertGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ExpertDelegatedTask>(e =>
        {
            e.Property(t => t.CreatedByManagerUserId).HasMaxLength(450).IsRequired();
            e.Property(t => t.AssigneeExpertUserId).HasMaxLength(450).IsRequired();
            e.Property(t => t.Notes).HasMaxLength(2000);
            e.Property(t => t.CompletionNotes).HasMaxLength(2000);
            e.HasIndex(t => t.ExpertGroupId);
            e.HasIndex(t => t.AssigneeExpertUserId);
            e.HasIndex(t => t.TeacherTenantId);
            e.HasIndex(t => t.Status);
            e.HasOne(t => t.ExpertGroup).WithMany().HasForeignKey(t => t.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.TeacherTenant).WithMany().HasForeignKey(t => t.TeacherTenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExpertWorkspaceItem>(e =>
        {
            e.Property(i => i.Title).HasMaxLength(200).IsRequired();
            e.Property(i => i.Description).HasMaxLength(4000);
            e.Property(i => i.CreatedByUserId).HasMaxLength(450).IsRequired();
            e.Property(i => i.AssignedToUserId).HasMaxLength(450);
            e.Property(i => i.OutcomeNotes).HasMaxLength(2000);
            e.HasIndex(i => new { i.ExpertGroupId, i.ItemType, i.Status });
            e.HasOne(i => i.ExpertGroup).WithMany().HasForeignKey(i => i.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.RelatedTeacherTenant).WithMany().HasForeignKey(i => i.RelatedTeacherTenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ExpertGovernanceEvent>(e =>
        {
            e.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            e.Property(x => x.PayloadJson).HasMaxLength(4000);
            e.HasIndex(x => x.ExpertGroupId);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.ExpertGroup).WithMany().HasForeignKey(x => x.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExpertMembershipInvite>(e =>
        {
            e.Property(i => i.InvitedByUserId).HasMaxLength(450).IsRequired();
            e.Property(i => i.Email).HasMaxLength(256).IsRequired();
            e.Property(i => i.FirstName).HasMaxLength(120).IsRequired();
            e.Property(i => i.LastName).HasMaxLength(120).IsRequired();
            e.Property(i => i.Phone).HasMaxLength(40);
            e.Property(i => i.Specialty).HasMaxLength(200);
            e.Property(i => i.IntendedRole).HasMaxLength(200);
            e.Property(i => i.Presentation).HasMaxLength(4000);
            e.Property(i => i.Justification).HasMaxLength(2000);
            e.Property(i => i.PersonalMessage).HasMaxLength(2000);
            e.Property(i => i.Token).HasMaxLength(64).IsRequired();
            e.Property(i => i.CandidateUserId).HasMaxLength(450);
            e.Property(i => i.EligibleVoterUserIdsCsv).HasMaxLength(8000);
            e.Property(i => i.AdminClosedByUserId).HasMaxLength(450);
            e.Property(i => i.AdminNotes).HasMaxLength(2000);
            e.HasIndex(i => i.Token).IsUnique();
            e.HasIndex(i => i.ExpertGroupId);
            e.HasIndex(i => i.Email);
            e.HasIndex(i => i.Status);
            e.HasOne(i => i.ExpertGroup).WithMany().HasForeignKey(i => i.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExpertMembershipVote>(e =>
        {
            e.Property(v => v.VoterUserId).HasMaxLength(450).IsRequired();
            e.Property(v => v.Comment).HasMaxLength(2000);
            e.HasIndex(v => new { v.InviteId, v.VoterUserId }).IsUnique();
            e.HasOne(v => v.Invite).WithMany(i => i.Votes).HasForeignKey(v => v.InviteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeacherDocument>(e =>
        {
            e.Property(d => d.FileName).HasMaxLength(260).IsRequired();
            e.Property(d => d.FileUrl).HasMaxLength(500).IsRequired();
            e.Property(d => d.ContentType).HasMaxLength(120);
            e.Property(d => d.UploadedByUserId).HasMaxLength(450).IsRequired();
            e.Property(d => d.Notes).HasMaxLength(500);
            e.HasIndex(d => d.TenantId);
            e.HasOne(d => d.Tenant).WithMany(t => t.TeacherDocuments).HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeacherApplicationInvite>(e =>
        {
            e.Property(i => i.Email).HasMaxLength(256).IsRequired();
            e.Property(i => i.FirstName).HasMaxLength(120);
            e.Property(i => i.PersonalMessage).HasMaxLength(2000);
            e.Property(i => i.InvitedByUserId).HasMaxLength(450).IsRequired();
            e.Property(i => i.Token).HasMaxLength(64).IsRequired();
            e.HasIndex(i => i.Token).IsUnique();
            e.HasIndex(i => i.ExpertGroupId);
            e.HasIndex(i => i.Email);
            e.HasIndex(i => i.SentAt);
            e.HasOne(i => i.ExpertGroup).WithMany().HasForeignKey(i => i.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExpertRemark>(e =>
        {
            e.Property(r => r.AuthorUserId).HasMaxLength(450).IsRequired();
            e.Property(r => r.Message).HasMaxLength(2000).IsRequired();
            e.HasIndex(r => r.TenantId);
            e.HasIndex(r => r.CreatedAt);
            e.HasOne(r => r.Tenant).WithMany().HasForeignKey(r => r.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ExpertGroup>().WithMany().HasForeignKey(r => r.ExpertGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Homework>().WithMany().HasForeignKey(r => r.RelatedHomeworkId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Document>().WithMany().HasForeignKey(r => r.RelatedDocumentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Discipline>(e =>
        {
            e.Property(d => d.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(d => d.ExpertGroupId);
            e.HasIndex(d => new { d.ExpertGroupId, d.Name }).IsUnique();
            e.HasOne(d => d.ExpertGroup).WithMany().HasForeignKey(d => d.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DisciplineServiceItem>(e =>
        {
            e.Property(s => s.Title).HasMaxLength(200).IsRequired();
            e.Property(s => s.Description).HasMaxLength(2000);
            e.HasIndex(s => s.DisciplineId);
            e.HasOne(s => s.Discipline).WithMany(d => d.Services).HasForeignKey(s => s.DisciplineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeacherDisciplineAssignment>(e =>
        {
            e.Property(a => a.AssignedByUserId).HasMaxLength(450).IsRequired();
            e.HasIndex(a => new { a.DisciplineId, a.TenantId }).IsUnique();
            e.HasIndex(a => a.TenantId);
            e.HasOne(a => a.Discipline).WithMany(d => d.Assignments).HasForeignKey(a => a.DisciplineId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Homework>(e =>
        {
            e.Property(h => h.Grade).HasPrecision(5, 2);
        });

        builder.Entity<Unavailability>(e => e.HasIndex(u => u.TenantId));
        builder.Entity<Holiday>(e => e.HasIndex(h => h.TenantId));
        builder.Entity<Vacation>(e => e.HasIndex(v => v.TenantId));

        builder.Entity<Tenant>(e =>
        {
            e.Property(t => t.VisibleCountryCodes).HasMaxLength(500);
        });

        builder.Entity<ParentProfile>(e =>
        {
            e.Property(p => p.Country).HasMaxLength(2);
            e.HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Student>(e =>
        {
            e.Property(s => s.Country).HasMaxLength(2);
            e.HasOne(s => s.Tenant).WithMany(t => t.Students).HasForeignKey(s => s.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Parent).WithMany(p => p.Children).HasForeignKey(s => s.ParentProfileId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
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
            e.Property(u => u.CalendarFeedToken).HasMaxLength(128);
            e.HasIndex(u => u.CalendarFeedToken);
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
