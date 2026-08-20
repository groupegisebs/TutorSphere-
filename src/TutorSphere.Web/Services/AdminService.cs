using TutorSphere.Application.Common;

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

    public Task<ApiResult<bool>> DeleteUserAsync(string userId)
        => _api.DeleteWithErrorAsync($"api/admin/users/{Uri.EscapeDataString(userId)}");

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

    public Task<ApiResult<ExpertGroupItem>> CreateExpertGroupAsync(
        string name,
        string? contactName,
        string? contactEmail,
        string? contactPhone,
        string? countryCode,
        bool isInternational,
        string? description = null,
        string? managerEmail = null,
        string? managerFirstName = null,
        string? managerLastName = null,
        string? managerPhone = null,
        string? managerFunctionTitle = null,
        DateTime? managerMandateStartsAtUtc = null)
        => _api.PostWithErrorAsync<ExpertGroupItem>("api/admin/expert-groups", new
        {
            name,
            contactName,
            contactEmail,
            contactPhone,
            countryCode,
            isInternational,
            description,
            managerEmail,
            managerFirstName,
            managerLastName,
            managerPhone,
            managerFunctionTitle,
            managerMandateStartsAtUtc,
            createManagerAccount = true
        });

    public Task<ApiResult<ExpertGroupItem>> UpdateExpertGroupAsync(
        Guid id, string name, string? contactName, string? contactEmail, string? contactPhone, string? logoUrl, bool isActive,
        string? description = null, string? countryCode = null)
        => _api.PutWithErrorAsync<ExpertGroupItem>($"api/admin/expert-groups/{id}", new
        {
            name,
            contactName,
            contactEmail,
            contactPhone,
            logoUrl,
            isActive,
            description,
            countryCode
        });

    public async Task<ApiResult<bool>> DeleteExpertGroupAsync(Guid id)
        => await _api.DeleteWithErrorAsync($"api/admin/expert-groups/{id}");

    public async Task<ExpertGroupDeletionImpact?> GetExpertGroupDeletionImpactAsync(Guid id)
        => await _api.GetAsync<ExpertGroupDeletionImpact>($"api/admin/expert-groups/{id}/deletion-impact");

    /// <summary>Le nom recopié par l'administrateur est renvoyé tel quel : l'API refuse s'il diffère.</summary>
    public async Task<ApiResult<bool>> DeleteExpertGroupCascadeAsync(Guid id, string confirmName)
        => await _api.DeleteWithErrorAsync(
            $"api/admin/expert-groups/{id}?cascade=true&confirm={Uri.EscapeDataString(confirmName)}");

    public Task<ApiResult<object>> ArchiveExpertGroupAsync(Guid id)
        => _api.PostWithErrorAsync<object>($"api/admin/expert-groups/{id}/archive", new { });

    public Task<ApiResult<object>> ContactExpertGroupAsync(Guid id, string? subject = null, string? message = null)
        => _api.PostWithErrorAsync<object>($"api/admin/expert-groups/{id}/contact", new
        {
            subject = subject ?? "Contact Super Admin",
            category = 10,
            priority = 0,
            message = message ?? "Conversation ouverte depuis le Control Center."
        });

    public Task<ApiResult<ExpertGroupItem>> TransferGroupManagerAsync(
        Guid groupId, string newManagerUserId, string? phone = null, string? functionTitle = null)
        => _api.PostWithErrorAsync<ExpertGroupItem>($"api/admin/expert-groups/{groupId}/manager", new
        {
            newManagerUserId,
            phone,
            functionTitle
        });

    public async Task<List<ExpertMemberItem>> GetExpertGroupMembersAsync(Guid groupId)
        => await _api.GetAsync<List<ExpertMemberItem>>($"api/admin/expert-groups/{groupId}/members") ?? [];

    public async Task<ApiResult<ExpertMemberItem>> AddExpertByEmailAsync(
        Guid groupId, string email, bool invite = false, string? firstName = null, string? lastName = null)
        => await _api.PostWithErrorAsync<ExpertMemberItem>($"api/admin/expert-groups/{groupId}/members/by-email", new
        {
            email,
            invite,
            firstName,
            lastName
        });

    public async Task<bool> RemoveExpertMemberAsync(Guid groupId, string userId)
        => await _api.DeleteAsync($"api/admin/expert-groups/{groupId}/members/{Uri.EscapeDataString(userId)}");

    public async Task<List<ExpertMembershipInviteItem>> GetExpertMembershipInvitesAsync(Guid? groupId = null)
    {
        var url = groupId is Guid g
            ? $"api/admin/expert-membership-invites?groupId={g}"
            : "api/admin/expert-membership-invites";
        return await _api.GetAsync<List<ExpertMembershipInviteItem>>(url) ?? [];
    }

    public Task<ApiResult<ExpertMembershipInviteItem>> ForceApproveMembershipAsync(Guid inviteId, string? notes = null)
        => _api.PostWithErrorAsync<ExpertMembershipInviteItem>(
            $"api/admin/expert-membership-invites/{inviteId}/force-approve", new { notes });

    public Task<ApiResult<ExpertMembershipInviteItem>> ForceRejectMembershipAsync(Guid inviteId, string? notes = null)
        => _api.PostWithErrorAsync<ExpertMembershipInviteItem>(
            $"api/admin/expert-membership-invites/{inviteId}/force-reject", new { notes });

    public Task<ApiResult<ExpertMembershipInviteItem>> CancelMembershipInviteAsync(Guid inviteId, string? notes = null)
        => _api.PostWithErrorAsync<ExpertMembershipInviteItem>(
            $"api/admin/expert-membership-invites/{inviteId}/cancel", new { notes });

    public Task<ApiResult<ExpertMembershipInviteItem>> ValidateMembershipInviteAsync(Guid inviteId, string? notes = null)
        => _api.PostWithErrorAsync<ExpertMembershipInviteItem>(
            $"api/admin/expert-membership-invites/{inviteId}/validate", new { notes });

    public Task<ApiResult<ExpertMembershipInviteItem>> ExtendMembershipInviteAsync(
        Guid inviteId, int? extendInviteDays = null, int? extendVoteDays = null, string? notes = null)
        => _api.PostWithErrorAsync<ExpertMembershipInviteItem>(
            $"api/admin/expert-membership-invites/{inviteId}/extend",
            new { notes, extendInviteDays, extendVoteDays });

    public async Task<List<PendingTeacherItem>> GetPendingTeacherApprovalsAsync()
        => await _api.GetAsync<List<PendingTeacherItem>>("api/admin/pending-teacher-approvals") ?? [];

    public Task<ApiResult<AdminCreatedAccountItem>> CreateParentAccountAsync(
        string email, string firstName, string lastName, string? phone = null)
        => _api.PostWithErrorAsync<AdminCreatedAccountItem>("api/admin/parents", new
        {
            email,
            firstName,
            lastName,
            phone
        });

    public Task<ApiResult<AdminCreatedAccountItem>> CreateStudentAccountAsync(
        string email,
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        string? phone = null,
        string? parentEmail = null)
        => _api.PostWithErrorAsync<AdminCreatedAccountItem>("api/admin/students", new
        {
            email,
            firstName,
            lastName,
            dateOfBirth,
            phone,
            parentEmail
        });

    public Task<ApiResult<AdminCreatedAccountItem>> CreateTeacherAccountAsync(
        string? email,
        string firstName,
        string lastName,
        Guid expertGroupId,
        string? schoolName = null,
        string? slug = null,
        string? city = null,
        string? phone = null,
        bool activateSchool = true,
        object? initialOffering = null)
        => _api.PostWithErrorAsync<AdminCreatedAccountItem>("api/admin/teachers", new
        {
            email = string.IsNullOrWhiteSpace(email) ? null : email,
            firstName,
            lastName,
            expertGroupId,
            schoolName,
            slug,
            city,
            phone,
            activateSchool,
            initialOffering
        });
}

public sealed record AdminCreatedAccountItem(
    string UserId,
    string Email,
    string FullName,
    string Role,
    string TemporaryPassword,
    bool CredentialsSent,
    Guid? TenantId = null,
    string? TenantSlug = null,
    Guid? ExpertGroupId = null,
    string? ExpertGroupName = null,
    Guid? OfferingId = null);

public sealed record ExpertGroupItem(
    Guid Id,
    string Name,
    string? LogoUrl,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? CountryCode,
    bool IsInternational,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt,
    string? Description = null,
    int LifecycleStatus = 0,
    Guid? ActiveManagerMandateId = null,
    string? ManagerFullName = null,
    string? ManagerEmail = null,
    string? ManagerPhone = null,
    string? ManagerUserId = null,
    bool CanHardDelete = true);

public sealed record ExpertGroupDeletionImpact(
    Guid Id,
    string Name,
    bool IsActive,
    int LifecycleStatus,
    List<ExpertGroupDeletionEntry> Deleted,
    List<ExpertGroupDeletionEntry> Detached)
{
    public int TotalDeleted => Deleted.Sum(d => d.Count);
}

public sealed record ExpertGroupDeletionEntry(string Label, int Count);

public sealed record ExpertMemberItem(
    Guid Id,
    Guid ExpertGroupId,
    string UserId,
    string Email,
    string FullName,
    bool AccountCreated = false,
    bool CredentialsSent = false,
    bool NotificationSent = false);

public sealed record ExpertMembershipInviteItem(
    Guid Id,
    Guid ExpertGroupId,
    string GroupName,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string? Specialty,
    string? IntendedRole,
    string? Presentation,
    string? Justification,
    string InvitedByUserId,
    string? InvitedByName,
    int Status,
    DateTime SentAtUtc,
    DateTime InviteExpiresAtUtc,
    DateTime? VoteOpenedAtUtc,
    DateTime? VoteExpiresAtUtc,
    int EligibleVoterCount,
    int RequiredApprovalCount,
    int ApprovalCount,
    int RejectCount,
    int AbstainCount,
    int? MyVote,
    object? Votes,
    string? CandidateUserId,
    DateTime? DecisionAtUtc,
    string? AdminNotes);

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
    List<MoneyTotal>? MonthRevenueTotals = null,
    int LiveLessons = 0,
    int ActiveSubscriptions = 0,
    List<AdminDailyCount>? DailySignups = null,
    List<AdminPaymentSlice>? PaymentBreakdown = null,
    List<AdminActivityItem>? RecentActivity = null)
{
    /// <summary>Recette du mois, une ligne par devise (« 1 200 CAD + 450 000 XAF »).</summary>
    public string MonthRevenueDisplay =>
        MonthRevenueTotals is { Count: > 0 } totals ? MoneyTotals.Format(totals) : "—";
}

public sealed record AdminDailyCount(DateTime Date, int Count);
public sealed record AdminPaymentSlice(string Label, decimal Amount, decimal Percent, string Currency = "CAD");
public sealed record AdminActivityItem(string Title, string Detail, DateTime At, string Color);
public sealed record AdminHealthCheck(string Name, bool Ok, string Detail, string Latency);
public sealed record AdminHealth(bool Healthy, DateTime CheckedAt, List<AdminHealthCheck> Checks);

internal sealed record AdminActionResult(string Message);
