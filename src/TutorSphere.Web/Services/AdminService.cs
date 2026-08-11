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

    public async Task<AdminHealth?> GetHealthAsync()
        => await _api.GetAsync<AdminHealth>("api/admin/health");

    public async Task<List<AdminSchoolItem>> GetSchoolsAsync()
        => await _api.GetAsync<List<AdminSchoolItem>>("api/admin/schools") ?? [];

    public async Task<List<AdminPromoCodeItem>> GetPromoCodesAsync()
        => await _api.GetAsync<List<AdminPromoCodeItem>>("api/admin/promo-codes") ?? [];

    public async Task<List<AdminPromoCodeItem>?> CreatePromoCodesAsync(
        string? code,
        int quantity,
        int licenseYears,
        DateTime? expiresAt,
        string? notes)
        => await _api.PostAsync<List<AdminPromoCodeItem>>("api/admin/promo-codes", new
        {
            code,
            quantity,
            licenseYears,
            expiresAt,
            notes
        });

    public async Task<AdminPromoCodeItem?> SetPromoCodeActiveAsync(Guid id, bool isActive)
        => await _api.PutAsync<AdminPromoCodeItem>($"api/admin/promo-codes/{id}", new { isActive });

    public async Task<List<ExpertGroupItem>> GetExpertGroupsAsync()
        => await _api.GetAsync<List<ExpertGroupItem>>("api/admin/expert-groups") ?? [];

    public async Task<ExpertGroupItem?> CreateExpertGroupAsync(
        string name, string? contactEmail, string? contactPhone, string? countryCode, bool isInternational)
        => await _api.PostAsync<ExpertGroupItem>("api/admin/expert-groups", new
        {
            name,
            contactEmail,
            contactPhone,
            countryCode,
            isInternational
        });

    public async Task<ExpertGroupItem?> UpdateExpertGroupAsync(
        Guid id, string name, string? contactEmail, string? contactPhone, string? logoUrl, bool isActive)
        => await _api.PutAsync<ExpertGroupItem>($"api/admin/expert-groups/{id}", new
        {
            name,
            contactEmail,
            contactPhone,
            logoUrl,
            isActive
        });

    public async Task<bool> DeleteExpertGroupAsync(Guid id)
        => await _api.DeleteAsync($"api/admin/expert-groups/{id}");

    public async Task<List<ExpertMemberItem>> GetExpertGroupMembersAsync(Guid groupId)
        => await _api.GetAsync<List<ExpertMemberItem>>($"api/admin/expert-groups/{groupId}/members") ?? [];

    public async Task<ExpertMemberItem?> AddExpertByEmailAsync(
        Guid groupId, string email, bool invite = false, string? firstName = null, string? lastName = null)
        => await _api.PostAsync<ExpertMemberItem>($"api/admin/expert-groups/{groupId}/members/by-email", new
        {
            email,
            invite,
            firstName,
            lastName
        });

    public async Task<bool> RemoveExpertMemberAsync(Guid groupId, string userId)
        => await _api.DeleteAsync($"api/admin/expert-groups/{groupId}/members/{Uri.EscapeDataString(userId)}");

    public async Task<List<PendingTeacherItem>> GetPendingTeacherApprovalsAsync()
        => await _api.GetAsync<List<PendingTeacherItem>>("api/admin/pending-teacher-approvals") ?? [];
}

public sealed record ExpertGroupItem(
    Guid Id,
    string Name,
    string? LogoUrl,
    string? ContactEmail,
    string? ContactPhone,
    string? CountryCode,
    bool IsInternational,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt);

public sealed record ExpertMemberItem(
    Guid Id,
    Guid ExpertGroupId,
    string UserId,
    string Email,
    string FullName,
    bool AccountCreated = false,
    bool CredentialsSent = false,
    bool NotificationSent = false);

public sealed record PendingTeacherItem(
    Guid TenantId,
    string SchoolName,
    string Slug,
    string? Country,
    string? City,
    int ApprovalStatus,
    DateTime CreatedAt,
    string? OwnerEmail,
    string? OwnerName,
    int DocumentCount,
    Guid? AssignedExpertGroupId,
    string? AssignedExpertGroupName);

public sealed record TeacherReviewDetailItem(
    Guid TenantId,
    string SchoolName,
    string Slug,
    string? Description,
    string? Country,
    string? City,
    string Language,
    int ApprovalStatus,
    string? ExpertApprovalNotes,
    DateTime? ExpertApprovedAt,
    Guid? ApprovedByExpertGroupId,
    string? ApprovedByExpertGroupName,
    string? ApprovedByExpertGroupLogoUrl,
    string? OwnerUserId,
    string? OwnerEmail,
    string? OwnerName,
    string? Presentation,
    string? Portfolio,
    string? LogoUrl,
    List<TeacherReviewDocumentItem> Documents,
    Guid? SuggestedExpertGroupId,
    string? SuggestedExpertGroupName);

public sealed record TeacherReviewDocumentItem(
    Guid Id,
    Guid TenantId,
    int DocumentType,
    string FileName,
    string FileUrl,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    string? Notes);

public sealed record AdminPromoCodeItem(
    Guid Id,
    string Code,
    bool IsActive,
    int LicenseYears,
    DateTime? ExpiresAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime? RedeemedAt,
    Guid? RedeemedByTenantId,
    string? RedeemedByUserId,
    string? RedeemedBySchoolName,
    bool IsAvailable);

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
    List<AdminRecentUser>? RecentUsers = null,
    decimal MonthRevenue = 0,
    string MonthCurrency = "CAD",
    int LiveLessons = 0,
    int ActiveSubscriptions = 0,
    List<AdminDailyCount>? DailySignups = null,
    List<AdminPaymentSlice>? PaymentBreakdown = null,
    List<AdminActivityItem>? RecentActivity = null);

public sealed record AdminDailyCount(DateTime Date, int Count);
public sealed record AdminPaymentSlice(string Label, decimal Amount, decimal Percent);
public sealed record AdminActivityItem(string Title, string Detail, DateTime At, string Color);
public sealed record AdminHealthCheck(string Name, bool Ok, string Detail, string Latency);
public sealed record AdminHealth(bool Healthy, DateTime CheckedAt, List<AdminHealthCheck> Checks);

internal sealed record AdminActionResult(string Message);
