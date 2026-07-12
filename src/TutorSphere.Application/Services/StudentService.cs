using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IStudentService
{
    Task<IReadOnlyList<StudentDto>> GetAllAsync(CancellationToken ct = default);
    Task<StudentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StudentDto?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<StudentDto> CreateAsync(CreateStudentRequest request, CancellationToken ct = default);
    Task<StudentDto> UpdateAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LessonDto>> GetLessonsAsync(Guid studentId, CancellationToken ct = default);
}

public class StudentService : IStudentService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public StudentService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<IReadOnlyList<StudentDto>> GetAllAsync(CancellationToken ct = default)
    {
        var parents = _db.ParentProfiles.ToDictionary(p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());
        var students = _db.Students
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList()
            .Select(s => MapToDto(s, parents.GetValueOrDefault(s.ParentProfileId)))
            .ToList();
        return Task.FromResult<IReadOnlyList<StudentDto>>(students);
    }

    public Task<StudentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var student = _db.Students.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(student is null ? null : MapToDto(student, ResolveParentName(student.ParentProfileId)));
    }

    public Task<StudentDto?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var student = _db.Students.FirstOrDefault(s => s.UserId == userId);
        return Task.FromResult(student is null ? null : MapToDto(student, ResolveParentName(student.ParentProfileId)));
    }

    public async Task<StudentDto> CreateAsync(CreateStudentRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var student = new Student
        {
            TenantId = tenantId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            DateOfBirth = request.DateOfBirth,
            ParentProfileId = request.ParentProfileId ?? Guid.Empty,
            SchoolLevel = request.SchoolLevel?.Trim(),
            SchoolName = request.SchoolName?.Trim(),
            Subjects = request.Subjects?.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true
        };

        _db.Add(student);
        await _db.SaveChangesAsync(ct);
        return MapToDto(student, ResolveParentName(student.ParentProfileId));
    }

    public async Task<StudentDto> UpdateAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default)
    {
        var student = _db.Students.FirstOrDefault(s => s.Id == id)
            ?? throw new InvalidOperationException("Étudiant introuvable.");

        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.Email = request.Email?.Trim();
        student.Phone = request.Phone?.Trim();
        student.DateOfBirth = request.DateOfBirth;
        if (request.ParentProfileId.HasValue)
            student.ParentProfileId = request.ParentProfileId.Value;
        student.SchoolLevel = request.SchoolLevel?.Trim();
        student.SchoolName = request.SchoolName?.Trim();
        student.Subjects = request.Subjects?.Trim();
        student.Notes = request.Notes?.Trim();
        if (request.IsActive.HasValue)
            student.IsActive = request.IsActive.Value;
        student.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(student, ResolveParentName(student.ParentProfileId));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var student = _db.Students.FirstOrDefault(s => s.Id == id)
            ?? throw new InvalidOperationException("Étudiant introuvable.");

        _db.Remove(student);
        await _db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<LessonDto>> GetLessonsAsync(Guid studentId, CancellationToken ct = default)
    {
        var lessonIds = _db.LessonAttendances
            .Where(a => a.StudentId == studentId)
            .Select(a => a.LessonId)
            .ToHashSet();

        var lessons = _db.Lessons
            .Where(l => lessonIds.Contains(l.Id))
            .OrderByDescending(l => l.StartTime)
            .ToList()
            .Select(MapLessonToDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<LessonDto>>(lessons);
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");
        return _tenantContext.TenantId.Value;
    }

    private string? ResolveParentName(Guid parentProfileId)
    {
        if (parentProfileId == Guid.Empty) return null;
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == parentProfileId);
        return parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
    }

    private static StudentDto MapToDto(Student s, string? parentName = null) => new(
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
        s.CreatedAt);

    private static LessonDto MapLessonToDto(Lesson l) => new(
        l.Id,
        l.Title,
        l.Description,
        l.Subject,
        l.StartTime,
        l.EndTime,
        l.Mode.ToString(),
        l.Location,
        l.MeetingUrl,
        l.SessionNotes,
        l.CreatedAt,
        l.UpdatedAt);

    private static IReadOnlyList<string> ParseSubjects(string? subjects) =>
        string.IsNullOrWhiteSpace(subjects)
            ? []
            : subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
