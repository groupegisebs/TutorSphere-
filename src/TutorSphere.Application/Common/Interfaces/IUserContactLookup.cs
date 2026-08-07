namespace TutorSphere.Application.Common.Interfaces;

public interface IUserContactLookup
{
    Task<(string Email, string DisplayName)?> GetAsync(string userId, CancellationToken ct = default);

    /// <summary>Langue préférée (fr, en, …) pour l'e-mail donné ; défaut fr.</summary>
    Task<string> GetPreferredLanguageByEmailAsync(string email, CancellationToken ct = default);
}
