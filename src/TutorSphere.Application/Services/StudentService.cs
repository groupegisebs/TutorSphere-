using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IStudentService
{
    Task<IReadOnlyList<StudentDto>> GetAllAsync(CancellationToken ct = default);
    Task<StudentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
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
        var students = _db.Students
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList()
            .Select(MapToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<StudentDto>>(students);
    }

    public Task<StudentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var student = _db.Students.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(student is null ? null : MapToDto(student));
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
            DateOfBirth = request.DateOfBirth,
            ParentProfileId = request.ParentProfileId ?? Guid.Empty
        };

        _db.Add(student);
        await _db.SaveChangesAsync(ct);
        return MapToDto(student);
    }

    public async Task<StudentDto> UpdateAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default)
    {
        var student = _db.Students.FirstOrDefault(s => s.Id == id)
            ?? throw new InvalidOperationException("Étudiant introuvable.");

        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.Email = request.Email?.Trim();
        student.DateOfBirth = request.DateOfBirth;
        if (request.ParentProfileId.HasValue)
            student.ParentProfileId = request.ParentProfileId.Value;
        student.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(student);
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

    private static StudentDto MapToDto(Student s) => new(
        s.Id,
        s.FirstName,
        s.LastName,
        s.Email,
        s.DateOfBirth,
        s.Age,
        s.IsMinor,
        s.IsAutonomous,
        s.ParentProfileId,
        null);

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
}
