using System.Text.Json;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Documents;
using TutorSphere.Application.DTOs.Homework;
using TutorSphere.Application.DTOs.LessonReports;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IStudentPortalService
{
    Task<StudentDto?> GetMeAsync(string userId, CancellationToken ct = default);
    Task<StudentDto> UpdateMeAsync(string userId, UpdateMyStudentProfileRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LessonDto>> GetLessonsAsync(
        string userId,
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<HomeworkDto>> GetHomeworkAsync(string userId, CancellationToken ct = default);
    Task<HomeworkDto?> GetHomeworkByIdAsync(string userId, Guid homeworkId, CancellationToken ct = default);
    Task<HomeworkDto> SubmitHomeworkAsync(
        string userId,
        Guid homeworkId,
        SubmitHomeworkRequest request,
        CancellationToken ct = default);
    Task<DocumentDto> UploadHomeworkAttachmentAsync(
        string userId,
        Guid homeworkId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(string userId, CancellationToken ct = default);
    Task<DocumentDto?> GetDocumentForStudentAsync(string userId, Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<LessonReportDto>> GetReportsAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDto>> GetTeacherContactsAsync(string userId, CancellationToken ct = default);
}

public class StudentPortalService : IStudentPortalService
{
    private readonly IApplicationDbContext _db;

    public StudentPortalService(IApplicationDbContext db) => _db = db;

    public Task<StudentDto?> GetMeAsync(string userId, CancellationToken ct = default)
    {
        var student = ResolveStudent(userId);
        return Task.FromResult(student is null ? null : MapStudent(student));
    }

    public async Task<StudentDto> UpdateMeAsync(
        string userId,
        UpdateMyStudentProfileRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("Le prénom et le nom sont obligatoires.");

        var student = ResolveStudent(userId);
        if (student is null)
        {
            if (!request.DateOfBirth.HasValue)
                throw new InvalidOperationException(
                    "Profil élève introuvable. Indiquez votre date de naissance pour le créer.");

            var dob = request.DateOfBirth.Value.Date;
            if (dob > DateTime.UtcNow.Date)
                throw new InvalidOperationException("La date de naissance ne peut pas être dans le futur.");

            var age = (int)((DateTime.Today - dob).TotalDays / 365.25);
            if (age < 14)
                throw new InvalidOperationException(
                    "Seuls les élèves de 14 ans et plus peuvent gérer un profil autonome.");

            var tenantId = _db.Tenants.Select(t => t.Id).FirstOrDefault();
            if (tenantId == Guid.Empty)
                throw new InvalidOperationException("Aucune école disponible.");

            var billing = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
            if (billing is null)
            {
                billing = new ParentProfile
                {
                    TenantId = tenantId,
                    UserId = userId,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = string.Empty
                };
                _db.Add(billing);
                await _db.SaveChangesAsync(ct);
            }

            student = new Student
            {
                TenantId = tenantId,
                UserId = userId,
                ParentProfileId = billing.Id,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                DateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc),
                IsActive = true
            };
            _db.Add(student);
            await _db.SaveChangesAsync(ct);
            return MapStudent(student);
        }

        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        if (request.DateOfBirth.HasValue)
        {
            var dob = request.DateOfBirth.Value.Date;
            if (dob > DateTime.UtcNow.Date)
                throw new InvalidOperationException("La date de naissance ne peut pas être dans le futur.");
            student.DateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
        }

        student.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapStudent(student);
    }

    public Task<IReadOnlyList<LessonDto>> GetLessonsAsync(
        string userId,
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken ct = default)
    {
        var student = ResolveStudent(userId);
        if (student is null)
            return Task.FromResult<IReadOnlyList<LessonDto>>([]);

        var lessonIds = _db.LessonAttendancesForAnyTenant
            .Where(a => a.StudentId == student.Id)
            .Select(a => a.LessonId)
            .Distinct()
            .ToList();

        // Agenda actif : exclure les annulations libres (les dates affichées suivent StartTime/EndTime à jour).
        var query = _db.LessonsForAnyTenant.Where(l =>
            lessonIds.Contains(l.Id)
            && l.SettlementStatus != LessonSettlementStatus.CancelledFree);
        if (start.HasValue)
            query = query.Where(l => l.EndTime > start.Value);
        if (end.HasValue)
            query = query.Where(l => l.StartTime < end.Value);

        var lessons = query
            .OrderBy(l => l.StartTime)
            .ToList()
            .Select(MapLesson)
            .ToList();

        return Task.FromResult<IReadOnlyList<LessonDto>>(lessons);
    }

    public Task<IReadOnlyList<HomeworkDto>> GetHomeworkAsync(string userId, CancellationToken ct = default)
    {
        var student = ResolveStudent(userId);
        if (student is null)
            return Task.FromResult<IReadOnlyList<HomeworkDto>>([]);

        var items = _db.HomeworksForAnyTenant
            .Where(h => h.StudentId == student.Id && !h.IsDraft)
            .OrderByDescending(h => h.CreatedAt)
            .ToList()
            .Select(MapHomework)
            .ToList();

        return Task.FromResult<IReadOnlyList<HomeworkDto>>(items);
    }

    public Task<HomeworkDto?> GetHomeworkByIdAsync(string userId, Guid homeworkId, CancellationToken ct = default)
    {
        var student = ResolveStudent(userId);
        if (student is null) return Task.FromResult<HomeworkDto?>(null);

        var homework = _db.HomeworksForAnyTenant
            .FirstOrDefault(h => h.Id == homeworkId && h.StudentId == student.Id && !h.IsDraft);
        return Task.FromResult(homework is null ? null : MapHomework(homework));
    }

    public async Task<HomeworkDto> SubmitHomeworkAsync(
        string userId,
        Guid homeworkId,
        SubmitHomeworkRequest request,
        CancellationToken ct = default)
    {
        var student = ResolveStudent(userId)
            ?? throw new InvalidOperationException("Profil élève introuvable.");

        var homework = _db.HomeworksForAnyTenant.FirstOrDefault(h => h.Id == homeworkId && h.StudentId == student.Id)
            ?? throw new InvalidOperationException("Devoir introuvable.");

        if (homework.IsDraft)
            throw new InvalidOperationException("Ce devoir n'est pas encore publié.");

        if (homework.SubmittedAt.HasValue)
            throw new InvalidOperationException("Ce devoir a déjà été soumis.");

        var allowed = homework.SubmissionModes == HomeworkSubmissionMode.None
            ? HomeworkSubmissionMode.Online
            : homework.SubmissionModes;
        var mode = request.Mode == HomeworkSubmissionMode.None
            ? HomeworkSubmissionMode.Online
            : request.Mode;

        // Une seule méthode à la fois : le bit choisi doit être autorisé.
        if ((allowed & mode) == 0)
            throw new InvalidOperationException("Cette méthode de remise n'est pas autorisée pour ce devoir.");

        var text = request.SubmissionNotes?.Trim();
        var attachments = request.Attachments?
            .Where(a => a.DocumentId != Guid.Empty && !string.IsNullOrWhiteSpace(a.FileName))
            .ToList() ?? [];

        var needsFile = mode is HomeworkSubmissionMode.PaperScan
            or HomeworkSubmissionMode.Video
            or HomeworkSubmissionMode.FileUpload;
        if (needsFile && attachments.Count == 0)
            throw new InvalidOperationException("Ajoutez au moins un fichier pour cette méthode de remise.");

        if (mode == HomeworkSubmissionMode.Online && string.IsNullOrWhiteSpace(text) && attachments.Count == 0)
            throw new InvalidOperationException("Rédigez une réponse ou joignez un fichier.");

        var payload = new HomeworkSubmissionPayload(mode, text, attachments);
        homework.SubmittedAt = DateTime.UtcNow;
        homework.SubmissionNotes = JsonSerializer.Serialize(payload, HomeworkJson.Options);
        homework.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapHomework(homework);
    }

    public async Task<DocumentDto> UploadHomeworkAttachmentAsync(
        string userId,
        Guid homeworkId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        CancellationToken ct = default)
    {
        var student = ResolveStudent(userId)
            ?? throw new InvalidOperationException("Profil élève introuvable.");

        var homework = _db.HomeworksForAnyTenant.FirstOrDefault(h => h.Id == homeworkId && h.StudentId == student.Id)
            ?? throw new InvalidOperationException("Devoir introuvable.");

        if (homework.SubmittedAt.HasValue)
            throw new InvalidOperationException("Ce devoir a déjà été soumis.");

        var doc = new Document
        {
            TenantId = homework.TenantId,
            Name = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileSizeBytes = fileSizeBytes,
            FileUrl = fileUrl,
            UploadedByUserId = userId,
            StudentId = student.Id,
            LessonId = homework.LessonId,
            Folder = "Remises"
        };

        _db.Add(doc);
        await _db.SaveChangesAsync(ct);
        return MapDocument(doc);
    }

    public Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(string userId, CancellationToken ct = default)
    {
        var student = ResolveStudent(userId);
        if (student is null)
            return Task.FromResult<IReadOnlyList<DocumentDto>>([]);

        var lessonIds = _db.LessonAttendancesForAnyTenant
            .Where(a => a.StudentId == student.Id)
            .Select(a => a.LessonId)
            .Distinct()
            .ToList();

        var docs = _db.DocumentsForAnyTenant
            .Where(d => d.StudentId == student.Id
                        || (d.LessonId != null && lessonIds.Contains(d.LessonId.Value)))
            .OrderByDescending(d => d.CreatedAt)
            .ToList()
            .Select(MapDocument)
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentDto>>(docs);
    }

    public async Task<DocumentDto?> GetDocumentForStudentAsync(string userId, Guid documentId, CancellationToken ct = default)
    {
        var docs = await GetDocumentsAsync(userId, ct);
        return docs.FirstOrDefault(d => d.Id == documentId);
    }

    public Task<IReadOnlyList<LessonReportDto>> GetReportsAsync(string userId, CancellationToken ct = default)
    {
        var student = ResolveStudent(userId);
        if (student is null)
            return Task.FromResult<IReadOnlyList<LessonReportDto>>([]);

        var reports = _db.LessonReportsForAnyTenant
            .Where(r => r.StudentId == student.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToList()
            .Select(MapReport)
            .ToList();

        return Task.FromResult<IReadOnlyList<LessonReportDto>>(reports);
    }

    public Task<IReadOnlyList<ConversationDto>> GetTeacherContactsAsync(string userId, CancellationToken ct = default)
    {
        var student = ResolveStudent(userId);
        if (student is null)
            return Task.FromResult<IReadOnlyList<ConversationDto>>([]);

        var tenantIds = new HashSet<Guid> { student.TenantId };
        foreach (var tid in _db.StudentSubscriptionsForAnyTenant
                     .Where(s => s.StudentId == student.Id)
                     .Select(s => s.TenantId)
                     .Distinct())
            tenantIds.Add(tid);

        var owners = _db.Tenants
            .Where(t => tenantIds.Contains(t.Id) && t.OwnerUserId != null && t.OwnerUserId != "")
            .Select(t => new { t.OwnerUserId, t.Name })
            .ToList();

        // Merge with existing message partners who are not already listed
        var existingPartnerIds = _db.Messages
            .Where(m => m.SenderUserId == userId || m.RecipientUserId == userId)
            .AsEnumerable()
            .Select(m => m.SenderUserId == userId ? m.RecipientUserId : m.SenderUserId)
            .Distinct()
            .ToHashSet();

        var contacts = new List<ConversationDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var owner in owners)
        {
            if (string.IsNullOrWhiteSpace(owner.OwnerUserId) || owner.OwnerUserId == userId)
                continue;
            if (!seen.Add(owner.OwnerUserId))
                continue;

            var last = _db.Messages
                .Where(m =>
                    (m.SenderUserId == userId && m.RecipientUserId == owner.OwnerUserId) ||
                    (m.SenderUserId == owner.OwnerUserId && m.RecipientUserId == userId))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            var unread = _db.Messages.Count(m =>
                m.SenderUserId == owner.OwnerUserId && m.RecipientUserId == userId && !m.IsRead);

            contacts.Add(new ConversationDto(
                owner.OwnerUserId,
                string.IsNullOrWhiteSpace(owner.Name) ? "Enseignant" : $"Enseignant — {owner.Name}",
                last is null
                    ? null
                    : new MessageDto(
                        last.Id,
                        last.SenderUserId,
                        last.RecipientUserId,
                        last.Subject,
                        last.Body,
                        last.IsRead,
                        last.ReadAt,
                        last.CreatedAt),
                unread));
        }

        // Keep chat history with partners already messaged even if not tenant owner
        foreach (var partnerId in existingPartnerIds)
        {
            if (partnerId == userId || !seen.Add(partnerId))
                continue;

            var last = _db.Messages
                .Where(m =>
                    (m.SenderUserId == userId && m.RecipientUserId == partnerId) ||
                    (m.SenderUserId == partnerId && m.RecipientUserId == userId))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            contacts.Add(new ConversationDto(
                partnerId,
                "Enseignant",
                last is null
                    ? null
                    : new MessageDto(
                        last.Id,
                        last.SenderUserId,
                        last.RecipientUserId,
                        last.Subject,
                        last.Body,
                        last.IsRead,
                        last.ReadAt,
                        last.CreatedAt),
                _db.Messages.Count(m =>
                    m.SenderUserId == partnerId && m.RecipientUserId == userId && !m.IsRead)));
        }

        return Task.FromResult<IReadOnlyList<ConversationDto>>(contacts);
    }

    private Student? ResolveStudent(string userId) =>
        _db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == userId && s.IsActive);

    private StudentDto MapStudent(Student s)
    {
        string? parentName = null;
        if (s.ParentProfileId is Guid pid)
        {
            var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.Id == pid);
            if (parent is not null)
                parentName = $"{parent.FirstName} {parent.LastName}".Trim();
        }

        return new StudentDto(
            s.Id,
            s.FirstName,
            s.LastName,
            s.Email,
            s.Phone,
            s.DateOfBirth,
            s.Age,
            s.IsMinor,
            s.IsAutonomous,
            s.ParentProfileId,
            parentName,
            s.PhotoUrl,
            s.SchoolLevel,
            s.SchoolName,
            ParseSubjects(s.Subjects),
            s.Notes,
            s.IsActive,
            s.CreatedAt,
            !string.IsNullOrEmpty(s.UserId),
            s.LoginAccessCode);
    }

    private static IReadOnlyList<string> ParseSubjects(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static LessonDto MapLesson(Lesson l) => new(
        l.Id, l.Title, l.Description, l.Subject, l.StartTime, l.EndTime,
        l.Mode.ToString(), l.Location, l.MeetingUrl, l.SessionNotes, l.CreatedAt, l.UpdatedAt,
        l.SettlementStatus.ToString(), l.CancelledAt, l.SessionCounted, l.TutorLiable, l.TutorLiabilityResolution);

    private static HomeworkDto MapHomework(Homework h) => HomeworkService.MapPublic(h);

    private static DocumentDto MapDocument(Document d) => new(
        d.Id,
        d.Name,
        d.FileSizeBytes,
        d.ContentType,
        d.CreatedAt,
        d.StudentId,
        d.LessonId,
        d.FileUrl);

    private static LessonReportDto MapReport(LessonReport r) => new(
        r.Id, r.TenantId, r.LessonId, r.StudentId, r.TopicsStudied, r.Participation,
        r.Strengths, r.Weaknesses, r.HomeworkAssigned, r.Observations,
        r.SentToParent, r.SentAt, r.CreatedAt, r.UpdatedAt);
}
