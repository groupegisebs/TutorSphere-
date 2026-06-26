using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Parents;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IParentService
{
    Task<IReadOnlyList<ParentDto>> GetAllAsync(CancellationToken ct = default);
    Task<ParentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ParentDto?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<ParentDto> CreateAsync(CreateParentRequest request, CancellationToken ct = default);
    Task<ParentDto> UpdateAsync(Guid id, UpdateParentRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StudentDto>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentDto>> GetChildrenForUserAsync(string userId, CancellationToken ct = default);
    Task<StudentDto> AddChildForUserAsync(string userId, ParentAddChildRequest request, CancellationToken ct = default);
}

public class ParentService : IParentService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ParentService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<IReadOnlyList<ParentDto>> GetAllAsync(CancellationToken ct = default)
    {
        var parents = _db.ParentProfiles
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToList()
            .Select(p => MapToDto(p, _db.Students.Count(s => s.ParentProfileId == p.Id)))
            .ToList();
        return Task.FromResult<IReadOnlyList<ParentDto>>(parents);
    }

    public Task<ParentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == id);
        if (parent is null) return Task.FromResult<ParentDto?>(null);
        var count = _db.Students.Count(s => s.ParentProfileId == id);
        return Task.FromResult<ParentDto?>(MapToDto(parent, count));
    }

    public Task<ParentDto?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.UserId == userId);
        if (parent is null) return Task.FromResult<ParentDto?>(null);
        var count = _db.Students.Count(s => s.ParentProfileId == parent.Id);
        return Task.FromResult<ParentDto?>(MapToDto(parent, count));
    }

    public async Task<ParentDto> CreateAsync(CreateParentRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var parent = new ParentProfile
        {
            TenantId = tenantId,
            UserId = string.Empty,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim()
        };

        _db.Add(parent);
        await _db.SaveChangesAsync(ct);
        return MapToDto(parent, 0);
    }

    public async Task<ParentDto> UpdateAsync(Guid id, UpdateParentRequest request, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == id)
            ?? throw new InvalidOperationException("Parent introuvable.");

        parent.FirstName = request.FirstName.Trim();
        parent.LastName = request.LastName.Trim();
        parent.Email = request.Email.Trim();
        parent.Phone = request.Phone?.Trim();
        parent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        var count = _db.Students.Count(s => s.ParentProfileId == id);
        return MapToDto(parent, count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == id)
            ?? throw new InvalidOperationException("Parent introuvable.");

        _db.Remove(parent);
        await _db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<StudentDto>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        var children = _db.Students
            .Where(s => s.ParentProfileId == parentId)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList()
            .Select(MapStudentToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<StudentDto>>(children);
    }

    public async Task<IReadOnlyList<StudentDto>> GetChildrenForUserAsync(string userId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return [];

        return await GetChildrenAsync(parent.Id, ct);
    }

    public async Task<StudentDto> AddChildForUserAsync(string userId, ParentAddChildRequest request, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.UserId == userId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("Le prénom et le nom sont obligatoires.");

        var student = new Student
        {
            TenantId = parent.TenantId,
            ParentProfileId = parent.Id,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email?.Trim(),
            DateOfBirth = request.DateOfBirth,
            SchoolLevel = request.SchoolLevel?.Trim(),
            SchoolName = request.SchoolName?.Trim(),
            Subjects = request.Subjects?.Trim(),
            IsActive = true
        };

        _db.Add(student);
        await _db.SaveChangesAsync(ct);
        return MapStudentToDto(student);
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");
        return _tenantContext.TenantId.Value;
    }

    private static ParentDto MapToDto(ParentProfile p, int childrenCount) => new(
        p.Id,
        p.FirstName,
        p.LastName,
        p.Email,
        p.Phone,
        childrenCount);

    private static StudentDto MapStudentToDto(Student s) => new(
        s.Id,
        s.FirstName,
        s.LastName,
        s.Email,
        s.DateOfBirth,
        s.Age,
        s.IsMinor,
        s.IsAutonomous,
        s.ParentProfileId,
        null,
        s.SchoolLevel,
        s.SchoolName,
        ParseSubjects(s.Subjects));

    private static IReadOnlyList<string> ParseSubjects(string? subjects) =>
        string.IsNullOrWhiteSpace(subjects)
            ? []
            : subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
