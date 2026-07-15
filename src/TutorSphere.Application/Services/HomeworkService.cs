using System.Text.Json;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Homework;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public class HomeworkService : IHomeworkService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public HomeworkService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<HomeworkDto> CreateAsync(CreateHomeworkRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        await EnsureStudentExistsAsync(request.StudentId, ct);

        if (request.LessonId.HasValue)
            await EnsureLessonExistsAsync(request.LessonId.Value, ct);

        var homework = BuildEntity(tenantId, request.StudentId, request.LessonId, request.AssignmentGroupId,
            request.Title, request.Description, request.DueDate, request.Subject, request.Instructions,
            request.EstimatedMinutes, request.SubmissionModes, request.Content, request.Criteria, request.IsDraft);

        _db.Add(homework);
        await _db.SaveChangesAsync(ct);
        return MapToDto(homework);
    }

    public async Task<IReadOnlyList<HomeworkDto>> CreateBatchAsync(
        CreateHomeworkBatchRequest request,
        CancellationToken ct = default)
    {
        if (request.StudentIds is null || request.StudentIds.Count == 0)
            throw new InvalidOperationException("Sélectionnez au moins un élève.");

        var tenantId = RequireTenantId();
        var groupId = Guid.NewGuid();
        var created = new List<Homework>();

        foreach (var studentId in request.StudentIds.Distinct())
        {
            await EnsureStudentExistsAsync(studentId, ct);
            var homework = BuildEntity(tenantId, studentId, request.LessonId, groupId,
                request.Title, request.Description, request.DueDate, request.Subject, request.Instructions,
                request.EstimatedMinutes, request.SubmissionModes, request.Content, request.Criteria, request.IsDraft);
            _db.Add(homework);
            created.Add(homework);
        }

        await _db.SaveChangesAsync(ct);
        return created.Select(MapToDto).ToList();
    }

    public Task<HomeworkDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var homework = _db.Homeworks.FirstOrDefault(h => h.Id == id);
        return Task.FromResult(homework is null ? null : MapToDto(homework));
    }

    public Task<IReadOnlyList<HomeworkDto>> GetByStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        var items = _db.Homeworks
            .Where(h => h.StudentId == studentId && !h.IsDraft)
            .OrderByDescending(h => h.CreatedAt)
            .ToList()
            .Select(MapToDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<HomeworkDto>>(items);
    }

    public Task<IReadOnlyList<HomeworkDto>> GetForCurrentTenantAsync(CancellationToken ct = default)
    {
        RequireTenantId();
        var items = _db.Homeworks
            .OrderByDescending(h => h.CreatedAt)
            .ToList()
            .Select(MapToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<HomeworkDto>>(items);
    }

    public async Task<HomeworkDto> UpdateAsync(Guid id, UpdateHomeworkRequest request, CancellationToken ct = default)
    {
        var homework = GetHomeworkOrThrow(id);

        homework.Title = request.Title.Trim();
        homework.Description = request.Description?.Trim();
        homework.DueDate = request.DueDate;
        if (request.Subject is not null) homework.Subject = request.Subject.Trim();
        if (request.Instructions is not null) homework.Instructions = request.Instructions.Trim();
        if (request.EstimatedMinutes.HasValue) homework.EstimatedMinutes = request.EstimatedMinutes;
        if (request.SubmissionModes.HasValue) homework.SubmissionModes = request.SubmissionModes.Value;
        if (request.Content is not null) homework.ContentJson = Serialize(request.Content);
        if (request.Criteria is not null) homework.CriteriaJson = Serialize(request.Criteria);
        if (request.IsDraft.HasValue) homework.IsDraft = request.IsDraft.Value;
        homework.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(homework);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var homework = GetHomeworkOrThrow(id);
        _db.Remove(homework);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HomeworkDto> SubmitAsync(Guid id, SubmitHomeworkRequest request, CancellationToken ct = default)
    {
        var homework = GetHomeworkOrThrow(id);

        if (homework.SubmittedAt.HasValue)
            throw new InvalidOperationException("Ce devoir a déjà été soumis.");

        var allowed = homework.SubmissionModes == HomeworkSubmissionMode.None
            ? HomeworkSubmissionMode.Online
            : homework.SubmissionModes;
        var mode = request.Mode == HomeworkSubmissionMode.None
            ? HomeworkSubmissionMode.Online
            : request.Mode;
        if ((allowed & mode) == 0)
            throw new InvalidOperationException("Cette méthode de remise n'est pas autorisée pour ce devoir.");

        var text = request.SubmissionNotes?.Trim();
        var attachments = request.Attachments?
            .Where(a => a.DocumentId != Guid.Empty && !string.IsNullOrWhiteSpace(a.FileName))
            .ToList() ?? [];

        var payload = new HomeworkSubmissionPayload(mode, text, attachments);
        homework.SubmittedAt = DateTime.UtcNow;
        homework.SubmissionNotes = JsonSerializer.Serialize(payload, JsonOpts);
        homework.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(homework);
    }

    public async Task<HomeworkDto> GradeAsync(Guid id, GradeHomeworkRequest request, CancellationToken ct = default)
    {
        var homework = GetHomeworkOrThrow(id);

        if (request.Grade is < 0 or > 100)
            throw new InvalidOperationException("La note doit être entre 0 et 100.");

        homework.Grade = request.Grade;
        homework.Feedback = request.Feedback?.Trim();
        homework.IsGraded = true;
        homework.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(homework);
    }

    private static Homework BuildEntity(
        Guid tenantId,
        Guid studentId,
        Guid? lessonId,
        Guid? groupId,
        string title,
        string? description,
        DateTime? dueDate,
        string? subject,
        string? instructions,
        int? estimatedMinutes,
        HomeworkSubmissionMode modes,
        IReadOnlyList<HomeworkContentBlockDto>? content,
        IReadOnlyList<HomeworkCriterionDto>? criteria,
        bool isDraft) => new()
    {
        TenantId = tenantId,
        StudentId = studentId,
        LessonId = lessonId,
        AssignmentGroupId = groupId,
        Title = title.Trim(),
        Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
        Description = description?.Trim(),
        Instructions = instructions?.Trim(),
        DueDate = dueDate,
        EstimatedMinutes = estimatedMinutes,
        SubmissionModes = modes == HomeworkSubmissionMode.None ? HomeworkSubmissionMode.Online : modes,
        ContentJson = Serialize(content ?? []),
        CriteriaJson = Serialize(criteria ?? []),
        IsDraft = isDraft
    };

    private Homework GetHomeworkOrThrow(Guid id)
    {
        RequireTenantId();
        return _db.Homeworks.FirstOrDefault(h => h.Id == id)
            ?? throw new InvalidOperationException("Devoir introuvable.");
    }

    private Guid RequireTenantId() =>
        _tenantContext.TenantId
        ?? throw new InvalidOperationException("Le contexte locataire est requis.");

    private Task EnsureStudentExistsAsync(Guid studentId, CancellationToken ct)
    {
        if (!_db.Students.Any(s => s.Id == studentId))
            throw new InvalidOperationException("Élève introuvable.");
        return Task.CompletedTask;
    }

    private Task EnsureLessonExistsAsync(Guid lessonId, CancellationToken ct)
    {
        if (!_db.Lessons.Any(l => l.Id == lessonId))
            throw new InvalidOperationException("Leçon introuvable.");
        return Task.CompletedTask;
    }

    private static string? Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOpts);

    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? []; }
        catch { return []; }
    }

    private static HomeworkDto MapToDto(Homework homework) => MapPublic(homework);

    public static HomeworkDto MapPublic(Homework homework) => new(
        homework.Id,
        homework.TenantId,
        homework.StudentId,
        homework.LessonId,
        homework.AssignmentGroupId,
        homework.Title,
        homework.Subject,
        homework.Description,
        homework.Instructions,
        homework.DueDate,
        homework.EstimatedMinutes,
        homework.SubmissionModes,
        DeserializeList<HomeworkContentBlockDto>(homework.ContentJson),
        DeserializeList<HomeworkCriterionDto>(homework.CriteriaJson),
        homework.IsDraft,
        homework.SubmittedAt,
        homework.SubmissionNotes,
        homework.Grade,
        homework.Feedback,
        homework.IsGraded,
        homework.CreatedAt,
        homework.UpdatedAt);
}
