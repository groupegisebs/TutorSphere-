namespace TutorSphere.Application.Common.Interfaces;

public interface IUserContactLookup
{
    Task<(string Email, string DisplayName)?> GetAsync(string userId, CancellationToken ct = default);
}
