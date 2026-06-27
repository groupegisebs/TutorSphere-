namespace TutorSphere.Web.Services;

/// <summary>
/// Circuit-scoped cache for the signed-in parent's profile (api/parent/me).
/// </summary>
public sealed class ParentProfileState
{
    private readonly ApiClient _api;
    private readonly AuthService _auth;
    private Task? _loadTask;

    public string DisplayName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string? PhotoUrl { get; private set; }
    public int UnreadMessagesCount { get; private set; }

    public ParentProfileState(ApiClient api, AuthService auth)
    {
        _api = api;
        _auth = auth;
    }

    public Task EnsureLoadedAsync()
    {
        _loadTask ??= LoadCoreAsync();
        return _loadTask;
    }

    private async Task LoadCoreAsync()
    {
        await _auth.EnsureSessionRestoredAsync(forceJs: true);
        if (string.IsNullOrEmpty(_auth.Token))
        {
            if (!string.IsNullOrWhiteSpace(_auth.UserName))
                DisplayName = _auth.UserName!;
            else if (!string.IsNullOrWhiteSpace(_auth.UserEmail))
                DisplayName = _auth.UserEmail!;
            else if (_auth.IsSessionExpired)
                DisplayName = "Parent";

            if (string.IsNullOrWhiteSpace(Email))
                Email = _auth.UserEmail ?? "";

            return;
        }

        var profile = await _api.GetAsync<ParentMeDto>("api/parent/me");

        if (profile is not null)
        {
            var name = $"{profile.FirstName} {profile.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(name))
                DisplayName = name;

            if (!string.IsNullOrWhiteSpace(profile.Email))
                Email = profile.Email;

            PhotoUrl = profile.PhotoUrl;
            UnreadMessagesCount = profile.UnreadMessagesCount;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
            DisplayName = _auth.UserName ?? _auth.UserEmail ?? "Parent";

        if (string.IsNullOrWhiteSpace(Email))
            Email = _auth.UserEmail ?? "";
    }

    private sealed record ParentMeDto(
        string FirstName,
        string LastName,
        string Email,
        string? Phone,
        int ChildrenCount,
        int UnreadMessagesCount = 0,
        string? PhotoUrl = null);
}
