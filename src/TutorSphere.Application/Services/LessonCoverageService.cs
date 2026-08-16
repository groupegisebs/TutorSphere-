using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.LessonCoverage;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Domain.Payouts;

namespace TutorSphere.Application.Services;

public interface ILessonCoverageService
{
    Task<IReadOnlyList<UnavailableTeacherDto>> ListUnavailableTeachersAsync(
        string expertUserId, Guid? groupId, CancellationToken ct = default);
    Task<IReadOnlyList<LessonCoverageTeacherOptionDto>> ListGroupTeachersAsync(
        string expertUserId, Guid? groupId, CancellationToken ct = default);
    Task<UnavailableTeacherDto> DeclareAbsenceAsync(
        string expertUserId, DeclareTeacherAbsenceRequest request, Guid? groupId, CancellationToken ct = default);
    Task DeleteAbsenceAsync(
        string expertUserId, Guid unavailabilityId, Guid? groupId, CancellationToken ct = default);
    Task<IReadOnlyList<LessonCoverageTeacherOptionDto>> ListSubstituteOptionsAsync(
        string expertUserId, Guid originalTenantId, Guid? groupId, CancellationToken ct = default);
    Task<IReadOnlyList<LessonCoverageDto>> ListUpcomingLessonsAsync(
        string expertUserId, Guid originalTenantId, Guid? groupId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<LessonCoverageDto>> ListGroupAssignmentsAsync(
        string expertUserId, Guid? groupId, CancellationToken ct = default);
    Task<IReadOnlyList<LessonCoverageDto>> ProposeAsync(
        string expertUserId, CreateLessonCoverageRequest request, Guid? groupId, CancellationToken ct = default);
    Task CancelAsync(string expertUserId, Guid assignmentId, Guid? groupId, CancellationToken ct = default);

    Task<IReadOnlyList<LessonCoverageDto>> ListPendingForParentAsync(string parentUserId, CancellationToken ct = default);
    Task<LessonCoverageDto> RespondAsParentAsync(string parentUserId, Guid assignmentId, bool approve, CancellationToken ct = default);

    Task<IReadOnlyList<LessonCoverageDto>> ListPendingForStudentAsync(string studentUserId, CancellationToken ct = default);
    Task<LessonCoverageDto> RespondAsStudentAsync(string studentUserId, Guid assignmentId, bool approve, CancellationToken ct = default);
}

public sealed class LessonCoverageService(
    IApplicationDbContext db,
    IEmailService email,
    IExpertGroupManagerService managers,
    IUserContactLookup contacts,
    IAppUrlProvider urls) : ILessonCoverageService
{
    public Task<IReadOnlyList<UnavailableTeacherDto>> ListUnavailableTeachersAsync(
        string expertUserId, Guid? groupId, CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        var teacherIds = GroupTeacherIds(gid);
        var now = DateTime.UtcNow;

        var unavs = db.UnavailabilitiesForAnyTenant
            .Where(u => teacherIds.Contains(u.TenantId) && u.EndTime > now)
            .OrderBy(u => u.StartTime)
            .ToList();

        var tenants = db.Tenants.Where(t => teacherIds.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Name);
        var result = new List<UnavailableTeacherDto>();
        foreach (var u in unavs)
        {
            tenants.TryGetValue(u.TenantId, out var name);
            result.Add(new UnavailableTeacherDto(
                u.TenantId, name ?? "Enseignant", u.Id, u.StartTime, u.EndTime, u.Reason,
                CountUpcomingLessons(u.TenantId, u.StartTime, u.EndTime)));
        }

        return Task.FromResult<IReadOnlyList<UnavailableTeacherDto>>(result);
    }

    /// <summary>Enseignants approuvés du groupe : cible possible d'une absence saisie par le responsable.</summary>
    public Task<IReadOnlyList<LessonCoverageTeacherOptionDto>> ListGroupTeachersAsync(
        string expertUserId, Guid? groupId, CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        var teacherIds = GroupTeacherIds(gid);
        var options = db.Tenants
            .Where(t => teacherIds.Contains(t.Id) && t.ExpertApprovalStatus == ExpertApprovalStatus.Approved)
            .OrderBy(t => t.Name)
            .Select(t => new LessonCoverageTeacherOptionDto(t.Id, t.Name))
            .ToList();
        return Task.FromResult<IReadOnlyList<LessonCoverageTeacherOptionDto>>(options);
    }

    /// <summary>
    /// Le responsable enregistre l'absence à la place de l'enseignant : sans cela, aucun remplacement
    /// n'est possible avant que l'enseignant n'ouvre lui-même son agenda.
    /// </summary>
    public async Task<UnavailableTeacherDto> DeclareAbsenceAsync(
        string expertUserId, DeclareTeacherAbsenceRequest request, Guid? groupId, CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        EnsureTeacherInGroup(gid, request.TenantId);

        var start = request.StartTime;
        var end = request.EndTime;
        if (end <= start)
            throw new InvalidOperationException("La fin de l'absence doit suivre son début.");
        if (end <= DateTime.UtcNow)
            throw new InvalidOperationException("Cette période est déjà passée : aucune séance ne peut être réaffectée.");
        if ((end - start).TotalDays > 180)
            throw new InvalidOperationException("Une absence ne peut pas dépasser six mois.");

        var overlap = db.UnavailabilitiesForAnyTenant.Any(u =>
            u.TenantId == request.TenantId && u.StartTime < end && u.EndTime > start);
        if (overlap)
            throw new InvalidOperationException("Une indisponibilité couvre déjà tout ou partie de cette période.");

        var entity = new Unavailability
        {
            TenantId = request.TenantId,
            StartTime = start,
            EndTime = end,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Absence signalée par le groupe" : request.Reason.Trim()
        };
        db.Add(entity);
        await db.SaveChangesAsync(ct);

        var name = db.Tenants.FirstOrDefault(t => t.Id == request.TenantId)?.Name ?? "Enseignant";
        return new UnavailableTeacherDto(
            entity.TenantId, name, entity.Id, entity.StartTime, entity.EndTime, entity.Reason,
            CountUpcomingLessons(entity.TenantId, entity.StartTime, entity.EndTime));
    }

    public async Task DeleteAbsenceAsync(
        string expertUserId, Guid unavailabilityId, Guid? groupId, CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        var teacherIds = GroupTeacherIds(gid);
        var entity = db.UnavailabilitiesForAnyTenant.FirstOrDefault(u => u.Id == unavailabilityId)
            ?? throw new InvalidOperationException("Indisponibilité introuvable.");
        if (!teacherIds.Contains(entity.TenantId))
            throw new InvalidOperationException("Cet enseignant n'appartient pas à ce groupe d'experts.");

        var linked = db.LessonCoverageAssignments.Any(c =>
            c.UnavailabilityId == unavailabilityId
            && (c.Status == LessonCoverageStatus.Pending || c.Status == LessonCoverageStatus.Approved));
        if (linked)
            throw new InvalidOperationException("Des remplacements sont rattachés à cette période : annulez-les d'abord.");

        db.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private int CountUpcomingLessons(Guid tenantId, DateTime windowStart, DateTime windowEnd)
    {
        var now = DateTime.UtcNow;
        return db.LessonsForAnyTenant.Count(l =>
            l.TenantId == tenantId
            && l.SettlementStatus == LessonSettlementStatus.Scheduled
            && l.StartTime >= windowStart
            && l.StartTime < windowEnd
            && l.StartTime > now);
    }

    public Task<IReadOnlyList<LessonCoverageTeacherOptionDto>> ListSubstituteOptionsAsync(
        string expertUserId, Guid originalTenantId, Guid? groupId, CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        var teacherIds = GroupTeacherIds(gid)
            .Where(id => id != originalTenantId)
            .ToList();
        var options = db.Tenants
            .Where(t => teacherIds.Contains(t.Id)
                        && t.ExpertApprovalStatus == ExpertApprovalStatus.Approved)
            .OrderBy(t => t.Name)
            .ToList()
            .Where(t => t.HasValidLicense())
            .Select(t => new LessonCoverageTeacherOptionDto(t.Id, t.Name))
            .ToList();
        return Task.FromResult<IReadOnlyList<LessonCoverageTeacherOptionDto>>(options);
    }

    public Task<IReadOnlyList<LessonCoverageDto>> ListUpcomingLessonsAsync(
        string expertUserId, Guid originalTenantId, Guid? groupId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        EnsureTeacherInGroup(gid, originalTenantId);
        var start = from ?? DateTime.UtcNow;
        var end = to ?? start.AddDays(30);
        var lessons = LoadCandidateLessons(originalTenantId, start, end);
        return Task.FromResult(MapMany(lessons, LoadCoverages(lessons.Select(l => l.Id))));
    }

    public Task<IReadOnlyList<LessonCoverageDto>> ListGroupAssignmentsAsync(
        string expertUserId, Guid? groupId, CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        var rows = db.LessonCoverageAssignments
            .Where(c => c.ExpertGroupId == gid)
            .OrderByDescending(c => c.CreatedAt)
            .Take(200)
            .ToList();
        var lessonIds = rows.Select(r => r.LessonId).Distinct().ToList();
        var lessons = db.LessonsForAnyTenant.Where(l => lessonIds.Contains(l.Id)).ToList();
        return Task.FromResult(MapAssignments(rows, lessons));
    }

    public async Task<IReadOnlyList<LessonCoverageDto>> ProposeAsync(
        string expertUserId,
        CreateLessonCoverageRequest request,
        Guid? groupId,
        CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length < 3)
            throw new InvalidOperationException("Indiquez la raison de l'indisponibilité.");
        if (request.OriginalTenantId == request.SubstituteTenantId)
            throw new InvalidOperationException("Le suppléant doit être un autre enseignant du groupe.");

        EnsureTeacherInGroup(gid, request.OriginalTenantId);
        EnsureTeacherInGroup(gid, request.SubstituteTenantId);

        var substitute = db.Tenants.FirstOrDefault(t => t.Id == request.SubstituteTenantId)
            ?? throw new InvalidOperationException("Suppléant introuvable.");
        if (substitute.ExpertApprovalStatus != ExpertApprovalStatus.Approved || !substitute.HasValidLicense())
            throw new InvalidOperationException("Le suppléant doit avoir une session active et une fiche approuvée.");

        DateTime windowStart, windowEnd;
        if (request.UnavailabilityId is Guid uid)
        {
            var unav = db.UnavailabilitiesForAnyTenant.FirstOrDefault(u =>
                u.Id == uid && u.TenantId == request.OriginalTenantId)
                ?? throw new InvalidOperationException("Indisponibilité introuvable.");
            windowStart = unav.StartTime;
            windowEnd = unav.EndTime;
        }
        else
        {
            windowStart = request.WindowStart ?? DateTime.UtcNow;
            windowEnd = request.WindowEnd ?? windowStart.AddDays(14);
        }

        if (windowEnd <= windowStart)
            throw new InvalidOperationException("La période de remplacement est invalide.");

        var candidates = LoadCandidateLessons(request.OriginalTenantId, windowStart, windowEnd);
        if (request.LessonIds is { Count: > 0 })
        {
            var wanted = request.LessonIds.ToHashSet();
            candidates = candidates.Where(l => wanted.Contains(l.Id)).ToList();
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException("Aucune séance à venir à réaffecter sur cette période.");

        var blockedLessonIds = db.LessonCoverageAssignments
            .Where(c => c.Status == LessonCoverageStatus.Pending || c.Status == LessonCoverageStatus.Approved)
            .Select(c => c.LessonId)
            .ToHashSet();

        var substituteBusy = db.LessonsForAnyTenant
            .Where(l => l.TenantId == request.SubstituteTenantId
                        && l.SettlementStatus == LessonSettlementStatus.Scheduled
                        && l.EndTime > windowStart
                        && l.StartTime < windowEnd)
            .Select(l => new { l.StartTime, l.EndTime })
            .ToList();
        var substituteCoveringIds = db.LessonCoverageAssignments
            .Where(c => c.SubstituteTenantId == request.SubstituteTenantId
                        && (c.Status == LessonCoverageStatus.Approved || c.Status == LessonCoverageStatus.Pending))
            .Select(c => c.LessonId)
            .ToList();
        if (substituteCoveringIds.Count > 0)
        {
            substituteBusy.AddRange(db.LessonsForAnyTenant
                .Where(l => substituteCoveringIds.Contains(l.Id)
                            && l.SettlementStatus == LessonSettlementStatus.Scheduled)
                .Select(l => new { l.StartTime, l.EndTime })
                .ToList());
        }

        var created = new List<LessonCoverageAssignment>();
        var skippedBusy = 0;
        foreach (var lesson in candidates)
        {
            if (blockedLessonIds.Contains(lesson.Id) || lesson.DeliveredByTenantId.HasValue)
                continue;
            if (substituteBusy.Any(b => lesson.StartTime < b.EndTime && lesson.EndTime > b.StartTime))
            {
                skippedBusy++;
                continue;
            }

            var row = new LessonCoverageAssignment
            {
                ExpertGroupId = gid,
                OriginalTenantId = request.OriginalTenantId,
                SubstituteTenantId = request.SubstituteTenantId,
                LessonId = lesson.Id,
                UnavailabilityId = request.UnavailabilityId,
                Reason = reason,
                ProposedByUserId = expertUserId,
                Status = LessonCoverageStatus.Pending
            };
            db.Add(row);
            created.Add(row);
        }

        if (created.Count == 0)
        {
            throw new InvalidOperationException(skippedBusy > 0
                ? "Le suppléant a déjà un cours (ou un autre remplacement) sur ces horaires."
                : "Ces séances ont déjà un remplacement proposé ou accepté.");
        }

        await db.SaveChangesAsync(ct);
        await NotifyFamiliesAsync(created, ct);
        await NotifySubstituteAsync(created, ct);
        var lessons = candidates.Where(l => created.Any(c => c.LessonId == l.Id)).ToList();
        return MapAssignments(created, lessons);
    }

    public async Task CancelAsync(string expertUserId, Guid assignmentId, Guid? groupId, CancellationToken ct = default)
    {
        var gid = RequireMemberGroupId(expertUserId, groupId);
        var row = db.LessonCoverageAssignments.FirstOrDefault(c => c.Id == assignmentId && c.ExpertGroupId == gid)
            ?? throw new InvalidOperationException("Proposition introuvable.");
        if (row.Status != LessonCoverageStatus.Pending)
            throw new InvalidOperationException("Seule une proposition en attente peut être annulée.");
        row.Status = LessonCoverageStatus.Cancelled;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<LessonCoverageDto>> ListPendingForParentAsync(string parentUserId, CancellationToken ct = default)
    {
        var childIds = ChildIdsForParent(parentUserId);
        return Task.FromResult(PendingForStudents(childIds));
    }

    public async Task<LessonCoverageDto> RespondAsParentAsync(
        string parentUserId, Guid assignmentId, bool approve, CancellationToken ct = default)
    {
        var childIds = ChildIdsForParent(parentUserId);
        if (childIds.Count == 0)
            throw new InvalidOperationException("Aucun enfant associé.");
        return await RespondAsync(assignmentId, approve, parentUserId, childIds, requireAutonomousStudent: false, ct);
    }

    public Task<IReadOnlyList<LessonCoverageDto>> ListPendingForStudentAsync(string studentUserId, CancellationToken ct = default)
    {
        var student = db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == studentUserId);
        if (student is null || !student.IsAutonomous)
            return Task.FromResult<IReadOnlyList<LessonCoverageDto>>([]);
        return Task.FromResult(PendingForStudents([student.Id]));
    }

    public async Task<LessonCoverageDto> RespondAsStudentAsync(
        string studentUserId, Guid assignmentId, bool approve, CancellationToken ct = default)
    {
        var student = db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == studentUserId)
            ?? throw new InvalidOperationException("Profil élève introuvable.");
        if (!student.IsAutonomous)
            throw new InvalidOperationException("L'accord d'un mineur de moins de 14 ans passe par le parent.");
        return await RespondAsync(assignmentId, approve, studentUserId, [student.Id], requireAutonomousStudent: true, ct);
    }

    private async Task<LessonCoverageDto> RespondAsync(
        Guid assignmentId,
        bool approve,
        string actorUserId,
        IReadOnlyList<Guid> allowedStudentIds,
        bool requireAutonomousStudent,
        CancellationToken ct)
    {
        var row = db.LessonCoverageAssignments.FirstOrDefault(c => c.Id == assignmentId)
            ?? throw new InvalidOperationException("Proposition introuvable.");
        if (row.Status != LessonCoverageStatus.Pending)
            throw new InvalidOperationException("Cette proposition a déjà été traitée.");

        var lesson = db.LessonsForAnyTenant.FirstOrDefault(l => l.Id == row.LessonId)
            ?? throw new InvalidOperationException("Séance introuvable.");
        if (lesson.StartTime <= DateTime.UtcNow)
            throw new InvalidOperationException("La séance a déjà commencé — le remplacement n'est plus proposable.");

        var studentOnLesson = db.LessonAttendancesForAnyTenant.Any(a =>
            a.LessonId == lesson.Id && allowedStudentIds.Contains(a.StudentId));
        if (!studentOnLesson)
            throw new InvalidOperationException("Vous n'êtes pas concerné par cette séance.");

        row.Status = approve ? LessonCoverageStatus.Approved : LessonCoverageStatus.Rejected;
        row.RespondedAt = DateTime.UtcNow;
        row.RespondedByUserId = actorUserId;
        row.UpdatedAt = DateTime.UtcNow;
        lesson.DeliveredByTenantId = approve ? row.SubstituteTenantId : null;
        lesson.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await NotifyTeachersOfDecisionAsync(row, lesson, approve, ct);
        _ = requireAutonomousStudent;
        return MapAssignments([row], [lesson]).First();
    }

    private List<Guid> ChildIdsForParent(string parentUserId)
    {
        var parent = db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId);
        if (parent is null) return [];
        return db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .Select(s => s.Id)
            .ToList();
    }

    private IReadOnlyList<LessonCoverageDto> PendingForStudents(IReadOnlyList<Guid> studentIds)
    {
        if (studentIds.Count == 0) return [];
        var lessonIds = db.LessonAttendancesForAnyTenant
            .Where(a => studentIds.Contains(a.StudentId))
            .Select(a => a.LessonId)
            .Distinct()
            .ToList();
        var rows = db.LessonCoverageAssignments
            .Where(c => c.Status == LessonCoverageStatus.Pending && lessonIds.Contains(c.LessonId))
            .OrderBy(c => c.CreatedAt)
            .ToList();
        var lessons = db.LessonsForAnyTenant
            .Where(l => rows.Select(r => r.LessonId).Contains(l.Id))
            .ToList();
        return MapAssignments(rows, lessons);
    }

    private async Task NotifyFamiliesAsync(IReadOnlyList<LessonCoverageAssignment> created, CancellationToken ct)
    {
        var original = TeacherName(created[0].OriginalTenantId);
        var substitute = TeacherName(created[0].SubstituteTenantId);
        var lessonIds = created.Select(c => c.LessonId).ToList();
        var lessons = db.LessonsForAnyTenant.Where(l => lessonIds.Contains(l.Id)).ToDictionary(l => l.Id);
        var attendances = db.LessonAttendancesForAnyTenant.Where(a => lessonIds.Contains(a.LessonId)).ToList();
        var studentIds = attendances.Select(a => a.StudentId).Distinct().ToList();
        var students = db.StudentsForAnyTenant.Where(s => studentIds.Contains(s.Id)).ToList();
        var parentIds = students.Where(s => s.ParentProfileId.HasValue).Select(s => s.ParentProfileId!.Value).Distinct().ToList();
        var parents = db.ParentProfilesForAnyTenant.Where(p => parentIds.Contains(p.Id)).ToDictionary(p => p.Id);

        foreach (var row in created)
        {
            if (!lessons.TryGetValue(row.LessonId, out var lesson))
                continue;
            foreach (var studentId in attendances.Where(a => a.LessonId == lesson.Id).Select(a => a.StudentId))
            {
                var student = students.FirstOrDefault(s => s.Id == studentId);
                if (student is null) continue;
                var studentName = $"{student.FirstName} {student.LastName}".Trim();
                try
                {
                    if (student.IsAutonomous && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        await email.SendLessonCoverageProposedAsync(
                            student.Email, studentName, studentName, lesson.Title, lesson.StartTime,
                            original, substitute, row.Reason, PortalUrl("student"), ct);
                    }
                    if (student.ParentProfileId is Guid pid && parents.TryGetValue(pid, out var parent)
                        && !string.IsNullOrWhiteSpace(parent.Email))
                    {
                        await email.SendLessonCoverageProposedAsync(
                            parent.Email, parent.FirstName, studentName, lesson.Title, lesson.StartTime,
                            original, substitute, row.Reason, PortalUrl("parent"), ct);
                    }
                }
                catch
                {
                    // l'affectation est déjà enregistrée
                }
            }
        }
    }

    /// <summary>Le suppléant doit savoir qu'on compte sur lui, avant même l'accord des familles.</summary>
    private async Task NotifySubstituteAsync(IReadOnlyList<LessonCoverageAssignment> created, CancellationToken ct)
    {
        var first = created[0];
        var contact = await TeacherContactAsync(first.SubstituteTenantId, ct);
        if (contact is null) return;
        var lessonIds = created.Select(c => c.LessonId).ToList();
        var firstStart = db.LessonsForAnyTenant
            .Where(l => lessonIds.Contains(l.Id))
            .OrderBy(l => l.StartTime)
            .Select(l => l.StartTime)
            .FirstOrDefault();
        try
        {
            await email.SendLessonCoverageSubstituteAsync(
                contact.Value.Email,
                contact.Value.Name,
                TeacherName(first.OriginalTenantId),
                created.Count,
                firstStart,
                first.Reason,
                $"{urls.WebBaseUrl.TrimEnd('/')}/tutor/calendar",
                ct);
        }
        catch
        {
            // l'affectation est déjà enregistrée
        }
    }

    /// <summary>Les deux enseignants apprennent la décision de la famille : sinon personne ne sait qui assure la séance.</summary>
    private async Task NotifyTeachersOfDecisionAsync(LessonCoverageAssignment row, Lesson lesson, bool approved, CancellationToken ct)
    {
        var original = TeacherName(row.OriginalTenantId);
        var substitute = TeacherName(row.SubstituteTenantId);
        var url = $"{urls.WebBaseUrl.TrimEnd('/')}/tutor/calendar";
        foreach (var tenantId in new[] { row.SubstituteTenantId, row.OriginalTenantId })
        {
            var contact = await TeacherContactAsync(tenantId, ct);
            if (contact is null) continue;
            try
            {
                await email.SendLessonCoverageDecisionAsync(
                    contact.Value.Email, contact.Value.Name, lesson.Title, lesson.StartTime,
                    original, substitute, approved, url, ct);
            }
            catch
            {
                // la décision est déjà enregistrée
            }
        }
    }

    private string TeacherName(Guid tenantId) =>
        db.Tenants.FirstOrDefault(t => t.Id == tenantId)?.Name ?? "l'enseignant";

    private async Task<(string Email, string Name)?> TeacherContactAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        if (tenant is null || string.IsNullOrWhiteSpace(tenant.OwnerUserId)) return null;
        var contact = await contacts.GetAsync(tenant.OwnerUserId, ct);
        if (contact is null || string.IsNullOrWhiteSpace(contact.Value.Email)) return null;
        var name = string.IsNullOrWhiteSpace(contact.Value.DisplayName) ? tenant.Name : contact.Value.DisplayName;
        return (contact.Value.Email, name);
    }

    private string PortalUrl(string space) => $"{urls.WebBaseUrl.TrimEnd('/')}/{space}/coverage";

    private List<Lesson> LoadCandidateLessons(Guid tenantId, DateTime windowStart, DateTime windowEnd)
    {
        var now = DateTime.UtcNow;
        return db.LessonsForAnyTenant
            .Where(l => l.TenantId == tenantId
                        && l.SettlementStatus == LessonSettlementStatus.Scheduled
                        && l.StartTime > now
                        && l.StartTime >= windowStart
                        && l.StartTime < windowEnd)
            .OrderBy(l => l.StartTime)
            .ToList();
    }

    private Dictionary<Guid, LessonCoverageAssignment> LoadCoverages(IEnumerable<Guid> lessonIds)
    {
        var ids = lessonIds.Distinct().ToList();
        return db.LessonCoverageAssignments
            .Where(c => ids.Contains(c.LessonId) && c.Status != LessonCoverageStatus.Cancelled)
            .OrderByDescending(c => c.CreatedAt)
            .ToList()
            .GroupBy(c => c.LessonId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private IReadOnlyList<LessonCoverageDto> MapMany(
        IReadOnlyList<Lesson> lessons, Dictionary<Guid, LessonCoverageAssignment> coverages)
    {
        var assignments = lessons
            .Select(l => coverages.TryGetValue(l.Id, out var c)
                ? c
                : new LessonCoverageAssignment
                {
                    Id = Guid.Empty,
                    LessonId = l.Id,
                    OriginalTenantId = l.TenantId,
                    SubstituteTenantId = Guid.Empty,
                    Reason = "",
                    Status = LessonCoverageStatus.Pending,
                    CreatedAt = l.CreatedAt
                })
            .ToList();
        return MapAssignments(assignments, lessons);
    }

    private IReadOnlyList<LessonCoverageDto> MapAssignments(
        IReadOnlyList<LessonCoverageAssignment> rows, IReadOnlyList<Lesson> lessons)
    {
        var byId = lessons.ToDictionary(l => l.Id);
        var tenantIds = rows.Select(r => r.OriginalTenantId)
            .Concat(rows.Select(r => r.SubstituteTenantId))
            .Concat(lessons.Select(l => l.TenantId))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var names = db.Tenants.Where(t => tenantIds.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Name);
        var lessonIds = rows.Select(r => r.LessonId).Distinct().ToList();
        var attendances = db.LessonAttendancesForAnyTenant.Where(a => lessonIds.Contains(a.LessonId)).ToList();
        var studentIds = attendances.Select(a => a.StudentId).Distinct().ToList();
        var students = db.StudentsForAnyTenant.Where(s => studentIds.Contains(s.Id)).ToDictionary(s => s.Id);

        return rows.Select(r =>
        {
            byId.TryGetValue(r.LessonId, out var lesson);
            names.TryGetValue(r.OriginalTenantId, out var original);
            names.TryGetValue(r.SubstituteTenantId, out var substitute);
            var studentNames = attendances
                .Where(a => a.LessonId == r.LessonId)
                .Select(a => students.TryGetValue(a.StudentId, out var s) ? $"{s.FirstName} {s.LastName}".Trim() : "")
                .Where(n => n.Length > 0)
                .ToList();
            return new LessonCoverageDto(
                r.Id,
                r.LessonId,
                r.OriginalTenantId,
                r.SubstituteTenantId,
                original ?? "Enseignant",
                r.SubstituteTenantId == Guid.Empty ? "—" : substitute ?? "Suppléant",
                lesson?.Title ?? "Séance",
                lesson?.Subject,
                lesson?.StartTime ?? default,
                lesson?.EndTime ?? default,
                r.Reason,
                r.Status.ToString(),
                r.CreatedAt,
                r.RespondedAt,
                r.TransferredTutorAmount,
                studentNames);
        }).ToList();
    }

    private Guid RequireMemberGroupId(string expertUserId, Guid? preferredGroupId)
    {
        var memberships = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            .ToList();

        if (preferredGroupId is Guid gid)
        {
            if (memberships.Any(m => m.ExpertGroupId == gid) || managers.IsActiveManager(expertUserId))
            {
                var group = db.ExpertGroups.FirstOrDefault(g => g.Id == gid && g.IsActive)
                    ?? throw new InvalidOperationException("Groupe introuvable.");
                if (memberships.Any(m => m.ExpertGroupId == gid) || SameManagerGroup(expertUserId, gid))
                    return gid;
            }
        }

        if (memberships.Count > 0)
            return memberships[0].ExpertGroupId;

        if (managers.IsActiveManager(expertUserId))
        {
            var mandate = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
                m.UserId == expertUserId && m.Status == ExpertGroupManagerMandateStatus.Active);
            if (mandate is not null)
                return mandate.ExpertGroupId;
        }

        throw new InvalidOperationException("Accès réservé à un membre actif du groupe d'experts.");
    }

    private bool SameManagerGroup(string userId, Guid groupId)
    {
        return db.ExpertGroupManagerMandates.Any(m =>
            m.UserId == userId
            && m.ExpertGroupId == groupId
            && m.Status == ExpertGroupManagerMandateStatus.Active);
    }

    private HashSet<Guid> GroupTeacherIds(Guid groupId) =>
        db.Tenants
            .Where(t => t.ApprovedByExpertGroupId == groupId)
            .Select(t => t.Id)
            .ToHashSet();

    private void EnsureTeacherInGroup(Guid groupId, Guid tenantId)
    {
        var teacher = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Enseignant introuvable.");
        if (teacher.ApprovedByExpertGroupId != groupId)
            throw new InvalidOperationException("Cet enseignant n'appartient pas à ce groupe d'experts.");
    }
}
