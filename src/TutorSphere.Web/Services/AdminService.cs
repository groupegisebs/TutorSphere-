namespace TutorSphere.Web.Services;

public sealed class AdminService
{
    private readonly ApiClient _api;

    public AdminService(ApiClient api) => _api = api;

    public async Task<List<AdminUserItem>> GetUsersAsync(string role)
        => await _api.GetAsync<List<AdminUserItem>>($"api/admin/users?role={Uri.EscapeDataString(role)}") ?? [];

    public async Task<bool> ActivateUserAsync(string userId)
        => await _api.PostAsync<AdminActionResult>($"api/admin/users/{userId}/activate", new { }) is not null;

    public async Task<bool> DeactivateUserAsync(string userId)
        => await _api.PostAsync<AdminActionResult>($"api/admin/users/{userId}/deactivate", new { }) is not null;

    public async Task<AdminStats?> GetStatsAsync()
        => await _api.GetAsync<AdminStats>("api/admin/stats");
}

public sealed record AdminUserItem(string Id, string Email, string FullName, string Role, bool IsActive);
public sealed record AdminStats(int TotalTutors, int TotalParents, int TotalStudents, int ActiveUsers, int InactiveUsers);

internal sealed record AdminActionResult(string Message);
