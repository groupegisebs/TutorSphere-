using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.LessonReports;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public class LessonReportService : ILessonReportService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IEmailService _email;
    private readonly ILogger<LessonReportService> _logger;

    public LessonReportService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        IEmailService email,
        ILogger<LessonReportService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _logger = logger;
    }

    public async Task<LessonReportDto> CreateAsync(CreateLessonReportRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        await EnsureLessonExistsAsync(request.LessonId, ct);
        await EnsureStudentExistsAsync(request.StudentId, ct);

        if (_db.LessonReports.Any(r => r.LessonId == request.LessonId && r.StudentId == request.StudentId))
            throw new InvalidOperationException("Un rapport existe déjà pour cette leçon et cet élève.");

        var report = new LessonReport
        {
            TenantId = tenantId,
            LessonId = request.LessonId,
            StudentId = request.StudentId,
            TopicsStudied = request.TopicsStudied?.Trim(),
            Participation = request.Participation?.Trim(),
            Strengths = request.Strengths?.Trim(),
            Weaknesses = request.Weaknesses?.Trim(),
            HomeworkAssigned = request.HomeworkAssigned?.Trim(),
            Observations = request.Observations?.Trim()
        };

        _db.Add(report);
        await _db.SaveChangesAsync(ct);

        return MapToDto(report);
    }

    public Task<LessonReportDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var report = _db.LessonReports.FirstOrDefault(r => r.Id == id);
        return Task.FromResult(report is null ? null : MapToDto(report));
    }

    public Task<IReadOnlyList<LessonReportDto>> GetByLessonAsync(Guid lessonId, CancellationToken ct = default)
    {
        var items = _db.LessonReports
            .Where(r => r.LessonId == lessonId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDto(r))
            .ToList();

        return Task.FromResult<IReadOnlyList<LessonReportDto>>(items);
    }

    public Task<IReadOnlyList<LessonReportDto>> GetByStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        var items = _db.LessonReports
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDto(r))
            .ToList();

        return Task.FromResult<IReadOnlyList<LessonReportDto>>(items);
    }

    public async Task<LessonReportDto> UpdateAsync(Guid id, UpdateLessonReportRequest request, CancellationToken ct = default)
    {
        var report = GetReportOrThrow(id);

        report.TopicsStudied = request.TopicsStudied?.Trim();
        report.Participation = request.Participation?.Trim();
        report.Strengths = request.Strengths?.Trim();
        report.Weaknesses = request.Weaknesses?.Trim();
        report.HomeworkAssigned = request.HomeworkAssigned?.Trim();
        report.Observations = request.Observations?.Trim();
        report.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(report);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var report = GetReportOrThrow(id);
        _db.Remove(report);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<LessonReportDto> MarkSentToParentAsync(Guid id, CancellationToken ct = default)
    {
        var report = GetReportOrThrow(id);

        if (report.SentToParent)
            throw new InvalidOperationException("Ce rapport a déjà été envoyé au parent.");

        report.SentToParent = true;
        report.SentAt = DateTime.UtcNow;
        report.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await SendReportEmailToParentAsync(report, ct);

        return MapToDto(report);
    }

    private async Task SendReportEmailToParentAsync(LessonReport report, CancellationToken ct)
    {
        var student = _db.Students.FirstOrDefault(s => s.Id == report.StudentId);
        if (student is null)
        {
            _logger.LogWarning("Rapport {Id} : élève introuvable pour l'envoi de l'e-mail.", report.Id);
            return;
        }

        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == student.ParentProfileId);
        if (parent is null || string.IsNullOrWhiteSpace(parent.Email))
        {
            _logger.LogWarning("Rapport {Id} : parent sans e-mail, envoi ignoré.", report.Id);
            return;
        }

        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == report.TenantId);
        var tutorName = tenant?.Name ?? "Votre tuteur";

        await _email.SendLessonReportToParentAsync(
            parentEmail: parent.Email,
            parentFirstName: parent.FirstName,
            studentName: $"{student.FirstName} {student.LastName}",
            tutorName: tutorName,
            ct: ct);
    }

    private LessonReport GetReportOrThrow(Guid id)
    {
        RequireTenantId();
        return _db.LessonReports.FirstOrDefault(r => r.Id == id)
            ?? throw new InvalidOperationException("Rapport pédagogique introuvable.");
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

    private static LessonReportDto MapToDto(LessonReport report) => new(
        report.Id,
        report.TenantId,
        report.LessonId,
        report.StudentId,
        report.TopicsStudied,
        report.Participation,
        report.Strengths,
        report.Weaknesses,
        report.HomeworkAssigned,
        report.Observations,
        report.SentToParent,
        report.SentAt,
        report.CreatedAt,
        report.UpdatedAt);
}
