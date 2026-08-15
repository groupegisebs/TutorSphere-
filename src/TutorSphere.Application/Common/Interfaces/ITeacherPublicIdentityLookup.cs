namespace TutorSphere.Application.Common.Interfaces;

public sealed record TeacherPublicNameParts(string FirstName, string LastName);

/// <summary>
/// Prénoms / noms d’enseignants pour l’affichage public. Ne jamais exposer e-mail ou téléphone.
/// </summary>
public interface ITeacherPublicIdentityLookup
{
    Task<IReadOnlyDictionary<string, TeacherPublicNameParts>> GetByUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken ct = default);
}
