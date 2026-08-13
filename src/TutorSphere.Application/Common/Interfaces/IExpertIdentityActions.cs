namespace TutorSphere.Application.Common.Interfaces;

/// <summary>Actions Identity pour l'admission Expert (rôle, compte candidat, e-mail credentials).</summary>
public interface IExpertIdentityActions
{
    Task<string?> FindUserIdByEmailAsync(string email, CancellationToken ct = default);

    Task EnsureExpertRoleAsync(string userId, CancellationToken ct = default);

    /// <summary>Crée ou met à jour le compte candidat ; retourne le userId.</summary>
    Task<string> EnsureCandidateUserAsync(
        string email,
        string firstName,
        string lastName,
        string? password,
        CancellationToken ct = default);

    Task NotifyExpertAdmittedAsync(string userId, string groupName, CancellationToken ct = default);
}
