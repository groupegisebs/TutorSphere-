using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Homework;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public class HomeworkService : IHomeworkService
{
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

        var homework = new Homework
        {
            TenantId = tenantId,
            StudentId = request.StudentId,
            LessonId = request.LessonId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            DueDate = request.DueDate
        };

        _db.Add(homework);
        await _db.SaveChangesAsync(ct);

        return MapToDto(homework);
    }

    public Task<HomeworkDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var homework = _db.Homeworks.FirstOrDefault(h => h.Id == id);
        return Task.FromResult(homework is null ? null : MapToDto(homework));
    }

    public Task<IReadOnlyList<HomeworkDto>> GetByStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        var items = _db.Homeworks
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => MapToDto(h))
            .ToList();

        return Task.FromResult<IReadOnlyList<HomeworkDto>>(items);
    }

    public async Task<HomeworkDto> UpdateAsync(Guid id, UpdateHomeworkRequest request, CancellationToken ct = default)
    {
        var homework = GetHomeworkOrThrow(id);

        homework.Title = request.Title.Trim();
        homework.Description = request.Description?.Trim();
        homework.DueDate = request.DueDate;
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

        homework.SubmittedAt = DateTime.UtcNow;
        homework.SubmissionNotes = request.SubmissionNotes?.Trim();
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

    private static HomeworkDto MapToDto(Homework homework) => new(
        homework.Id,
        homework.TenantId,
        homework.StudentId,
        homework.LessonId,
        homework.Title,
        homework.Description,
        homework.DueDate,
        homework.SubmittedAt,
        homework.SubmissionNotes,
        homework.Grade,
        homework.Feedback,
        homework.IsGraded,
        homework.CreatedAt,
        homework.UpdatedAt);
}
