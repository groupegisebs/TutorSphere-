namespace TutorSphere.Web.Services;

public sealed class AdminService
{
    private readonly ApiClient _api;

    public AdminService(ApiClient api) => _api = api;

    public async Task<List<AdminUserItem>> GetUsersAsync(string? role = null, string? q = null)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(role)) qs.Add($"role={Uri.EscapeDataString(role)}");
        if (!string.IsNullOrWhiteSpace(q)) qs.Add($"q={Uri.EscapeDataString(q)}");
        var path = qs.Count == 0 ? "api/admin/users" : $"api/admin/users?{string.Join("&", qs)}";
        return await _api.GetAsync<List<AdminUserItem>>(path) ?? [];
    }

    public async Task<AdminUserDetail?> GetUserAsync(string userId)
        => await _api.GetAsync<AdminUserDetail>($"api/admin/users/{userId}");

    public async Task<bool> ActivateUserAsync(string userId)
        => await _api.PostAsync<AdminActionResult>($"api/admin/users/{userId}/activate", new { }) is not null;

    public async Task<bool> DeactivateUserAsync(string userId)
        => await _api.PostAsync<AdminActionResult>($"api/admin/users/{userId}/deactivate", new { }) is not null;

    public async Task<bool> ResetPasswordAsync(string userId)
        => await _api.PostAsync<AdminActionResult>($"api/admin/users/{userId}/reset-password", new { }) is not null;

    public async Task<AdminStats?> GetStatsAsync()
        => await _api.GetAsync<AdminStats>("api/admin/stats");

    public async Task<List<AdminSchoolItem>> GetSchoolsAsync()
        => await _api.GetAsync<List<AdminSchoolItem>>("api/admin/schools") ?? [];
}

public sealed record AdminUserItem(
    string Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    string? Phone = null,
    string? Country = null,
    string? City = null,
    string? SchoolName = null,
    Guid? TenantId = null,
    DateTime? CreatedAt = null,
    DateTime? LastLoginAt = null);

public sealed record AdminUserDetail(
    string Id,
    string Email,
    string FullName,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    string? Phone,
    string? Country,
    string? City,
    string? SchoolName,
    Guid? TenantId,
    string PreferredLanguage,
    string TimeZone,
    DateTime? CreatedAt,
    DateTime? LastLoginAt);

public sealed record AdminSchoolItem(
    Guid Id,
    string Name,
    string Slug,
    string? Country,
    string? City,
    string Status,
    string Plan,
    int StudentCount,
    int TeacherCount,
    DateTime CreatedAt);

public sealed record AdminCountryStat(string Country, int Count);
public sealed record AdminTopSchool(Guid Id, string Name, string? Country, int StudentCount);
public sealed record AdminRecentUser(
    string Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string? Country,
    string? SchoolName);

public sealed record AdminStats(
    int TotalUsers,
    int TotalTutors,
    int TotalParents,
    int TotalStudents,
    int TotalTeachers,
    int TotalSchools,
    int ActiveCourses,
    int ActiveUsers,
    int InactiveUsers,
    List<AdminCountryStat>? Countries = null,
    List<AdminTopSchool>? TopSchools = null,
    List<AdminRecentUser>? RecentUsers = null);

internal sealed record AdminActionResult(string Message);
