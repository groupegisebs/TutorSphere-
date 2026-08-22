using TutorSphere.Application.Common;
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
        var students = _db.Students
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList();
        var parentIds = students
            .Where(s => s.ParentProfileId.HasValue)
            .Select(s => s.ParentProfileId!.Value)
            .Distinct()
            .ToList();
        var parents = parentIds.Count == 0
            ? new Dictionary<Guid, ParentProfile>()
            : _db.ParentProfilesForAnyTenant
                .Where(p => parentIds.Contains(p.Id))
                .ToDictionary(p => p.Id);

        var dtos = students
            .Select(s =>
            {
                parents.TryGetValue(s.ParentProfileId ?? Guid.Empty, out var parent);
                var parentName = parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
                return MapToDto(s, parentName, parent?.Country);
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<StudentDto>>(dtos);
    }

    public Task<StudentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var student = _db.Students.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(student is null ? null : MapWithParent(student));
    }

    public Task<StudentDto?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var student = _db.Students.FirstOrDefault(s => s.UserId == userId);
        return Task.FromResult(student is null ? null : MapWithParent(student));
    }

    public async Task<StudentDto> CreateAsync(CreateStudentRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();

        Guid? parentId = request.ParentProfileId;
        if (parentId is Guid pid && pid != Guid.Empty)
        {
            var parentExists = _db.ParentProfiles.Any(p => p.Id == pid);
            if (!parentExists)
                throw new InvalidOperationException("Parent introuvable dans votre espace.");
        }
        else
        {
            parentId = null;
        }

        var student = new Student
        {
            TenantId = tenantId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            DateOfBirth = request.DateOfBirth,
            ParentProfileId = parentId,
            SchoolLevel = request.SchoolLevel?.Trim(),
            SchoolName = request.SchoolName?.Trim(),
            Subjects = request.Subjects?.Trim(),
            Notes = request.Notes?.Trim(),
            Country = FamilyResidence.TryIso(ResolveParentCountry(parentId)),
            IsActive = true
        };

        _db.Add(student);
        await _db.SaveChangesAsync(ct);
        return MapWithParent(student);
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
        {
            var pid = request.ParentProfileId.Value;
            if (pid == Guid.Empty)
            {
                student.ParentProfileId = null;
            }
            else
            {
                if (!_db.ParentProfiles.Any(p => p.Id == pid))
                    throw new InvalidOperationException("Parent introuvable dans votre espace.");
                student.ParentProfileId = pid;
            }
        }
        student.SchoolLevel = request.SchoolLevel?.Trim();
        student.SchoolName = request.SchoolName?.Trim();
        student.Subjects = request.Subjects?.Trim();
        student.Notes = request.Notes?.Trim();
        if (request.IsActive.HasValue)
            student.IsActive = request.IsActive.Value;
        student.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapWithParent(student);
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

    private ParentProfile? FindParent(Guid? parentProfileId)
    {
        if (parentProfileId is null || parentProfileId == Guid.Empty) return null;
        return _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.Id == parentProfileId.Value);
    }

    private string? ResolveParentCountry(Guid? parentProfileId) =>
        FindParent(parentProfileId)?.Country;

    private StudentDto MapWithParent(Student s)
    {
        var parent = FindParent(s.ParentProfileId);
        var parentName = parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
        return MapToDto(s, parentName, parent?.Country);
    }

    private static StudentDto MapToDto(Student s, string? parentName = null, string? parentCountry = null) => new(
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
        null,
        FamilyResidence.EffectiveChildCountry(s.Country, parentCountry));

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
        l.UpdatedAt,
        l.SettlementStatus.ToString(),
        l.CancelledAt,
        l.SessionCounted,
        l.TutorLiable,
        l.TutorLiabilityResolution);

    private static IReadOnlyList<string> ParseSubjects(string? subjects) =>
        string.IsNullOrWhiteSpace(subjects)
            ? []
            : subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
