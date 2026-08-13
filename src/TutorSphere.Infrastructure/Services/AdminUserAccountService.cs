using Microsoft.AspNetCore.Identity;
using TutorSphere.Application.Common.Interfaces;
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
}

public class AdminUserAccountService(
    IApplicationDbContext db,
    UserManager<ApplicationUser> users) : IAdminUserAccountService
{
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
