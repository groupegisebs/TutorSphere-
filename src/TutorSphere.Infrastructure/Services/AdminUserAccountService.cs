using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Services;

public interface IAdminUserAccountService
{
    /// <summary>
    /// Hard-deletes a Parent or Student account (and related profiles). SuperAdmin only.
    /// </summary>
    Task DeleteParentOrStudentAsync(string targetUserId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes a Tutor / TeachingAssistant account and owned teacher tenants. SuperAdmin / PlatformAdmin.
    /// </summary>
    Task DeleteTeacherAsync(string targetUserId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes a teacher tenant/profile and related data. SuperAdmin only.
    /// Cancels remaining scheduled lessons and refunds completed parent payments first
    /// (no teacher/parent consent). Then removes the graph.
    /// Optionally removes the owner Tutor identity when it has no other role.
    /// </summary>
    Task DeleteTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public class AdminUserAccountService(
    IApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IPaymentGatewayService payments,
    IBillingEmailOrchestrator billingEmail,
    IEmailService email,
    ILogger<AdminUserAccountService> logger) : IAdminUserAccountService
{
    private static readonly HashSet<string> ProtectedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "platform-parents",
        "tutorsphere-parents",
        "holding",
        "marketplace"
    };

    public async Task DeleteParentOrStudentAsync(string targetUserId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(targetUserId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var roles = await users.GetRolesAsync(user);
        var isParent = roles.Contains(UserRoles.Parent);
        var isStudent = roles.Contains(UserRoles.Student);

        if (!isParent && !isStudent)
            throw new InvalidOperationException("Seuls les comptes Parent ou Élève peuvent être supprimés définitivement.");

        if (roles.Any(r => r is UserRoles.SuperAdmin or UserRoles.PlatformAdmin or UserRoles.Tutor
                or UserRoles.TeachingAssistant or UserRoles.Expert or UserRoles.GroupManager))
            throw new InvalidOperationException("Ce compte a un rôle protégé et ne peut pas être supprimé via cette action.");

        if (isParent)
            await DeleteParentGraphAsync(user.Id, ct);
        else
            await DeleteStudentGraphByUserIdAsync(user.Id, ct);

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task DeleteTeacherAsync(string targetUserId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(targetUserId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var roles = await users.GetRolesAsync(user);
        var isTeacher = roles.Contains(UserRoles.Tutor) || roles.Contains(UserRoles.TeachingAssistant);
        if (!isTeacher)
            throw new InvalidOperationException("Ce compte n'est pas un enseignant.");

        if (roles.Any(r => r is UserRoles.SuperAdmin or UserRoles.PlatformAdmin
                or UserRoles.Expert or UserRoles.GroupManager))
            throw new InvalidOperationException(
                "Ce compte a un rôle protégé (admin / expert) et ne peut pas être supprimé via cette action.");

        var ownedTenants = db.Tenants
            .Where(t => t.OwnerUserId == targetUserId)
            .Select(t => t.Id)
            .ToList();

        foreach (var tenantId in ownedTenants)
            await DeleteTenantAsync(tenantId, ct);

        // DeleteTenantAsync may already have removed the identity when it was tutor-only.
        user = await users.FindByIdAsync(targetUserId);
        if (user is null)
            return;

        // Still present (ex. TeachingAssistant without owned tenant, or leftover).
        if (user.TenantId is Guid leftoverTenant
            && db.Tenants.Any(t => t.Id == leftoverTenant && t.OwnerUserId == targetUserId))
        {
            await DeleteTenantAsync(leftoverTenant, ct);
            user = await users.FindByIdAsync(targetUserId);
            if (user is null) return;
        }

        user.TenantId = null;
        await users.UpdateAsync(user);

        var del = await users.DeleteAsync(user);
        if (!del.Succeeded)
            throw new InvalidOperationException(string.Join("; ", del.Errors.Select(e => e.Description)));
    }

    public async Task DeleteTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Profil introuvable.");

        if (ProtectedSlugs.Contains(tenant.Slug)
            || tenant.Name.Contains("Parents", StringComparison.OrdinalIgnoreCase)
               && tenant.Slug.Contains("parent", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ce profil système (espace parents / holding) ne peut pas être supprimé.");
        }

        var ownerUserId = tenant.OwnerUserId;
        var tutorName = string.IsNullOrWhiteSpace(tenant.Name) ? "votre enseignant" : tenant.Name.Trim();

        tenant.IsPublicProfile = false;
        tenant.Status = TenantStatus.Suspended;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await SettleTeacherTenantBeforeDeleteAsync(tenantId, tutorName, ct);

        if (!string.IsNullOrWhiteSpace(ownerUserId))
        {
            var owner = await users.FindByIdAsync(ownerUserId);
            if (owner is not null && !string.IsNullOrWhiteSpace(owner.Email))
            {
                try
                {
                    await email.SendAccountDeactivatedAsync(
                        owner.Email,
                        string.IsNullOrWhiteSpace(owner.FirstName) ? tutorName : owner.FirstName,
                        "Votre profil enseignant a été retiré par l'administration. Les cours programmés ont été annulés et les parents déjà payés ont été remboursés.",
                        ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Échec e-mail suppression enseignant {UserId}", ownerUserId);
                }
            }
        }

        // Order matters for FKs.
        var remarks = db.ExpertRemarksForAnyTenant.Where(r => r.TenantId == tenantId).ToList();
        if (remarks.Count > 0) db.RemoveRange(remarks);

        var teacherDocs = db.TeacherDocumentsForAnyTenant.Where(d => d.TenantId == tenantId).ToList();
        if (teacherDocs.Count > 0) db.RemoveRange(teacherDocs);

        var assignments = db.TeacherDisciplineAssignments.Where(a => a.TenantId == tenantId).ToList();
        if (assignments.Count > 0) db.RemoveRange(assignments);

        var groupOfferLinks = db.GroupOfferTeachers.Where(x => x.TeacherTenantId == tenantId).ToList();
        if (groupOfferLinks.Count > 0) db.RemoveRange(groupOfferLinks);

        var delegated = db.ExpertDelegatedTasks.Where(t => t.TeacherTenantId == tenantId).ToList();
        if (delegated.Count > 0) db.RemoveRange(delegated);

        var workspace = db.ExpertWorkspaceItems.Where(w => w.RelatedTeacherTenantId == tenantId).ToList();
        if (workspace.Count > 0) db.RemoveRange(workspace);

        var invites = db.TeacherApplicationInvites.Where(i => i.AcceptedTenantId == tenantId).ToList();
        foreach (var inv in invites)
            inv.AcceptedTenantId = null;

        var subs = db.StudentSubscriptionsForAnyTenant.Where(s => s.TenantId == tenantId).ToList();
        if (subs.Count > 0)
        {
            var subIds = subs.Select(s => s.Id).ToList();
            var subPayments = db.PaymentsForAnyTenant
                .Where(p => p.SubscriptionId.HasValue && subIds.Contains(p.SubscriptionId.Value))
                .ToList();
            if (subPayments.Count > 0) db.RemoveRange(subPayments);
            db.RemoveRange(subs);
        }

        var offerings = db.SubscriptionOfferingsForAnyTenant.Where(o => o.TenantId == tenantId).ToList();
        if (offerings.Count > 0) db.RemoveRange(offerings);

        var lessons = db.LessonsForAnyTenant.Where(l => l.TenantId == tenantId).ToList();
        if (lessons.Count > 0)
        {
            var lessonIds = lessons.Select(l => l.Id).ToList();
            var attendances = db.LessonAttendancesForAnyTenant.Where(a => lessonIds.Contains(a.LessonId)).ToList();
            if (attendances.Count > 0) db.RemoveRange(attendances);
            db.RemoveRange(lessons);
        }

        var reportsByTenant = db.LessonReportsForAnyTenant.Where(r => r.TenantId == tenantId).ToList();
        if (reportsByTenant.Count > 0) db.RemoveRange(reportsByTenant);

        var homeworksByTenant = db.HomeworksForAnyTenant.Where(h => h.TenantId == tenantId).ToList();
        if (homeworksByTenant.Count > 0) db.RemoveRange(homeworksByTenant);

        var unavail = db.Unavailabilities.Where(u => u.TenantId == tenantId).ToList();
        if (unavail.Count > 0) db.RemoveRange(unavail);

        var availabilities = db.TeacherAvailabilitiesForAnyTenant.Where(a => a.TenantId == tenantId).ToList();
        if (availabilities.Count > 0) db.RemoveRange(availabilities);

        var holidays = db.Holidays.Where(h => h.TenantId == tenantId).ToList();
        if (holidays.Count > 0) db.RemoveRange(holidays);

        var vacations = db.Vacations.Where(v => v.TenantId == tenantId).ToList();
        if (vacations.Count > 0) db.RemoveRange(vacations);

        var invoices = db.InvoicesForAnyTenant.Where(i => i.TenantId == tenantId).ToList();
        if (invoices.Count > 0)
        {
            var invoiceIds = invoices.Select(i => i.Id).ToList();
            var invPay = db.PaymentsForAnyTenant
                .Where(p => p.InvoiceId.HasValue && invoiceIds.Contains(p.InvoiceId.Value))
                .ToList();
            if (invPay.Count > 0) db.RemoveRange(invPay);
            db.RemoveRange(invoices);
        }

        var payments = db.PaymentsForAnyTenant.Where(p => p.TenantId == tenantId).ToList();
        if (payments.Count > 0) db.RemoveRange(payments);

        var licensePays = db.PlatformLicensePaymentsForAnyTenant.Where(p => p.TenantId == tenantId).ToList();
        if (licensePays.Count > 0) db.RemoveRange(licensePays);

        var payouts = db.TutorPayoutsForAnyTenant.Where(p => p.TenantId == tenantId).ToList();
        if (payouts.Count > 0) db.RemoveRange(payouts);

        var payoutAccounts = db.TutorPayoutAccounts.Where(a => a.TenantId == tenantId).ToList();
        if (payoutAccounts.Count > 0) db.RemoveRange(payoutAccounts);

        var messages = db.Messages.Where(m => m.TenantId == tenantId).ToList();
        if (messages.Count > 0) db.RemoveRange(messages);

        var documents = db.DocumentsForAnyTenant.Where(d => d.TenantId == tenantId).ToList();
        if (documents.Count > 0) db.RemoveRange(documents);

        var brandings = db.TenantBrandings.Where(b => b.TenantId == tenantId).ToList();
        if (brandings.Count > 0) db.RemoveRange(brandings);

        // Students / parents attached to this teacher tenant
        var parentProfiles = db.ParentProfilesForAnyTenant.Where(p => p.TenantId == tenantId).ToList();
        if (parentProfiles.Count > 0)
        {
            var parentIds = parentProfiles.Select(p => p.Id).ToList();
            var linkedStudents = db.StudentsForAnyTenant
                .Where(s => s.ParentProfileId.HasValue && parentIds.Contains(s.ParentProfileId.Value))
                .ToList();
            foreach (var child in linkedStudents)
                await DeleteStudentEntityAsync(child, deleteIdentityUser: true, ct);
            db.RemoveRange(parentProfiles);
        }

        var students = db.StudentsForAnyTenant.Where(s => s.TenantId == tenantId).ToList();
        foreach (var student in students)
            await DeleteStudentEntityAsync(student, deleteIdentityUser: true, ct);

        db.Remove(tenant);
        await db.SaveChangesAsync(ct);

        // Detach other users pointing at this tenant
        var linkedUsers = users.Users.Where(u => u.TenantId == tenantId).ToList();
        foreach (var u in linkedUsers)
        {
            u.TenantId = null;
            await users.UpdateAsync(u);
        }

        if (!string.IsNullOrWhiteSpace(ownerUserId))
        {
            var owner = await users.FindByIdAsync(ownerUserId);
            if (owner is not null)
            {
                var roles = await users.GetRolesAsync(owner);
                var onlyTutor = roles.Contains(UserRoles.Tutor)
                    && !roles.Any(r => r is UserRoles.SuperAdmin or UserRoles.PlatformAdmin
                        or UserRoles.Expert or UserRoles.GroupManager or UserRoles.Parent);
                var ownsOther = db.Tenants.Any(t => t.OwnerUserId == ownerUserId && t.Id != tenantId);
                if (onlyTutor && !ownsOther)
                {
                    var del = await users.DeleteAsync(owner);
                    if (!del.Succeeded)
                        throw new InvalidOperationException(
                            $"Profil supprimé, mais le compte propriétaire n'a pas pu l'être : {string.Join("; ", del.Errors.Select(e => e.Description))}");
                }
            }
        }
    }

    private async Task SettleTeacherTenantBeforeDeleteAsync(Guid tenantId, string tutorName, CancellationToken ct)
    {
        const string cancelReason =
            "Enseignant retiré par l'administration. Les cours programmés sont annulés ; les paiements déjà effectués sont remboursés.";

        var now = DateTime.UtcNow;
        var lessons = db.LessonsForAnyTenant
            .Where(l => l.TenantId == tenantId && l.SettlementStatus == LessonSettlementStatus.Scheduled)
            .ToList();

        var lessonIds = lessons.Select(l => l.Id).ToList();
        var attendances = lessonIds.Count == 0
            ? []
            : db.LessonAttendancesForAnyTenant.Where(a => lessonIds.Contains(a.LessonId)).ToList();
        var attendanceByLesson = attendances
            .GroupBy(a => a.LessonId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.StudentId).Distinct().ToList());

        var studentIds = attendances.Select(a => a.StudentId).Distinct().ToList();
        var students = studentIds.Count == 0
            ? []
            : db.StudentsForAnyTenant.Where(s => studentIds.Contains(s.Id)).ToList();
        var studentsById = students.ToDictionary(s => s.Id);
        var parentIds = students
            .Where(s => s.ParentProfileId.HasValue)
            .Select(s => s.ParentProfileId!.Value)
            .Distinct()
            .ToList();
        var parentsById = parentIds.Count == 0
            ? new Dictionary<Guid, ParentProfile>()
            : db.ParentProfilesForAnyTenant.Where(p => parentIds.Contains(p.Id)).ToList()
                .ToDictionary(p => p.Id);

        var tenantSubs = db.StudentSubscriptionsForAnyTenant.Where(s => s.TenantId == tenantId).ToList();

        var tenantPayments = db.PaymentsForAnyTenant.Where(p => p.TenantId == tenantId).ToList();
        foreach (var payment in tenantPayments)
        {
            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Failed;
                payment.UpdatedAt = now;
                continue;
            }

            if (payment.Status != PaymentStatus.Completed)
                continue;

            await payments.RefundCompletedPaymentAsync(payment.Id, ct);
            await billingEmail.NotifyPaymentRefundedAsync(payment.Id, tutorName, ct);
        }

        foreach (var sub in tenantSubs)
        {
            await payments.TryCancelGatewaySubscriptionAsync(sub.Id, cancelImmediately: true, ct);
            if (sub.Status is SubscriptionStatus.Pending
                or SubscriptionStatus.AwaitingPayment
                or SubscriptionStatus.Active
                or SubscriptionStatus.Paused)
            {
                sub.Status = SubscriptionStatus.Cancelled;
                sub.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var lesson in lessons)
        {
            if (lesson.SessionCounted
                && attendanceByLesson.TryGetValue(lesson.Id, out var creditedStudentIds))
            {
                foreach (var studentId in creditedStudentIds)
                {
                    var sub = tenantSubs
                        .Where(s => s.StudentId == studentId
                                    && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Paused))
                        .OrderByDescending(s => s.CreatedAt)
                        .FirstOrDefault();
                    if (sub is null) continue;
                    sub.SessionsRemaining += 1;
                    sub.UpdatedAt = now;
                }

                lesson.SessionCounted = false;
            }

            lesson.CancelledAt = now;
            lesson.CancellationReason = cancelReason;
            lesson.SettlementStatus = LessonSettlementStatus.CancelledFree;
            lesson.TutorLiable = false;
            lesson.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        foreach (var lesson in lessons)
        {
            if (!attendanceByLesson.TryGetValue(lesson.Id, out var attendeeIds))
                continue;
            var subject = lesson.Subject ?? lesson.Title;
            foreach (var studentId in attendeeIds)
            {
                if (!studentsById.TryGetValue(studentId, out var student))
                    continue;
                try
                {
                    if (!string.IsNullOrWhiteSpace(student.Email))
                    {
                        await email.SendLessonCancelledAsync(
                            student.Email,
                            $"{student.FirstName} {student.LastName}".Trim(),
                            tutorName,
                            subject,
                            lesson.StartTime,
                            ct);
                    }

                    if (student.ParentProfileId is Guid pid
                        && parentsById.TryGetValue(pid, out var parent)
                        && !string.IsNullOrWhiteSpace(parent.Email))
                    {
                        await email.SendLessonCancelledAsync(
                            parent.Email,
                            parent.FirstName,
                            tutorName,
                            subject,
                            lesson.StartTime,
                            ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Échec e-mail d'annulation (suppression enseignant) pour l'élève {StudentId}", studentId);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task DeleteParentGraphAsync(string parentUserId, CancellationToken ct)
    {
        var profiles = db.ParentProfilesForAnyTenant.Where(p => p.UserId == parentUserId).ToList();
        var parentIds = profiles.Select(p => p.Id).ToList();

        var children = parentIds.Count == 0
            ? new List<Student>()
            : db.StudentsForAnyTenant
                .Where(s => s.ParentProfileId.HasValue && parentIds.Contains(s.ParentProfileId.Value))
                .ToList();

        foreach (var child in children)
            await DeleteStudentEntityAsync(child, deleteIdentityUser: true, ct);

        if (parentIds.Count > 0)
        {
            var invoices = db.InvoicesForAnyTenant.Where(i => parentIds.Contains(i.ParentProfileId)).ToList();
            if (invoices.Count > 0)
            {
                var invoiceIds = invoices.Select(i => i.Id).ToList();
                var invoicePayments = db.PaymentsForAnyTenant
                    .Where(p => p.InvoiceId.HasValue && invoiceIds.Contains(p.InvoiceId.Value))
                    .ToList();
                if (invoicePayments.Count > 0)
                    db.RemoveRange(invoicePayments);
                db.RemoveRange(invoices);
            }

            db.RemoveRange(profiles);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task DeleteStudentGraphByUserIdAsync(string studentUserId, CancellationToken ct)
    {
        var students = db.StudentsForAnyTenant.Where(s => s.UserId == studentUserId).ToList();
        foreach (var student in students)
            await DeleteStudentEntityAsync(student, deleteIdentityUser: false, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task DeleteStudentEntityAsync(Student student, bool deleteIdentityUser, CancellationToken ct)
    {
        var studentId = student.Id;

        var subs = db.StudentSubscriptionsForAnyTenant.Where(s => s.StudentId == studentId).ToList();
        if (subs.Count > 0)
        {
            var subIds = subs.Select(s => s.Id).ToList();
            var subPayments = db.PaymentsForAnyTenant
                .Where(p => p.SubscriptionId.HasValue && subIds.Contains(p.SubscriptionId.Value))
                .ToList();
            if (subPayments.Count > 0)
                db.RemoveRange(subPayments);
            db.RemoveRange(subs);
        }

        var attendances = db.LessonAttendancesForAnyTenant.Where(a => a.StudentId == studentId).ToList();
        if (attendances.Count > 0) db.RemoveRange(attendances);

        var homeworks = db.HomeworksForAnyTenant.Where(h => h.StudentId == studentId).ToList();
        if (homeworks.Count > 0) db.RemoveRange(homeworks);

        var reports = db.LessonReportsForAnyTenant.Where(r => r.StudentId == studentId).ToList();
        if (reports.Count > 0) db.RemoveRange(reports);

        var docs = db.DocumentsForAnyTenant.Where(d => d.StudentId == studentId).ToList();
        if (docs.Count > 0) db.RemoveRange(docs);

        var linkedUserId = student.UserId;
        db.Remove(student);

        if (deleteIdentityUser && !string.IsNullOrWhiteSpace(linkedUserId))
        {
            await db.SaveChangesAsync(ct);
            var childUser = await users.FindByIdAsync(linkedUserId);
            if (childUser is not null)
            {
                var childRoles = await users.GetRolesAsync(childUser);
                if (childRoles.Contains(UserRoles.Student)
                    && !childRoles.Any(r => r is UserRoles.SuperAdmin or UserRoles.PlatformAdmin
                        or UserRoles.Parent or UserRoles.Tutor or UserRoles.Expert or UserRoles.GroupManager))
                {
                    var del = await users.DeleteAsync(childUser);
                    if (!del.Succeeded)
                        throw new InvalidOperationException(
                            $"Impossible de supprimer le compte élève lié : {string.Join("; ", del.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}
