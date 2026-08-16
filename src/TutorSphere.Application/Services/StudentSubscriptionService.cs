using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.StudentSubscriptions;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IStudentSubscriptionService
{
    Task<StudentSubscriptionDto> EnrollAsync(string parentUserId, EnrollStudentRequest request, CancellationToken ct = default);
    /// <summary>Élève autonome (14+) s'inscrit lui-même à une offre.</summary>
    Task<StudentSubscriptionDto> EnrollSelfAsync(string studentUserId, EnrollSelfRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<StudentSubscriptionDto>> GetForParentUserAsync(string parentUserId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentSubscriptionDto>> GetForStudentUserAsync(string studentUserId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentSubscriptionDto>> GetForCurrentTenantAsync(CancellationToken ct = default);
    Task CancelAsync(string parentUserId, Guid subscriptionId, CancellationToken ct = default);
    Task CancelSelfAsync(string studentUserId, Guid subscriptionId, CancellationToken ct = default);
    Task<StudentSubscriptionDto> AcceptAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<StudentSubscriptionDto> RejectAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<StudentSubscriptionDto> PauseAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<StudentSubscriptionDto> ResumeAsync(Guid subscriptionId, CancellationToken ct = default);

    Task<IReadOnlyList<ExpertPendingEnrollmentDto>> ListPendingForExpertGroupAsync(
        string expertUserId, Guid? groupId, CancellationToken ct = default);
    Task<ExpertPendingEnrollmentDto> AcceptForExpertGroupAsync(
        string expertUserId, Guid subscriptionId, Guid? groupId, CancellationToken ct = default);
    Task<ExpertPendingEnrollmentDto> RejectForExpertGroupAsync(
        string expertUserId, Guid subscriptionId, Guid? groupId, CancellationToken ct = default);
}

public class StudentSubscriptionService : IStudentSubscriptionService
{
    private readonly Common.Interfaces.IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISubscriptionLessonScheduler _lessonScheduler;
    private readonly IBillingEmailOrchestrator _billingEmail;
    private readonly IPaymentGatewayService _payments;
    private readonly IExpertGroupManagerService _managers;
    private readonly IExpertMonitoringService _monitoring;

    public StudentSubscriptionService(
        Common.Interfaces.IApplicationDbContext db,
        ITenantContext tenantContext,
        ISubscriptionLessonScheduler lessonScheduler,
        IBillingEmailOrchestrator billingEmail,
        IPaymentGatewayService payments,
        IExpertGroupManagerService managers,
        IExpertMonitoringService monitoring)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lessonScheduler = lessonScheduler;
        _billingEmail = billingEmail;
        _payments = payments;
        _managers = managers;
        _monitoring = monitoring;
    }

    public async Task<StudentSubscriptionDto> EnrollAsync(
        string parentUserId,
        EnrollStudentRequest request,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s =>
                s.Id == request.StudentId && s.ParentProfileId == parent.Id)
            ?? throw new InvalidOperationException("Enfant introuvable.");

        return await CreateEnrollmentAsync(
            student,
            request.OfferingId,
            $"{parent.FirstName} {parent.LastName}".Trim(),
            ct);
    }

    public async Task<StudentSubscriptionDto> EnrollSelfAsync(
        string studentUserId,
        EnrollSelfRequest request,
        CancellationToken ct = default)
    {
        var student = _db.StudentsForAnyTenant.FirstOrDefault(s =>
                s.UserId == studentUserId && s.IsActive)
            ?? throw new InvalidOperationException(
                "Profil élève introuvable. Complétez votre date de naissance dans les paramètres.");

        if (!student.DateOfBirth.HasValue)
            throw new InvalidOperationException(
                "Indiquez votre date de naissance dans les paramètres pour vous abonner.");

        if (!student.IsAutonomous)
            throw new InvalidOperationException(
                "Seuls les élèves de 14 ans et plus peuvent s'abonner seuls. Demandez à un parent.");

        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o =>
                o.Id == request.OfferingId && o.IsActive)
            ?? throw new InvalidOperationException("Offre introuvable ou inactive.");

        await EnsureSelfBillingParentAsync(student, offering.TenantId, ct);

        return await CreateEnrollmentAsync(
            student,
            request.OfferingId,
            $"{student.FirstName} {student.LastName}".Trim(),
            ct);
    }

    private async Task<StudentSubscriptionDto> CreateEnrollmentAsync(
        Domain.Entities.Student student,
        Guid offeringId,
        string payerDisplayName,
        CancellationToken ct)
    {
        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o =>
                o.Id == offeringId && o.IsActive)
            ?? throw new InvalidOperationException("Offre introuvable ou inactive.");

        var duplicate = _db.StudentSubscriptionsForAnyTenant.Any(s =>
            s.StudentId == student.Id
            && s.OfferingId == offering.Id
            && (s.Status == SubscriptionStatus.Pending
                || s.Status == SubscriptionStatus.AwaitingPayment
                || s.Status == SubscriptionStatus.Active));
        if (duplicate)
            throw new InvalidOperationException("Vous êtes déjà abonné (ou en cours d'abonnement) à cette offre.");

        var activeCount = _db.StudentSubscriptionsForAnyTenant.Count(s =>
            s.OfferingId == offering.Id
            && (s.Status == SubscriptionStatus.Pending
                || s.Status == SubscriptionStatus.AwaitingPayment
                || s.Status == SubscriptionStatus.Active));
        if (activeCount >= offering.MaxCapacity)
            throw new InvalidOperationException(
                $"Cette offre est complète ({offering.MaxCapacity} place(s) maximum).");

        var now = DateTime.UtcNow;
        var endDate = now.AddDays(Math.Max(1, offering.DurationDays));
        StudentScheduleConflictChecker.EnsureNoOfferingConflict(
            _db,
            student.Id,
            offering.Id,
            offering.Conditions,
            now,
            endDate);

        if (student.TenantId != offering.TenantId)
        {
            student.TenantId = offering.TenantId;
            student.UpdatedAt = DateTime.UtcNow;
        }

        var subscription = new Domain.Entities.StudentSubscription
        {
            TenantId = offering.TenantId,
            StudentId = student.Id,
            OfferingId = offering.Id,
            Status = SubscriptionStatus.Pending,
            StartDate = now,
            EndDate = endDate,
            SessionsRemaining = PackPaymentProcess.EnrollmentSessionsRemaining
        };

        _db.Add(subscription);
        await _db.SaveChangesAsync(ct);

        await _billingEmail.NotifyEnrollmentRequestedAsync(subscription.Id, ct);

        return Map(subscription, offering.Title, offering.Subject, offering.Price, offering.Currency,
            $"{student.FirstName} {student.LastName}".Trim(),
            payerDisplayName);
    }

    /// <summary>
    /// Profil « parent » de facturation pour l'élève autonome (même UserId) —
    /// réutilise le flux PayGateway existant.
    /// </summary>
    private async Task EnsureSelfBillingParentAsync(
        Domain.Entities.Student student,
        Guid preferredTenantId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(student.UserId))
            throw new InvalidOperationException("Compte élève incomplet.");

        if (student.ParentProfileId is Guid existingId)
        {
            if (_db.ParentProfilesForAnyTenant.Any(p => p.Id == existingId))
                return;
        }

        var selfParent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == student.UserId);
        if (selfParent is null)
        {
            var tenantId = preferredTenantId != Guid.Empty
                ? preferredTenantId
                : student.TenantId;
            if (tenantId == Guid.Empty)
                tenantId = _db.Tenants.Select(t => t.Id).FirstOrDefault();
            if (tenantId == Guid.Empty)
                throw new InvalidOperationException("Impossible de créer le profil de facturation.");

            selfParent = new Domain.Entities.ParentProfile
            {
                TenantId = tenantId,
                UserId = student.UserId,
                FirstName = student.FirstName,
                LastName = student.LastName,
                Email = student.Email ?? string.Empty
            };
            _db.Add(selfParent);
            await _db.SaveChangesAsync(ct);
        }

        student.ParentProfileId = selfParent.Id;
        student.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<StudentSubscriptionDto>> GetForStudentUserAsync(
        string studentUserId,
        CancellationToken ct = default)
    {
        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == studentUserId && s.IsActive);
        if (student is null)
            return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>([]);

        var studentName = $"{student.FirstName} {student.LastName}".Trim();
        var subs = _db.StudentSubscriptionsForAnyTenant
            .Where(s => s.StudentId == student.Id)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        var offeringIds = subs.Select(s => s.OfferingId).Distinct().ToList();
        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => offeringIds.Contains(o.Id))
            .ToDictionary(o => o.Id);

        var result = subs.Select(s =>
        {
            offerings.TryGetValue(s.OfferingId, out var offering);
            return Map(
                s,
                offering?.Title ?? "Offre",
                offering?.Subject,
                offering?.Price ?? 0,
                offering?.Currency ?? "CAD",
                studentName,
                studentName);
        }).ToList();

        return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>(result);
    }

    public Task<IReadOnlyList<StudentSubscriptionDto>> GetForParentUserAsync(
        string parentUserId,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId);
        if (parent is null)
            return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>([]);

        var childIds = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .Select(s => s.Id)
            .ToList();

        var parentName = $"{parent.FirstName} {parent.LastName}".Trim();
        var subs = _db.StudentSubscriptionsForAnyTenant
            .Where(s => childIds.Contains(s.StudentId))
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        var offeringIds = subs.Select(s => s.OfferingId).Distinct().ToList();
        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => offeringIds.Contains(o.Id))
            .ToDictionary(o => o.Id);

        var students = _db.StudentsForAnyTenant
            .Where(s => childIds.Contains(s.Id))
            .ToDictionary(s => s.Id);

        var result = subs.Select(s =>
        {
            offerings.TryGetValue(s.OfferingId, out var offering);
            students.TryGetValue(s.StudentId, out var student);
            return Map(
                s,
                offering?.Title ?? "Offre",
                offering?.Subject,
                offering?.Price ?? 0,
                offering?.Currency ?? "CAD",
                student is null ? "" : $"{student.FirstName} {student.LastName}".Trim(),
                parentName);
        }).ToList();

        return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>(result);
    }

    public Task<IReadOnlyList<StudentSubscriptionDto>> GetForCurrentTenantAsync(CancellationToken ct = default)
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is not Guid tenantId)
            return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>([]);

        var assignedOfferingIds = AssignedOfferingIds(tenantId);
        var subs = _db.StudentSubscriptionsForAnyTenant
            .Where(s => s.TenantId == tenantId || assignedOfferingIds.Contains(s.OfferingId))
            .ToList()
            .OrderBy(s => EnrollmentRank(s.Status))
            .ThenByDescending(s => s.CreatedAt)
            .ToList();

        return Task.FromResult(MapMany(subs));
    }

    public async Task CancelAsync(string parentUserId, Guid subscriptionId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var childIds = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .Select(s => s.Id)
            .ToHashSet();

        var sub = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s => s.Id == subscriptionId)
            ?? throw new InvalidOperationException("Abonnement introuvable.");

        if (!childIds.Contains(sub.StudentId))
            throw new InvalidOperationException("Abonnement introuvable.");

        await CancelSubscriptionEntityAsync(sub, ct);
    }

    public async Task CancelSelfAsync(string studentUserId, Guid subscriptionId, CancellationToken ct = default)
    {
        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == studentUserId && s.IsActive)
            ?? throw new InvalidOperationException("Profil élève introuvable.");

        var sub = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s =>
                s.Id == subscriptionId && s.StudentId == student.Id)
            ?? throw new InvalidOperationException("Abonnement introuvable.");

        await CancelSubscriptionEntityAsync(sub, ct);
    }

    private async Task CancelSubscriptionEntityAsync(
        Domain.Entities.StudentSubscription sub,
        CancellationToken ct)
    {
        if (sub.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Rejected or SubscriptionStatus.Expired)
            throw new InvalidOperationException("Cet abonnement ne peut plus être annulé.");

        sub.Status = SubscriptionStatus.Cancelled;
        sub.UpdatedAt = DateTime.UtcNow;
        ClosePendingPayments(sub.Id);
        await _db.SaveChangesAsync(ct);

        await _payments.TryCancelGatewaySubscriptionAsync(sub.Id, cancelImmediately: true, ct);
        await _lessonScheduler.CancelUnconsumedFutureAsync(sub.Id, ct);
    }

    public async Task<StudentSubscriptionDto> AcceptAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var sub = RequireTeacherOwnedSubscription(subscriptionId);
        await AcceptCoreAsync(sub, ct);
        await _billingEmail.NotifyEnrollmentAcceptedAsync(sub.Id, ct);
        return MapSubscription(sub);
    }

    public async Task<StudentSubscriptionDto> RejectAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var sub = RequireTeacherOwnedSubscription(subscriptionId);
        await RejectCoreAsync(sub, ct);
        await _billingEmail.NotifyEnrollmentRejectedAsync(sub.Id, ct);
        return MapSubscription(sub);
    }

    public async Task<StudentSubscriptionDto> PauseAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var sub = RequireTeacherOwnedSubscription(subscriptionId);
        if (sub.Status != SubscriptionStatus.Active)
            throw new InvalidOperationException("Seul un abonnement actif peut être mis en pause.");

        sub.Status = SubscriptionStatus.Paused;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapSubscription(sub);
    }

    public async Task<StudentSubscriptionDto> ResumeAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var sub = RequireTeacherOwnedSubscription(subscriptionId);
        if (sub.Status != SubscriptionStatus.Paused)
            throw new InvalidOperationException("Seul un abonnement en pause peut être réactivé.");

        sub.Status = SubscriptionStatus.Active;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _lessonScheduler.EnsureScheduledAsync(sub.Id, ct);
        return MapSubscription(sub);
    }

    public Task<IReadOnlyList<ExpertPendingEnrollmentDto>> ListPendingForExpertGroupAsync(
        string expertUserId, Guid? groupId, CancellationToken ct = default)
    {
        var gid = ResolveExpertGroupId(expertUserId, groupId);
        return Task.FromResult(LoadPendingForGroup(gid));
    }

    public async Task<ExpertPendingEnrollmentDto> AcceptForExpertGroupAsync(
        string expertUserId, Guid subscriptionId, Guid? groupId, CancellationToken ct = default)
    {
        var (gid, sub) = LoadPendingInGroup(expertUserId, subscriptionId, groupId);
        await AcceptCoreAsync(sub, ct);
        await _billingEmail.NotifyEnrollmentAcceptedAsync(sub.Id, ct);
        await NotifyTeacherViaRemarkAsync(expertUserId, sub, accepted: true, ct);
        return MapExpert(sub, gid);
    }

    public async Task<ExpertPendingEnrollmentDto> RejectForExpertGroupAsync(
        string expertUserId, Guid subscriptionId, Guid? groupId, CancellationToken ct = default)
    {
        var (gid, sub) = LoadPendingInGroup(expertUserId, subscriptionId, groupId);
        await RejectCoreAsync(sub, ct);
        await _billingEmail.NotifyEnrollmentRejectedAsync(sub.Id, ct);
        await NotifyTeacherViaRemarkAsync(expertUserId, sub, accepted: false, ct);
        return MapExpert(sub, gid);
    }

    private async Task AcceptCoreAsync(Domain.Entities.StudentSubscription sub, CancellationToken ct)
    {
        if (sub.Status != SubscriptionStatus.Pending)
            throw new InvalidOperationException("Seules les demandes en attente peuvent être acceptées.");

        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == sub.OfferingId)
            ?? throw new InvalidOperationException("Offre introuvable.");

        if (offering.Price <= 0)
        {
            sub.Status = SubscriptionStatus.Active;
            sub.SessionsRemaining = PackPaymentProcess.SessionsOnFreeAccept(offering.SessionCount);
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _lessonScheduler.EnsureScheduledAsync(sub.Id, ct);
        }
        else
        {
            sub.Status = SubscriptionStatus.AwaitingPayment;
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task RejectCoreAsync(Domain.Entities.StudentSubscription sub, CancellationToken ct)
    {
        if (sub.Status != SubscriptionStatus.Pending)
            throw new InvalidOperationException("Seules les demandes en attente peuvent être refusées.");

        sub.Status = SubscriptionStatus.Rejected;
        sub.UpdatedAt = DateTime.UtcNow;
        ClosePendingPayments(sub.Id);
        await _db.SaveChangesAsync(ct);
    }

    private (Guid GroupId, Domain.Entities.StudentSubscription Sub) LoadPendingInGroup(
        string expertUserId, Guid subscriptionId, Guid? groupId)
    {
        var gid = ResolveExpertGroupId(expertUserId, groupId);
        var sub = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s => s.Id == subscriptionId)
            ?? throw new InvalidOperationException("Demande d'inscription introuvable.");
        var teacher = _db.Tenants.FirstOrDefault(t => t.Id == sub.TenantId)
            ?? throw new InvalidOperationException("Enseignant introuvable.");
        if (teacher.ApprovedByExpertGroupId != gid)
            throw new InvalidOperationException("Cette demande n'appartient pas aux enseignants de votre groupe.");
        return (gid, sub);
    }

    private IReadOnlyList<ExpertPendingEnrollmentDto> LoadPendingForGroup(Guid groupId)
    {
        var teacherIds = _db.Tenants
            .Where(t => t.ApprovedByExpertGroupId == groupId)
            .Select(t => t.Id)
            .ToHashSet();
        if (teacherIds.Count == 0)
            return [];

        var rows = _db.StudentSubscriptionsForAnyTenant
            .Where(s => teacherIds.Contains(s.TenantId) && s.Status == SubscriptionStatus.Pending)
            .OrderBy(s => s.CreatedAt)
            .ToList();
        return rows.Select(s => MapExpert(s, groupId)).ToList();
    }

    private async Task NotifyTeacherViaRemarkAsync(
        string expertUserId,
        Domain.Entities.StudentSubscription sub,
        bool accepted,
        CancellationToken ct)
    {
        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == sub.StudentId);
        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == sub.OfferingId);
        var studentName = student is null ? "un élève" : $"{student.FirstName} {student.LastName}".Trim();
        var course = offering?.Title ?? "un cours";
        string followUp = accepted
            ? (sub.Status == SubscriptionStatus.AwaitingPayment
                ? "Le parent (ou l'élève) peut maintenant payer pour activer le forfait."
                : "L'élève est admis. Les séances peuvent être planifiées.")
            : "La famille a été informée du refus.";
        var verb = accepted ? "accepté" : "refusé";
        var message =
            $"Le groupe d'experts a {verb} l'inscription de {studentName} à « {course} » à votre place. {followUp}";

        try
        {
            await _monitoring.AddRemarkAsync(
                expertUserId,
                sub.TenantId,
                new DTOs.ExpertApproval.CreateExpertRemarkRequest(ExpertRemarkCategory.Activity, message),
                ct);
        }
        catch
        {
            // La décision est déjà enregistrée ; l'avertissement enseignant est best-effort.
        }
    }

    private ExpertPendingEnrollmentDto MapExpert(Domain.Entities.StudentSubscription sub, Guid groupId)
    {
        _ = groupId;
        var teacher = _db.Tenants.FirstOrDefault(t => t.Id == sub.TenantId);
        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == sub.StudentId);
        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == sub.OfferingId);
        return new ExpertPendingEnrollmentDto(
            sub.Id,
            sub.TenantId,
            teacher?.Name ?? "Enseignant",
            sub.StudentId,
            student is null ? "" : $"{student.FirstName} {student.LastName}".Trim(),
            ResolveParentName(student),
            sub.OfferingId,
            offering?.Title ?? "Offre",
            offering?.Price ?? 0,
            offering?.Currency ?? "CAD",
            sub.CreatedAt,
            sub.Status.ToString());
    }

    private Guid ResolveExpertGroupId(string expertUserId, Guid? preferredGroupId)
    {
        var memberships = _db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            .ToList();

        if (preferredGroupId is Guid gid)
        {
            _ = _db.ExpertGroups.FirstOrDefault(g => g.Id == gid && g.IsActive)
                ?? throw new InvalidOperationException("Groupe introuvable.");
            if (memberships.Any(m => m.ExpertGroupId == gid) || _managers.IsActiveManager(expertUserId, gid))
                return gid;
            if (memberships.Count == 0)
                return gid;
            throw new InvalidOperationException("Ce groupe n'est pas le vôtre.");
        }

        if (memberships.Count > 0)
            return memberships[0].ExpertGroupId;

        if (_managers.IsActiveManager(expertUserId))
        {
            var mandate = _db.ExpertGroupManagerMandates.FirstOrDefault(m =>
                m.UserId == expertUserId && m.Status == ExpertGroupManagerMandateStatus.Active);
            if (mandate is not null)
                return mandate.ExpertGroupId;
        }

        throw new InvalidOperationException("Accès réservé à un membre actif du groupe d'experts.");
    }

    private Domain.Entities.StudentSubscription RequireTeacherOwnedSubscription(Guid subscriptionId)
    {
        var sub = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s => s.Id == subscriptionId)
            ?? throw new InvalidOperationException("Demande d'inscription introuvable.");
        EnsureTeacherOwns(sub);
        return sub;
    }

    private void EnsureTeacherOwns(Domain.Entities.StudentSubscription sub)
    {
        var tenantId = RequireTeacherTenantId();
        if (sub.TenantId == tenantId)
            return;

        var assigned = AssignedOfferingIds(tenantId).Contains(sub.OfferingId);
        if (!assigned)
            throw new InvalidOperationException("Cette inscription n'appartient pas à vos cours.");
    }

    private Guid RequireTeacherTenantId()
    {
        if (_tenantContext.HasTenant && _tenantContext.TenantId is Guid tenantId)
            return tenantId;
        throw new InvalidOperationException("École enseignant introuvable.");
    }

    private List<Guid> AssignedOfferingIds(Guid tenantId) =>
        _db.GroupOfferTeachers
            .Where(a => a.TeacherTenantId == tenantId
                && a.SubscriptionOfferingId != null
                && (a.AssignmentStatus == GroupOfferTeacherAssignmentStatus.Approved
                    || a.AssignmentStatus == GroupOfferTeacherAssignmentStatus.Active))
            .Select(a => a.SubscriptionOfferingId!.Value)
            .Distinct()
            .ToList();

    private static int EnrollmentRank(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Pending => 0,
        SubscriptionStatus.AwaitingPayment => 1,
        SubscriptionStatus.Active => 2,
        SubscriptionStatus.Paused => 3,
        _ => 4
    };

    private IReadOnlyList<StudentSubscriptionDto> MapMany(List<Domain.Entities.StudentSubscription> subs)
    {
        if (subs.Count == 0)
            return [];

        var offeringIds = subs.Select(s => s.OfferingId).Distinct().ToList();
        var studentIds = subs.Select(s => s.StudentId).Distinct().ToList();
        var tenantIds = subs.Select(s => s.TenantId).Distinct().ToList();

        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => offeringIds.Contains(o.Id))
            .ToDictionary(o => o.Id);

        var students = _db.StudentsForAnyTenant
            .Where(s => studentIds.Contains(s.Id))
            .ToList();

        var parentIds = students
            .Where(s => s.ParentProfileId.HasValue)
            .Select(s => s.ParentProfileId!.Value)
            .Distinct()
            .ToList();

        var parents = _db.ParentProfilesForAnyTenant
            .Where(p => parentIds.Contains(p.Id))
            .ToDictionary(p => p.Id);

        var teachers = _db.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionary(t => t.Id, t => t.Name);

        var studentsById = students.ToDictionary(s => s.Id);

        return subs.Select(s =>
        {
            offerings.TryGetValue(s.OfferingId, out var offering);
            studentsById.TryGetValue(s.StudentId, out var student);
            string? parentName = null;
            if (student?.ParentProfileId is Guid pid && parents.TryGetValue(pid, out var parent))
                parentName = $"{parent.FirstName} {parent.LastName}".Trim();

            teachers.TryGetValue(s.TenantId, out var teacherName);

            return Map(
                s,
                offering?.Title ?? "Offre",
                offering?.Subject,
                offering?.Price ?? 0,
                offering?.Currency ?? "CAD",
                student is null ? "" : $"{student.FirstName} {student.LastName}".Trim(),
                parentName,
                teacherName);
        }).ToList();
    }

    private StudentSubscriptionDto MapSubscription(Domain.Entities.StudentSubscription sub)
    {
        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == sub.OfferingId);
        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == sub.StudentId);
        var teacherName = _db.Tenants.FirstOrDefault(t => t.Id == sub.TenantId)?.Name;
        return Map(
            sub,
            offering?.Title ?? "Offre",
            offering?.Subject,
            offering?.Price ?? 0,
            offering?.Currency ?? "CAD",
            student is null ? "" : $"{student.FirstName} {student.LastName}".Trim(),
            ResolveParentName(student),
            teacherName);
    }

    private void ClosePendingPayments(Guid subscriptionId)
    {
        var pending = _db.PaymentsForAnyTenant
            .Where(p => p.SubscriptionId == subscriptionId && p.Status == PaymentStatus.Pending)
            .ToList();
        var now = DateTime.UtcNow;
        foreach (var payment in pending)
        {
            PackPaymentProcess.ClosePendingPayment(payment, now);
        }
    }

    private string? ResolveParentName(Domain.Entities.Student? student)
    {
        if (student?.ParentProfileId is not Guid pid)
            return null;

        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.Id == pid);
        return parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
    }

    private static StudentSubscriptionDto Map(
        Domain.Entities.StudentSubscription s,
        string offeringTitle,
        string? subject,
        decimal price,
        string currency,
        string studentName,
        string? parentName = null,
        string? teacherName = null) => new(
        s.Id,
        s.TenantId,
        s.StudentId,
        studentName,
        s.OfferingId,
        offeringTitle,
        subject,
        price,
        currency,
        s.Status.ToString(),
        s.StartDate,
        s.EndDate,
        s.SessionsRemaining,
        parentName,
        teacherName,
        s.CreatedAt);
}
